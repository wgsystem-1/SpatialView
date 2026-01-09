using Microsoft.Data.Sqlite;
using SpatialView.Engine.Geometry.IO;
using System.Data;
using System.Text;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// GeoPackage 데이터 소스
/// SQLite 기반의 OGC GeoPackage 포맷 지원
/// </summary>
public class GeoPackageDataSource : DataSourceBase
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _geometryColumn;
    private SqliteConnection? _connection;
    private GeoPackageInfo? _gpkgInfo;
    private TableSchema? _schema;
    private Geometry.Envelope? _envelope;

    public override string Name { get; }
    public override string ConnectionString => _connectionString;
    public override DataSourceType SourceType => DataSourceType.GeoPackage;

    public GeoPackageDataSource(string filePath, string tableName, string? geometryColumn = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"GeoPackage file not found: {filePath}");

        _connectionString = $"Data Source={filePath};Mode=ReadOnly;Cache=Shared";
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        Name = $"{Path.GetFileNameWithoutExtension(filePath)}.{tableName}";
        Description = $"GeoPackage: {Name}";

        Initialize();

        _geometryColumn = geometryColumn ?? DetectGeometryColumn();
        if (string.IsNullOrEmpty(_geometryColumn))
            throw new InvalidOperationException($"No geometry column found in table '{tableName}'");

        LoadMetadata();
    }

    private void Initialize()
    {
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        ValidateGeoPackage();
        _gpkgInfo = ReadGeoPackageInfo();
    }

    private void ValidateGeoPackage()
    {
        using var command = _connection!.CreateCommand();
        command.CommandText = "SELECT application_id FROM pragma_application_id()";
        
        var result = command.ExecuteScalar();
        if (result == null || !result.Equals(0x47504B47)) // 'GPKG' in ASCII
        {
            throw new InvalidDataException("File is not a valid GeoPackage");
        }
    }

    private GeoPackageInfo ReadGeoPackageInfo()
    {
        var info = new GeoPackageInfo();

        // Read GeoPackage contents
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT table_name, data_type, identifier, description, min_x, min_y, max_x, max_y, srs_id
            FROM gpkg_contents 
            WHERE table_name = @tableName";
        command.Parameters.AddWithValue("@tableName", _tableName);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            info.TableName = reader.GetString("table_name");
            info.DataType = reader.GetString("data_type");
            info.Identifier = reader.IsDBNull("identifier") ? null : reader.GetString("identifier");
            info.Description = reader.IsDBNull("description") ? null : reader.GetString("description");
            info.MinX = reader.IsDBNull("min_x") ? null : reader.GetDouble("min_x");
            info.MinY = reader.IsDBNull("min_y") ? null : reader.GetDouble("min_y");
            info.MaxX = reader.IsDBNull("max_x") ? null : reader.GetDouble("max_x");
            info.MaxY = reader.IsDBNull("max_y") ? null : reader.GetDouble("max_y");
            info.SrsId = reader.IsDBNull("srs_id") ? 4326 : reader.GetInt32("srs_id");
        }
        else
        {
            throw new InvalidOperationException($"Table '{_tableName}' not found in GeoPackage contents");
        }

        return info;
    }

    private string DetectGeometryColumn()
    {
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT column_name 
            FROM gpkg_geometry_columns 
            WHERE table_name = @tableName 
            LIMIT 1";
        command.Parameters.AddWithValue("@tableName", _tableName);

        var result = command.ExecuteScalar();
        return result?.ToString() ?? "geom";
    }

    private void LoadMetadata()
    {
        SRID = _gpkgInfo?.SrsId ?? 4326;
        
        // Set envelope from metadata
        if (_gpkgInfo?.HasBounds == true)
        {
            _envelope = new Geometry.Envelope(
                _gpkgInfo.MinX!.Value, _gpkgInfo.MaxX!.Value,
                _gpkgInfo.MinY!.Value, _gpkgInfo.MaxY!.Value);
        }
        else
        {
            _envelope = CalculateExtent();
        }

        _schema = BuildSchema();
    }

    public Geometry.Envelope? GetExtent()
    {
        return _envelope;
    }

    public async Task<IEnumerable<IFeature>> GetFeaturesAsync(Geometry.Envelope? extent = null)
    {
        return await Task.Run(() => GetFeatures(extent));
    }

    public IEnumerable<IFeature> GetFeatures(Geometry.Envelope? extent = null)
    {
        var sql = new StringBuilder($"SELECT * FROM \"{_tableName}\"");
        var parameters = new List<SqliteParameter>();

        // Add spatial filter if extent is provided
        if (extent != null && !extent.IsNull)
        {
            sql.Append($" WHERE \"{_geometryColumn}\" IS NOT NULL");
            
            // Use RTREE spatial index if available
            if (HasSpatialIndex())
            {
                sql.Append($@" AND rowid IN (
                    SELECT id FROM ""rtree_{_tableName}_{_geometryColumn}"" 
                    WHERE minx <= @maxX AND maxx >= @minX AND miny <= @maxY AND maxy >= @minY
                )");
                
                parameters.Add(new SqliteParameter("@minX", extent.MinX));
                parameters.Add(new SqliteParameter("@maxX", extent.MaxX));
                parameters.Add(new SqliteParameter("@minY", extent.MinY));
                parameters.Add(new SqliteParameter("@maxY", extent.MaxY));
            }
            else
            {
                // Fallback: use envelope intersection
                sql.Append($@" AND (
                    ST_MinX(""{_geometryColumn}"") <= @maxX AND ST_MaxX(""{_geometryColumn}"") >= @minX AND
                    ST_MinY(""{_geometryColumn}"") <= @maxY AND ST_MaxY(""{_geometryColumn}"") >= @minY
                )");
                
                parameters.Add(new SqliteParameter("@minX", extent.MinX));
                parameters.Add(new SqliteParameter("@maxX", extent.MaxX));
                parameters.Add(new SqliteParameter("@minY", extent.MinY));
                parameters.Add(new SqliteParameter("@maxY", extent.MaxY));
            }
        }

        using var command = _connection!.CreateCommand();
        command.CommandText = sql.ToString();
        command.Parameters.AddRange(parameters.ToArray());

        using var reader = command.ExecuteReader();
        ulong featureId = 0;

        while (reader.Read())
        {
            var geometry = ReadGeometry(reader, _geometryColumn);
            if (geometry == null)
                continue;

            // Additional extent filtering for precise intersection
            if (extent != null && !extent.IsNull && !extent.Intersects(geometry.GetBounds()))
                continue;

            var attributes = ReadAttributes(reader);
            
            yield return new Feature(featureId++, geometry, new AttributeTable(attributes));
        }
    }

    public long GetFeatureCount()
    {
        using var command = _connection!.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{_tableName}\" WHERE \"{_geometryColumn}\" IS NOT NULL";
        
        var result = command.ExecuteScalar();
        return result is long count ? count : 0;
    }

    public TableSchema GetSchema()
    {
        return _schema ?? new TableSchema();
    }

    private Geometry.IGeometry? ReadGeometry(IDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var blob = (byte[])reader.GetValue(ordinal);
            return ParseGeoPackageGeometry(blob);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading geometry: {ex.Message}");
            return null;
        }
    }

    private Geometry.IGeometry? ParseGeoPackageGeometry(byte[] blob)
    {
        if (blob == null || blob.Length < 8)
            return null;

        try
        {
            using var stream = new MemoryStream(blob);
            using var reader = new BinaryReader(stream);

            // Read GeoPackage Binary header
            var magic1 = reader.ReadByte(); // 'G'
            var magic2 = reader.ReadByte(); // 'P'
            
            if (magic1 != 0x47 || magic2 != 0x50) // 'GP'
            {
                // Not GeoPackage format, try standard WKB
                return WkbParser.Parse(blob);
            }

            var version = reader.ReadByte();
            var flags = reader.ReadByte();

            // Read SRID (4 bytes, big-endian)
            var sridBytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(sridBytes);
            var srid = BitConverter.ToInt32(sridBytes, 0);

            // Skip envelope if present (optional)
            var envelopeType = (flags & 0x0E) >> 1;
            if (envelopeType > 0)
            {
                var envelopeSize = envelopeType switch
                {
                    1 => 32, // XY
                    2 => 48, // XYZ
                    3 => 48, // XYM
                    4 => 64, // XYZM
                    _ => 0
                };
                reader.ReadBytes(envelopeSize);
            }

            // Read WKB geometry
            var wkbData = reader.ReadBytes((int)(stream.Length - stream.Position));
            return WkbParser.Parse(wkbData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse GeoPackage geometry: {ex.Message}");
            
            // Fallback: try standard WKB
            try
            {
                return WkbParser.Parse(blob);
            }
            catch
            {
                return null;
            }
        }
    }

    private Dictionary<string, object?> ReadAttributes(IDataReader reader)
    {
        var attributes = new Dictionary<string, object?>();
        
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            
            // Skip geometry column
            if (columnName.Equals(_geometryColumn, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            attributes[columnName] = value;
        }

        return attributes;
    }

    private bool HasSpatialIndex()
    {
        using var command = _connection!.CreateCommand();
        command.CommandText = $@"
            SELECT COUNT(*) 
            FROM sqlite_master 
            WHERE type = 'table' 
            AND name = 'rtree_{_tableName}_{_geometryColumn}'";
        
        var result = command.ExecuteScalar();
        return result is long count && count > 0;
    }

    private Geometry.Envelope? CalculateExtent()
    {
        using var command = _connection!.CreateCommand();
        command.CommandText = $@"
            SELECT 
                MIN(ST_MinX(""{_geometryColumn}"")) as min_x,
                MIN(ST_MinY(""{_geometryColumn}"")) as min_y,
                MAX(ST_MaxX(""{_geometryColumn}"")) as max_x,
                MAX(ST_MaxY(""{_geometryColumn}"")) as max_y
            FROM ""{_tableName}"" 
            WHERE ""{_geometryColumn}"" IS NOT NULL";

        using var reader = command.ExecuteReader();
        if (reader.Read() && !reader.IsDBNull("min_x"))
        {
            return new Geometry.Envelope(
                reader.GetDouble("min_x"),
                reader.GetDouble("max_x"),
                reader.GetDouble("min_y"),
                reader.GetDouble("max_y"));
        }

        return null;
    }

    private TableSchema BuildSchema()
    {
        var schema = new TableSchema();
        
        using var command = _connection!.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{_tableName}\")";
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var columnName = reader.GetString("name");
            var typeName = reader.GetString("type").ToUpperInvariant();
            var notNull = reader.GetInt32("notnull") == 1;
            
            // Skip geometry column
            if (columnName.Equals(_geometryColumn, StringComparison.OrdinalIgnoreCase))
                continue;

            var dataType = typeName switch
            {
                string t when t.Contains("INT") => typeof(long),
                string t when t.Contains("REAL") || t.Contains("FLOAT") || t.Contains("DOUBLE") => typeof(double),
                string t when t.Contains("TEXT") || t.Contains("CHAR") => typeof(string),
                string t when t.Contains("BLOB") => typeof(byte[]),
                string t when t.Contains("DATE") || t.Contains("TIME") => typeof(DateTime),
                string t when t.Contains("BOOL") => typeof(bool),
                _ => typeof(string)
            };

            var column = new ColumnInfo
            {
                Name = columnName,
                DataType = dataType,
                DatabaseTypeName = typeName,
                AllowNull = !notNull
            };

            schema.Columns.Add(column);
        }

        return schema;
    }

    public new void Dispose()
    {
        _connection?.Dispose();
    }

    #region Abstract Method Implementations

    public override IEnumerable<string> GetTableNames()
    {
        var tables = new List<string>();
        
        using var command = _connection!.CreateCommand();
        command.CommandText = @"
            SELECT table_name 
            FROM gpkg_contents 
            WHERE data_type IN ('features', '2d-gridded-coverage')";
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString("table_name"));
        }
        
        return tables;
    }

    public override async Task<bool> OpenAsync()
    {
        try
        {
            if (IsConnected)
                return true;
                
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync();
            
            ValidateGeoPackage();
            _gpkgInfo = ReadGeoPackageInfo();
            LoadMetadata();
            
            IsConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            LogError("Failed to open GeoPackage connection", ex);
            IsConnected = false;
            return false;
        }
    }

    public override void Close()
    {
        if (_connection != null)
        {
            _connection.Close();
            _connection.Dispose();
            _connection = null;
        }
        IsConnected = false;
    }

    public override async Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        if (!ValidateTableName(tableName))
            return null;
            
        return await Task.Run(() =>
        {
            var schema = new TableSchema
            {
                TableName = tableName,
                GeometryColumn = _geometryColumn,
                SRID = SRID
            };
            
            // Get geometry type
            using (var command = _connection!.CreateCommand())
            {
                command.CommandText = @"
                    SELECT geometry_type_name 
                    FROM gpkg_geometry_columns 
                    WHERE table_name = @tableName AND column_name = @geomColumn";
                command.Parameters.AddWithValue("@tableName", tableName);
                command.Parameters.AddWithValue("@geomColumn", _geometryColumn);
                
                var result = command.ExecuteScalar();
                if (result != null)
                    schema.GeometryType = result.ToString();
            }
            
            // Get columns
            using (var command = _connection!.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var columnName = reader.GetString("name");
                    var typeName = reader.GetString("type").ToUpperInvariant();
                    var notNull = reader.GetInt32("notnull") == 1;
                    var pk = reader.GetInt32("pk") > 0;
                    
                    if (pk)
                        schema.PrimaryKeyColumn = columnName;
                    
                    // Skip geometry column in column list
                    if (columnName.Equals(_geometryColumn, StringComparison.OrdinalIgnoreCase))
                        continue;
                        
                    var dataType = typeName switch
                    {
                        string t when t.Contains("INT") => typeof(long),
                        string t when t.Contains("REAL") || t.Contains("FLOAT") || t.Contains("DOUBLE") => typeof(double),
                        string t when t.Contains("TEXT") || t.Contains("CHAR") => typeof(string),
                        string t when t.Contains("BLOB") => typeof(byte[]),
                        string t when t.Contains("DATE") || t.Contains("TIME") => typeof(DateTime),
                        string t when t.Contains("BOOL") => typeof(bool),
                        _ => typeof(string)
                    };
                    
                    schema.Columns.Add(new ColumnInfo
                    {
                        Name = columnName,
                        DataType = dataType,
                        DatabaseTypeName = typeName,
                        AllowNull = !notNull
                    });
                }
            }
            
            // Get feature count and extent
            schema.FeatureCount = GetFeatureCount();
            schema.Extent = GetExtent();
            
            return schema;
        });
    }

    public override async Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        if (!ValidateTableName(tableName))
            return 0;
            
        return await Task.Run(() =>
        {
            using var command = _connection!.CreateCommand();
            var sql = new StringBuilder($"SELECT COUNT(*) FROM \"{tableName}\" WHERE \"{_geometryColumn}\" IS NOT NULL");
            
            if (filter != null)
            {
                var whereClause = BuildWhereClause(filter);
                if (!string.IsNullOrEmpty(whereClause))
                {
                    sql.Append(" AND ");
                    sql.Append(whereClause.Replace("WHERE ", ""));
                }
            }
            
            command.CommandText = sql.ToString();
            var result = command.ExecuteScalar();
            return result is long count ? count : 0;
        });
    }

    public override async Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        if (!ValidateTableName(tableName))
            return null;
            
        return await Task.Run(() =>
        {
            // First check if extent is stored in gpkg_contents
            using (var command = _connection!.CreateCommand())
            {
                command.CommandText = @"
                    SELECT min_x, min_y, max_x, max_y 
                    FROM gpkg_contents 
                    WHERE table_name = @tableName";
                command.Parameters.AddWithValue("@tableName", tableName);
                
                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull("min_x"))
                {
                    return new Geometry.Envelope(
                        reader.GetDouble("min_x"),
                        reader.GetDouble("max_x"),
                        reader.GetDouble("min_y"),
                        reader.GetDouble("max_y"));
                }
            }
            
            // Calculate extent from geometry
            return CalculateExtent();
        });
    }

    public override async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        if (!ValidateTableName(tableName))
            yield break;
            
        await foreach (var feature in GetFeaturesAsyncEnumerable(tableName, filter))
        {
            yield return feature;
        }
    }

    public override async Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        if (!ValidateTableName(tableName))
            return null;
            
        return await Task.Run(() =>
        {
            using var command = _connection!.CreateCommand();
            
            // Try to find the primary key column
            string? pkColumn = null;
            using (var pkCommand = _connection.CreateCommand())
            {
                pkCommand.CommandText = $"PRAGMA table_info(\"{tableName}\")";
                using var pkReader = pkCommand.ExecuteReader();
                while (pkReader.Read())
                {
                    if (pkReader.GetInt32("pk") > 0)
                    {
                        pkColumn = pkReader.GetString("name");
                        break;
                    }
                }
            }
            
            if (string.IsNullOrEmpty(pkColumn))
                return null;
                
            command.CommandText = $"SELECT * FROM \"{tableName}\" WHERE \"{pkColumn}\" = @id";
            command.Parameters.AddWithValue("@id", id);
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var geometry = ReadGeometry(reader, _geometryColumn);
                if (geometry == null)
                    return null;
                    
                var attributes = ReadAttributes(reader);
                
                return new Feature(id, geometry, new AttributeTable(attributes));
            }
            
            return null;
        });
    }

    private async IAsyncEnumerable<IFeature> GetFeaturesAsyncEnumerable(string tableName, IQueryFilter? filter)
    {
        await Task.Yield();
        
        var sql = new StringBuilder($"SELECT * FROM \"{tableName}\"");
        var parameters = new List<SqliteParameter>();
        
        // Build WHERE clause
        var whereConditions = new List<string>();
        whereConditions.Add($"\"{_geometryColumn}\" IS NOT NULL");
        
        if (filter?.SpatialFilter != null && filter.SpatialFilter.FilterGeometry != null)
        {
            var extent = filter.SpatialFilter.FilterGeometry.GetBounds();
            if (!extent.IsNull)
            {
                // Use RTREE spatial index if available
                if (HasSpatialIndex())
                {
                    whereConditions.Add($"rowid IN (" +
                        $"SELECT id FROM \"rtree_{tableName}_{_geometryColumn}\" " +
                        $"WHERE minx <= @maxX AND maxx >= @minX AND miny <= @maxY AND maxy >= @minY" +
                        ")");
                }
                else
                {
                    whereConditions.Add($"(" +
                        $"ST_MinX(\"{_geometryColumn}\") <= @maxX AND ST_MaxX(\"{_geometryColumn}\") >= @minX AND " +
                        $"ST_MinY(\"{_geometryColumn}\") <= @maxY AND ST_MaxY(\"{_geometryColumn}\") >= @minY" +
                        ")");
                }
                
                parameters.Add(new SqliteParameter("@minX", extent.MinX));
                parameters.Add(new SqliteParameter("@maxX", extent.MaxX));
                parameters.Add(new SqliteParameter("@minY", extent.MinY));
                parameters.Add(new SqliteParameter("@maxY", extent.MaxY));
            }
        }
        
        if (filter?.AttributeFilter != null && !string.IsNullOrEmpty(filter.AttributeFilter.WhereClause))
        {
            whereConditions.Add($"({filter.AttributeFilter.WhereClause})");
        }
        
        if (whereConditions.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", whereConditions));
        }
        
        // Add ORDER BY
        var orderBy = BuildOrderByClause(filter);
        if (!string.IsNullOrEmpty(orderBy))
            sql.Append(" ").Append(orderBy);
            
        // Add LIMIT/OFFSET
        var limit = BuildLimitClause(filter);
        if (!string.IsNullOrEmpty(limit))
            sql.Append(" ").Append(limit);
            
        using var command = _connection!.CreateCommand();
        command.CommandText = sql.ToString();
        command.Parameters.AddRange(parameters.ToArray());
        
        using var reader = command.ExecuteReader();
        ulong featureId = 0;
        
        while (reader.Read())
        {
            var geometry = ReadGeometry(reader, _geometryColumn);
            if (geometry == null)
                continue;
                
            var attributes = ReadAttributes(reader);
            
            yield return new Feature(featureId++, geometry, new AttributeTable(attributes));
        }
    }

    #endregion
}

#region Supporting Classes

internal class GeoPackageInfo
{
    public string TableName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? Identifier { get; set; }
    public string? Description { get; set; }
    public double? MinX { get; set; }
    public double? MinY { get; set; }
    public double? MaxX { get; set; }
    public double? MaxY { get; set; }
    public int SrsId { get; set; } = 4326;

    public bool HasBounds => MinX.HasValue && MinY.HasValue && MaxX.HasValue && MaxY.HasValue;
}

#endregion