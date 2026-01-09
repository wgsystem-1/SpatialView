using System.Collections.Concurrent;

namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 비동기 데이터 로더
/// 백그라운드에서 레이어 데이터를 로드하고 캐싱
/// </summary>
public class AsyncDataLoader : IDisposable
{
    private readonly ConcurrentDictionary<string, LoadingTask> _loadingTasks;
    private readonly SemaphoreSlim _loadingSemaphore;
    private readonly CancellationTokenSource _globalCancellation;
    private bool _disposed;

    public AsyncDataLoader(int? maxConcurrentLoads = null)
    {
        var maxLoads = maxConcurrentLoads ?? Environment.ProcessorCount;
        _loadingTasks = new ConcurrentDictionary<string, LoadingTask>();
        _loadingSemaphore = new SemaphoreSlim(maxLoads, maxLoads);
        _globalCancellation = new CancellationTokenSource();
    }

    /// <summary>
    /// 데이터 로드 완료 이벤트
    /// </summary>
    public event EventHandler<DataLoadedEventArgs>? DataLoaded;

    /// <summary>
    /// 데이터 로드 시작 이벤트
    /// </summary>
    public event EventHandler<DataLoadStartedEventArgs>? DataLoadStarted;

    /// <summary>
    /// 데이터 로드 실패 이벤트
    /// </summary>
    public event EventHandler<DataLoadErrorEventArgs>? DataLoadError;

    /// <summary>
    /// 비동기로 레이어 데이터 로드
    /// </summary>
    /// <param name="layer">로드할 레이어</param>
    /// <param name="extent">로드할 영역</param>
    /// <param name="priority">로드 우선순위 (높을수록 우선)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>로드 태스크</returns>
    public async Task<IEnumerable<IFeature>?> LoadDataAsync(
        ILayer layer, 
        Geometry.Envelope? extent = null, 
        LoadPriority priority = LoadPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncDataLoader));

        var taskKey = GenerateTaskKey(layer, extent);
        
        // 이미 로딩 중인 태스크가 있는지 확인
        if (_loadingTasks.TryGetValue(taskKey, out var existingTask))
        {
            // 우선순위가 더 높으면 기존 태스크 취소하고 새로 시작
            if (priority > existingTask.Priority)
            {
                existingTask.CancellationTokenSource.Cancel();
                _loadingTasks.TryRemove(taskKey, out _);
            }
            else
            {
                // 기존 태스크 결과 대기
                return await existingTask.Task;
            }
        }

        var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _globalCancellation.Token, cancellationToken);

        var loadingTask = new LoadingTask
        {
            Key = taskKey,
            Priority = priority,
            CancellationTokenSource = combinedCancellation,
            Task = ExecuteLoadAsync(layer, extent, taskKey, priority, combinedCancellation.Token)
        };

        _loadingTasks[taskKey] = loadingTask;

        try
        {
            return await loadingTask.Task;
        }
        finally
        {
            _loadingTasks.TryRemove(taskKey, out _);
            combinedCancellation.Dispose();
        }
    }

    /// <summary>
    /// 특정 레이어의 로딩 태스크 취소
    /// </summary>
    /// <param name="layer">취소할 레이어</param>
    public void CancelLoading(ILayer layer)
    {
        var layerKey = layer.Name;
        var tasksToCancel = _loadingTasks.Values
            .Where(t => t.Key.StartsWith(layerKey))
            .ToList();

        foreach (var task in tasksToCancel)
        {
            task.CancellationTokenSource.Cancel();
        }
    }

    /// <summary>
    /// 모든 로딩 태스크 취소
    /// </summary>
    public void CancelAllLoading()
    {
        foreach (var task in _loadingTasks.Values)
        {
            task.CancellationTokenSource.Cancel();
        }
        _loadingTasks.Clear();
    }

    /// <summary>
    /// 로딩 상태 가져오기
    /// </summary>
    /// <returns>현재 로딩 중인 태스크 수</returns>
    public int GetLoadingTaskCount()
    {
        return _loadingTasks.Count;
    }

    /// <summary>
    /// 실제 데이터 로드 실행
    /// </summary>
    private async Task<IEnumerable<IFeature>?> ExecuteLoadAsync(
        ILayer layer, 
        Geometry.Envelope? extent, 
        string taskKey,
        LoadPriority priority,
        CancellationToken cancellationToken)
    {
        await _loadingSemaphore.WaitAsync(cancellationToken);

        try
        {
            // 로딩 시작 알림
            DataLoadStarted?.Invoke(this, new DataLoadStartedEventArgs 
            { 
                Layer = layer, 
                Extent = extent,
                Priority = priority 
            });

            cancellationToken.ThrowIfCancellationRequested();

            // 실제 데이터 로드
            IEnumerable<IFeature>? features = null;

            if (layer is VectorLayer vectorLayer)
            {
                features = await LoadVectorDataAsync(vectorLayer, extent, cancellationToken);
            }
            else if (layer is ITileLayer tileLayer)
            {
                // 타일 레이어는 별도 처리
                features = await LoadTileDataAsync(tileLayer, extent, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 로딩 완료 알림
            DataLoaded?.Invoke(this, new DataLoadedEventArgs 
            { 
                Layer = layer, 
                Features = features,
                Extent = extent,
                Priority = priority 
            });

            return features;
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Data loading cancelled for {layer.Name}");
            return null;
        }
        catch (Exception ex)
        {
            // 로딩 오류 알림
            DataLoadError?.Invoke(this, new DataLoadErrorEventArgs 
            { 
                Layer = layer, 
                Error = ex,
                Extent = extent 
            });

            System.Diagnostics.Debug.WriteLine($"Data loading failed for {layer.Name}: {ex.Message}");
            return null;
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    /// <summary>
    /// 벡터 레이어 데이터 로드
    /// </summary>
    private async Task<IEnumerable<IFeature>?> LoadVectorDataAsync(
        VectorLayer layer, 
        Geometry.Envelope? extent, 
        CancellationToken cancellationToken)
    {
        var dataSource = layer.DataSource;
        if (dataSource == null) return null;

        // 청크 단위로 데이터 로드 (메모리 효율성)
        const int chunkSize = 1000;
        var allFeatures = new List<IFeature>();
        
        try
        {
            // 기본 테이블에서 피처 가져오기 (extent를 쿼리 필터로 변환 필요)
            var queryFilter = extent != null ? new Sources.QueryFilter
            {
                SpatialFilter = new Sources.SpatialFilter(extent.ToPolygon())
            } : null;
            
            var tables = dataSource.GetTableNames();
            var tableName = tables.FirstOrDefault() ?? "default";
            var features = await dataSource.GetFeaturesAsync(tableName, queryFilter);
            
            // 대용량 데이터의 경우 스트리밍 방식으로 처리
            var chunkCount = 0;
            foreach (var feature in features)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                allFeatures.Add(feature);
                
                // CPU 집약적 작업을 위한 짧은 대기
                if (allFeatures.Count % chunkSize == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            return allFeatures;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Vector data loading failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 타일 레이어 데이터 로드
    /// </summary>
    private async Task<IEnumerable<IFeature>?> LoadTileDataAsync(
        ITileLayer layer, 
        Geometry.Envelope? extent, 
        CancellationToken cancellationToken)
    {
        // 타일 레이어는 일반적으로 피처가 아닌 이미지 타일을 다룸
        // 여기서는 placeholder 구현
        await Task.Delay(10, cancellationToken);
        return Enumerable.Empty<IFeature>();
    }

    /// <summary>
    /// 태스크 키 생성
    /// </summary>
    private string GenerateTaskKey(ILayer layer, Geometry.Envelope? extent)
    {
        var extentStr = extent != null 
            ? $"_{extent.MinX:F2}_{extent.MinY:F2}_{extent.MaxX:F2}_{extent.MaxY:F2}"
            : "_all";
        
        return $"{layer.Name}{extentStr}";
    }

    public void Dispose()
    {
        if (_disposed) return;

        _globalCancellation.Cancel();
        CancelAllLoading();
        
        _globalCancellation.Dispose();
        _loadingSemaphore.Dispose();
        
        _disposed = true;
    }
}

#region Supporting Classes and Enums

/// <summary>
/// 로드 우선순위
/// </summary>
public enum LoadPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// 로딩 태스크 정보
/// </summary>
internal class LoadingTask
{
    public string Key { get; set; } = string.Empty;
    public LoadPriority Priority { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    public Task<IEnumerable<IFeature>?> Task { get; set; } = System.Threading.Tasks.Task.FromResult<IEnumerable<IFeature>?>(null);
}

/// <summary>
/// 데이터 로드 시작 이벤트 인수
/// </summary>
public class DataLoadStartedEventArgs : EventArgs
{
    public ILayer Layer { get; set; } = null!;
    public Geometry.Envelope? Extent { get; set; }
    public LoadPriority Priority { get; set; }
}

/// <summary>
/// 데이터 로드 완료 이벤트 인수
/// </summary>
public class DataLoadedEventArgs : EventArgs
{
    public ILayer Layer { get; set; } = null!;
    public IEnumerable<IFeature>? Features { get; set; }
    public Geometry.Envelope? Extent { get; set; }
    public LoadPriority Priority { get; set; }
}

/// <summary>
/// 데이터 로드 오류 이벤트 인수
/// </summary>
public class DataLoadErrorEventArgs : EventArgs
{
    public ILayer Layer { get; set; } = null!;
    public Exception Error { get; set; } = null!;
    public Geometry.Envelope? Extent { get; set; }
}

#endregion