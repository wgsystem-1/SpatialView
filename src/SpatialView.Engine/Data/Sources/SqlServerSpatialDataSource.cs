using Microsoft.SqlServer.Types;
using SpatialView.Engine.Geometry;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// SQL Server Spatial 데이터베이스 프로바이더
/// SqlGeometry/SqlGeography 타입 지원
/// </summary>
public class SqlServerSpatialDataSource : DataSourceBase
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _geometryColumn;
    private readonly string? _idColumn;
    private readonly SpatialDataType _spatialType;
    private readonly int _srid;
    private SqlConnection? _connection;
    private new readonly object _lockObject = new();

    /// <summary>
    /// 쿼리 타임아웃 (초)
    /// </summary>
    public int QueryTimeout { get; set; } = 30;

    /// <summary>
    /// 공간 인덱스 힌트 사용 여부
    /// </summary>
    public bool UseSpatialIndex { get; set; } = true;

    /// <summary>
    /// 페이징 크기
    /// </summary>
    public int PageSize { get; set; } = 1000;

    /// <summary>
    /// TOP 절 사용 여부 (OFFSET/FETCH 대신)
    /// </summary>
    public bool UseTopClause { get; set; } = false;

    public SqlServerSpatialDataSource(
        string connectionString,
        string tableName,
        string geometryColumn = "Shape",
        string? idColumn = "ID",
        SpatialDataType spatialType = SpatialDataType.Geometry,
        int srid = 4326)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _geometryColumn = geometryColumn ?? throw new ArgumentNullException(nameof(geometryColumn));
        _idColumn = idColumn;
        _spatialType = spatialType;
        _srid = srid;

        SRID = srid;
    }

    public override string Name => $"SqlServerSpatial_{_tableName}";
    public override string ConnectionString => _connectionString;
    public override DataSourceType SourceType => DataSourceType.SqlServer;

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
            System.Diagnostics.Debug.WriteLine($"SQL Server Spatial query failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"SQL Server Spatial query failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"SQL Server Spatial get feature failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"SQL Server Spatial insert failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"SQL Server Spatial update failed: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"SQL Server Spatial delete failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 공간 인덱스 정보 가져오기
    /// </summary>
    public async Task<List<SpatialIndexInfo>> GetSpatialIndexesAsync()
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            var query = @"
                SELECT 
                    i.name AS IndexName,
                    i.type_desc AS IndexType,
                    c.name AS ColumnName,
                    si.tessellation_scheme,
                    si.bounding_box_xmin,
                    si.bounding_box_ymin,
                    si.bounding_box_xmax,
                    si.bounding_box_ymax,
                    si.level_1_grid,
                    si.level_2_grid,
                    si.level_3_grid,
                    si.level_4_grid
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                LEFT JOIN sys.spatial_indexes si ON i.object_id = si.object_id AND i.index_id = si.index_id
                WHERE OBJECT_NAME(i.object_id) = @tableName
                AND i.type_desc LIKE '%SPATIAL%'";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@tableName", _tableName);
            
            var indexes = new List<SpatialIndexInfo>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                indexes.Add(new SpatialIndexInfo
                {
                    IndexName = reader.GetString("IndexName"),
                    IndexType = reader.GetString("IndexType"),
                    ColumnName = reader.GetString("ColumnName"),
                    TessellationScheme = reader.IsDBNull("tessellation_scheme") ? null : reader.GetString("tessellation_scheme"),
                    BoundingBoxXMin = reader.IsDBNull("bounding_box_xmin") ? null : reader.GetDouble("bounding_box_xmin"),
                    BoundingBoxYMin = reader.IsDBNull("bounding_box_ymin") ? null : reader.GetDouble("bounding_box_ymin"),
                    BoundingBoxXMax = reader.IsDBNull("bounding_box_xmax") ? null : reader.GetDouble("bounding_box_xmax"),
                    BoundingBoxYMax = reader.IsDBNull("bounding_box_ymax") ? null : reader.GetDouble("bounding_box_ymax"),
                    Level1Grid = reader.IsDBNull("level_1_grid") ? null : reader.GetString("level_1_grid"),
                    Level2Grid = reader.IsDBNull("level_2_grid") ? null : reader.GetString("level_2_grid"),
                    Level3Grid = reader.IsDBNull("level_3_grid") ? null : reader.GetString("level_3_grid"),
                    Level4Grid = reader.IsDBNull("level_4_grid") ? null : reader.GetString("level_4_grid")
                });
            }

            return indexes;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get spatial indexes failed: {ex.Message}");
            return new List<SpatialIndexInfo>();
        }
    }

    /// <summary>
    /// 공간 인덱스 생성
    /// </summary>
    public async Task<bool> CreateSpatialIndexAsync(string indexName, SpatialIndexOptions? options = null)
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            var sql = new StringBuilder();
            sql.AppendLine($"CREATE SPATIAL INDEX {indexName}");
            sql.AppendLine($"ON {_tableName} ({_geometryColumn})");
            
            if (options != null)
            {
                sql.AppendLine("WITH (");
                var withOptions = new List<string>();
                
                if (options.BoundingBox != null)
                {
                    withOptions.Add($"BOUNDING_BOX = ({options.BoundingBox.MinX}, {options.BoundingBox.MinY}, {options.BoundingBox.MaxX}, {options.BoundingBox.MaxY})");
                }
                
                if (!string.IsNullOrEmpty(options.TessellationScheme))
                {
                    withOptions.Add($"TESSELLATION = {options.TessellationScheme}");
                }
                
                if (!string.IsNullOrEmpty(options.GridDensity))
                {
                    withOptions.Add($"GRIDS = ({options.GridDensity})");
                }

                sql.AppendLine(string.Join(",\n", withOptions));
                sql.AppendLine(")");
            }

            using var command = new SqlCommand(sql.ToString(), connection);
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
    public async Task<SqlServerTableStatistics?> GetTableStatisticsAsync()
    {
        try
        {
            using var connection = await CreateConnectionAsync();
            
            // 행 수 조회
            var countQuery = $"SELECT COUNT(*) FROM {_tableName}";
            using var countCommand = new SqlCommand(countQuery, connection);
            var rowCount = Convert.ToInt64(await countCommand.ExecuteScalarAsync());

            // 공간 범위 조회 (Geometry 타입)
            Envelope? extent = null;
            if (_spatialType == SpatialDataType.Geometry)
            {
                var extentQuery = $@"
                    SELECT 
                        MIN({_geometryColumn}.STEnvelope().STPointN(1).STX) as MinX,
                        MIN({_geometryColumn}.STEnvelope().STPointN(1).STY) as MinY,
                        MAX({_geometryColumn}.STEnvelope().STPointN(3).STX) as MaxX,
                        MAX({_geometryColumn}.STEnvelope().STPointN(3).STY) as MaxY
                    FROM {_tableName}
                    WHERE {_geometryColumn} IS NOT NULL";

                using var extentCommand = new SqlCommand(extentQuery, connection);
                using var extentReader = await extentCommand.ExecuteReaderAsync();
                
                if (await extentReader.ReadAsync() && 
                    !extentReader.IsDBNull("MinX") && !extentReader.IsDBNull("MinY") &&
                    !extentReader.IsDBNull("MaxX") && !extentReader.IsDBNull("MaxY"))
                {
                    extent = new Envelope(
                        extentReader.GetDouble("MinX"),
                        extentReader.GetDouble("MinY"),
                        extentReader.GetDouble("MaxX"),
                        extentReader.GetDouble("MaxY"));
                }
            }

            return new SqlServerTableStatistics
            {
                RowCount = rowCount,
                SpatialExtent = extent,
                SpatialDataType = _spatialType,
                SRID = _srid,
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

    private async Task<SqlConnection> CreateConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private SqlConnection CreateConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private SqlCommand CreateSelectCommand(SqlConnection connection, Envelope? extent, IQueryFilter? filter)
    {
        var sql = new StringBuilder();
        
        if (UseTopClause && PageSize > 0)
        {
            sql.Append($"SELECT TOP {PageSize} ");
        }
        else
        {
            sql.Append("SELECT ");
        }
        
        // ID 컬럼
        if (!string.IsNullOrEmpty(_idColumn))
            sql.Append($"{_idColumn}, ");
        
        // 지오메트리를 WKT로 변환
        sql.Append($"{_geometryColumn}.STAsText() as geometry_wkt, ");
        sql.Append($"{_geometryColumn}.STSrid() as geometry_srid, ");
        
        // 다른 컬럼들
        sql.Append("* ");
        sql.Append($"FROM {_tableName} ");
        
        var whereConditions = new List<string>();
        var parameters = new List<SqlParameter>();

        // 공간 필터
        if (extent != null && UseSpatialIndex)
        {
            if (_spatialType == SpatialDataType.Geometry)
            {
                whereConditions.Add($"{_geometryColumn}.STIntersects(geometry::STGeomFromText(@envelope, @srid)) = 1");
                var envelopeWkt = $"POLYGON(({extent.MinX} {extent.MinY}, {extent.MaxX} {extent.MinY}, {extent.MaxX} {extent.MaxY}, {extent.MinX} {extent.MaxY}, {extent.MinX} {extent.MinY}))";
                parameters.Add(new SqlParameter("@envelope", envelopeWkt));
            }
            else
            {
                whereConditions.Add($"{_geometryColumn}.STIntersects(geography::STGeomFromText(@envelope, @srid)) = 1");
                var envelopeWkt = $"POLYGON(({extent.MinX} {extent.MinY}, {extent.MaxX} {extent.MinY}, {extent.MaxX} {extent.MaxY}, {extent.MinX} {extent.MaxY}, {extent.MinX} {extent.MinY}))";
                parameters.Add(new SqlParameter("@envelope", envelopeWkt));
            }
            parameters.Add(new SqlParameter("@srid", _srid));
        }

        // NULL 지오메트리 제외
        whereConditions.Add($"{_geometryColumn} IS NOT NULL");

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

        // 정렬 (페이징을 위해)
        if (!string.IsNullOrEmpty(_idColumn))
        {
            sql.Append($" ORDER BY {_idColumn}");
        }

        // 페이징 (OFFSET/FETCH)
        if (!UseTopClause && PageSize > 0)
        {
            sql.Append($" OFFSET 0 ROWS FETCH NEXT {PageSize} ROWS ONLY");
        }

        var command = new SqlCommand(sql.ToString(), connection);
        command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    private SqlCommand CreateSelectByIdCommand(SqlConnection connection, object id)
    {
        var sql = $@"
            SELECT {_idColumn}, {_geometryColumn}.STAsText() as geometry_wkt, 
                   {_geometryColumn}.STSrid() as geometry_srid, *
            FROM {_tableName}
            WHERE {_idColumn} = @id";

        var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        return command;
    }

    private SqlCommand CreateInsertCommand(SqlConnection connection, IFeature feature)
    {
        var columns = new List<string>();
        var values = new List<string>();
        var parameters = new List<SqlParameter>();

        // 지오메트리
        columns.Add(_geometryColumn);
        if (_spatialType == SpatialDataType.Geometry)
        {
            values.Add("geometry::STGeomFromText(@geometry, @srid)");
        }
        else
        {
            values.Add("geography::STGeomFromText(@geometry, @srid)");
        }
        
        parameters.Add(new SqlParameter("@geometry", ConvertToWkt(feature.Geometry)));
        parameters.Add(new SqlParameter("@srid", _srid));

        // 속성들
        if (feature.Attributes != null)
        {
            foreach (var attr in feature.Attributes.GetNames())
            {
                if (attr != _geometryColumn && attr != _idColumn)
                {
                    columns.Add($"[{attr}]");
                    values.Add($"@{attr}");
                    parameters.Add(new SqlParameter($"@{attr}", feature.Attributes[attr] ?? DBNull.Value));
                }
            }
        }

        var sql = $@"
            INSERT INTO {_tableName} ({string.Join(", ", columns)})
            VALUES ({string.Join(", ", values)})";

        var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    private SqlCommand CreateUpdateCommand(SqlConnection connection, IFeature feature)
    {
        var setClauses = new List<string>();
        var parameters = new List<SqlParameter>();

        // 지오메트리
        if (_spatialType == SpatialDataType.Geometry)
        {
            setClauses.Add($"{_geometryColumn} = geometry::STGeomFromText(@geometry, @srid)");
        }
        else
        {
            setClauses.Add($"{_geometryColumn} = geography::STGeomFromText(@geometry, @srid)");
        }
        
        parameters.Add(new SqlParameter("@geometry", ConvertToWkt(feature.Geometry)));
        parameters.Add(new SqlParameter("@srid", _srid));

        // 속성들
        if (feature.Attributes != null)
        {
            foreach (var attr in feature.Attributes.GetNames())
            {
                if (attr != _geometryColumn && attr != _idColumn)
                {
                    setClauses.Add($"[{attr}] = @{attr}");
                    parameters.Add(new SqlParameter($"@{attr}", feature.Attributes[attr] ?? DBNull.Value));
                }
            }
        }

        // ID
        parameters.Add(new SqlParameter("@id", feature.Id));

        var sql = $@"
            UPDATE {_tableName}
            SET {string.Join(", ", setClauses)}
            WHERE {_idColumn} = @id";

        var command = new SqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        return command;
    }

    private SqlCommand CreateDeleteCommand(SqlConnection connection, object id)
    {
        var sql = $"DELETE FROM {_tableName} WHERE {_idColumn} = @id";
        var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        return command;
    }

    private async Task<List<IFeature>> ReadFeaturesFromReader(SqlDataReader reader)
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

    private async Task<IFeature?> ReadFeatureFromReader(SqlDataReader reader)
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
            if (!reader.IsDBNull("geometry_wkt"))
            {
                var wkt = reader.GetString("geometry_wkt");
                geometry = Geometry.IO.WktParser.Parse(wkt);
            }

            if (geometry == null) return null;

            // 속성 읽기
            var attributes = new AttributeTable();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                if (fieldName != "geometry_wkt" && fieldName != "geometry_srid" && !reader.IsDBNull(i))
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

    private static string ConvertToWkt(IGeometry geometry)
    {
        return Geometry.IO.WktParser.Write(geometry);
    }

    #endregion

    #region Abstract Method Implementations

    public override IEnumerable<string> GetTableNames()
    {
        var tables = new List<string>();
        try
        {
            using var connection = CreateConnection();
            using var command = new SqlCommand(@"
                SELECT DISTINCT TABLE_NAME 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE COLUMN_NAME IN (SELECT column_name FROM sys.columns WHERE system_type_id = 240)", connection);
                
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
                
            _connection = new SqlConnection(_connectionString);
            await _connection.OpenAsync();
            
            IsConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            LogError("Failed to open SQL Server connection", ex);
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
                GeometryColumn = _geometryColumn,
                SRID = _srid
            };
            
            // Get column information
            using var command = new SqlCommand($@"
                SELECT 
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                    ON c.TABLE_NAME = tc.TABLE_NAME
                LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE pk 
                    ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                WHERE c.TABLE_NAME = @tableName
                ORDER BY c.ORDINAL_POSITION", connection);
                
            command.Parameters.AddWithValue("@tableName", tableName);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString("COLUMN_NAME");
                var dataType = reader.GetString("DATA_TYPE");
                var isNullable = reader.GetString("IS_NULLABLE") == "YES";
                var isPrimaryKey = reader.GetInt32("IS_PRIMARY_KEY") == 1;
                
                if (isPrimaryKey)
                    schema.PrimaryKeyColumn = columnName;
                
                if (!columnName.Equals(_geometryColumn, StringComparison.OrdinalIgnoreCase))
                {
                    schema.Columns.Add(new ColumnInfo
                    {
                        Name = columnName,
                        DataType = MapSqlServerType(dataType),
                        DatabaseTypeName = dataType,
                        AllowNull = isNullable
                    });
                }
            }
            
            schema.FeatureCount = await GetFeatureCountAsync(tableName);
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
            var sql = $"SELECT COUNT(*) FROM [{tableName}] WHERE [{_geometryColumn}] IS NOT NULL";
            
            var whereClause = BuildWhereClause(filter);
            if (!string.IsNullOrEmpty(whereClause))
                sql += " AND " + whereClause.Replace("WHERE ", "");
                
            using var command = new SqlCommand(sql, connection);
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
            string sql;
            
            if (_spatialType == SpatialDataType.Geography)
            {
                sql = $@"
                    SELECT 
                        MIN([{_geometryColumn}].Long) as MinX,
                        MIN([{_geometryColumn}].Lat) as MinY,
                        MAX([{_geometryColumn}].Long) as MaxX,
                        MAX([{_geometryColumn}].Lat) as MaxY
                    FROM [{tableName}]
                    WHERE [{_geometryColumn}] IS NOT NULL";
            }
            else
            {
                sql = $@"
                    SELECT 
                        MIN([{_geometryColumn}].STPointN(1).STX) as MinX,
                        MIN([{_geometryColumn}].STPointN(1).STY) as MinY,
                        MAX([{_geometryColumn}].STPointN(1).STX) as MaxX,
                        MAX([{_geometryColumn}].STPointN(1).STY) as MaxY
                    FROM [{tableName}]
                    WHERE [{_geometryColumn}] IS NOT NULL";
            }
            
            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync() && !reader.IsDBNull("MinX"))
            {
                return new Geometry.Envelope(
                    reader.GetDouble("MinX"),
                    reader.GetDouble("MaxX"),
                    reader.GetDouble("MinY"),
                    reader.GetDouble("MaxY"));
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
            
        var features = await GetFeaturesAsync(null, filter);
        if (features != null)
        {
            foreach (var feature in features)
            {
                yield return feature;
            }
        }
    }

    public override async Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        return await GetFeatureAsync(id);
    }

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

    private static Type MapSqlServerType(string typeName)
    {
        return typeName.ToLowerInvariant() switch
        {
            "int" or "bigint" or "smallint" or "tinyint" => typeof(long),
            "decimal" or "numeric" or "money" or "smallmoney" => typeof(decimal),
            "float" or "real" => typeof(double),
            "bit" => typeof(bool),
            "datetime" or "datetime2" or "date" or "time" => typeof(DateTime),
            "uniqueidentifier" => typeof(Guid),
            "varbinary" or "binary" or "image" => typeof(byte[]),
            _ => typeof(string)
        };
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
}

#region Supporting Classes and Enums

/// <summary>
/// 공간 데이터 타입
/// </summary>
public enum SpatialDataType
{
    /// <summary>평면 지오메트리</summary>
    Geometry,
    /// <summary>지리 좌표계</summary>
    Geography
}

/// <summary>
/// 공간 인덱스 정보
/// </summary>
public class SpatialIndexInfo
{
    public string IndexName { get; set; } = string.Empty;
    public string IndexType { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string? TessellationScheme { get; set; }
    public double? BoundingBoxXMin { get; set; }
    public double? BoundingBoxYMin { get; set; }
    public double? BoundingBoxXMax { get; set; }
    public double? BoundingBoxYMax { get; set; }
    public string? Level1Grid { get; set; }
    public string? Level2Grid { get; set; }
    public string? Level3Grid { get; set; }
    public string? Level4Grid { get; set; }
}

/// <summary>
/// 공간 인덱스 생성 옵션
/// </summary>
public class SpatialIndexOptions
{
    public Envelope? BoundingBox { get; set; }
    public string? TessellationScheme { get; set; } // GEOMETRY_GRID, GEOMETRY_AUTO_GRID
    public string? GridDensity { get; set; } // LEVEL_1 = LOW, LEVEL_2 = MEDIUM, LEVEL_3 = HIGH, LEVEL_4 = HIGH
}

/// <summary>
/// SQL Server 테이블 통계
/// </summary>
public class SqlServerTableStatistics
{
    public long RowCount { get; set; }
    public Envelope? SpatialExtent { get; set; }
    public SpatialDataType SpatialDataType { get; set; }
    public int SRID { get; set; }
    public DateTime LastUpdated { get; set; }
}

#endregion