using System.Diagnostics;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// 데이터 소스 기본 구현 클래스
/// 공통 기능들을 제공하는 추상 클래스
/// </summary>
public abstract class DataSourceBase : IDataSource
{
    private bool _disposed = false;
    protected readonly object _lockObject = new();
    
    /// <inheritdoc/>
    public abstract string Name { get; }
    
    /// <inheritdoc/>
    public string? Description { get; set; }
    
    /// <inheritdoc/>
    public abstract string ConnectionString { get; }
    
    /// <inheritdoc/>
    public abstract DataSourceType SourceType { get; }
    
    /// <inheritdoc/>
    public virtual int SRID { get; protected set; }
    
    /// <inheritdoc/>
    public virtual Geometry.Envelope? Extent { get; protected set; }
    
    /// <inheritdoc/>
    public bool IsConnected { get; protected set; }
    
    /// <inheritdoc/>
    public virtual bool IsReadOnly => true;
    
    /// <summary>
    /// 마지막 오류 메시지
    /// </summary>
    protected string? LastError { get; set; }
    
    /// <summary>
    /// 연결 시도 횟수
    /// </summary>
    protected int ConnectionAttempts { get; set; }
    
    /// <summary>
    /// 최대 연결 시도 횟수
    /// </summary>
    protected virtual int MaxConnectionAttempts => 3;
    
    /// <summary>
    /// 연결 타임아웃 (초)
    /// </summary>
    protected virtual int ConnectionTimeoutSeconds => 30;
    
    #region 추상 메서드
    
    /// <inheritdoc/>
    public abstract IEnumerable<string> GetTableNames();
    
    /// <inheritdoc/>
    public abstract Task<bool> OpenAsync();
    
    /// <inheritdoc/>
    public virtual void Open()
    {
        OpenAsync().GetAwaiter().GetResult();
    }
    
    /// <inheritdoc/>
    public abstract void Close();
    
    /// <inheritdoc/>
    public abstract Task<TableSchema?> GetSchemaAsync(string tableName);
    
    /// <inheritdoc/>
    public abstract Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null);
    
    /// <inheritdoc/>
    public abstract Task<Geometry.Envelope?> GetExtentAsync(string tableName);
    
    /// <inheritdoc/>
    public abstract IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null);
    
    /// <inheritdoc/>
    public virtual async Task<List<IFeature>> GetFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        var features = new List<IFeature>();
        await foreach (var feature in QueryFeaturesAsync(tableName, filter))
        {
            features.Add(feature);
        }
        return features;
    }
    
    /// <inheritdoc/>
    public abstract Task<IFeature?> GetFeatureAsync(string tableName, object id);
    
    #endregion
    
    #region 가상 메서드 (기본 구현 제공)
    
    /// <inheritdoc/>
    public virtual Task<bool> InsertFeatureAsync(string tableName, IFeature feature)
    {
        if (IsReadOnly)
        {
            LastError = "Data source is read-only";
            return Task.FromResult(false);
        }
        
        throw new NotImplementedException("Insert operation not implemented for this data source type");
    }
    
    /// <inheritdoc/>
    public virtual Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        if (IsReadOnly)
        {
            LastError = "Data source is read-only";
            return Task.FromResult(false);
        }
        
        throw new NotImplementedException("Update operation not implemented for this data source type");
    }
    
    /// <inheritdoc/>
    public virtual Task<bool> DeleteFeatureAsync(string tableName, object id)
    {
        if (IsReadOnly)
        {
            LastError = "Data source is read-only";
            return Task.FromResult(false);
        }
        
        throw new NotImplementedException("Delete operation not implemented for this data source type");
    }
    
    /// <inheritdoc/>
    public virtual async Task<bool> TestConnectionAsync()
    {
        try
        {
            var wasConnected = IsConnected;
            
            if (!IsConnected)
            {
                var result = await OpenAsync();
                if (!result) return false;
            }
            
            // 간단한 테스트: 테이블 목록 가져오기
            var tables = GetTableNames().Take(1).ToList();
            
            if (!wasConnected)
            {
                Close();
            }
            
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }
    
    /// <inheritdoc/>
    public virtual IEnumerable<IFeature> GetFeatures(Geometry.Envelope envelope)
    {
        // 기본 구현: 첫 번째 테이블에서 영역 필터로 피처를 가져옴
        var tableNames = GetTableNames();
        var firstTable = tableNames.FirstOrDefault();
        
        if (string.IsNullOrEmpty(firstTable))
        {
            return Enumerable.Empty<IFeature>();
        }
        
        try
        {
            // 영역 필터 생성  
            var envelopeGeometry = envelope.ToPolygon();
            var spatialFilter = new SpatialFilter(envelopeGeometry, SpatialRelationship.Intersects);
            
            var queryFilter = new QueryFilter
            {
                SpatialFilter = spatialFilter
            };
            
            // 비동기 메서드를 동기로 호출 (주의: 데드락 가능성)
            var features = GetFeaturesAsync(firstTable, queryFilter).GetAwaiter().GetResult();
            return features;
        }
        catch (Exception ex)
        {
            LogError($"Error getting features by envelope: {ex.Message}", ex);
            return Enumerable.Empty<IFeature>();
        }
    }

    /// <inheritdoc/>
    public virtual async Task<DataSourceValidationResult> ValidateAsync()
    {
        var result = new DataSourceValidationResult();
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 연결 테스트
            if (!await TestConnectionAsync())
            {
                result.Errors.Add($"Connection test failed: {LastError}");
                result.IsValid = false;
                result.ValidationTime = stopwatch.Elapsed;
                return result;
            }
            
            result.Information.Add("Connection test passed");
            
            // 연결이 안 되어 있으면 연결
            var wasConnected = IsConnected;
            if (!IsConnected)
            {
                await OpenAsync();
            }
            
            try
            {
                // 테이블 목록 검증
                var tables = GetTableNames().ToList();
                result.ValidatedTableCount = tables.Count;
                
                if (tables.Count == 0)
                {
                    result.Warnings.Add("No tables found in data source");
                }
                else
                {
                    result.Information.Add($"Found {tables.Count} table(s)");
                    
                    // 각 테이블 검증
                    foreach (var tableName in tables)
                    {
                        try
                        {
                            var schema = await GetSchemaAsync(tableName);
                            if (schema == null)
                            {
                                result.Warnings.Add($"Could not retrieve schema for table: {tableName}");
                                continue;
                            }
                            
                            // 지오메트리 컬럼 확인
                            if (string.IsNullOrEmpty(schema.GeometryColumn))
                            {
                                result.Warnings.Add($"Table '{tableName}' has no geometry column");
                            }
                            
                            // SRID 확인
                            if (schema.SRID <= 0)
                            {
                                result.Warnings.Add($"Table '{tableName}' has invalid or unknown SRID: {schema.SRID}");
                            }
                            
                            // 피처 수 확인
                            var featureCount = await GetFeatureCountAsync(tableName);
                            if (featureCount == 0)
                            {
                                result.Warnings.Add($"Table '{tableName}' contains no features");
                            }
                            else
                            {
                                result.Information.Add($"Table '{tableName}': {featureCount} features");
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Error validating table '{tableName}': {ex.Message}");
                        }
                    }
                }
            }
            finally
            {
                if (!wasConnected)
                {
                    Close();
                }
            }
            
            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Validation failed: {ex.Message}");
            result.IsValid = false;
        }
        finally
        {
            stopwatch.Stop();
            result.ValidationTime = stopwatch.Elapsed;
        }
        
        return result;
    }
    
    #endregion
    
    #region 헬퍼 메서드
    
    /// <summary>
    /// 연결 문자열의 유효성 검사
    /// </summary>
    /// <param name="connectionString">연결 문자열</param>
    /// <returns>유효성 검사 결과</returns>
    protected virtual bool ValidateConnectionString(string connectionString)
    {
        return !string.IsNullOrWhiteSpace(connectionString);
    }
    
    /// <summary>
    /// 테이블 이름의 유효성 검사
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <returns>유효성 검사 결과</returns>
    protected virtual bool ValidateTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return false;
            
        // 기본적인 SQL 인젝션 방지
        var invalidPatterns = new[] { ";", "--", "/*", "*/", "xp_", "sp_" };
        var lowerTableName = tableName.ToLowerInvariant();
        
        return !invalidPatterns.Any(pattern => lowerTableName.Contains(pattern));
    }
    
    /// <summary>
    /// 쿼리 필터를 SQL WHERE 절로 변환
    /// </summary>
    /// <param name="filter">쿼리 필터</param>
    /// <returns>WHERE 절 문자열</returns>
    protected virtual string BuildWhereClause(IQueryFilter? filter)
    {
        if (filter == null) return string.Empty;
        
        var conditions = new List<string>();
        
        // 공간 필터
        if (filter.SpatialFilter != null)
        {
            var spatialCondition = BuildSpatialCondition(filter.SpatialFilter);
            if (!string.IsNullOrEmpty(spatialCondition))
            {
                conditions.Add(spatialCondition);
            }
        }
        
        // 속성 필터
        if (filter.AttributeFilter != null && !string.IsNullOrEmpty(filter.AttributeFilter.WhereClause))
        {
            conditions.Add($"({filter.AttributeFilter.WhereClause})");
        }
        
        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
    }
    
    /// <summary>
    /// 공간 조건을 SQL로 변환 (하위 클래스에서 재정의)
    /// </summary>
    /// <param name="spatialFilter">공간 필터</param>
    /// <returns>SQL 조건</returns>
    protected virtual string BuildSpatialCondition(ISpatialFilter spatialFilter)
    {
        // 기본 구현 - 하위 클래스에서 재정의 필요
        return string.Empty;
    }
    
    /// <summary>
    /// ORDER BY 절 구성
    /// </summary>
    /// <param name="filter">쿼리 필터</param>
    /// <returns>ORDER BY 절</returns>
    protected virtual string BuildOrderByClause(IQueryFilter? filter)
    {
        if (filter?.OrderBy == null || filter.OrderBy.Count == 0)
            return string.Empty;
        
        var orderByItems = filter.OrderBy.Select(sort => 
            $"{EscapeColumnName(sort.FieldName)} {(sort.Direction == SortDirection.Ascending ? "ASC" : "DESC")}");
        
        return "ORDER BY " + string.Join(", ", orderByItems);
    }
    
    /// <summary>
    /// LIMIT/OFFSET 절 구성
    /// </summary>
    /// <param name="filter">쿼리 필터</param>
    /// <returns>LIMIT/OFFSET 절</returns>
    protected virtual string BuildLimitClause(IQueryFilter? filter)
    {
        if (filter == null) return string.Empty;
        
        var parts = new List<string>();
        
        if (filter.MaxFeatures > 0)
        {
            parts.Add($"LIMIT {filter.MaxFeatures}");
        }
        
        if (filter.Offset > 0)
        {
            parts.Add($"OFFSET {filter.Offset}");
        }
        
        return string.Join(" ", parts);
    }
    
    /// <summary>
    /// 컬럼 이름 이스케이프 (하위 클래스에서 재정의)
    /// </summary>
    /// <param name="columnName">컬럼 이름</param>
    /// <returns>이스케이프된 컬럼 이름</returns>
    protected virtual string EscapeColumnName(string columnName)
    {
        // 기본 구현: 더블 쿼트로 감싸기
        return $"\"{columnName}\"";
    }
    
    /// <summary>
    /// 안전한 연결 해제
    /// </summary>
    protected virtual void SafeClose()
    {
        try
        {
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error closing connection: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 오류 로깅
    /// </summary>
    /// <param name="message">오류 메시지</param>
    /// <param name="exception">예외 (옵션)</param>
    protected virtual void LogError(string message, Exception? exception = null)
    {
        LastError = message;
        var logMessage = exception != null ? $"{message}: {exception.Message}" : message;
        System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] {logMessage}");
    }
    
    #endregion
    
    #region IDisposable 구현
    
    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// 리소스 해제
    /// </summary>
    /// <param name="disposing">관리 리소스 해제 여부</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 관리 리소스 해제
                SafeClose();
            }
            
            // 비관리 리소스 해제 (필요시 하위 클래스에서 구현)
            
            _disposed = true;
        }
    }
    
    /// <summary>
    /// 소멸자
    /// </summary>
    ~DataSourceBase()
    {
        Dispose(false);
    }
    
    /// <summary>
    /// 해제 상태 확인
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
    
    #endregion
}