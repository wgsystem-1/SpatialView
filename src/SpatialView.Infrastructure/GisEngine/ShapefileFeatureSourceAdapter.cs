using SpatialView.Core.GisEngine;
using SpatialView.Engine.Data.Sources;
using SpatialView.Engine.Geometry;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// Engine.ShapefileDataSource를 Core.IFeatureSource로 어댑팅하는 클래스
/// 피처를 캐싱하여 성능 최적화
/// </summary>
public class ShapefileFeatureSourceAdapter : IFeatureSource
{
    private readonly ShapefileDataSource _dataSource;
    private bool _disposed = false;
    
    // 피처 캐시 (ID -> Feature)
    private Dictionary<uint, CoreFeatureAdapter>? _featureCache;
    private List<uint>? _featureIds;
    private Envelope? _cachedExtent;
    private bool _cacheLoaded = false;
    private readonly object _cacheLock = new();

    public ShapefileFeatureSourceAdapter(ShapefileDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public bool IsOpen => _dataSource.IsConnected;

    public int FeatureCount => (int)(_dataSource.GetFeatureCount());

    public int SRID 
    { 
        get => _dataSource.SRID; 
        set => throw new NotSupportedException("SRID cannot be modified after creation");
    }

    public void Open()
    {
        _dataSource.Open();
    }

    public void Close()
    {
        _dataSource.Close();
    }

    public Envelope GetExtents()
    {
        var extent = _dataSource.GetExtent();
        return extent ?? new Envelope();
    }

    /// <summary>
    /// 피처 캐시 로드 (최초 1회만 실행)
    /// </summary>
    private void EnsureCacheLoaded()
    {
        if (_cacheLoaded) return;

        lock (_cacheLock)
        {
            if (_cacheLoaded) return;

            _featureCache = new Dictionary<uint, CoreFeatureAdapter>();
            _featureIds = new List<uint>();
            _cachedExtent = _dataSource.GetExtent();

            var features = _dataSource.GetFeatures();
            if (features != null)
            {
                uint id = 0;
                foreach (var feature in features)
                {
                    var adapter = new CoreFeatureAdapter(feature, id);
                    _featureCache[id] = adapter;
                    _featureIds.Add(id);
                    id++;
                }
            }

            _cacheLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[ShapefileFeatureSourceAdapter] Cache loaded: {_featureCache.Count} features");
        }
    }

    public IGeometry? GetGeometryByID(uint id)
    {
        EnsureCacheLoaded();
        return _featureCache?.TryGetValue(id, out var feature) == true ? feature.Geometry : null;
    }

    public IList<uint> GetFeaturesInView(Envelope envelope)
    {
        EnsureCacheLoaded();
        
        if (_featureCache == null || _featureIds == null)
            return new List<uint>();

        // 범위 필터링
        var result = new List<uint>();
        foreach (var kvp in _featureCache)
        {
            var geom = kvp.Value.Geometry;
            if (geom != null)
            {
                var bounds = geom.GetBounds();
                if (bounds != null && envelope.Intersects(bounds))
                {
                    result.Add(kvp.Key);
                }
            }
        }

        return result;
    }

    public IEnumerable<Core.GisEngine.IFeature> GetAllFeatures()
    {
        EnsureCacheLoaded();
        return _featureCache?.Values ?? Enumerable.Empty<Core.GisEngine.IFeature>();
    }

    public Core.GisEngine.IFeature? GetFeatureByID(uint id)
    {
        EnsureCacheLoaded();
        return _featureCache?.TryGetValue(id, out var feature) == true ? feature : null;
    }

    public Core.GisEngine.IFeature? GetFeature(uint id)
    {
        return GetFeatureByID(id);
    }

    public IList<uint> GetObjectIDsInView(Envelope envelope)
    {
        return GetFeaturesInView(envelope);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _featureCache?.Clear();
            _featureIds?.Clear();
            _dataSource?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Engine.IFeature를 Core.IFeature로 어댑팅하는 클래스
/// </summary>
internal class CoreFeatureAdapter : Core.GisEngine.IFeature
{
    private readonly Engine.Data.IFeature _engineFeature;
    private readonly uint _id;

    public CoreFeatureAdapter(Engine.Data.IFeature engineFeature, uint id)
    {
        _engineFeature = engineFeature ?? throw new ArgumentNullException(nameof(engineFeature));
        _id = id;
    }

    public uint ID => _id;

    public IGeometry? Geometry => _engineFeature.Geometry;

    public object? GetAttribute(string name)
    {
        return _engineFeature.GetAttribute(name);
    }

    public IEnumerable<string> AttributeNames => _engineFeature.Attributes?.GetNames() ?? Enumerable.Empty<string>();

    public void SetAttribute(string name, object? value)
    {
        if (_engineFeature.Attributes != null)
        {
            _engineFeature.Attributes[name] = value;
        }
    }
}
