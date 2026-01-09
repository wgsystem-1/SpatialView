using System.Globalization;
using System.Text;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// 자체 구현된 Shapefile 데이터 소스
/// .shp, .shx, .dbf 파일을 직접 파싱하여 SharpMap 의존성 제거
/// </summary>
public partial class ShapefileDataSource : DataSourceBase
{
    private readonly string _shapefilePath;
    private readonly string _shxPath;
    private readonly string _dbfPath;
    private ShapefileHeader? _header;
    private ShapefileIndex? _index;
    private DbfTable? _dbfTable;
    private Geometry.Envelope? _envelope;

    public override string Name { get; }
    public override string ConnectionString => _shapefilePath;
    public override DataSourceType SourceType => DataSourceType.Shapefile;
    
    /// <summary>
    /// Shapefile의 지오메트리 타입 (헤더에서 읽음)
    /// </summary>
    public ShapeType GeometryShapeType => _header?.ShapeType ?? ShapeType.NullShape;
    
    public ShapefileDataSource(string shapefilePath)
    {
        _shapefilePath = shapefilePath ?? throw new ArgumentNullException(nameof(shapefilePath));
        
        if (!File.Exists(shapefilePath))
            throw new FileNotFoundException($"Shapefile not found: {shapefilePath}");

        var basePath = Path.ChangeExtension(shapefilePath, null);
        _shxPath = basePath + ".shx";
        _dbfPath = basePath + ".dbf";

        if (!File.Exists(_shxPath))
            _shxPath = basePath + ".SHX";
        if (!File.Exists(_dbfPath))
            _dbfPath = basePath + ".DBF";

        if (!File.Exists(_shxPath))
            throw new FileNotFoundException($"Index file not found: {Path.GetFileName(_shxPath)}");
        if (!File.Exists(_dbfPath))
            throw new FileNotFoundException($"Database file not found: {Path.GetFileName(_dbfPath)}");

        Name = Path.GetFileNameWithoutExtension(shapefilePath);
        Description = $"Shapefile: {Name}";
        SRID = DetectSRID(shapefilePath);
        
        Initialize();
    }

    private void Initialize()
    {
        _header = ReadShapefileHeader();
        _index = ReadShapefileIndex();
        _dbfTable = ReadDbfTable();
        _envelope = new Geometry.Envelope(_header.MinX, _header.MaxX, _header.MinY, _header.MaxY);
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
        if (_header == null || _index == null || _dbfTable == null)
            yield break;

        var recordCount = _index.Records.Count;
        
        for (int i = 0; i < recordCount; i++)
        {
            var indexRecord = _index.Records[i];
            
            // 삭제된 레코드 건너뛰기
            if (indexRecord.Offset == 0 || indexRecord.Length == 0)
                continue;

            var geometry = ReadGeometrySafely(indexRecord, i);
            if (geometry == null) continue;

            // 범위 필터링
            if (extent != null && !extent.Intersects(geometry.GetBounds()))
                continue;

            var attributes = ReadAttributesSafely(i);
            if (attributes != null)
            {
                var feature = new Feature((ulong)i, geometry, new AttributeTable(attributes));
                yield return feature;
            }
        }
    }

    public long GetFeatureCount()
    {
        return _index?.Records.Count ?? 0;
    }

    public TableSchema GetSchema()
    {
        return _dbfTable?.GetSchema() ?? new TableSchema();
    }

    private ShapefileHeader ReadShapefileHeader()
    {
        using var stream = new FileStream(_shapefilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        // Big-endian file code (9994)
        var fileCode = ReadInt32BigEndian(reader);
        if (fileCode != 9994)
            throw new InvalidDataException("Invalid Shapefile format");

        // Skip unused fields (5 * 4 bytes)
        reader.ReadBytes(20);

        // Big-endian file length
        var fileLength = ReadInt32BigEndian(reader) * 2; // Convert from 16-bit words to bytes

        // Little-endian version (1000)
        var version = reader.ReadInt32();
        if (version != 1000)
            throw new InvalidDataException("Unsupported Shapefile version");

        // Shape type
        var shapeType = (ShapeType)reader.ReadInt32();

        // Bounding box
        var minX = reader.ReadDouble();
        var minY = reader.ReadDouble();
        var maxX = reader.ReadDouble();
        var maxY = reader.ReadDouble();
        var minZ = reader.ReadDouble();
        var maxZ = reader.ReadDouble();
        var minM = reader.ReadDouble();
        var maxM = reader.ReadDouble();

        return new ShapefileHeader
        {
            FileCode = fileCode,
            FileLength = fileLength,
            Version = version,
            ShapeType = shapeType,
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
            MinZ = minZ,
            MaxZ = maxZ,
            MinM = minM,
            MaxM = maxM
        };
    }

    private ShapefileIndex ReadShapefileIndex()
    {
        using var stream = new FileStream(_shxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        // Header (same as .shp but smaller)
        var fileCode = ReadInt32BigEndian(reader);
        if (fileCode != 9994)
            throw new InvalidDataException("Invalid Shapefile index format");

        reader.ReadBytes(20); // Skip unused
        var fileLength = ReadInt32BigEndian(reader) * 2;

        reader.ReadBytes(32); // Skip version and bounds

        var records = new List<ShapeIndexRecord>();
        
        while (stream.Position < stream.Length)
        {
            var offset = ReadInt32BigEndian(reader) * 2; // Convert to byte offset
            var length = ReadInt32BigEndian(reader) * 2; // Convert to byte length
            
            records.Add(new ShapeIndexRecord { Offset = offset, Length = length });
        }

        return new ShapefileIndex { Records = records };
    }

    private DbfTable ReadDbfTable()
    {
        var encoding = DetectDbfEncoding();
        return new DbfTable(_dbfPath, encoding);
    }

    private Geometry.IGeometry? ReadGeometry(ShapeIndexRecord record)
    {
        using var stream = new FileStream(_shapefilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(record.Offset, SeekOrigin.Begin);
        
        using var reader = new BinaryReader(stream);

        // Record header
        var recordNumber = ReadInt32BigEndian(reader);
        var contentLength = ReadInt32BigEndian(reader) * 2;

        // Shape type
        var shapeType = (ShapeType)reader.ReadInt32();

        return shapeType switch
        {
            ShapeType.NullShape => null,
            ShapeType.Point => ReadPoint(reader),
            ShapeType.PolyLine => ReadPolyLine(reader),
            ShapeType.Polygon => ReadPolygon(reader),
            ShapeType.MultiPoint => ReadMultiPoint(reader),
            ShapeType.PointZ => ReadPointZ(reader),
            ShapeType.PolyLineZ => ReadPolyLineZ(reader),
            ShapeType.PolygonZ => ReadPolygonZ(reader),
            ShapeType.MultiPointZ => ReadMultiPointZ(reader),
            ShapeType.PointM => ReadPointM(reader),
            ShapeType.PolyLineM => ReadPolyLineM(reader),
            ShapeType.PolygonM => ReadPolygonM(reader),
            ShapeType.MultiPointM => ReadMultiPointM(reader),
            _ => throw new NotSupportedException($"Shape type {shapeType} is not supported")
        };
    }

    private Geometry.Point ReadPoint(BinaryReader reader)
    {
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        return new Geometry.Point(x, y);
    }

    private Geometry.LineString ReadPolyLine(BinaryReader reader)
    {
        // Skip bounding box
        reader.ReadBytes(32);

        var numParts = reader.ReadInt32();
        var numPoints = reader.ReadInt32();

        // Read part indices
        var parts = new int[numParts];
        for (int i = 0; i < numParts; i++)
            parts[i] = reader.ReadInt32();

        // Read points
        var coordinates = new List<Geometry.ICoordinate>();
        for (int i = 0; i < numPoints; i++)
        {
            var x = reader.ReadDouble();
            var y = reader.ReadDouble();
            coordinates.Add(new Geometry.Coordinate(x, y));
        }

        // For now, return first part as LineString
        // TODO: Handle multiple parts properly
        var startIndex = parts[0];
        var endIndex = numParts > 1 ? parts[1] : numPoints;
        var lineCoords = coordinates.Skip(startIndex).Take(endIndex - startIndex).ToList();

        return new Geometry.LineString(lineCoords);
    }

    private Geometry.Polygon ReadPolygon(BinaryReader reader)
    {
        // Skip bounding box
        reader.ReadBytes(32);

        var numParts = reader.ReadInt32();
        var numPoints = reader.ReadInt32();

        // Read part indices
        var parts = new int[numParts];
        for (int i = 0; i < numParts; i++)
            parts[i] = reader.ReadInt32();

        // Read points
        var coordinates = new List<Geometry.ICoordinate>();
        for (int i = 0; i < numPoints; i++)
        {
            var x = reader.ReadDouble();
            var y = reader.ReadDouble();
            coordinates.Add(new Geometry.Coordinate(x, y));
        }

        // Create polygon with first part as exterior ring
        // TODO: Handle holes (interior rings) properly
        var startIndex = parts[0];
        var endIndex = numParts > 1 ? parts[1] : numPoints;
        var ringCoords = coordinates.Skip(startIndex).Take(endIndex - startIndex).ToList();

        // Ensure ring is closed
        if (!ringCoords.First().Equals(ringCoords.Last()))
            ringCoords.Add(ringCoords.First());

        var ring = new Geometry.LinearRing(ringCoords.ToArray());
        return new Geometry.Polygon(ring);
    }

    private Geometry.MultiPoint ReadMultiPoint(BinaryReader reader)
    {
        // Skip bounding box
        reader.ReadBytes(32);

        var numPoints = reader.ReadInt32();
        var points = new List<Geometry.Point>();

        for (int i = 0; i < numPoints; i++)
        {
            var x = reader.ReadDouble();
            var y = reader.ReadDouble();
            points.Add(new Geometry.Point(x, y));
        }

        return new Geometry.MultiPoint(points);
    }

    // Z and M variants - simplified implementations
    private Geometry.Point ReadPointZ(BinaryReader reader)
    {
        var x = reader.ReadDouble();
        var y = reader.ReadDouble();
        var z = reader.ReadDouble();
        return new Geometry.Point(x, y, z);
    }

    private Geometry.LineString ReadPolyLineZ(BinaryReader reader) => ReadPolyLine(reader); // Simplified
    private Geometry.Polygon ReadPolygonZ(BinaryReader reader) => ReadPolygon(reader); // Simplified
    private Geometry.MultiPoint ReadMultiPointZ(BinaryReader reader) => ReadMultiPoint(reader); // Simplified
    private Geometry.Point ReadPointM(BinaryReader reader) => ReadPoint(reader); // Simplified
    private Geometry.LineString ReadPolyLineM(BinaryReader reader) => ReadPolyLine(reader); // Simplified
    private Geometry.Polygon ReadPolygonM(BinaryReader reader) => ReadPolygon(reader); // Simplified
    private Geometry.MultiPoint ReadMultiPointM(BinaryReader reader) => ReadMultiPoint(reader); // Simplified

    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private Encoding DetectDbfEncoding()
    {
        // Simple encoding detection - can be enhanced
        var basePath = Path.ChangeExtension(_shapefilePath, null);
        
        // Check for .cpg file
        var cpgPath = basePath + ".cpg";
        if (!File.Exists(cpgPath))
            cpgPath = basePath + ".CPG";
            
        if (File.Exists(cpgPath))
        {
            var encodingName = File.ReadAllText(cpgPath).Trim().ToUpperInvariant();
            try
            {
                return encodingName switch
                {
                    "UTF-8" or "UTF8" => Encoding.UTF8,
                    "CP949" or "949" => Encoding.GetEncoding(949),
                    "EUC-KR" or "EUCKR" => Encoding.GetEncoding(51949),
                    _ => Encoding.GetEncoding(encodingName)
                };
            }
            catch
            {
                // Fall through to default
            }
        }

        // Check DBF LDID (Language Driver ID)
        try
        {
            using var fs = new FileStream(_dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length > 29)
            {
                fs.Seek(29, SeekOrigin.Begin);
                var ldid = fs.ReadByte();
                
                return ldid switch
                {
                    0x79 => Encoding.GetEncoding(949), // Korean
                    0x03 or 0x57 => Encoding.GetEncoding(1252), // Western European
                    _ => Encoding.GetEncoding(949) // Default to Korean
                };
            }
        }
        catch
        {
            // Fall through to default
        }

        return Encoding.GetEncoding(949); // Default Korean encoding
    }

    private static int DetectSRID(string shapefilePath)
    {
        var basePath = Path.ChangeExtension(shapefilePath, null);
        var prjPath = basePath + ".prj";
        
        if (!File.Exists(prjPath))
            prjPath = basePath + ".PRJ";
            
        if (File.Exists(prjPath))
        {
            try
            {
                var wkt = File.ReadAllText(prjPath);
                
                // Extract EPSG code from AUTHORITY clause
                var match = System.Text.RegularExpressions.Regex.Match(
                    wkt, 
                    @"AUTHORITY\s*\[\s*""EPSG""\s*,\s*""?(\d+)""?\s*\]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (match.Success && int.TryParse(match.Groups[1].Value, out int epsg))
                    return epsg;
                
                // Common Korean coordinate systems
                if (wkt.Contains("Korea_2000_Korea_Unified_CS") || wkt.Contains("Korea 2000 / Unified CS"))
                    return 5179;
                    
                if (wkt.Contains("WGS_1984") || wkt.Contains("WGS 84"))
                    return 4326;
                    
                if (wkt.Contains("Web_Mercator"))
                    return 3857;
            }
            catch
            {
                // Fall through to default
            }
        }
        
        return 4326; // Default WGS84
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dbfTable?.Dispose();
        }
        base.Dispose(disposing);
    }
}

#region Supporting Classes

internal class ShapefileHeader
{
    public int FileCode { get; set; }
    public int FileLength { get; set; }
    public int Version { get; set; }
    public ShapeType ShapeType { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MinZ { get; set; }
    public double MaxZ { get; set; }
    public double MinM { get; set; }
    public double MaxM { get; set; }
    
    /// <summary>
    /// 레코드 개수 (추정값)
    /// </summary>
    public int RecordCount => (FileLength * 2 - 100) / 8;
}

internal class ShapefileIndex
{
    public List<ShapeIndexRecord> Records { get; set; } = new();
}

internal class ShapeIndexRecord
{
    public int Offset { get; set; }
    public int Length { get; set; }
}

/// <summary>
/// Shapefile 지오메트리 타입
/// </summary>
public enum ShapeType
{
    NullShape = 0,
    Point = 1,
    PolyLine = 3,
    Polygon = 5,
    MultiPoint = 8,
    PointZ = 11,
    PolyLineZ = 13,
    PolygonZ = 15,
    MultiPointZ = 18,
    PointM = 21,
    PolyLineM = 23,
    PolygonM = 25,
    MultiPointM = 28
}

internal class DbfTable : IDisposable
{
    private readonly FileStream _stream;
    private readonly BinaryReader _reader;
    private readonly Encoding _encoding;
    private readonly DbfHeader _header;
    private readonly List<DbfField> _fields;

    public DbfTable(string dbfPath, Encoding encoding)
    {
        _encoding = encoding;
        _stream = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _reader = new BinaryReader(_stream, _encoding);
        
        _header = ReadHeader();
        _fields = ReadFields();
    }

    private DbfHeader ReadHeader()
    {
        var version = _reader.ReadByte();
        var lastUpdate = new DateTime(1900 + _reader.ReadByte(), _reader.ReadByte(), _reader.ReadByte());
        var recordCount = _reader.ReadInt32();
        var headerLength = _reader.ReadInt16();
        var recordLength = _reader.ReadInt16();
        
        // Skip reserved bytes
        _reader.ReadBytes(20);

        return new DbfHeader
        {
            Version = version,
            LastUpdate = lastUpdate,
            RecordCount = recordCount,
            HeaderLength = headerLength,
            RecordLength = recordLength
        };
    }

    private List<DbfField> ReadFields()
    {
        var fields = new List<DbfField>();
        
        while (_stream.Position < _header.HeaderLength - 1)
        {
            var nameBytes = _reader.ReadBytes(11);
            var name = _encoding.GetString(nameBytes).TrimEnd('\0');
            
            var type = (char)_reader.ReadByte();
            _reader.ReadBytes(4); // Skip displacement
            var length = _reader.ReadByte();
            var decimals = _reader.ReadByte();
            _reader.ReadBytes(14); // Skip reserved
            
            fields.Add(new DbfField
            {
                Name = name,
                Type = type,
                Length = length,
                Decimals = decimals
            });
        }
        
        // Skip field terminator
        _reader.ReadByte();
        
        return fields;
    }

    public Dictionary<string, object?> ReadRecord(int recordIndex)
    {
        if (recordIndex < 0 || recordIndex >= _header.RecordCount)
            throw new ArgumentOutOfRangeException(nameof(recordIndex));

        var recordOffset = _header.HeaderLength + (recordIndex * _header.RecordLength);
        _stream.Seek(recordOffset, SeekOrigin.Begin);

        var deletionFlag = _reader.ReadByte();
        if (deletionFlag == 0x2A) // '*' means deleted
            return new Dictionary<string, object?>();

        var record = new Dictionary<string, object?>();
        
        foreach (var field in _fields)
        {
            var valueBytes = _reader.ReadBytes(field.Length);
            var valueStr = _encoding.GetString(valueBytes).Trim();
            
            object? value = field.Type switch
            {
                'C' => valueStr, // Character
                'N' => ParseNumber(valueStr, field.Decimals), // Number
                'D' => ParseDate(valueStr), // Date
                'L' => ParseLogical(valueStr), // Logical
                'M' => valueStr, // Memo
                _ => valueStr
            };
            
            record[field.Name] = value;
        }
        
        return record;
    }

    private static object? ParseNumber(string value, byte decimals)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
            
        if (decimals == 0)
        {
            return long.TryParse(value, out long longValue) ? longValue : null;
        }
        else
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue) 
                ? doubleValue : null;
        }
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8)
            return null;
            
        if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            return date;
            
        return null;
    }

    private static bool? ParseLogical(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
            
        var ch = char.ToUpperInvariant(value[0]);
        return ch switch
        {
            'T' or 'Y' => true,
            'F' or 'N' => false,
            _ => null
        };
    }

    public TableSchema GetSchema()
    {
        var schema = new TableSchema();
        
        foreach (var field in _fields)
        {
            var column = new ColumnInfo
            {
                Name = field.Name,
                DataType = field.Type switch
                {
                    'C' or 'M' => typeof(string),
                    'N' => field.Decimals == 0 ? typeof(long) : typeof(double),
                    'D' => typeof(DateTime),
                    'L' => typeof(bool),
                    _ => typeof(string)
                },
                MaxLength = field.Length,
                AllowNull = true
            };
            
            schema.Columns.Add(column);
        }
        
        return schema;
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _stream?.Dispose();
    }
}

// ShapefileDataSource Abstract Method Implementations
partial class ShapefileDataSource
{
    #region Abstract Method Implementations

    public override IEnumerable<string> GetTableNames()
    {
        return new[] { Name };
    }

    public override async Task<bool> OpenAsync()
    {
        try
        {
            // Shapefile 초기화 로직이 필요하면 여기에 추가
            IsConnected = true;
            return await Task.FromResult<bool>(true);
        }
        catch
        {
            IsConnected = false;
            return await Task.FromResult<bool>(false);
        }
    }

    public override void Close()
    {
        // No explicit close needed for file-based source
        IsConnected = false;
    }

    public override async Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        return await Task.FromResult(GetSchema());
    }

    public override async Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        return await Task.FromResult(_header?.RecordCount ?? 0);
    }

    public override async Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        return await Task.FromResult(_envelope);
    }

    public override async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        var features = GetFeatures(tableName, filter);
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
        var features = GetFeatures(tableName, null);
        return features?.FirstOrDefault(f => f.Id?.ToString() == id.ToString());
    }
    
    /// <summary>
    /// 피처 목록 가져오기 (동기 메서드)
    /// </summary>
    private List<IFeature>? GetFeatures(string? tableName, IQueryFilter? filter)
    {
        // 공간 필터가 있으면 해당 범위의 피처만 반환
        Geometry.Envelope? extent = null;
        if (filter?.SpatialFilter?.FilterGeometry != null)
        {
            extent = filter.SpatialFilter.FilterGeometry.Envelope;
        }
        
        // GetFeatures(Envelope?)를 호출하여 실제 피처 반환
        return GetFeatures(extent).ToList();
    }

    /// <summary>
    /// 안전하게 지오메트리를 읽어오는 헬퍼 메서드
    /// </summary>
    private IGeometry? ReadGeometrySafely(ShapeIndexRecord indexRecord, int featureIndex)
    {
        try
        {
            return ReadGeometry(indexRecord);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading geometry for feature {featureIndex}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 안전하게 속성을 읽어오는 헬퍼 메서드  
    /// </summary>
    private Dictionary<string, object>? ReadAttributesSafely(int recordIndex)
    {
        try
        {
            return _dbfTable.ReadRecord(recordIndex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading attributes for record {recordIndex}: {ex.Message}");
            return null;
        }
    }

    #endregion
}


internal class DbfHeader
{
    public byte Version { get; set; }
    public DateTime LastUpdate { get; set; }
    public int RecordCount { get; set; }
    public short HeaderLength { get; set; }
    public short RecordLength { get; set; }
}

internal class DbfField
{
    public string Name { get; set; } = string.Empty;
    public char Type { get; set; }
    public byte Length { get; set; }
    public byte Decimals { get; set; }
}

#endregion