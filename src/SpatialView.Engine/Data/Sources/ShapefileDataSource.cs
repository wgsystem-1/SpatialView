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
    private readonly string _dbfPath;
    private FileStream _stream;
    private BinaryReader _reader;
    private BinaryWriter? _writer;
    private readonly Encoding _encoding;
    private DbfHeader _header;
    private readonly List<DbfField> _fields;
    private bool _isWriteMode;

    public DbfTable(string dbfPath, Encoding encoding)
    {
        _dbfPath = dbfPath;
        _encoding = encoding;
        _stream = new FileStream(dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        _reader = new BinaryReader(_stream, _encoding);

        _header = ReadHeader();
        _fields = ReadFields();
    }

    /// <summary>
    /// 쓰기 모드로 전환 (파일 다시 열기)
    /// </summary>
    public bool EnableWriteMode()
    {
        if (_isWriteMode) return true;

        try
        {
            _reader?.Dispose();
            _stream?.Dispose();

            _stream = new FileStream(_dbfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            _reader = new BinaryReader(_stream, _encoding);
            _writer = new BinaryWriter(_stream, _encoding);
            _isWriteMode = true;
            return true;
        }
        catch
        {
            // 쓰기 모드 전환 실패 시 읽기 모드로 복구
            _stream = new FileStream(_dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_stream, _encoding);
            _writer = null;
            _isWriteMode = false;
            return false;
        }
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

    /// <summary>
    /// 레코드의 속성값 업데이트 (쓰기 모드에서만 동작)
    /// </summary>
    public bool WriteRecord(int recordIndex, Dictionary<string, object?> attributes)
    {
        if (!_isWriteMode || _writer == null)
            return false;

        if (recordIndex < 0 || recordIndex >= _header.RecordCount)
            return false;

        try
        {
            var recordOffset = _header.HeaderLength + (recordIndex * _header.RecordLength);
            _stream.Seek(recordOffset, SeekOrigin.Begin);

            // 삭제 플래그 (공백 = 유효한 레코드)
            _writer.Write((byte)' ');

            // 각 필드 값 쓰기
            foreach (var field in _fields)
            {
                string valueStr;
                if (attributes.TryGetValue(field.Name, out var value) && value != null && value != DBNull.Value)
                {
                    valueStr = FormatFieldValue(value, field);
                }
                else
                {
                    valueStr = new string(' ', field.Length);
                }

                // 필드 길이에 맞춰 패딩/잘라내기
                var bytes = _encoding.GetBytes(valueStr.PadRight(field.Length));
                if (bytes.Length > field.Length)
                {
                    bytes = bytes.Take(field.Length).ToArray();
                }
                else if (bytes.Length < field.Length)
                {
                    var paddedBytes = new byte[field.Length];
                    Array.Copy(bytes, paddedBytes, bytes.Length);
                    // 나머지는 공백으로 채우기
                    for (int i = bytes.Length; i < field.Length; i++)
                        paddedBytes[i] = (byte)' ';
                    bytes = paddedBytes;
                }

                _writer.Write(bytes);
            }

            _writer.Flush();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error writing record {recordIndex}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 필드 값을 DBF 형식 문자열로 변환
    /// </summary>
    private string FormatFieldValue(object value, DbfField field)
    {
        return field.Type switch
        {
            'C' or 'M' => value.ToString() ?? "",
            'N' => FormatNumber(value, field),
            'D' => FormatDate(value),
            'L' => FormatLogical(value),
            _ => value.ToString() ?? ""
        };
    }

    private string FormatNumber(object value, DbfField field)
    {
        if (value is double d)
        {
            if (field.Decimals > 0)
                return d.ToString($"F{field.Decimals}", CultureInfo.InvariantCulture).PadLeft(field.Length);
            return ((long)d).ToString().PadLeft(field.Length);
        }
        if (value is float f)
        {
            if (field.Decimals > 0)
                return f.ToString($"F{field.Decimals}", CultureInfo.InvariantCulture).PadLeft(field.Length);
            return ((long)f).ToString().PadLeft(field.Length);
        }
        if (value is decimal dec)
        {
            if (field.Decimals > 0)
                return dec.ToString($"F{field.Decimals}", CultureInfo.InvariantCulture).PadLeft(field.Length);
            return ((long)dec).ToString().PadLeft(field.Length);
        }
        if (value is int i)
            return i.ToString().PadLeft(field.Length);
        if (value is long l)
            return l.ToString().PadLeft(field.Length);

        return value.ToString()?.PadLeft(field.Length) ?? new string(' ', field.Length);
    }

    private static string FormatDate(object value)
    {
        if (value is DateTime dt)
            return dt.ToString("yyyyMMdd");
        return "        "; // 8 spaces
    }

    private static string FormatLogical(object value)
    {
        if (value is bool b)
            return b ? "T" : "F";
        return "?";
    }

    /// <summary>
    /// 레코드 수 반환
    /// </summary>
    public int RecordCount => _header.RecordCount;

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
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
    }

    /// <summary>
    /// DBF 파일 전체 재작성 (필드 구조 변경 지원)
    /// </summary>
    /// <param name="features">저장할 피처 목록</param>
    /// <param name="fieldDefinitions">필드 정의 (이름, 타입, 길이, 소수점)</param>
    /// <returns>성공 여부</returns>
    public bool RewriteDbf(IEnumerable<IFeature> features, List<DbfFieldDefinition> fieldDefinitions)
    {
        if (fieldDefinitions == null || fieldDefinitions.Count == 0)
            return false;

        var featureList = features.ToList();
        var tempPath = _dbfPath + ".tmp";

        try
        {
            // 먼저 기존 파일 닫기
            _writer?.Dispose();
            _reader?.Dispose();
            _stream?.Dispose();

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs, _encoding))
            {
                // 필드 정보로 새 헤더 계산
                var recordLength = (short)(1 + fieldDefinitions.Sum(f => f.Length)); // 1 for deletion flag
                var headerLength = (short)(32 + fieldDefinitions.Count * 32 + 1); // 32 header + 32*fields + terminator

                // 헤더 작성
                WriteDbfHeader(writer, featureList.Count, headerLength, recordLength);

                // 필드 정의 작성
                foreach (var field in fieldDefinitions)
                {
                    WriteDbfFieldDescriptor(writer, field);
                }

                // 헤더 종결자
                writer.Write((byte)0x0D);

                // 레코드 작성
                foreach (var feature in featureList)
                {
                    WriteDbfRecord(writer, feature, fieldDefinitions);
                }

                // 파일 종결자
                writer.Write((byte)0x1A);
            }

            // 원본 파일 교체
            File.Delete(_dbfPath);
            File.Move(tempPath, _dbfPath);

            // 파일 다시 열기
            _stream = new FileStream(_dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new BinaryReader(_stream, _encoding);
            _header = ReadHeader();
            _fields.Clear();
            _fields.AddRange(ReadFields());

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RewriteDbf error: {ex.Message}");
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            // 파일 다시 열기 시도
            try
            {
                _stream = new FileStream(_dbfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                _reader = new BinaryReader(_stream, _encoding);
            }
            catch { }

            return false;
        }
    }

    private void WriteDbfHeader(BinaryWriter writer, int recordCount, short headerLength, short recordLength)
    {
        // Version (dBase III)
        writer.Write((byte)0x03);
        // Last update date (YY MM DD)
        var now = DateTime.Now;
        writer.Write((byte)(now.Year - 1900));
        writer.Write((byte)now.Month);
        writer.Write((byte)now.Day);
        // Record count
        writer.Write(recordCount);
        // Header length
        writer.Write(headerLength);
        // Record length
        writer.Write(recordLength);
        // Reserved bytes (20)
        writer.Write(new byte[20]);
    }

    private void WriteDbfFieldDescriptor(BinaryWriter writer, DbfFieldDefinition field)
    {
        // Field name (11 bytes, null-padded)
        var nameBytes = _encoding.GetBytes(field.Name.PadRight(11, '\0'));
        if (nameBytes.Length > 11)
            nameBytes = nameBytes.Take(11).ToArray();
        else if (nameBytes.Length < 11)
        {
            var padded = new byte[11];
            Array.Copy(nameBytes, padded, nameBytes.Length);
            nameBytes = padded;
        }
        writer.Write(nameBytes);

        // Field type (1 byte)
        writer.Write((byte)field.Type);

        // Reserved (4 bytes)
        writer.Write(new byte[4]);

        // Field length (1 byte)
        writer.Write(field.Length);

        // Decimal count (1 byte)
        writer.Write(field.Decimals);

        // Reserved (14 bytes)
        writer.Write(new byte[14]);
    }

    private void WriteDbfRecord(BinaryWriter writer, IFeature feature, List<DbfFieldDefinition> fields)
    {
        // Deletion flag (space = not deleted)
        writer.Write((byte)' ');

        foreach (var field in fields)
        {
            var value = feature.Attributes[field.Name];
            var valueStr = FormatFieldValue(value ?? DBNull.Value, new DbfField
            {
                Name = field.Name,
                Type = field.Type,
                Length = field.Length,
                Decimals = field.Decimals
            });

            // 필드 길이에 맞춰 패딩/잘라내기
            var bytes = _encoding.GetBytes(valueStr.PadRight(field.Length));
            if (bytes.Length > field.Length)
                bytes = bytes.Take(field.Length).ToArray();
            else if (bytes.Length < field.Length)
            {
                var padded = new byte[field.Length];
                Array.Copy(bytes, padded, bytes.Length);
                for (int i = bytes.Length; i < field.Length; i++)
                    padded[i] = (byte)' ';
                bytes = padded;
            }

            writer.Write(bytes);
        }
    }

    /// <summary>
    /// 현재 필드 정의 목록 가져오기
    /// </summary>
    public List<DbfFieldDefinition> GetFieldDefinitions()
    {
        return _fields.Select(f => new DbfFieldDefinition
        {
            Name = f.Name,
            Type = f.Type,
            Length = f.Length,
            Decimals = f.Decimals
        }).ToList();
    }
}

/// <summary>
/// DBF 필드 정의
/// </summary>
public class DbfFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public char Type { get; set; } = 'C'; // C=Character, N=Numeric, D=Date, L=Logical
    public byte Length { get; set; } = 10;
    public byte Decimals { get; set; } = 0;

    /// <summary>
    /// .NET 타입에서 DBF 필드 정의 생성
    /// </summary>
    public static DbfFieldDefinition FromType(string name, Type type)
    {
        return type switch
        {
            Type t when t == typeof(string) => new DbfFieldDefinition { Name = name, Type = 'C', Length = 254 },
            Type t when t == typeof(int) || t == typeof(long) => new DbfFieldDefinition { Name = name, Type = 'N', Length = 19 },
            Type t when t == typeof(double) || t == typeof(float) || t == typeof(decimal) => new DbfFieldDefinition { Name = name, Type = 'N', Length = 19, Decimals = 6 },
            Type t when t == typeof(DateTime) => new DbfFieldDefinition { Name = name, Type = 'D', Length = 8 },
            Type t when t == typeof(bool) => new DbfFieldDefinition { Name = name, Type = 'L', Length = 1 },
            _ => new DbfFieldDefinition { Name = name, Type = 'C', Length = 254 }
        };
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

    /// <summary>
    /// 피처 속성 업데이트 (Shapefile DBF 파일에 저장)
    /// </summary>
    public override async Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        if (_dbfTable == null)
        {
            LastError = "DBF table not loaded";
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                // 쓰기 모드 활성화
                if (!_dbfTable.EnableWriteMode())
                {
                    LastError = "Cannot enable write mode. File may be locked or read-only.";
                    return false;
                }

                // FID로 레코드 인덱스 찾기 (FID = 레코드 인덱스 + 1 이므로)
                var recordIndex = (int)feature.Id - 1;
                if (recordIndex < 0 || recordIndex >= _dbfTable.RecordCount)
                {
                    LastError = $"Invalid feature ID: {feature.Id}";
                    return false;
                }

                // 속성 딕셔너리 생성
                var attributes = new Dictionary<string, object?>();
                foreach (var attrName in feature.Attributes.AttributeNames)
                {
                    if (attrName == "Geometry" || attrName == "_geom_")
                        continue;
                    attributes[attrName] = feature.Attributes[attrName];
                }

                // DBF 레코드 업데이트
                if (!_dbfTable.WriteRecord(recordIndex, attributes))
                {
                    LastError = "Failed to write record to DBF file";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Update failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"UpdateFeatureAsync error: {ex}");
                return false;
            }
        });
    }

    /// <summary>
    /// 여러 피처의 속성을 일괄 업데이트
    /// </summary>
    public async Task<int> UpdateFeaturesAsync(string tableName, IEnumerable<IFeature> features)
    {
        if (_dbfTable == null)
        {
            LastError = "DBF table not loaded";
            return 0;
        }

        return await Task.Run(() =>
        {
            try
            {
                // 쓰기 모드 활성화
                if (!_dbfTable.EnableWriteMode())
                {
                    LastError = "Cannot enable write mode. File may be locked or read-only.";
                    return 0;
                }

                int successCount = 0;
                foreach (var feature in features)
                {
                    var recordIndex = (int)feature.Id - 1;
                    if (recordIndex < 0 || recordIndex >= _dbfTable.RecordCount)
                        continue;

                    var attributes = new Dictionary<string, object?>();
                    foreach (var attrName in feature.Attributes.AttributeNames)
                    {
                        if (attrName == "Geometry" || attrName == "_geom_")
                            continue;
                        attributes[attrName] = feature.Attributes[attrName];
                    }

                    if (_dbfTable.WriteRecord(recordIndex, attributes))
                        successCount++;
                }

                return successCount;
            }
            catch (Exception ex)
            {
                LastError = $"Batch update failed: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"UpdateFeaturesAsync error: {ex}");
                return 0;
            }
        });
    }

    /// <summary>
    /// DBF 파일 전체 재작성 (필드 구조 변경 포함)
    /// </summary>
    public bool RewriteDbf(IEnumerable<IFeature> features, List<DbfFieldDefinition> fieldDefinitions)
    {
        if (_dbfTable == null)
        {
            LastError = "DBF table not loaded";
            return false;
        }

        return _dbfTable.RewriteDbf(features, fieldDefinitions);
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