using Microsoft.Data.Sqlite;
using System.Data;
using System.Text;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// SQLite/SpatiaLite 데이터 소스
/// SQLite 데이터베이스의 공간 데이터에 접근하는 데이터 소스
/// </summary>
public class SqliteDataSource : DataSourceBase
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private readonly Dictionary<string, TableSchema> _schemaCache;
    private bool _isSpatiaLite;
    
    /// <inheritdoc/>
    public override string Name { get; }
    
    /// <inheritdoc/>
    public override string ConnectionString => _connectionString;
    
    /// <inheritdoc/>
    public override DataSourceType SourceType => DataSourceType.SQLite;
    
    /// <inheritdoc/>
    public override int SRID { get; protected set; } = 4326;
    
    /// <inheritdoc/>
    public override bool IsReadOnly { get; } = false;
    
    /// <summary>
    /// SpatiaLite 확장 사용 여부
    /// </summary>
    public bool IsSpatiaLite => _isSpatiaLite;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="connectionString">SQLite 연결 문자열</param>
    /// <param name="name">데이터 소스 이름 (옵션)</param>
    public SqliteDataSource(string connectionString, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        
        _connectionString = connectionString;
        Name = name ?? ExtractDatabaseName(connectionString);
        _schemaCache = new Dictionary<string, TableSchema>();
        
        Description = $"SQLite database: {Name}";
        
        // 연결 문자열에서 읽기 전용 모드 확인
        IsReadOnly = connectionString.Contains("Mode=ReadOnly", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// 파일 경로에서 생성
    /// </summary>
    /// <param name="filePath">SQLite 파일 경로</param>
    /// <param name="readOnly">읽기 전용 모드</param>
    /// <returns>SQLite 데이터 소스</returns>
    public static SqliteDataSource FromFile(string filePath, bool readOnly = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate
        };
        
        var name = Path.GetFileNameWithoutExtension(filePath);
        return new SqliteDataSource(builder.ConnectionString, name);
    }
    
    /// <inheritdoc/>
    public override IEnumerable<string> GetTableNames()
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
        {
            throw new InvalidOperationException("Connection is not open");
        }
        
        try
        {
            using var command = _connection!.CreateCommand();
            
            // 지오메트리 컬럼이 있는 테이블만 조회
            if (_isSpatiaLite)
            {
                command.CommandText = @"
                    SELECT DISTINCT f_table_name 
                    FROM geometry_columns 
                    ORDER BY f_table_name";
            }
            else
            {
                // 일반 SQLite - 모든 테이블 반환
                command.CommandText = @"
                    SELECT name 
                    FROM sqlite_master 
                    WHERE type='table' AND name NOT LIKE 'sqlite_%'
                    ORDER BY name";
            }
            
            var tableNames = new List<string>();
            using var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                var tableName = reader.GetString(0);
                if (!string.IsNullOrEmpty(tableName))
                {
                    tableNames.Add(tableName);
                }
            }
            
            return tableNames;
        }
        catch (Exception ex)
        {
            LogError("Failed to get table names", ex);
            return Enumerable.Empty<string>();
        }
    }
    
    /// <inheritdoc/>
    public override async Task<bool> OpenAsync()
    {
        ThrowIfDisposed();
        
        if (IsConnected) return true;
        
        try
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();
            
            // SpatiaLite 확장 확인
            await CheckSpatiaLiteAsync();
            
            // 기본 SRID 설정
            if (_isSpatiaLite)
            {
                SRID = await GetDefaultSRIDAsync();
            }
            
            IsConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            LogError("Failed to open SQLite connection", ex);
            SafeClose();
            return false;
        }
    }
    
    /// <inheritdoc/>
    public override void Close()
    {
        ThrowIfDisposed();
        
        if (_connection != null)
        {
            _connection.Close();
            _connection.Dispose();
            _connection = null;
        }
        
        _schemaCache.Clear();
        IsConnected = false;
    }
    
    /// <inheritdoc/>
    public override async Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        ThrowIfDisposed();
        
        if (!ValidateTableName(tableName))
        {
            return null;
        }
        
        if (!IsConnected)
        {
            await OpenAsync();
        }
        
        // 캐시된 스키마 확인
        if (_schemaCache.TryGetValue(tableName, out var cachedSchema))
        {
            return cachedSchema;
        }
        
        try
        {
            var schema = await BuildTableSchemaAsync(tableName);
            if (schema != null)
            {
                _schemaCache[tableName] = schema;
            }
            
            return schema;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get schema for table '{tableName}'", ex);
            return null;
        }
    }
    
    /// <inheritdoc/>
    public override async Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
        {
            await OpenAsync();
        }
        
        try
        {
            using var command = _connection!.CreateCommand();
            
            var whereClause = BuildWhereClause(filter);
            command.CommandText = $"SELECT COUNT(*) FROM {EscapeTableName(tableName)} {whereClause}";
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        catch (Exception ex)
        {
            LogError($"Failed to get feature count for table '{tableName}'", ex);
            return 0;
        }
    }
    
    /// <inheritdoc/>
    public override async Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
        {
            await OpenAsync();
        }
        
        try
        {
            var schema = await GetSchemaAsync(tableName);
            if (schema?.GeometryColumn == null)
            {
                return null;
            }
            
            using var command = _connection!.CreateCommand();
            
            if (_isSpatiaLite)
            {
                // SpatiaLite의 Extent 함수 사용
                command.CommandText = $@"
                    SELECT Extent({EscapeColumnName(schema.GeometryColumn)}) 
                    FROM {EscapeTableName(tableName)}";
                
                var extentResult = await command.ExecuteScalarAsync();
                if (extentResult is string extentStr && !string.IsNullOrEmpty(extentStr))
                {
                    return ParseSpatiaLiteExtent(extentStr);
                }
            }
            else
            {
                // 일반 SQLite - 수동으로 경계 계산
                LogError($"Extent calculation not supported for non-spatial SQLite tables");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get extent for table '{tableName}'", ex);
            return null;
        }
    }
    
    /// <inheritdoc/>
    public override async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        ThrowIfDisposed();
        
        if (!IsConnected)
        {
            await OpenAsync();
        }
        
        var schema = await GetSchemaAsync(tableName);
        if (schema == null)
        {
            yield break;
        }
        
        using var command = _connection!.CreateCommand();
        
        // SQL 쿼리 구성
        var sql = BuildSelectQuery(tableName, schema, filter);
        command.CommandText = sql;
        
        // 매개변수 설정
        if (filter?.AttributeFilter?.Parameters != null)
        {
            foreach (var param in filter.AttributeFilter.Parameters)
            {
                command.Parameters.AddWithValue($"@{param.Key}", param.Value);
            }
        }
        
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var feature = await CreateFeatureFromReaderAsync(reader, schema);
            if (feature != null)
            {
                yield return feature;
            }
        }
    }
    
    /// <inheritdoc/>
    public override async Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        ThrowIfDisposed();
        
        var schema = await GetSchemaAsync(tableName);
        if (schema?.PrimaryKeyColumn == null)
        {
            return null;
        }
        
        var filter = new QueryFilter
        {
            AttributeFilter = new AttributeFilter($"{schema.PrimaryKeyColumn} = @id", new Dictionary<string, object> { ["id"] = id }),
            MaxFeatures = 1
        };
        
        await foreach (var feature in QueryFeaturesAsync(tableName, filter))
        {
            return feature;
        }
        
        return null;
    }
    
    /// <inheritdoc/>
    public override async Task<bool> InsertFeatureAsync(string tableName, IFeature feature)
    {
        ThrowIfDisposed();
        
        if (IsReadOnly)
        {
            LogError("Cannot insert feature: Data source is read-only");
            return false;
        }
        
        var schema = await GetSchemaAsync(tableName);
        if (schema == null)
        {
            LogError($"Schema not found for table: {tableName}");
            return false;
        }
        
        try
        {
            using var command = _connection!.CreateCommand();
            
            var sql = BuildInsertQuery(tableName, schema, feature);
            command.CommandText = sql;
            
            // 매개변수 설정
            SetFeatureParameters(command, feature, schema);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            LogError("Failed to insert feature", ex);
            return false;
        }
    }
    
    /// <inheritdoc/>
    public override async Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        ThrowIfDisposed();
        
        if (IsReadOnly)
        {
            LogError("Cannot update feature: Data source is read-only");
            return false;
        }
        
        var schema = await GetSchemaAsync(tableName);
        if (schema?.PrimaryKeyColumn == null)
        {
            LogError("Cannot update feature: No primary key column found");
            return false;
        }
        
        var id = feature.GetAttribute(schema.PrimaryKeyColumn);
        if (id == null)
        {
            LogError("Cannot update feature: Primary key value is null");
            return false;
        }
        
        try
        {
            using var command = _connection!.CreateCommand();
            
            var sql = BuildUpdateQuery(tableName, schema, feature);
            command.CommandText = sql;
            
            // 매개변수 설정
            SetFeatureParameters(command, feature, schema);
            command.Parameters.AddWithValue("@pk", id);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            LogError("Failed to update feature", ex);
            return false;
        }
    }
    
    /// <inheritdoc/>
    public override async Task<bool> DeleteFeatureAsync(string tableName, object id)
    {
        ThrowIfDisposed();
        
        if (IsReadOnly)
        {
            LogError("Cannot delete feature: Data source is read-only");
            return false;
        }
        
        var schema = await GetSchemaAsync(tableName);
        if (schema?.PrimaryKeyColumn == null)
        {
            LogError("Cannot delete feature: No primary key column found");
            return false;
        }
        
        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = $"DELETE FROM {EscapeTableName(tableName)} WHERE {EscapeColumnName(schema.PrimaryKeyColumn)} = @id";
            command.Parameters.AddWithValue("@id", id);
            
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            LogError("Failed to delete feature", ex);
            return false;
        }
    }
    
    #region 내부 메서드
    
    /// <summary>
    /// 연결 문자열에서 데이터베이스 이름 추출
    /// </summary>
    private string ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;
            
            if (!string.IsNullOrEmpty(dataSource))
            {
                return Path.GetFileNameWithoutExtension(dataSource);
            }
        }
        catch
        {
            // 파싱 실패 시 기본값 사용
        }
        
        return "SQLite Database";
    }
    
    /// <summary>
    /// SpatiaLite 확장 확인
    /// </summary>
    private async Task CheckSpatiaLiteAsync()
    {
        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='geometry_columns' AND type='table'";
            
            var result = await command.ExecuteScalarAsync();
            _isSpatiaLite = Convert.ToInt32(result) > 0;
        }
        catch
        {
            _isSpatiaLite = false;
        }
    }
    
    /// <summary>
    /// 기본 SRID 가져오기
    /// </summary>
    private async Task<int> GetDefaultSRIDAsync()
    {
        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = "SELECT srid FROM geometry_columns LIMIT 1";
            
            var result = await command.ExecuteScalarAsync();
            if (result != null && int.TryParse(result.ToString(), out var srid))
            {
                return srid;
            }
        }
        catch
        {
            // 실패 시 기본값 사용
        }
        
        return 4326; // WGS84 기본값
    }
    
    /// <summary>
    /// 테이블 스키마 구성
    /// </summary>
    private async Task<TableSchema> BuildTableSchemaAsync(string tableName)
    {
        var schema = new TableSchema
        {
            TableName = tableName,
            SRID = SRID
        };
        
        // 컬럼 정보 가져오기
        using var command = _connection!.CreateCommand();
        command.CommandText = $"PRAGMA table_info({EscapeTableName(tableName)})";
        
        using var reader = await command.ExecuteReaderAsync();
        var columns = new List<ColumnInfo>();
        
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString("name");
            var dataType = reader.GetString("type");
            var notNull = reader.GetBoolean("notnull");
            var defaultValue = reader.IsDBNull("dflt_value") ? null : reader.GetValue("dflt_value");
            var isPrimaryKey = reader.GetBoolean("pk");
            
            var columnInfo = new ColumnInfo
            {
                Name = columnName,
                DataType = MapSqliteTypeToClrType(dataType),
                DatabaseTypeName = dataType,
                AllowNull = !notNull,
                DefaultValue = defaultValue
            };
            
            if (isPrimaryKey)
            {
                schema.PrimaryKeyColumn = columnName;
            }
            
            columns.Add(columnInfo);
        }
        
        schema.Columns.AddRange(columns);
        
        // 지오메트리 컬럼 정보 (SpatiaLite인 경우)
        if (_isSpatiaLite)
        {
            await GetGeometryColumnInfoAsync(schema);
        }
        
        // 피처 수 계산
        schema.FeatureCount = await GetFeatureCountAsync(tableName);
        
        return schema;
    }
    
    /// <summary>
    /// 지오메트리 컬럼 정보 가져오기
    /// </summary>
    private async Task GetGeometryColumnInfoAsync(TableSchema schema)
    {
        try
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = @"
                SELECT f_geometry_column, geometry_type, srid 
                FROM geometry_columns 
                WHERE f_table_name = @tableName";
            command.Parameters.AddWithValue("@tableName", schema.TableName);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                schema.GeometryColumn = reader.GetString("f_geometry_column");
                schema.GeometryType = reader.GetString("geometry_type");
                schema.SRID = reader.GetInt32("srid");
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to get geometry column info", ex);
        }
    }
    
    /// <summary>
    /// SQLite 데이터 타입을 CLR 타입으로 매핑
    /// </summary>
    private Type MapSqliteTypeToClrType(string sqliteType)
    {
        var lowerType = sqliteType.ToLowerInvariant();
        
        return lowerType switch
        {
            var t when t.Contains("int") => typeof(long),
            var t when t.Contains("real") || t.Contains("float") || t.Contains("double") => typeof(double),
            var t when t.Contains("text") || t.Contains("char") || t.Contains("clob") => typeof(string),
            var t when t.Contains("blob") => typeof(byte[]),
            var t when t.Contains("numeric") || t.Contains("decimal") => typeof(decimal),
            var t when t.Contains("bool") => typeof(bool),
            var t when t.Contains("date") || t.Contains("time") => typeof(DateTime),
            var t when t.Contains("geometry") => typeof(Geometry.IGeometry),
            _ => typeof(string)
        };
    }
    
    /// <summary>
    /// SpatiaLite Extent 문자열 파싱
    /// </summary>
    private Geometry.Envelope? ParseSpatiaLiteExtent(string extentStr)
    {
        // SpatiaLite Extent 형식: "BOX(xmin ymin,xmax ymax)"
        if (!extentStr.StartsWith("BOX(") || !extentStr.EndsWith(")"))
        {
            return null;
        }
        
        var coords = extentStr[4..^1]; // "BOX(" 와 ")" 제거
        var parts = coords.Split(',');
        
        if (parts.Length != 2) return null;
        
        var minParts = parts[0].Trim().Split(' ');
        var maxParts = parts[1].Trim().Split(' ');
        
        if (minParts.Length != 2 || maxParts.Length != 2) return null;
        
        if (double.TryParse(minParts[0], out var minX) &&
            double.TryParse(minParts[1], out var minY) &&
            double.TryParse(maxParts[0], out var maxX) &&
            double.TryParse(maxParts[1], out var maxY))
        {
            return new Geometry.Envelope(minX, maxX, minY, maxY);
        }
        
        return null;
    }
    
    /// <summary>
    /// SELECT 쿼리 구성
    /// </summary>
    private string BuildSelectQuery(string tableName, TableSchema schema, IQueryFilter? filter)
    {
        var sql = new StringBuilder("SELECT ");
        
        // 컬럼 선택
        if (filter?.Columns?.Count > 0)
        {
            sql.Append(string.Join(", ", filter.Columns.Select(EscapeColumnName)));
        }
        else
        {
            sql.Append("*");
        }
        
        sql.Append($" FROM {EscapeTableName(tableName)}");
        
        // WHERE 절
        var whereClause = BuildWhereClause(filter);
        if (!string.IsNullOrEmpty(whereClause))
        {
            sql.Append(" ").Append(whereClause);
        }
        
        // ORDER BY 절
        var orderByClause = BuildOrderByClause(filter);
        if (!string.IsNullOrEmpty(orderByClause))
        {
            sql.Append(" ").Append(orderByClause);
        }
        
        // LIMIT/OFFSET 절
        var limitClause = BuildLimitClause(filter);
        if (!string.IsNullOrEmpty(limitClause))
        {
            sql.Append(" ").Append(limitClause);
        }
        
        return sql.ToString();
    }
    
    /// <summary>
    /// INSERT 쿼리 구성
    /// </summary>
    private string BuildInsertQuery(string tableName, TableSchema schema, IFeature feature)
    {
        var columns = schema.Columns
            .Where(c => c.Name != schema.PrimaryKeyColumn || feature.GetAttribute(c.Name) != null)
            .ToList();
        
        var columnNames = string.Join(", ", columns.Select(c => EscapeColumnName(c.Name)));
        var paramNames = string.Join(", ", columns.Select(c => $"@{c.Name}"));
        
        return $"INSERT INTO {EscapeTableName(tableName)} ({columnNames}) VALUES ({paramNames})";
    }
    
    /// <summary>
    /// UPDATE 쿼리 구성
    /// </summary>
    private string BuildUpdateQuery(string tableName, TableSchema schema, IFeature feature)
    {
        var columns = schema.Columns
            .Where(c => c.Name != schema.PrimaryKeyColumn)
            .ToList();
        
        var setClause = string.Join(", ", columns.Select(c => $"{EscapeColumnName(c.Name)} = @{c.Name}"));
        
        return $"UPDATE {EscapeTableName(tableName)} SET {setClause} WHERE {EscapeColumnName(schema.PrimaryKeyColumn)} = @pk";
    }
    
    /// <summary>
    /// 피처 매개변수 설정
    /// </summary>
    private void SetFeatureParameters(SqliteCommand command, IFeature feature, TableSchema schema)
    {
        foreach (var column in schema.Columns)
        {
            if (column.Name == schema.PrimaryKeyColumn && feature.GetAttribute(column.Name) == null)
                continue;
            
            var value = feature.GetAttribute(column.Name);
            
            // 지오메트리 컬럼 처리
            if (column.Name == schema.GeometryColumn && value is Geometry.IGeometry geometry)
            {
                // SpatiaLite 지오메트리 변환
                value = ConvertGeometryToSpatiaLite(geometry);
            }
            
            command.Parameters.AddWithValue($"@{column.Name}", value ?? DBNull.Value);
        }
    }
    
    /// <summary>
    /// Reader에서 피처 생성
    /// </summary>
    private async Task<IFeature?> CreateFeatureFromReaderAsync(SqliteDataReader reader, TableSchema schema)
    {
        try
        {
            var feature = new SqliteFeature();
            
            // 모든 컬럼 읽기
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                
                // 지오메트리 컬럼 처리
                if (columnName == schema.GeometryColumn && value != null)
                {
                    feature.Geometry = await ConvertSpatiaLiteToGeometryAsync(value);
                }
                else
                {
                    feature.SetAttribute(columnName, value);
                }
            }
            
            return feature;
        }
        catch (Exception ex)
        {
            LogError("Failed to create feature from reader", ex);
            return null;
        }
    }
    
    /// <summary>
    /// 지오메트리를 SpatiaLite 형식으로 변환
    /// </summary>
    private object ConvertGeometryToSpatiaLite(Geometry.IGeometry geometry)
    {
        // TODO: 실제 SpatiaLite BLOB 형식으로 변환
        // 지금은 WKT로 임시 처리
        return geometry.ToString() ?? string.Empty;
    }
    
    /// <summary>
    /// SpatiaLite 지오메트리를 엔진 지오메트리로 변환
    /// </summary>
    private Task<Geometry.IGeometry?> ConvertSpatiaLiteToGeometryAsync(object spatiaLiteValue)
    {
        // TODO: SpatiaLite BLOB에서 지오메트리 파싱
        // 지금은 null 반환
        return Task.FromResult<Geometry.IGeometry?>(null);
    }
    
    /// <summary>
    /// 테이블 이름 이스케이프
    /// </summary>
    private string EscapeTableName(string tableName)
    {
        return $"[{tableName}]";
    }
    
    /// <inheritdoc/>
    protected override string EscapeColumnName(string columnName)
    {
        return $"[{columnName}]";
    }
    
    #endregion
    
    #region 중첩 클래스
    
    /// <summary>
    /// SQLite 피처 구현
    /// </summary>
    private class SqliteFeature : IFeature
    {
        private readonly Dictionary<string, object?> _attributes = new();
        
        public object? Id { get; set; }
        public Geometry.IGeometry? Geometry { get; set; }
        public Styling.IStyle? Style { get; set; }
        IAttributeTable IFeature.Attributes => new AttributeTable(_attributes);
        public IDictionary<string, object?> Attributes => _attributes;
        public bool IsValid => Geometry?.IsValid ?? false;
        public Geometry.Envelope? BoundingBox => Geometry?.Envelope;
        
        public object? GetAttribute(string name)
        {
            return _attributes.TryGetValue(name, out var value) ? value : null;
        }
        
        public void SetAttribute(string name, object? value)
        {
            _attributes[name] = value;
        }
        
        public IEnumerable<string> GetAttributeNames()
        {
            return _attributes.Keys;
        }
    }
    
    #endregion
}