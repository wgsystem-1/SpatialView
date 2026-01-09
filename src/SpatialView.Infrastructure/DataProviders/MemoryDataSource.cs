using SpatialView.Engine.Data.Sources;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;
using IQueryFilter = SpatialView.Engine.Data.Sources.IQueryFilter;

namespace SpatialView.Infrastructure.DataProviders;

/// <summary>
/// 메모리 기반 데이터 소스
/// FileGDB, GeoJSON 등에서 로드된 지오메트리를 메모리에 저장
/// </summary>
public class MemoryDataSource : IDataSource
{
    private readonly List<IGeometry> _geometries;
    private readonly List<IFeature> _features;
    private readonly Envelope _extent;
    private bool _isDisposed;

    public MemoryDataSource(List<IGeometry> geometries)
    {
        _geometries = geometries ?? throw new ArgumentNullException(nameof(geometries));
        _features = new List<IFeature>();
        
        // 지오메트리로부터 Feature 생성
        for (int i = 0; i < _geometries.Count; i++)
        {
            var feature = new Feature
            {
                Geometry = _geometries[i]
            };
            feature.Attributes["ID"] = i + 1;
            _features.Add(feature);
        }

        // 전체 범위 계산
        if (_geometries.Count > 0)
        {
            _extent = new Envelope(_geometries[0].Envelope);
            foreach (var geom in _geometries.Skip(1))
            {
                _extent.ExpandToInclude(geom.Envelope);
            }
        }
        else
        {
            _extent = new Envelope();
        }
    }

    public string Name => "Memory";
    
    public string? Description { get; set; }
    
    public string ConnectionString => "memory://";
    
    public DataSourceType SourceType => DataSourceType.Memory;
    
    public int SRID => 0;
    
    public Envelope? Extent => _extent;
    
    public bool IsConnected => true;
    
    public bool IsReadOnly => false;

    public IEnumerable<string> GetTableNames()
    {
        return new[] { "default" };
    }

    public Task<bool> OpenAsync()
    {
        return Task.FromResult(true);
    }

    public void Close()
    {
        // 메모리 데이터소스는 특별한 close 작업 불필요
    }

    public Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        var schema = new TableSchema
        {
            TableName = tableName,
            GeometryColumn = "geometry",
            GeometryType = _geometries.FirstOrDefault()?.GeometryType.ToString() ?? "Unknown",
            SRID = 0,
            PrimaryKeyColumn = "ID",
            FeatureCount = _features.Count,
            Extent = _extent
        };
        
        schema.Columns.Add(new ColumnInfo
        {
            Name = "ID",
            DataType = typeof(int),
            AllowNull = false,
            IsUnique = true
        });
        
        return Task.FromResult<TableSchema?>(schema);
    }

    public Task<long> GetFeatureCountAsync(string tableName, Engine.Data.Sources.IQueryFilter? filter = null)
    {
        if (filter?.SpatialFilter != null)
        {
            return Task.FromResult((long)_features.Count(f => f.Geometry?.Envelope.Intersects(filter.SpatialFilter.FilterGeometry.Envelope) ?? false));
        }
        return Task.FromResult((long)_features.Count);
    }

    public long GetFeatureCount()
    {
        return _features.Count;
    }

    public Task<Envelope?> GetExtentAsync(string tableName)
    {
        return Task.FromResult<Envelope?>(_extent);
    }

    public Envelope GetExtent()
    {
        return _extent;
    }

    public async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string layerName, Engine.Data.Sources.IQueryFilter? filter = null)
    {
        foreach (var feature in GetFeatures(filter?.SpatialFilter?.FilterGeometry.Envelope ?? _extent))
        {
            yield return feature;
        }
    }

    public Task<List<IFeature>> GetFeaturesAsync(string tableName, Engine.Data.Sources.IQueryFilter? filter = null)
    {
        var features = GetFeatures(filter?.SpatialFilter?.FilterGeometry.Envelope ?? _extent).ToList();
        return Task.FromResult(features);
    }

    public IEnumerable<IFeature> GetFeatures(Envelope envelope)
    {
        return _features.Where(f => f.Geometry?.Envelope.Intersects(envelope) ?? false);
    }

    public IEnumerable<IFeature> GetFeatures()
    {
        return _features;
    }

    public Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        if (id is int intId && intId > 0 && intId <= _features.Count)
        {
            return Task.FromResult<IFeature?>(_features[intId - 1]);
        }
        return Task.FromResult<IFeature?>(null);
    }

    public Task<bool> InsertFeatureAsync(string tableName, IFeature feature)
    {
        _features.Add(feature);
        if (feature.Geometry != null)
        {
            _extent.ExpandToInclude(feature.Geometry.Envelope);
        }
        return Task.FromResult(true);
    }

    public Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        var id = feature.Attributes?["ID"];
        if (id is int intId && intId > 0 && intId <= _features.Count)
        {
            _features[intId - 1] = feature;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> DeleteFeatureAsync(string tableName, object id)
    {
        if (id is int intId && intId > 0 && intId <= _features.Count)
        {
            _features.RemoveAt(intId - 1);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public void Open()
    {
        // 메모리 데이터 소스는 항상 열려있음
    }
    
    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(true);
    }

    public Task<DataSourceValidationResult> ValidateAsync()
    {
        var result = new DataSourceValidationResult
        {
            IsValid = true,
            ValidatedTableCount = 1
        };
        
        if (_features.Count == 0)
        {
            result.Warnings.Add("데이터 소스에 피처가 없습니다.");
        }
        
        result.Information.Add($"총 {_features.Count}개의 피처가 로드됨");
        
        return Task.FromResult(result);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _features.Clear();
            _geometries.Clear();
            _isDisposed = true;
        }
    }
}