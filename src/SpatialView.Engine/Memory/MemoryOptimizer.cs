using System.Diagnostics;
using SpatialView.Engine.Data;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Memory;

/// <summary>
/// 메모리 최적화 관리자
/// GIS 엔진의 전체적인 메모리 사용량을 모니터링하고 최적화
/// </summary>
public class MemoryOptimizer : IDisposable
{
    private readonly Timer _monitorTimer;
    private readonly FeatureCache _featureCache;
    private readonly Dictionary<string, WeakReference> _weakReferences;
    private readonly MemoryOptimizationOptions _options;
    private readonly object _lock = new();
    private bool _disposed;
    
    /// <summary>
    /// 메모리 압박 상황 이벤트
    /// </summary>
    public event EventHandler<MemoryPressureEventArgs>? MemoryPressureDetected;
    
    /// <summary>
    /// 생성자
    /// </summary>
    public MemoryOptimizer(MemoryOptimizationOptions? options = null)
    {
        _options = options ?? MemoryOptimizationOptions.CreateDefault();
        _featureCache = new FeatureCache("GlobalFeatureCache", _options.CacheMemoryLimitMB);
        _weakReferences = new Dictionary<string, WeakReference>();
        
        // 메모리 모니터링 타이머 시작
        _monitorTimer = new Timer(MonitorMemory, null, 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(_options.MonitoringIntervalSeconds));
    }
    
    /// <summary>
    /// 피처 캐싱 (강한 참조)
    /// </summary>
    public void CacheFeature(string key, IFeature feature, bool useWeakReference = false)
    {
        if (useWeakReference)
        {
            lock (_lock)
            {
                _weakReferences[key] = new WeakReference(feature);
            }
        }
        else
        {
            _featureCache.Add(key, feature);
        }
    }
    
    /// <summary>
    /// 캐시된 피처 가져오기
    /// </summary>
    public IFeature? GetCachedFeature(string key)
    {
        // 먼저 강한 참조 캐시 확인
        var feature = _featureCache.Get(key);
        if (feature != null)
            return feature;
        
        // 약한 참조 확인
        lock (_lock)
        {
            if (_weakReferences.TryGetValue(key, out var weakRef))
            {
                if (weakRef.IsAlive)
                {
                    return weakRef.Target as IFeature;
                }
                else
                {
                    // 죽은 참조 제거
                    _weakReferences.Remove(key);
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 영역 기반 피처 프리로딩
    /// </summary>
    public async Task PreloadFeaturesAsync(Envelope viewport, double bufferRatio = 1.5)
    {
        await Task.Run(() =>
        {
            // 뷰포트보다 넓은 영역의 피처를 미리 로드
            var bufferedViewport = new Envelope(viewport);
            var buffer = Math.Max(viewport.Width, viewport.Height) * (bufferRatio - 1) / 2;
            bufferedViewport.ExpandBy(buffer);
            
            // TODO: 실제 데이터 소스에서 피처 로드
            // 여기서는 구조만 제공
        });
    }
    
    /// <summary>
    /// 메모리 압박 시 최적화 수행
    /// </summary>
    public void OptimizeUnderPressure(MemoryPressureLevel level)
    {
        switch (level)
        {
            case MemoryPressureLevel.Low:
                // 약한 참조만 정리
                CleanupWeakReferences();
                break;
                
            case MemoryPressureLevel.Medium:
                // 캐시 30% 정리
                _featureCache.Clear();
                CleanupWeakReferences();
                
                // 객체 풀 크기 축소
                MemoryPoolManager.ClearAll();
                break;
                
            case MemoryPressureLevel.High:
                // 모든 캐시 정리
                _featureCache.Clear();
                _weakReferences.Clear();
                MemoryPoolManager.ClearAll();
                
                // 강제 가비지 수집
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect();
                break;
        }
    }
    
    /// <summary>
    /// 메모리 사용량 모니터링
    /// </summary>
    private void MonitorMemory(object? state)
    {
        if (_disposed) return;
        
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024 * 1024);
            var gcTotalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
            
            var stats = new MemoryStatistics
            {
                WorkingSetMB = workingSetMB,
                ManagedMemoryMB = gcTotalMemoryMB,
                CacheStatistics = _featureCache.GetStatistics(),
                WeakReferenceCount = _weakReferences.Count,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            };
            
            // 메모리 압박 감지
            var pressureLevel = DetectMemoryPressure(stats);
            if (pressureLevel > MemoryPressureLevel.None)
            {
                MemoryPressureDetected?.Invoke(this, new MemoryPressureEventArgs(pressureLevel, stats));
                
                if (_options.AutoOptimize)
                {
                    OptimizeUnderPressure(pressureLevel);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Memory monitoring error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 메모리 압박 레벨 감지
    /// </summary>
    private MemoryPressureLevel DetectMemoryPressure(MemoryStatistics stats)
    {
        var totalAvailableMemoryMB = GetTotalAvailableMemoryMB();
        var memoryUsagePercent = (stats.WorkingSetMB * 100.0) / totalAvailableMemoryMB;
        
        if (memoryUsagePercent > _options.HighPressureThresholdPercent)
            return MemoryPressureLevel.High;
        if (memoryUsagePercent > _options.MediumPressureThresholdPercent)
            return MemoryPressureLevel.Medium;
        if (memoryUsagePercent > _options.LowPressureThresholdPercent)
            return MemoryPressureLevel.Low;
        
        return MemoryPressureLevel.None;
    }
    
    /// <summary>
    /// 사용 가능한 총 메모리 크기 가져오기
    /// </summary>
    private long GetTotalAvailableMemoryMB()
    {
        // Windows 환경에서 총 물리 메모리 가져오기
        try
        {
            var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
            return (long)(computerInfo.TotalPhysicalMemory / (1024 * 1024));
        }
        catch
        {
            // 실패 시 기본값 (4GB)
            return 4096;
        }
    }
    
    /// <summary>
    /// 죽은 약한 참조 정리
    /// </summary>
    private void CleanupWeakReferences()
    {
        lock (_lock)
        {
            var deadKeys = _weakReferences
                .Where(kvp => !kvp.Value.IsAlive)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in deadKeys)
            {
                _weakReferences.Remove(key);
            }
        }
    }
    
    /// <summary>
    /// 현재 메모리 통계 가져오기
    /// </summary>
    public MemoryStatistics GetCurrentStatistics()
    {
        var process = Process.GetCurrentProcess();
        return new MemoryStatistics
        {
            WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
            ManagedMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
            CacheStatistics = _featureCache.GetStatistics(),
            WeakReferenceCount = _weakReferences.Count,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _monitorTimer?.Dispose();
        _featureCache?.Dispose();
        _weakReferences?.Clear();
    }
}

/// <summary>
/// 메모리 압박 레벨
/// </summary>
public enum MemoryPressureLevel
{
    None,
    Low,
    Medium,
    High
}

/// <summary>
/// 메모리 압박 이벤트 인자
/// </summary>
public class MemoryPressureEventArgs : EventArgs
{
    public MemoryPressureLevel Level { get; }
    public MemoryStatistics Statistics { get; }
    
    public MemoryPressureEventArgs(MemoryPressureLevel level, MemoryStatistics statistics)
    {
        Level = level;
        Statistics = statistics;
    }
}

/// <summary>
/// 메모리 통계
/// </summary>
public class MemoryStatistics
{
    public long WorkingSetMB { get; set; }
    public long ManagedMemoryMB { get; set; }
    public CacheStatistics? CacheStatistics { get; set; }
    public int WeakReferenceCount { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    
    public override string ToString()
    {
        return $"Working Set: {WorkingSetMB}MB, Managed: {ManagedMemoryMB}MB, " +
               $"Cache: {CacheStatistics?.ItemCount ?? 0} items, " +
               $"WeakRefs: {WeakReferenceCount}, " +
               $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
    }
}

/// <summary>
/// 메모리 최적화 옵션
/// </summary>
public class MemoryOptimizationOptions
{
    /// <summary>
    /// 캐시 메모리 제한 (MB)
    /// </summary>
    public long CacheMemoryLimitMB { get; set; } = 100;
    
    /// <summary>
    /// 모니터링 주기 (초)
    /// </summary>
    public int MonitoringIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// 자동 최적화 활성화
    /// </summary>
    public bool AutoOptimize { get; set; } = true;
    
    /// <summary>
    /// 낮은 압박 임계값 (%)
    /// </summary>
    public double LowPressureThresholdPercent { get; set; } = 60.0;
    
    /// <summary>
    /// 중간 압박 임계값 (%)
    /// </summary>
    public double MediumPressureThresholdPercent { get; set; } = 75.0;
    
    /// <summary>
    /// 높은 압박 임계값 (%)
    /// </summary>
    public double HighPressureThresholdPercent { get; set; } = 85.0;
    
    /// <summary>
    /// 기본 설정 생성
    /// </summary>
    public static MemoryOptimizationOptions CreateDefault()
    {
        return new MemoryOptimizationOptions();
    }
    
    /// <summary>
    /// 적극적인 메모리 최적화 설정
    /// </summary>
    public static MemoryOptimizationOptions CreateAggressive()
    {
        return new MemoryOptimizationOptions
        {
            CacheMemoryLimitMB = 50,
            MonitoringIntervalSeconds = 15,
            AutoOptimize = true,
            LowPressureThresholdPercent = 50.0,
            MediumPressureThresholdPercent = 65.0,
            HighPressureThresholdPercent = 75.0
        };
    }
}