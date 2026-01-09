using System;
using System.Collections.Generic;
using System.Linq;
using SpatialView.Core.GisEngine;
using SpatialView.Engine.Data.Sources;
using SpatialView.Engine.Geometry;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// Engine의 IDataSource/테이블을 Core.IFeatureSource로 어댑팅
/// FileGDB 등 GDAL 기반 데이터 소스에 사용
/// </summary>
public class EngineDataSourceFeatureSourceAdapter : IFeatureSource
{
    private readonly IDataSource _dataSource;
    private readonly string _tableName;
    private bool _disposed;
    
    public EngineDataSourceFeatureSourceAdapter(IDataSource dataSource, string tableName)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
    }
    
    public bool IsOpen => _dataSource.IsConnected;
    
    public int FeatureCount => (int)_dataSource.GetFeatureCountAsync(_tableName).GetAwaiter().GetResult();
    
    public int SRID
    {
        get => _dataSource.SRID;
        set => throw new NotSupportedException("SRID 변경은 지원하지 않습니다.");
    }
    
    public void Open() => _dataSource.Open();
    
    public void Close() => _dataSource.Close();
    
    public Envelope GetExtents()
    {
        return _dataSource.GetExtentAsync(_tableName).GetAwaiter().GetResult() ?? new Envelope();
    }
    
    public IGeometry? GetGeometryByID(uint id)
    {
        return _dataSource.GetFeatureAsync(_tableName, id).GetAwaiter().GetResult()?.Geometry;
    }
    
    public IList<uint> GetFeaturesInView(Envelope envelope)
    {
        if (envelope == null || envelope.IsNull)
            return new List<uint>();
            
        // 테이블 이름을 지정하여 피처 조회
        var envelopeGeometry = envelope.ToPolygon();
        var spatialFilter = new Engine.Data.Sources.SpatialFilter(envelopeGeometry, Engine.Data.Sources.SpatialRelationship.Intersects);
        var queryFilter = new Engine.Data.Sources.QueryFilter
        {
            SpatialFilter = spatialFilter
        };
        
        var features = _dataSource.GetFeaturesAsync(_tableName, queryFilter)
            .GetAwaiter().GetResult() ?? new List<Engine.Data.IFeature>();
        return features.Select(ConvertToId).ToList();
    }
    
    public IEnumerable<IFeature> GetAllFeatures()
    {
        var features = _dataSource.GetFeaturesAsync(_tableName).GetAwaiter().GetResult()
                      ?? new List<Engine.Data.IFeature>();
        System.Diagnostics.Debug.WriteLine($"[EngineDataSourceFeatureSourceAdapter] GetAllFeatures: {features.Count} features from table '{_tableName}'");
        
        var result = new List<IFeature>();
        foreach (var f in features)
        {
            var converted = ConvertToCoreFeature(f);
            if (converted?.Geometry != null)
            {
                result.Add(converted);
            }
        }
        System.Diagnostics.Debug.WriteLine($"[EngineDataSourceFeatureSourceAdapter] GetAllFeatures: {result.Count} features with geometry");
        return result;
    }
    
    public IFeature? GetFeatureByID(uint id)
    {
        var feature = _dataSource.GetFeatureAsync(_tableName, id).GetAwaiter().GetResult();
        return feature != null ? ConvertToCoreFeature(feature) : null;
    }
    
    public IFeature? GetFeature(uint id) => GetFeatureByID(id);
    
    public IList<uint> GetObjectIDsInView(Envelope envelope) => GetFeaturesInView(envelope);
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _dataSource.Dispose();
            _disposed = true;
        }
    }
    
    internal static uint ConvertToId(Engine.Data.IFeature feature)
    {
        if (feature.Id is IConvertible)
        {
            try
            {
                return Convert.ToUInt32(feature.Id);
            }
            catch { }
        }
        
        var attr = feature.GetAttribute("id");
        if (attr != null && uint.TryParse(attr.ToString(), out var parsed))
        {
            return parsed;
        }
        
        return 0;
    }
    
    private static IFeature ConvertToCoreFeature(Engine.Data.IFeature engineFeature)
    {
        return new EngineCoreFeatureAdapter(engineFeature);
    }
}

/// <summary>
/// Engine.IFeature를 Core.IFeature로 어댑팅
/// </summary>
internal class EngineCoreFeatureAdapter : SpatialView.Core.GisEngine.IFeature
{
    private readonly Engine.Data.IFeature _engineFeature;
    
    public EngineCoreFeatureAdapter(Engine.Data.IFeature engineFeature)
    {
        _engineFeature = engineFeature ?? throw new ArgumentNullException(nameof(engineFeature));
    }
    
    public uint ID => EngineDataSourceFeatureSourceAdapter.ConvertToId(_engineFeature);
    
    public IGeometry? Geometry => _engineFeature.Geometry;
    
    public object? GetAttribute(string name) => _engineFeature.GetAttribute(name);
    
    public IEnumerable<string> AttributeNames => _engineFeature.Attributes?.GetNames() ?? Enumerable.Empty<string>();
    
    public void SetAttribute(string name, object? value)
    {
        if (_engineFeature.Attributes != null)
        {
            _engineFeature.Attributes[name] = value;
        }
    }
}

