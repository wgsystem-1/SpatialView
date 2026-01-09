using System.Collections.Concurrent;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// 메모리 기반 데이터 소스
/// 메모리에서 피처를 관리하는 데이터 소스 (테스트, 임시 데이터용)
/// </summary>
public class MemoryDataSource : DataSourceBase
{
    private readonly ConcurrentDictionary<string, MemoryTable> _tables;
    private readonly string _name;
    private int _nextFeatureId = 1;
    
    /// <inheritdoc/>
    public override string Name => _name;
    
    /// <inheritdoc/>
    public override string ConnectionString => $"memory://{_name}";
    
    /// <inheritdoc/>
    public override DataSourceType SourceType => DataSourceType.Memory;
    
    /// <inheritdoc/>
    public override int SRID { get; protected set; }
    
    /// <inheritdoc/>
    public override bool IsReadOnly => false;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="name">데이터 소스 이름</param>
    /// <param name="srid">좌표계 SRID</param>
    public MemoryDataSource(string name, int srid = 4326)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        
        _name = name;
        SRID = srid;
        _tables = new ConcurrentDictionary<string, MemoryTable>();
        
        Description = $"Memory data source: {name}";
    }
    
    /// <summary>
    /// 새 테이블 생성
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="geometryType">지오메트리 타입</param>
    /// <param name="srid">좌표계 SRID (기본값: 데이터 소스의 SRID)</param>
    /// <returns>생성 성공 여부</returns>
    public bool CreateTable(string tableName, string geometryType = "Geometry", int? srid = null)
    {
        ThrowIfDisposed();
        
        if (!ValidateTableName(tableName))
        {
            LogError($"Invalid table name: {tableName}");
            return false;
        }
        
        var table = new MemoryTable(tableName, geometryType, srid ?? SRID);
        return _tables.TryAdd(tableName, table);
    }
    
    /// <summary>
    /// 테이블 삭제
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <returns>삭제 성공 여부</returns>
    public bool DropTable(string tableName)
    {
        ThrowIfDisposed();
        
        return _tables.TryRemove(tableName, out var table) && table != null;
    }
    
    /// <summary>
    /// 테이블 존재 여부 확인
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <returns>존재 여부</returns>
    public bool TableExists(string tableName)
    {
        ThrowIfDisposed();
        return _tables.ContainsKey(tableName);
    }
    
    /// <inheritdoc/>
    public override IEnumerable<string> GetTableNames()
    {
        ThrowIfDisposed();
        return _tables.Keys.ToList();
    }
    
    /// <inheritdoc/>
    public override Task<bool> OpenAsync()
    {
        ThrowIfDisposed();
        
        IsConnected = true;
        return Task.FromResult(true);
    }
    
    /// <inheritdoc/>
    public override void Close()
    {
        ThrowIfDisposed();
        
        IsConnected = false;
        // 메모리 데이터는 유지됨
    }
    
    /// <inheritdoc/>
    public override Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            return Task.FromResult<TableSchema?>(null);
        }
        
        var schema = new TableSchema
        {
            TableName = tableName,
            GeometryColumn = "geometry",
            GeometryType = table.GeometryType,
            SRID = table.SRID,
            PrimaryKeyColumn = "id",
            FeatureCount = table.Features.Count,
            Extent = CalculateTableExtent(table)
        };
        
        // 컬럼 정보 구성
        schema.Columns.Add(new ColumnInfo
        {
            Name = "id",
            DataType = typeof(int),
            AllowNull = false,
            IsUnique = true
        });
        
        schema.Columns.Add(new ColumnInfo
        {
            Name = "geometry",
            DataType = typeof(Geometry.IGeometry),
            DatabaseTypeName = table.GeometryType,
            AllowNull = true
        });
        
        // 피처들에서 속성 정보 추출
        var attributeTypes = new Dictionary<string, Type>();
        foreach (var feature in table.Features.Values.Take(100)) // 최대 100개 샘플링
        {
            foreach (var attrName in feature.Attributes.AttributeNames)
            {
                if (attrName == "id" || attrName == "geometry") continue;
                
                var value = feature.GetAttribute(attrName);
                if (value != null)
                {
                    var valueType = value.GetType();
                    if (!attributeTypes.ContainsKey(attrName))
                    {
                        attributeTypes[attrName] = valueType;
                    }
                    else if (attributeTypes[attrName] != valueType)
                    {
                        attributeTypes[attrName] = typeof(string); // 타입 충돌 시 문자열로
                    }
                }
            }
        }
        
        foreach (var attr in attributeTypes)
        {
            schema.Columns.Add(new ColumnInfo
            {
                Name = attr.Key,
                DataType = attr.Value,
                AllowNull = true
            });
        }
        
        return Task.FromResult<TableSchema?>(schema);
    }
    
    /// <inheritdoc/>
    public override Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            return Task.FromResult(0L);
        }
        
        if (filter == null)
        {
            return Task.FromResult((long)table.Features.Count);
        }
        
        // 필터링된 개수 계산
        var filteredFeatures = ApplyFilter(table, filter);
        return Task.FromResult((long)filteredFeatures.Count());
    }
    
    /// <inheritdoc/>
    public override Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            return Task.FromResult<Geometry.Envelope?>(null);
        }
        
        var extent = CalculateTableExtent(table);
        return Task.FromResult(extent);
    }
    
    /// <inheritdoc/>
    public override async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            yield break;
        }
        
        var features = ApplyFilter(table, filter);
        
        foreach (var feature in features)
        {
            yield return feature;
        }
        
        await Task.CompletedTask; // 비동기 시그니처 유지
    }
    
    /// <inheritdoc/>
    public override Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            return Task.FromResult<IFeature?>(null);
        }
        
        if (int.TryParse(id.ToString(), out var featureId) && table.Features.TryGetValue(featureId, out var feature))
        {
            return Task.FromResult<IFeature?>(feature);
        }
        
        return Task.FromResult<IFeature?>(null);
    }
    
    /// <inheritdoc/>
    public override Task<bool> InsertFeatureAsync(string tableName, IFeature feature)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            LogError($"Table not found: {tableName}");
            return Task.FromResult(false);
        }
        
        try
        {
            // 피처 ID 결정: 피처에 이미 ID가 있으면 사용, 없으면 자동 할당
            int featureId;
            if (feature.Id != null)
            {
                // 기존 ID를 사용 (FileGDB의 FID 등)
                if (feature.Id is IConvertible)
                {
                    try
                    {
                        featureId = Convert.ToInt32(feature.Id);
                    }
                    catch
                    {
                        featureId = Interlocked.Increment(ref _nextFeatureId);
                    }
                }
                else if (int.TryParse(feature.Id.ToString(), out var parsedId))
                {
                    featureId = parsedId;
                }
                else
                {
                    featureId = Interlocked.Increment(ref _nextFeatureId);
                }
            }
            else
            {
                // ID가 없으면 자동 할당
                featureId = Interlocked.Increment(ref _nextFeatureId);
            }
            
            // ID를 속성에도 저장 (조회용)
            feature.Attributes["id"] = featureId;
            if (feature.Id == null || !featureId.Equals(feature.Id))
            {
                feature.Id = featureId;
            }
            
            // 테이블에 추가
            table.Features[featureId] = feature;
            
            // 공간 인덱스에 추가
            if (feature.Geometry?.Envelope != null)
            {
                table.SpatialIndex.Insert(feature.Geometry.Envelope, feature);
            }
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to insert feature", ex);
            return Task.FromResult(false);
        }
    }
    
    /// <inheritdoc/>
    public override Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            LogError($"Table not found: {tableName}");
            return Task.FromResult(false);
        }
        
        try
        {
            var featureId = feature.GetAttribute("id");
            if (featureId == null || !int.TryParse(featureId.ToString(), out var id))
            {
                LogError("Feature must have a valid ID for update");
                return Task.FromResult(false);
            }
            
            if (!table.Features.ContainsKey(id))
            {
                LogError($"Feature with ID {id} not found");
                return Task.FromResult(false);
            }
            
            // 기존 피처 제거 (공간 인덱스에서도)
            if (table.Features.TryGetValue(id, out var oldFeature) && oldFeature.Geometry?.Envelope != null)
            {
                table.SpatialIndex.Remove(oldFeature.Geometry.Envelope, oldFeature);
            }
            
            // 새 피처 추가
            table.Features[id] = feature;
            
            // 공간 인덱스에 추가
            if (feature.Geometry?.Envelope != null)
            {
                table.SpatialIndex.Insert(feature.Geometry.Envelope, feature);
            }
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LogError("Failed to update feature", ex);
            return Task.FromResult(false);
        }
    }
    
    /// <inheritdoc/>
    public override Task<bool> DeleteFeatureAsync(string tableName, object id)
    {
        ThrowIfDisposed();
        
        if (!_tables.TryGetValue(tableName, out var table))
        {
            LogError($"Table not found: {tableName}");
            return Task.FromResult(false);
        }
        
        try
        {
            if (!int.TryParse(id.ToString(), out var featureId))
            {
                LogError($"Invalid feature ID: {id}");
                return Task.FromResult(false);
            }
            
            if (table.Features.TryRemove(featureId, out var feature))
            {
                // 공간 인덱스에서도 제거
                if (feature.Geometry?.Envelope != null)
                {
                    table.SpatialIndex.Remove(feature.Geometry.Envelope, feature);
                }
                
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            LogError("Failed to delete feature", ex);
            return Task.FromResult(false);
        }
    }
    
    #region 헬퍼 메서드
    
    /// <summary>
    /// 테이블의 전체 영역 계산
    /// </summary>
    private Geometry.Envelope? CalculateTableExtent(MemoryTable table)
    {
        Geometry.Envelope? extent = null;
        
        foreach (var feature in table.Features.Values)
        {
            if (feature.Geometry?.Envelope != null)
            {
                if (extent == null)
                {
                    extent = feature.Geometry.Envelope.Copy();
                }
                else
                {
                    extent.ExpandToInclude(feature.Geometry.Envelope);
                }
            }
        }
        
        return extent;
    }
    
    /// <summary>
    /// 필터 적용
    /// </summary>
    private IEnumerable<IFeature> ApplyFilter(MemoryTable table, IQueryFilter? filter)
    {
        IEnumerable<IFeature> features = table.Features.Values;
        
        // 공간 필터 적용
        if (filter?.SpatialFilter != null)
        {
            var spatialFeatures = table.SpatialIndex.Query(filter.SpatialFilter.FilterGeometry);
            features = features.Where(f => spatialFeatures.Contains(f));
        }
        
        // 속성 필터 적용
        if (filter?.AttributeFilter != null && !string.IsNullOrEmpty(filter.AttributeFilter.WhereClause))
        {
            features = features.Where(f => EvaluateAttributeFilter(f, filter.AttributeFilter));
        }
        
        // 정렬 적용
        if (filter?.OrderBy?.Count > 0)
        {
            features = ApplySorting(features, filter.OrderBy);
        }
        
        // 오프셋 적용
        if (filter?.Offset > 0)
        {
            features = features.Skip(filter.Offset);
        }
        
        // 최대 개수 제한
        if (filter?.MaxFeatures > 0)
        {
            features = features.Take(filter.MaxFeatures);
        }
        
        return features;
    }
    
    /// <summary>
    /// 속성 필터 평가
    /// </summary>
    private bool EvaluateAttributeFilter(IFeature feature, IAttributeFilter attributeFilter)
    {
        // 간단한 속성 필터 평가
        var whereClause = attributeFilter.WhereClause.ToLowerInvariant();
        
        foreach (var attrName in feature.Attributes.AttributeNames)
        {
            var value = feature.GetAttribute(attrName)?.ToString()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(value) && whereClause.Contains(value))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 정렬 적용
    /// </summary>
    private IEnumerable<IFeature> ApplySorting(IEnumerable<IFeature> features, IList<SortField> sortFields)
    {
        var orderedFeatures = features.AsQueryable();
        
        for (int i = 0; i < sortFields.Count; i++)
        {
            var sortField = sortFields[i];
            
            if (i == 0)
            {
                orderedFeatures = sortField.Direction == SortDirection.Ascending
                    ? orderedFeatures.OrderBy(f => f.GetAttribute(sortField.FieldName))
                    : orderedFeatures.OrderByDescending(f => f.GetAttribute(sortField.FieldName));
            }
            else
            {
                var orderedQuery = (IOrderedQueryable<IFeature>)orderedFeatures;
                orderedFeatures = sortField.Direction == SortDirection.Ascending
                    ? orderedQuery.ThenBy(f => f.GetAttribute(sortField.FieldName))
                    : orderedQuery.ThenByDescending(f => f.GetAttribute(sortField.FieldName));
            }
        }
        
        return orderedFeatures.AsEnumerable();
    }
    
    #endregion
    
    #region 중첩 클래스
    
    /// <summary>
    /// 메모리 테이블 클래스
    /// </summary>
    private class MemoryTable
    {
        public string TableName { get; }
        public string GeometryType { get; }
        public int SRID { get; }
        public ConcurrentDictionary<int, IFeature> Features { get; }
        public Indexing.ISpatialIndex<IFeature> SpatialIndex { get; }
        
        public MemoryTable(string tableName, string geometryType, int srid)
        {
            TableName = tableName;
            GeometryType = geometryType;
            SRID = srid;
            Features = new ConcurrentDictionary<int, IFeature>();
            SpatialIndex = new Indexing.RTreeIndex<IFeature>();
        }
    }
    
    #endregion
    
    /// <summary>
    /// 데이터 소스 초기화 (모든 테이블과 데이터 제거)
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();
        
        foreach (var table in _tables.Values)
        {
            table.SpatialIndex.Clear();
            table.Features.Clear();
        }
        
        _tables.Clear();
        _nextFeatureId = 1;
    }
    
    /// <summary>
    /// 통계 정보 가져오기
    /// </summary>
    /// <returns>통계 정보</returns>
    public MemoryDataSourceStatistics GetStatistics()
    {
        ThrowIfDisposed();
        
        var stats = new MemoryDataSourceStatistics
        {
            TableCount = _tables.Count,
            TotalFeatures = _tables.Values.Sum(t => t.Features.Count),
            NextFeatureId = _nextFeatureId
        };
        
        foreach (var table in _tables.Values)
        {
            stats.TableStatistics[table.TableName] = new MemoryTableStatistics
            {
                FeatureCount = table.Features.Count,
                SpatialIndexStatistics = table.SpatialIndex.Statistics
            };
        }
        
        return stats;
    }
}

/// <summary>
/// 메모리 데이터 소스 통계 정보
/// </summary>
public class MemoryDataSourceStatistics
{
    /// <summary>
    /// 테이블 수
    /// </summary>
    public int TableCount { get; set; }
    
    /// <summary>
    /// 총 피처 수
    /// </summary>
    public int TotalFeatures { get; set; }
    
    /// <summary>
    /// 다음 피처 ID
    /// </summary>
    public int NextFeatureId { get; set; }
    
    /// <summary>
    /// 테이블별 통계 정보
    /// </summary>
    public Dictionary<string, MemoryTableStatistics> TableStatistics { get; } = new();
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Tables: {TableCount}, Features: {TotalFeatures}, Next ID: {NextFeatureId}";
    }
}

/// <summary>
/// 메모리 테이블 통계 정보
/// </summary>
public class MemoryTableStatistics
{
    /// <summary>
    /// 피처 수
    /// </summary>
    public int FeatureCount { get; set; }
    
    /// <summary>
    /// 공간 인덱스 통계
    /// </summary>
    public Indexing.SpatialIndexStatistics? SpatialIndexStatistics { get; set; }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Features: {FeatureCount}, Index: {SpatialIndexStatistics}";
    }
}