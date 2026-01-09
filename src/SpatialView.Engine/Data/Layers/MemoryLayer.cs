namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 메모리 내 데이터를 저장하는 레이어
/// 빠른 접근을 위해 공간 인덱스 사용
/// </summary>
public class MemoryLayer : ILayer, IDisposable
{
    private readonly SpatialIndex.ISpatialIndex<IFeature> _spatialIndex;
    private readonly List<IFeature> _features;
    private bool _disposed;
    
    /// <inheritdoc/>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <inheritdoc/>
    public string Name { get; set; }
    
    /// <inheritdoc/>
    public string Description { get; set; }
    
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
    public int SRID { get; set; } = 4326; // 기본 WGS84
    
    /// <inheritdoc/>
    public double MinimumZoom { get; set; } = 0;
    
    /// <inheritdoc/>
    public double MaximumZoom { get; set; } = double.MaxValue;
    
    /// <inheritdoc/>
    public bool Selectable { get; set; } = true;
    
    /// <inheritdoc/>
    public bool Editable { get; set; } = true;
    
    /// <inheritdoc/>
    public long FeatureCount => _features.Count;
    
    /// <inheritdoc/>
    public Geometry.Envelope? Extent
    {
        get
        {
            if (_features.Count == 0) return null;
            
            var extent = new Geometry.Envelope();
            foreach (var feature in _features)
            {
                if (feature.BoundingBox != null)
                {
                    extent.ExpandToInclude(feature.BoundingBox);
                }
            }
            
            return extent.IsNull ? null : extent;
        }
    }
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public MemoryLayer(string name = "Memory Layer")
    {
        Name = name;
        Description = "In-memory feature layer";
        _features = new List<IFeature>();
        
        // R-Tree 공간 인덱스 사용
        _spatialIndex = new SpatialIndex.RTree<IFeature>();
    }
    
    /// <summary>
    /// 피처 추가
    /// </summary>
    public void AddFeature(IFeature feature)
    {
        if (feature == null) throw new ArgumentNullException(nameof(feature));
        
        _features.Add(feature);
        
        if (feature.BoundingBox != null)
        {
            _spatialIndex.Insert(feature.BoundingBox, feature);
        }
    }
    
    /// <summary>
    /// 여러 피처 일괄 추가
    /// </summary>
    public void AddFeatures(IEnumerable<IFeature> features)
    {
        foreach (var feature in features)
        {
            AddFeature(feature);
        }
    }
    
    /// <summary>
    /// 피처 제거
    /// </summary>
    public bool RemoveFeature(IFeature feature)
    {
        if (feature == null) return false;
        
        var removed = _features.Remove(feature);
        if (removed && feature.BoundingBox != null)
        {
            _spatialIndex.Remove(feature.BoundingBox, feature);
        }
        
        return removed;
    }
    
    /// <inheritdoc/>
    public void DeleteFeature(IFeature feature)
    {
        RemoveFeature(feature);
    }
    
    /// <summary>
    /// 모든 피처 제거
    /// </summary>
    public void Clear()
    {
        _features.Clear();
        _spatialIndex.Clear();
    }
    
    /// <summary>
    /// ID로 피처 찾기
    /// </summary>
    public IFeature? FindFeatureById(object id)
    {
        return _features.FirstOrDefault(f => f.Id.Equals(id));
    }
    
    /// <inheritdoc/>
    public IEnumerable<IFeature> GetFeatures(Geometry.Envelope? extent = null)
    {
        if (extent == null)
        {
            return _features;
        }
        
        // 공간 인덱스를 사용한 빠른 검색
        return _spatialIndex.Query(extent);
    }
    
    /// <inheritdoc/>
    public IEnumerable<IFeature> GetFeatures(Geometry.IGeometry geometry)
    {
        if (geometry == null) throw new ArgumentNullException(nameof(geometry));
        
        var envelope = geometry.Envelope;
        var candidates = _spatialIndex.Query(envelope);
        
        // 정확한 기하학적 교차 테스트
        foreach (var candidate in candidates)
        {
            if (candidate.Geometry != null && 
                GeometryIntersects(geometry, candidate.Geometry))
            {
                yield return candidate;
            }
        }
    }
    
    /// <summary>
    /// 두 지오메트리가 교차하는지 확인 (간단한 구현)
    /// </summary>
    private bool GeometryIntersects(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 기본적인 Envelope 기반 교차 테스트
        // TODO: 정확한 기하학적 교차 알고리즘 구현
        return geom1.Envelope.Intersects(geom2.Envelope);
    }
    
    /// <summary>
    /// 영역 내 피처 개수
    /// </summary>
    public long GetFeatureCount(Geometry.Envelope? extent = null)
    {
        if (extent == null)
        {
            return _features.Count;
        }
        
        return _spatialIndex.Query(extent).Count;
    }
    
    /// <summary>
    /// 피처 업데이트 (공간 인덱스 동기화)
    /// </summary>
    public void UpdateFeature(IFeature feature)
    {
        if (feature == null) throw new ArgumentNullException(nameof(feature));
        
        // 기존 인덱스 엔트리 제거
        var existingFeature = _features.FirstOrDefault(f => f.Id.Equals(feature.Id));
        if (existingFeature?.BoundingBox != null)
        {
            _spatialIndex.Remove(existingFeature.BoundingBox, existingFeature);
        }
        
        // 새 인덱스 엔트리 추가
        if (feature.BoundingBox != null)
        {
            _spatialIndex.Insert(feature.BoundingBox, feature);
        }
    }
    
    /// <inheritdoc/>
    public void Refresh()
    {
        // 메모리 레이어는 별도 새로고침 불필요
        // 필요시 공간 인덱스 재구축
        RebuildSpatialIndex();
    }
    
    /// <summary>
    /// 공간 인덱스 재구축
    /// </summary>
    private void RebuildSpatialIndex()
    {
        _spatialIndex.Clear();
        
        foreach (var feature in _features)
        {
            if (feature.BoundingBox != null)
            {
                _spatialIndex.Insert(feature.BoundingBox, feature);
            }
        }
    }
    
    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _features.Clear();
                _spatialIndex.Clear();
            }
            _disposed = true;
        }
    }
    
    public override string ToString()
    {
        return $"MemoryLayer[{Name}, Features={FeatureCount}, Visible={Visible}]";
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
    
    /// <inheritdoc/>
    public Sources.IDataSource? DataSource { get; set; }
    
    /// <inheritdoc/>
    public Geometry.Envelope? GetExtent()
    {
        return Extent;
    }
}