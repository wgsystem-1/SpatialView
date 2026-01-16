using SpatialView.Engine.Data.Sources;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Styling;

namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 벡터 레이어 구현
/// </summary>
public class VectorLayer : ILayer
{
    private readonly object _lockObject = new();
    private List<IFeature> _featureCache = new();
    private bool _cacheValid;
    
    /// <inheritdoc/>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <inheritdoc/>
    public string Name { get; set; } = "Vector Layer";
    
    /// <inheritdoc/>
    public string Description { get; set; } = string.Empty;
    
    /// <inheritdoc/>
    public bool Visible { get; set; } = true;
    
    /// <summary>
    /// 레이어 활성화 여부 (호환성 속성)
    /// </summary>
    public bool Enabled { get => Visible; set => Visible = value; }
    
    /// <inheritdoc/>
    public double Opacity { get; set; } = 1.0;
    
    /// <inheritdoc/>
    public int ZIndex { get; set; }
    
    /// <inheritdoc/>
    public int SRID { get; set; } = 4326;
    
    /// <inheritdoc/>
    public Envelope? Extent { get; set; }
    
    /// <inheritdoc/>
    public double MinimumZoom { get; set; } = 0;
    
    /// <inheritdoc/>
    public double MaximumZoom { get; set; } = double.MaxValue;
    
    /// <summary>
    /// 최소 표시 축척 (호환성 속성)
    /// </summary>
    public double MinVisible { get => MinimumZoom; set => MinimumZoom = value; }
    
    /// <summary>
    /// 최대 표시 축척 (호환성 속성)
    /// </summary>
    public double MaxVisible { get => MaximumZoom; set => MaximumZoom = value; }
    
    /// <inheritdoc/>
    public bool Selectable { get; set; } = true;
    
    /// <inheritdoc/>
    public bool Editable { get; set; } = false;
    
    /// <summary>
    /// 데이터 소스
    /// </summary>
    public IDataSource? DataSource { get; set; }
    
    /// <summary>
    /// 테이블 이름 (데이터 소스가 다중 테이블 지원하는 경우)
    /// </summary>
    public string? TableName { get; set; }
    
    
    /// <inheritdoc/>
    public long FeatureCount 
    { 
        get 
        {
            lock (_lockObject)
            {
                return _featureCache.Count;
            }
        }
    }
    
    // 성능 최적화: 뷰포트별 캐시
    private Envelope? _lastViewExtent;
    private List<IFeature>? _viewportCache;
    
    // 빈 리스트 싱글톤 (메모리 절약)
    private static readonly List<IFeature> _emptyFeatureList = new();
    
    /// <inheritdoc/>
    public IEnumerable<IFeature> GetFeatures(Envelope? extent = null)
    {
        lock (_lockObject)
        {
            if (!_cacheValid)
            {
                LoadFeatures();
            }

            Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] Layer={Name}, extent={extent}, _featureCache.Count={_featureCache.Count}");

            // 성능 최적화: 빈 캐시는 즉시 반환
            if (_featureCache.Count == 0)
            {
                Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] 피처 캐시가 비어있음 -> 빈 리스트 반환");
                return _emptyFeatureList;
            }

            if (extent == null)
            {
                // 전체 피처 반환 - 캐시 직접 반환 (복사 없음)
                Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] extent=null -> 전체 피처 반환 ({_featureCache.Count}개)");
                return _featureCache;
            }

            // 뷰포트 캐시 확인 - 같은 뷰포트면 캐시 재사용
            if (_viewportCache != null && _lastViewExtent != null &&
                _lastViewExtent.MinX == extent.MinX && _lastViewExtent.MaxX == extent.MaxX &&
                _lastViewExtent.MinY == extent.MinY && _lastViewExtent.MaxY == extent.MaxY)
            {
                Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] 뷰포트 캐시 재사용 ({_viewportCache.Count}개)");
                return _viewportCache;
            }

            // 새 뷰포트 - 필터링 수행
            _lastViewExtent = new Envelope(extent);
            _viewportCache = new List<IFeature>();

            int intersectCount = 0;
            int nullEnvelopeCount = 0;
            int nullGeometryCount = 0;

            foreach (var f in _featureCache)
            {
                if (f.Geometry == null)
                {
                    nullGeometryCount++;
                    continue;
                }

                // Envelope가 없으면 포함 (안전판정) 후 로그
                if (f.Geometry.Envelope == null)
                {
                    Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] Envelope null 포함 처리 (FeatureId={f.Id})");
                    _viewportCache.Add(f);
                    nullEnvelopeCount++;
                    continue;
                }

                // 디버그: 첫 번째 피처의 Envelope 출력
                if (intersectCount == 0)
                {
                    Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] 첫 피처 Envelope: MinX={f.Geometry.Envelope.MinX:F2}, MaxX={f.Geometry.Envelope.MaxX:F2}, MinY={f.Geometry.Envelope.MinY:F2}, MaxY={f.Geometry.Envelope.MaxY:F2}");
                    Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] 검색 Envelope: MinX={extent.MinX:F2}, MaxX={extent.MaxX:F2}, MinY={extent.MinY:F2}, MaxY={extent.MaxY:F2}");
                }

                if (f.Geometry.Envelope.Intersects(extent))
                {
                    _viewportCache.Add(f);
                    intersectCount++;
                }
            }

            Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] 필터링 결과: {intersectCount}개 교차, {nullEnvelopeCount}개 null Envelope, {nullGeometryCount}개 null Geometry");
            Diagnostics.FileLogger.Log($"[VectorLayer.GetFeatures] 반환 피처 수: {_viewportCache.Count}개");

            return _viewportCache;
        }
    }
    
    /// <summary>
    /// 뷰포트 캐시 무효화
    /// </summary>
    public void InvalidateViewportCache()
    {
        lock (_lockObject)
        {
            _viewportCache = null;
            _lastViewExtent = null;
        }
    }
    
    /// <inheritdoc/>
    public IEnumerable<IFeature> GetFeatures(IGeometry geometry)
    {
        lock (_lockObject)
        {
            if (!_cacheValid)
            {
                LoadFeatures();
            }
            
            return _featureCache.Where(f => 
                f.Geometry != null && 
                f.Geometry.Intersects(geometry)).ToList();
        }
    }
    
    /// <inheritdoc/>
    public void AddFeature(IFeature feature)
    {
        lock (_lockObject)
        {
            _featureCache.Add(feature);
            UpdateExtent();
            // 뷰포트 캐시 무효화 - 새 피처가 화면에 표시되도록
            _viewportCache = null;
            _lastViewExtent = null;
        }
    }

    /// <inheritdoc/>
    public void DeleteFeature(IFeature feature)
    {
        lock (_lockObject)
        {
            var existingIndex = _featureCache.FindIndex(f => f.Id.Equals(feature.Id));
            if (existingIndex >= 0)
            {
                _featureCache.RemoveAt(existingIndex);
                UpdateExtent();
                // 뷰포트 캐시 무효화 - 삭제된 피처가 화면에서 사라지도록
                _viewportCache = null;
                _lastViewExtent = null;
            }
        }
    }

    /// <inheritdoc/>
    public void UpdateFeature(IFeature feature)
    {
        lock (_lockObject)
        {
            // 기존 피처를 찾아서 업데이트
            var existingIndex = _featureCache.FindIndex(f => f.Id.Equals(feature.Id));
            if (existingIndex >= 0)
            {
                _featureCache[existingIndex] = feature;
                UpdateExtent();
                // 뷰포트 캐시 무효화 - 수정된 피처가 화면에 반영되도록
                _viewportCache = null;
                _lastViewExtent = null;
            }
        }
    }
    
    /// <inheritdoc/>
    public void Refresh()
    {
        lock (_lockObject)
        {
            if (DataSource != null && !string.IsNullOrEmpty(TableName))
            {
                _cacheValid = false;
                _featureCache.Clear();
                LoadFeatures();
            }
            else
            {
                // DataSource 없이 SetFeatures로 채운 경우: 피처는 유지, 뷰포트 캐시만 무효화
                InvalidateViewportCache();
                System.Diagnostics.Debug.WriteLine($"VectorLayer.Refresh: DataSource 없음, 피처 캐시 유지 (Count={_featureCache.Count})");
            }
        }
    }
    
    /// <summary>
    /// 피처 캐시를 직접 설정 (외부에서 이미 로드된 피처를 사용할 때)
    /// </summary>
    public void SetFeatures(IEnumerable<IFeature> features)
    {
        lock (_lockObject)
        {
            _featureCache = features.ToList();
            _cacheValid = true;
            UpdateExtent();
        }
    }
    
    /// <summary>
    /// 캐시를 유효한 상태로 표시 (DataSource에서 직접 피처를 가져올 때)
    /// </summary>
    public void MarkCacheValid()
    {
        lock (_lockObject)
        {
            _cacheValid = true;
        }
    }
    
    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpatialView_render.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    /// <summary>
    /// 피처 로드
    /// </summary>
    private void LoadFeatures()
    {
        if (DataSource == null || string.IsNullOrEmpty(TableName))
        {
            return;
        }
        
        try
        {
            var features = DataSource.QueryFeaturesAsync(TableName)
                .ToBlockingEnumerable()
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"VectorLayer.LoadFeatures: {Name} - 로드된 피처 수={features.Count}");
            
            _featureCache = features;
            _cacheValid = true;
            
            // 범위 업데이트
            UpdateExtent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VectorLayer.LoadFeatures 오류: {ex.Message}");
            _featureCache.Clear();
            _cacheValid = false;
        }
    }
    
    /// <summary>
    /// 범위 업데이트
    /// </summary>
    private void UpdateExtent()
    {
        if (_featureCache.Count == 0)
        {
            // 피처가 없어도 기존 Extent를 유지 (외부에서 설정한 경우)
            return;
        }
        
        Envelope? combinedExtent = null;
        
        foreach (var feature in _featureCache)
        {
            if (feature.Geometry?.Envelope != null)
            {
                if (combinedExtent == null)
                {
                    combinedExtent = new Envelope(feature.Geometry.Envelope);
                }
                else
                {
                    combinedExtent.ExpandToInclude(feature.Geometry.Envelope);
                }
            }
        }
        
        // 계산된 범위가 있으면 업데이트, 없으면 기존 Extent 유지
        if (combinedExtent != null)
        {
            Extent = combinedExtent;
        }
    }
    
    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lockObject)
        {
            _featureCache.Clear();
            DataSource?.Dispose();
        }
    }
    
    /// <inheritdoc/>
    public bool IsVisible => Visible;
    
    /// <inheritdoc/>
    public bool IsSelectable => Selectable;
    
    /// <inheritdoc/>
    public bool IsEditable => Editable;
    
    /// <inheritdoc/>
    public double MinScale { get; set; } = 0;
    
    /// <inheritdoc/>
    public double MaxScale { get; set; } = double.MaxValue;
    
    /// <inheritdoc/>
    public Styling.IStyle? Style { get; set; }
    
    /// <summary>
    /// 라벨 스타일
    /// </summary>
    public Styling.ILabelStyle? LabelStyle { get; set; }
    
    /// <summary>
    /// 라벨 표시 여부
    /// </summary>
    public bool ShowLabels { get; set; } = false;
    
    /// <inheritdoc/>
    public Geometry.Envelope? GetExtent()
    {
        return Extent;
    }
}