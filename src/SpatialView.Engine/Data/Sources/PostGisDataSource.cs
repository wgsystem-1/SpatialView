using Npgsql;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Geometry.IO;
using System.Data;
using System.Text;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// PostGIS 데이터베이스 프로바이더
/// Npgsql을 통한 PostGIS 공간 데이터 액세스
/// </summary>
public class PostGisDataSource : DataSourceBase
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _geometryColumn;
    private readonly string? _idColumn;
    private readonly int _srid;
    private NpgsqlConnection? _connection;
    private new readonly object _lockObject = new();

    /// <summary>
    /// 연결 풀 사용 여부
    /// </summary>
    public bool UseConnectionPooling { get; set; } = true;

    /// <summary>
    /// 쿼리 타임아웃 (초)
    /// </summary>
    public int QueryTimeout { get; set; } = 30;

    /// <summary>
    /// 공간 인덱스 힌트 사용 여부
    /// </summary>
    public bool UseSpatialIndex { get; set; } = true;

    /// <summary>
    /// 페이징 크기 (큰 결과셋 처리용)
    /// </summary>
    public int PageSize { get; set; } = 1000;

    public override string Name { get; }
    public override string ConnectionString => _connectionString;
    public override DataSourceType SourceType => DataSourceType.PostGIS;

    public PostGisDataSource(
        string connectionString,
        string tableName,
        string geometryColumn = "geom",
        string? idColumn = "id",
        int srid = 4326)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _geometryColumn = geometryColumn ?? throw new ArgumentNullException(nameof(geometryColumn));
        _idColumn = idColumn;
        _srid = srid;

        Name = $"PostGIS_{tableName}";
        SRID = srid;
    }

    public async Task<IEnumerable<IFeature>?> GetFeaturesAsync(Envelope? extent = null, IQueryFilter? filter = null)
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            using var command = CreateSelectCommand(connection, extent, filter);
            
            command.CommandTimeout = QueryTimeout;
            using var reader = await command.ExecuteReaderAsync();
            
            return await ReadFeaturesFromReader(reader);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostGIS query failed: {ex.Message}");
            return null;
        }
    }

    public IEnumerable<IFeature>? GetFeatures(Envelope? extent = null, IQueryFilter? filter = null)
    {
        try
        {
            using var connection = CreateConnection();
            using var command = CreateSelectCommand(connection, extent, filter);
            
            command.CommandTimeout = QueryTimeout;
            using var reader = command.ExecuteReader();
            
            return ReadFeaturesFromReader(reader).Result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostGIS query failed: {ex.Message}");
            return null;
        }
    }

    public async Task<IFeature?> GetFeatureAsync(object id)
    {
        if (_idColumn == null) return null;

        try
        {
            using var connection = await CreateConnectionAsync();
            using var command = CreateSelectByIdCommand(connection, id);
            
            command.CommandTimeout = QueryTimeout;
            using var reader = await command.ExecuteReaderAsync();
            
            var features = await ReadFeaturesFromReader(reader);
            return features?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostGIS get feature failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> AddFeatureAsync(IFeature feature)
    {
        if (feature?.Geometry == null) return false;

        try
        {
            using var connection = await CreateConnectionAsync();
            using var command = CreateInsertCommand(connection, feature);
            
            command.CommandTimeout = QueryTimeout;
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostGIS insert failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateFeatureAsync(IFeature feature)
    {
        if (feature?.Geometry == null || _idColumn == null) return false;

        try
        {
            using var connection = await CreateConnectionAsync();
            using var command = CreateUpdateCommand(connection, feature);
            
            command.CommandTimeout = QueryTimeout;
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostGIS update failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFeatureAsync(object id)
    {
        if (_idColumn == null) return false;

        try
        {
            using var connection = await CreateConnectionAsync();
            using var command = CreateDeleteCommand(connection, id);
            
            command.CommandTimeout = QueryTimeout;
            var result = await command.ExecuteNonQueryAsync();
            return result > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PostGIS delete failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 테이블 정보 가져오기
    /// </summary>
    public async Task<TableInfo?> GetTableInfoAsync()
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            // 컬럼 정보 조회
            var columnsQuery = @"
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_name = @tableName
                ORDER BY ordinal_position";

            using var command = new NpgsqlCommand(columnsQuery, connection);
            command.Parameters.AddWithValue("@tableName", _tableName);
            
            var columns = new List<ColumnInfo>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString("column_name"),
                    DataType = GetDataTypeFromString(reader.GetString("data_type")),
                    DatabaseTypeName = reader.GetString("data_type"),
                    IsNullable = reader.GetString("is_nullable") == "YES"
                });
            }

            reader.Close();

            // 지오메트리 컬럼 정보 조회
            var geomQuery = @"
                SELECT coord_dimension, srid, type
                FROM geometry_columns
                WHERE f_table_name = @tableName AND f_geometry_column = @geomColumn";

            command.CommandText = geomQuery;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@tableName", _tableName);
            command.Parameters.AddWithValue("@geomColumn", _geometryColumn);
            
            using var geomReader = await command.ExecuteReaderAsync();
            
            GeometryInfo? geometryInfo = null;
            if (await geomReader.ReadAsync())
            {
                geometryInfo = new GeometryInfo
                {
                    Column = _geometryColumn,
                    Dimension = geomReader.GetInt32("coord_dimension"),
                    SRID = geomReader.GetInt32("srid"),
                    GeometryType = geomReader.GetString("type")
                };
            }

            return new TableInfo
            {
                TableName = _tableName,
                Columns = columns,
                GeometryInfo = geometryInfo
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get table info failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 공간 인덱스 존재 여부 확인
    /// </summary>
    public async Task<bool> HasSpatialIndexAsync()
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            var query = @"
                SELECT COUNT(*)
                FROM pg_indexes
                WHERE tablename = @tableName
                AND indexname LIKE '%' || @geomColumn || '%'
                AND indexdef LIKE '%USING gist%'";

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@tableName", _tableName);
            command.Parameters.AddWithValue("@geomColumn", _geometryColumn);
            
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 공간 인덱스 생성
    /// </summary>
    public async Task<bool> CreateSpatialIndexAsync(string? indexName = null)
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            indexName ??= $"idx_{_tableName}_{_geometryColumn}";
            var query = $"CREATE INDEX {indexName} ON {_tableName} USING gist ({_geometryColumn})";

            using var command = new NpgsqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Create spatial index failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 테이블 통계 정보 가져오기
    /// </summary>
    public async Task<TableStatistics?> GetTableStatisticsAsync()
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            // 행 수 조회
            var countQuery = $"SELECT COUNT(*) FROM {_tableName}";
            using var countCommand = new NpgsqlCommand(countQuery, connection);
            var rowCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());

            // 공간 범위 조회
            var extentQuery = $"SELECT ST_Extent({_geometryColumn}) FROM {_tableName}";
            using var extentCommand = new NpgsqlCommand(extentQuery, connection);
            var extentResult = await extentCommand.ExecuteScalarAsync();
            
            Envelope? extent = null;
            if (extentResult != DBNull.Value && extentResult is string extentString)
            {
                extent = ParsePostGISExtent(extentString);
            }

            return new TableStatistics
            {
                RowCount = rowCount,
                SpatialExtent = extent,
                LastUpdated = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get table statistics failed: {ex.Message}");
            return null;
        }
    }

    #region Private Methods

    private Type GetDataTypeFromString(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "integer" or "int4" => typeof(int),
            "bigint" or "int8" => typeof(long),
            "real" or "float4" => typeof(float),
            "double precision" or "float8" => typeof(double),
            "text" or "varchar" or "character varying" => typeof(string),
            "boolean" or "bool" => typeof(bool),
            "timestamp" or "timestamptz" => typeof(DateTime),
            "bytea" => typeof(byte[]),
            _ => typeof(string)
        };
    }

    private async Task<NpgsqlConnection> CreateConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private NpgsqlConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private NpgsqlCommand CreateSelectCommand(NpgsqlConnection connection, Envelope? extent, IQueryFilter? filter)
    {
        var sql = new StringBuilder();
        sql.Append($"SELECT ");
        
        // ID 컬럼
        if (!string.IsNullOrEmpty(_idColumn))
            sql.Append($"{_idColumn}, ");
        
        // 지오메트리를 WKB로 변환
        sql.Append($"ST_AsBinary({_geometryColumn}) as geometry_wkb, ");
        
        // 다른 컬럼들
        sql.Append("* ");
        sql.Append($"FROM {_tableName} ");
        
        var whereConditions = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        // 공간 필터
        if (extent != null && UseSpatialIndex)
        {
            whereConditions.Add($"{_geometryColumn} && ST_MakeEnvelope(@minX, @minY, @maxX, @maxY, @srid)");
            parameters.Add(new NpgsqlParameter("@minX", extent.MinX));
            parameters.Add(new NpgsqlParameter("@minY", extent.MinY));
            parameters.Add(new NpgsqlParameter("@maxX", extent.MaxX));
            parameters.Add(new NpgsqlParameter("@maxY", extent.MaxY));
            parameters.Add(new NpgsqlParameter("@srid", _srid));
        }

        // 속성 필터
        if (filter?.AttributeFilter?.WhereClause != null)
        {
            whereConditions.Add(filter.AttributeFilter.WhereClause);
        }

        if (whereConditions.Count > 0)
        {
            sql.Append("WHERE ");
            sql.Append(string.Join(" AND ", whereConditions));
        }

        // 페이징
        if (PageSize > 0)
        {
            sql.Append($" LIMIT {PageSize}");
        }

        var command = new NpgsqlCommand(sql.ToString(), connection);
        command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    private NpgsqlCommand CreateSelectByIdCommand(NpgsqlConnection connection, object id)
    {
        var sql = $@"
            SELECT {_idColumn}, ST_AsBinary({_geometryColumn}) as geometry_wkb, *
            FROM {_tableName}
            WHERE {_idColumn} = @id";

        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        return command;
    }

    private NpgsqlCommand CreateInsertCommand(NpgsqlConnection connection, IFeature feature)
    {
        var columns = new List<string>();
        var values = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        // 지오메트리
        columns.Add(_geometryColumn);
        values.Add($"ST_GeomFromWKB(@geometry, @srid)");
        parameters.Add(new NpgsqlParameter("@geometry", WkbParser.Write(feature.Geometry)));
        parameters.Add(new NpgsqlParameter("@srid", _srid));

        // 속성들
        if (feature.Attributes != null)
        {
            foreach (var attr in feature.Attributes.GetNames())
            {
                if (attr != _geometryColumn && attr != _idColumn)
                {
                    columns.Add(attr);
                    values.Add($"@{attr}");
                    parameters.Add(new NpgsqlParameter($"@{attr}", feature.Attributes[attr] ?? DBNull.Value));
                }
            }
        }

        var sql = $@"
            INSERT INTO {_tableName} ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})";

        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    private NpgsqlCommand CreateUpdateCommand(NpgsqlConnection connection, IFeature feature)
    {
        var setClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        // 지오메트리
        setClauses.Add($"{_geometryColumn} = ST_GeomFromWKB(@geometry, @srid)");
        parameters.Add(new NpgsqlParameter("@geometry", WkbParser.Write(feature.Geometry)));
        parameters.Add(new NpgsqlParameter("@srid", _srid));

        // 속성들
        if (feature.Attributes != null)
        {
            foreach (var attr in feature.Attributes.GetNames())
            {
                if (attr != _geometryColumn && attr != _idColumn)
                {
                    setClauses.Add($"{attr} = @{attr}");
                    parameters.Add(new NpgsqlParameter($"@{attr}", feature.Attributes[attr] ?? DBNull.Value));
                }
            }
        }

        // ID
        parameters.Add(new NpgsqlParameter("@id", feature.Id));

        var sql = $@"
            UPDATE {_tableName}
            SET {string.Join(", ", setClauses)}
            WHERE {_idColumn} = @id";

        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    private NpgsqlCommand CreateDeleteCommand(NpgsqlConnection connection, object id)
    {
        var sql = $"DELETE FROM {_tableName} WHERE {_idColumn} = @id";
        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        return command;
    }

    private async Task<List<IFeature>> ReadFeaturesFromReader(NpgsqlDataReader reader)
    {
        var features = new List<IFeature>();
        
        while (await reader.ReadAsync())
        {
            var feature = await ReadFeatureFromReader(reader);
            if (feature != null)
                features.Add(feature);
        }

        return features;
    }

    private async Task<IFeature?> ReadFeatureFromReader(NpgsqlDataReader reader)
    {
        try
        {
            // ID 읽기
            object? id = null;
            if (!string.IsNullOrEmpty(_idColumn) && !reader.IsDBNull(_idColumn))
            {
                id = reader[_idColumn];
            }

            // 지오메트리 읽기
            IGeometry? geometry = null;
            if (!reader.IsDBNull("geometry_wkb"))
            {
                var wkb = (byte[])reader["geometry_wkb"];
                geometry = WkbParser.Parse(wkb);
            }

            if (geometry == null) return null;

            // 속성 읽기
            var attributes = new AttributeTable();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                if (fieldName != "geometry_wkb" && !reader.IsDBNull(i))
                {
                    attributes[fieldName] = reader.GetValue(i);
                }
            }

            return new Feature(id?.ToString() ?? Guid.NewGuid().ToString(), geometry, attributes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading feature: {ex.Message}");
            return null;
        }
    }

    private static Envelope? ParsePostGISExtent(string extentString)
    {
        try
        {
            // PostGIS extent format: "BOX(minX minY,maxX maxY)"
            if (extentString.StartsWith("BOX(") && extentString.EndsWith(")"))
            {
                var coords = extentString.Substring(4, extentString.Length - 5).Split(',');
                if (coords.Length == 2)
                {
                    var min = coords[0].Split(' ');
                    var max = coords[1].Split(' ');
                    
                    if (min.Length == 2 && max.Length == 2 &&
                        double.TryParse(min[0], out var minX) &&
                        double.TryParse(min[1], out var minY) &&
                        double.TryParse(max[0], out var maxX) &&
                        double.TryParse(max[1], out var maxY))
                    {
                        return new Envelope(minX, minY, maxX, maxY);
                    }
                }
            }
        }
        catch
        {
            // 파싱 실패 시 null 반환
        }

        return null;
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lockObject)
            {
                _connection?.Dispose();
                _connection = null;
            }
        }
        base.Dispose(disposing);
    }

    #region Abstract Method Implementations

    public override IEnumerable<string> GetTableNames()
    {
        var tables = new List<string>();
        
        try
        {
            using var connection = CreateConnection();
            using var command = new NpgsqlCommand(@"
                SELECT DISTINCT f_table_name 
                FROM geometry_columns 
                WHERE f_table_schema = 'public'
                ORDER BY f_table_name", connection);
                
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to get table names", ex);
        }
        
        return tables;
    }

    public override async Task<bool> OpenAsync()
    {
        try
        {
            if (IsConnected)
                return true;
                
            _connection = new NpgsqlConnection(_connectionString);
            await _connection.OpenAsync();
            
            // Validate the table and geometry column exist
            using (var command = new NpgsqlCommand(@"
                SELECT COUNT(*) 
                FROM geometry_columns 
                WHERE f_table_name = @tableName 
                AND f_geometry_column = @geomColumn", _connection))
            {
                command.Parameters.AddWithValue("@tableName", _tableName);
                command.Parameters.AddWithValue("@geomColumn", _geometryColumn);
                
                var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                if (count == 0)
                {
                    throw new InvalidOperationException($"Table '{_tableName}' with geometry column '{_geometryColumn}' not found");
                }
            }
            
            IsConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            LogError("Failed to open PostGIS connection", ex);
            IsConnected = false;
            return false;
        }
    }

    public override void Close()
    {
        lock (_lockObject)
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
            IsConnected = false;
        }
    }

    public override async Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        if (!ValidateTableName(tableName))
            return null;
            
        try
        {
            using var connection = await CreateConnectionAsync();
            var schema = new TableSchema
            {
                TableName = tableName,
                SRID = _srid
            };
            
            // Get geometry column info
            using (var command = new NpgsqlCommand(@"
                SELECT f_geometry_column, coord_dimension, srid, type
                FROM geometry_columns
                WHERE f_table_name = @tableName
                LIMIT 1", connection))
            {
                command.Parameters.AddWithValue("@tableName", tableName);
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    schema.GeometryColumn = reader.GetString(0);
                    schema.GeometryType = reader.GetString(3);
                    schema.SRID = reader.GetInt32(2);
                }
            }
            
            // Get column information
            using (var command = new NpgsqlCommand(@"
                SELECT 
                    a.attname AS column_name,
                    pg_catalog.format_type(a.atttypid, a.atttypmod) AS data_type,
                    a.attnotnull AS not_null,
                    i.indisprimary AS is_primary
                FROM pg_attribute a
                LEFT JOIN pg_index i ON a.attrelid = i.indrelid AND a.attnum = ANY(i.indkey)
                WHERE a.attrelid = @tableName::regclass
                AND a.attnum > 0
                AND NOT a.attisdropped
                ORDER BY a.attnum", connection))
            {
                command.Parameters.AddWithValue("@tableName", tableName);
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(0);
                    var dataTypeName = reader.GetString(1);
                    var notNull = reader.GetBoolean(2);
                    var isPrimary = !reader.IsDBNull(3) && reader.GetBoolean(3);
                    
                    if (isPrimary)
                        schema.PrimaryKeyColumn = columnName;
                        
                    // Skip geometry column in column list
                    if (columnName.Equals(schema.GeometryColumn, StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    var dataType = MapPostgreSQLType(dataTypeName);
                    
                    schema.Columns.Add(new ColumnInfo
                    {
                        Name = columnName,
                        DataType = dataType,
                        DatabaseTypeName = dataTypeName,
                        AllowNull = !notNull
                    });
                }
            }
            
            // Get feature count
            schema.FeatureCount = await GetFeatureCountAsync(tableName);
            
            // Get extent
            schema.Extent = await GetExtentAsync(tableName);
            
            return schema;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get schema for table {tableName}", ex);
            return null;
        }
    }

    public override async Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        if (!ValidateTableName(tableName))
            return 0;
            
        try
        {
            using var connection = await CreateConnectionAsync();
            var sql = new StringBuilder($"SELECT COUNT(*) FROM {tableName}");
            
            var whereClause = BuildWhereClause(filter);
            if (!string.IsNullOrEmpty(whereClause))
                sql.Append(" ").Append(whereClause);
                
            using var command = new NpgsqlCommand(sql.ToString(), connection);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
        catch (Exception ex)
        {
            LogError($"Failed to get feature count for table {tableName}", ex);
            return 0;
        }
    }

    public override async Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        if (!ValidateTableName(tableName))
            return null;
            
        try
        {
            using var connection = await CreateConnectionAsync();
            
            // First try to get the geometry column
            string? geomColumn = null;
            using (var command = new NpgsqlCommand(@"
                SELECT f_geometry_column 
                FROM geometry_columns 
                WHERE f_table_name = @tableName 
                LIMIT 1", connection))
            {
                command.Parameters.AddWithValue("@tableName", tableName);
                geomColumn = await command.ExecuteScalarAsync() as string;
            }
            
            if (string.IsNullOrEmpty(geomColumn))
                return null;
                
            // Get extent
            var extentQuery = $"SELECT ST_Extent({geomColumn}) FROM {tableName}";
            using var extentCommand = new NpgsqlCommand(extentQuery, connection);
            var extentResult = await extentCommand.ExecuteScalarAsync();
            
            if (extentResult != DBNull.Value && extentResult is string extentString)
            {
                return ParsePostGISExtent(extentString);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get extent for table {tableName}", ex);
            return null;
        }
    }

    public override async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        if (!ValidateTableName(tableName))
            yield break;
            
        NpgsqlConnection? connection = null;
        NpgsqlCommand? command = null;
        NpgsqlDataReader? reader = null;
        
        try
        {
            connection = await CreateConnectionAsync();
            
            // Get geometry column for the table
            string? geomColumn = null;
            using (var geomCommand = new NpgsqlCommand(@"
                SELECT f_geometry_column 
                FROM geometry_columns 
                WHERE f_table_name = @tableName 
                LIMIT 1", connection))
            {
                geomCommand.Parameters.AddWithValue("@tableName", tableName);
                geomColumn = await geomCommand.ExecuteScalarAsync() as string;
            }
            
            if (string.IsNullOrEmpty(geomColumn))
                yield break;
                
            // Build query
            var sql = new StringBuilder();
            sql.Append($"SELECT ");
            
            if (!string.IsNullOrEmpty(_idColumn))
                sql.Append($"{_idColumn}, ");
                
            sql.Append($"ST_AsBinary({geomColumn}) as geometry_wkb, * ");
            sql.Append($"FROM {tableName} ");
            
            var whereClause = BuildWhereClause(filter);
            if (!string.IsNullOrEmpty(whereClause))
                sql.Append(whereClause).Append(" ");
                
            sql.Append(BuildOrderByClause(filter)).Append(" ");
            sql.Append(BuildLimitClause(filter));
            
            command = new NpgsqlCommand(sql.ToString(), connection);
            command.CommandTimeout = QueryTimeout;
            
            reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var feature = await ReadFeatureFromReader(reader);
                if (feature != null)
                    yield return feature;
            }
        }
        finally
        {
            reader?.Dispose();
            command?.Dispose();
            connection?.Dispose();
        }
    }

    public override async Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        if (!ValidateTableName(tableName) || _idColumn == null)
            return null;
            
        try
        {
            using var connection = await CreateConnectionAsync();
            
            // Get geometry column for the table
            string? geomColumn = null;
            using (var geomCommand = new NpgsqlCommand(@"
                SELECT f_geometry_column 
                FROM geometry_columns 
                WHERE f_table_name = @tableName 
                LIMIT 1", connection))
            {
                geomCommand.Parameters.AddWithValue("@tableName", tableName);
                geomColumn = await geomCommand.ExecuteScalarAsync() as string;
            }
            
            if (string.IsNullOrEmpty(geomColumn))
                return null;
                
            var sql = $@"
                SELECT {_idColumn}, ST_AsBinary({geomColumn}) as geometry_wkb, *
                FROM {tableName}
                WHERE {_idColumn} = @id";
                
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);
            command.CommandTimeout = QueryTimeout;
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return await ReadFeatureFromReader(reader);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            LogError($"Failed to get feature {id} from table {tableName}", ex);
            return null;
        }
    }

    protected override string BuildSpatialCondition(ISpatialFilter spatialFilter)
    {
        if (spatialFilter.FilterGeometry == null)
            return string.Empty;
            
        var wkt = Geometry.IO.WktParser.Write(spatialFilter.FilterGeometry);
        var spatialOp = spatialFilter.Relationship switch
        {
            SpatialRelationship.Intersects => "ST_Intersects",
            SpatialRelationship.Contains => "ST_Contains",
            SpatialRelationship.Within => "ST_Within",
            SpatialRelationship.Overlaps => "ST_Overlaps",
            SpatialRelationship.Touches => "ST_Touches",
            SpatialRelationship.Disjoint => "ST_Disjoint",
            _ => "ST_Intersects"
        };
        
        return $"{spatialOp}({_geometryColumn}, ST_GeomFromText('{wkt}', {_srid}))";
    }

    protected override string EscapeColumnName(string columnName)
    {
        // PostgreSQL uses double quotes for identifiers
        return $"\"{columnName}\"";
    }

    private static Type MapPostgreSQLType(string typeName)
    {
        var lowerType = typeName.ToLowerInvariant();
        return lowerType switch
        {
            var t when t.Contains("int") => typeof(long),
            var t when t.Contains("numeric") || t.Contains("decimal") => typeof(decimal),
            var t when t.Contains("real") || t.Contains("float") || t.Contains("double") => typeof(double),
            var t when t.Contains("text") || t.Contains("char") || t.Contains("varchar") => typeof(string),
            var t when t.Contains("bool") => typeof(bool),
            var t when t.Contains("date") || t.Contains("time") => typeof(DateTime),
            var t when t.Contains("uuid") => typeof(Guid),
            var t when t.Contains("bytea") => typeof(byte[]),
            var t when t.Contains("json") => typeof(string),
            _ => typeof(string)
        };
    }

    // Implement new table-based interface through wrappers
    public override async Task<bool> InsertFeatureAsync(string tableName, IFeature feature)
    {
        return await AddFeatureAsync(feature);
    }

    public override async Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        return await UpdateFeatureAsync(feature);
    }

    public override async Task<bool> DeleteFeatureAsync(string tableName, object id)
    {
        return await DeleteFeatureAsync(id);
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// 테이블 정보
/// </summary>
public class TableInfo
{
    public string TableName { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public GeometryInfo? GeometryInfo { get; set; }
}

/// <summary>
/// 지오메트리 정보
/// </summary>
public class GeometryInfo
{
    public string Column { get; set; } = string.Empty;
    public int Dimension { get; set; }
    public int SRID { get; set; }
    public string GeometryType { get; set; } = string.Empty;
}

/// <summary>
/// 테이블 통계
/// </summary>
public class TableStatistics
{
    public long RowCount { get; set; }
    public Envelope? SpatialExtent { get; set; }
    public DateTime LastUpdated { get; set; }
}

#endregion