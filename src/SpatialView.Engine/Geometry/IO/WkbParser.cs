using System.Text;

namespace SpatialView.Engine.Geometry.IO;

/// <summary>
/// Well-Known Binary (WKB) 형식의 지오메트리 파서
/// OGC Simple Features 스펙을 따른 WKB 파싱 지원
/// </summary>
public static class WkbParser
{
    // WKB Geometry Types (OGC Standard)
    private const uint WkbPoint = 1;
    private const uint WkbLineString = 2;
    private const uint WkbPolygon = 3;
    private const uint WkbMultiPoint = 4;
    private const uint WkbMultiLineString = 5;
    private const uint WkbMultiPolygon = 6;
    private const uint WkbGeometryCollection = 7;

    // Z-coordinate variants
    private const uint WkbPointZ = 1001;
    private const uint WkbLineStringZ = 1002;
    private const uint WkbPolygonZ = 1003;
    private const uint WkbMultiPointZ = 1004;
    private const uint WkbMultiLineStringZ = 1005;
    private const uint WkbMultiPolygonZ = 1006;
    private const uint WkbGeometryCollectionZ = 1007;

    /// <summary>
    /// WKB 바이트 배열을 지오메트리로 파싱
    /// </summary>
    /// <param name="wkb">WKB 바이트 배열</param>
    /// <returns>파싱된 지오메트리</returns>
    public static IGeometry Parse(byte[] wkb)
    {
        if (wkb == null || wkb.Length == 0)
            throw new ArgumentException("WKB data cannot be null or empty", nameof(wkb));

        using var stream = new MemoryStream(wkb);
        using var reader = new BinaryReader(stream);

        return ReadGeometry(reader);
    }

    /// <summary>
    /// 16진수 문자열로 인코딩된 WKB를 지오메트리로 파싱
    /// </summary>
    /// <param name="hexWkb">16진수 WKB 문자열</param>
    /// <returns>파싱된 지오메트리</returns>
    public static IGeometry ParseHex(string hexWkb)
    {
        if (string.IsNullOrWhiteSpace(hexWkb))
            throw new ArgumentException("Hex WKB string cannot be null or empty", nameof(hexWkb));

        var bytes = HexToBytes(hexWkb);
        return Parse(bytes);
    }

    /// <summary>
    /// 지오메트리를 WKB 바이트 배열로 변환
    /// </summary>
    /// <param name="geometry">변환할 지오메트리</param>
    /// <param name="littleEndian">리틀 엔디안 사용 여부 (기본값: true)</param>
    /// <returns>WKB 바이트 배열</returns>
    public static byte[] Write(IGeometry geometry, bool littleEndian = true)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteGeometry(writer, geometry, littleEndian);
        return stream.ToArray();
    }

    /// <summary>
    /// 지오메트리를 16진수 문자열로 인코딩된 WKB로 변환
    /// </summary>
    /// <param name="geometry">변환할 지오메트리</param>
    /// <param name="littleEndian">리틀 엔디안 사용 여부 (기본값: true)</param>
    /// <returns>16진수 WKB 문자열</returns>
    public static string WriteHex(IGeometry geometry, bool littleEndian = true)
    {
        var bytes = Write(geometry, littleEndian);
        return BytesToHex(bytes);
    }

    #region Reading Methods

    private static IGeometry ReadGeometry(BinaryReader reader)
    {
        // Read byte order
        var byteOrder = reader.ReadByte();
        var littleEndian = byteOrder == 1;

        // Read geometry type
        var geometryType = ReadUInt32(reader, littleEndian);
        var hasZ = geometryType > 1000;
        var baseType = hasZ ? geometryType - 1000 : geometryType;

        return baseType switch
        {
            WkbPoint => ReadPoint(reader, littleEndian, hasZ),
            WkbLineString => ReadLineString(reader, littleEndian, hasZ),
            WkbPolygon => ReadPolygon(reader, littleEndian, hasZ),
            WkbMultiPoint => ReadMultiPoint(reader, littleEndian, hasZ),
            WkbMultiLineString => ReadMultiLineString(reader, littleEndian, hasZ),
            WkbMultiPolygon => ReadMultiPolygon(reader, littleEndian, hasZ),
            WkbGeometryCollection => ReadGeometryCollection(reader, littleEndian),
            _ => throw new ArgumentException($"Unsupported WKB geometry type: {geometryType}")
        };
    }

    private static Point ReadPoint(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var x = ReadDouble(reader, littleEndian);
        var y = ReadDouble(reader, littleEndian);
        
        if (hasZ)
        {
            var z = ReadDouble(reader, littleEndian);
            return new Point(x, y, z);
        }
        
        return new Point(x, y);
    }

    private static LineString ReadLineString(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var numPoints = ReadUInt32(reader, littleEndian);
        var coordinates = new List<ICoordinate>();

        for (uint i = 0; i < numPoints; i++)
        {
            var x = ReadDouble(reader, littleEndian);
            var y = ReadDouble(reader, littleEndian);
            
            if (hasZ)
            {
                var z = ReadDouble(reader, littleEndian);
                coordinates.Add(new Coordinate(x, y, z));
            }
            else
            {
                coordinates.Add(new Coordinate(x, y));
            }
        }

        return new LineString(coordinates);
    }

    private static Polygon ReadPolygon(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var numRings = ReadUInt32(reader, littleEndian);
        
        if (numRings == 0)
            return Polygon.Empty;

        // Exterior ring
        var exteriorRing = ReadLinearRing(reader, littleEndian, hasZ);
        
        // Interior rings (holes)
        var interiorRings = new List<LinearRing>();
        for (uint i = 1; i < numRings; i++)
        {
            interiorRings.Add(ReadLinearRing(reader, littleEndian, hasZ));
        }

        return new Polygon(exteriorRing, interiorRings.ToArray());
    }

    private static LinearRing ReadLinearRing(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var numPoints = ReadUInt32(reader, littleEndian);
        var coordinates = new List<ICoordinate>();

        for (uint i = 0; i < numPoints; i++)
        {
            var x = ReadDouble(reader, littleEndian);
            var y = ReadDouble(reader, littleEndian);
            
            if (hasZ)
            {
                var z = ReadDouble(reader, littleEndian);
                coordinates.Add(new Coordinate(x, y, z));
            }
            else
            {
                coordinates.Add(new Coordinate(x, y));
            }
        }

        return new LinearRing(coordinates);
    }

    private static MultiPoint ReadMultiPoint(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var numPoints = ReadUInt32(reader, littleEndian);
        var points = new List<Point>();

        for (uint i = 0; i < numPoints; i++)
        {
            var point = (Point)ReadGeometry(reader);
            points.Add(point);
        }

        return new MultiPoint(points);
    }

    private static MultiLineString ReadMultiLineString(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var numLineStrings = ReadUInt32(reader, littleEndian);
        var lineStrings = new List<LineString>();

        for (uint i = 0; i < numLineStrings; i++)
        {
            var lineString = (LineString)ReadGeometry(reader);
            lineStrings.Add(lineString);
        }

        return new MultiLineString(lineStrings);
    }

    private static MultiPolygon ReadMultiPolygon(BinaryReader reader, bool littleEndian, bool hasZ)
    {
        var numPolygons = ReadUInt32(reader, littleEndian);
        var polygons = new List<Polygon>();

        for (uint i = 0; i < numPolygons; i++)
        {
            var polygon = (Polygon)ReadGeometry(reader);
            polygons.Add(polygon);
        }

        return new MultiPolygon(polygons);
    }

    private static GeometryCollection ReadGeometryCollection(BinaryReader reader, bool littleEndian)
    {
        var numGeometries = ReadUInt32(reader, littleEndian);
        var geometries = new List<IGeometry>();

        for (uint i = 0; i < numGeometries; i++)
        {
            geometries.Add(ReadGeometry(reader));
        }

        return new GeometryCollection(geometries);
    }

    #endregion

    #region Writing Methods

    private static void WriteGeometry(BinaryWriter writer, IGeometry geometry, bool littleEndian)
    {
        // Write byte order
        writer.Write(littleEndian ? (byte)1 : (byte)0);

        switch (geometry)
        {
            case Point point:
                WritePoint(writer, point, littleEndian);
                break;
            case LineString lineString:
                WriteLineString(writer, lineString, littleEndian);
                break;
            case Polygon polygon:
                WritePolygon(writer, polygon, littleEndian);
                break;
            case MultiPoint multiPoint:
                WriteMultiPoint(writer, multiPoint, littleEndian);
                break;
            case MultiLineString multiLineString:
                WriteMultiLineString(writer, multiLineString, littleEndian);
                break;
            case MultiPolygon multiPolygon:
                WriteMultiPolygon(writer, multiPolygon, littleEndian);
                break;
            case GeometryCollection collection:
                WriteGeometryCollection(writer, collection, littleEndian);
                break;
            default:
                throw new ArgumentException($"Unsupported geometry type: {geometry.GetType().Name}");
        }
    }

    private static void WritePoint(BinaryWriter writer, Point point, bool littleEndian)
    {
        var coord = point.Coordinate;
        var hasZ = !double.IsNaN(coord.Z);
        
        // Write geometry type
        WriteUInt32(writer, hasZ ? WkbPointZ : WkbPoint, littleEndian);
        
        // Write coordinates
        WriteDouble(writer, coord.X, littleEndian);
        WriteDouble(writer, coord.Y, littleEndian);
        
        if (hasZ)
            WriteDouble(writer, coord.Z, littleEndian);
    }

    private static void WriteLineString(BinaryWriter writer, LineString lineString, bool littleEndian)
    {
        var hasZ = lineString.Coordinates.Any(c => !double.IsNaN(c.Z));
        
        // Write geometry type
        WriteUInt32(writer, hasZ ? WkbLineStringZ : WkbLineString, littleEndian);
        
        // Write number of points
        WriteUInt32(writer, (uint)lineString.Coordinates.Length, littleEndian);
        
        // Write coordinates
        foreach (var coord in lineString.Coordinates)
        {
            WriteDouble(writer, coord.X, littleEndian);
            WriteDouble(writer, coord.Y, littleEndian);
            
            if (hasZ)
                WriteDouble(writer, !double.IsNaN(coord.Z) ? coord.Z : 0.0, littleEndian);
        }
    }

    private static void WritePolygon(BinaryWriter writer, Polygon polygon, bool littleEndian)
    {
        var hasZ = polygon.ExteriorRing.Coordinates.Any(c => !double.IsNaN(c.Z));
        
        // Write geometry type
        WriteUInt32(writer, hasZ ? WkbPolygonZ : WkbPolygon, littleEndian);
        
        // Write number of rings (1 exterior + N interior)
        WriteUInt32(writer, (uint)(1 + polygon.InteriorRings.Count), littleEndian);
        
        // Write exterior ring
        WriteLinearRing(writer, polygon.ExteriorRing, littleEndian, hasZ);
        
        // Write interior rings
        foreach (var interiorRing in polygon.InteriorRings)
        {
            WriteLinearRing(writer, interiorRing, littleEndian, hasZ);
        }
    }

    private static void WriteLinearRing(BinaryWriter writer, LinearRing ring, bool littleEndian, bool hasZ)
    {
        // Write number of points
        WriteUInt32(writer, (uint)ring.Coordinates.Length, littleEndian);
        
        // Write coordinates
        foreach (var coord in ring.Coordinates)
        {
            WriteDouble(writer, coord.X, littleEndian);
            WriteDouble(writer, coord.Y, littleEndian);
            
            if (hasZ)
                WriteDouble(writer, !double.IsNaN(coord.Z) ? coord.Z : 0.0, littleEndian);
        }
    }

    private static void WriteMultiPoint(BinaryWriter writer, MultiPoint multiPoint, bool littleEndian)
    {
        var hasZ = multiPoint.Geometries.Cast<Point>().Any(p => !double.IsNaN(p.Coordinate.Z));
        
        // Write geometry type
        WriteUInt32(writer, hasZ ? WkbMultiPointZ : WkbMultiPoint, littleEndian);
        
        // Write number of points
        WriteUInt32(writer, (uint)multiPoint.Geometries.Count, littleEndian);
        
        // Write points
        foreach (var point in multiPoint.Geometries.Cast<Point>())
        {
            WriteGeometry(writer, point, littleEndian);
        }
    }

    private static void WriteMultiLineString(BinaryWriter writer, MultiLineString multiLineString, bool littleEndian)
    {
        var hasZ = multiLineString.Geometries.Cast<LineString>().Any(ls => ls.Coordinates.Any(c => !double.IsNaN(c.Z)));
        
        // Write geometry type
        WriteUInt32(writer, hasZ ? WkbMultiLineStringZ : WkbMultiLineString, littleEndian);
        
        // Write number of linestrings
        WriteUInt32(writer, (uint)multiLineString.Geometries.Count, littleEndian);
        
        // Write linestrings
        foreach (var lineString in multiLineString.Geometries.Cast<LineString>())
        {
            WriteGeometry(writer, lineString, littleEndian);
        }
    }

    private static void WriteMultiPolygon(BinaryWriter writer, MultiPolygon multiPolygon, bool littleEndian)
    {
        var hasZ = multiPolygon.Geometries.Cast<Polygon>().Any(p => p.ExteriorRing.Coordinates.Any(c => !double.IsNaN(c.Z)));
        
        // Write geometry type
        WriteUInt32(writer, hasZ ? WkbMultiPolygonZ : WkbMultiPolygon, littleEndian);
        
        // Write number of polygons
        WriteUInt32(writer, (uint)multiPolygon.Geometries.Count, littleEndian);
        
        // Write polygons
        foreach (var polygon in multiPolygon.Geometries.Cast<Polygon>())
        {
            WriteGeometry(writer, polygon, littleEndian);
        }
    }

    private static void WriteGeometryCollection(BinaryWriter writer, GeometryCollection collection, bool littleEndian)
    {
        // Write geometry type
        WriteUInt32(writer, WkbGeometryCollection, littleEndian);
        
        // Write number of geometries
        WriteUInt32(writer, (uint)collection.Geometries.Count, littleEndian);
        
        // Write geometries
        foreach (var geometry in collection.Geometries)
        {
            WriteGeometry(writer, geometry, littleEndian);
        }
    }

    #endregion

    #region Binary I/O Helpers

    private static uint ReadUInt32(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (littleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static double ReadDouble(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(8);
        if (littleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    private static void WriteUInt32(BinaryWriter writer, uint value, bool littleEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (littleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes);
    }

    private static void WriteDouble(BinaryWriter writer, double value, bool littleEndian)
    {
        var bytes = BitConverter.GetBytes(value);
        if (littleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        writer.Write(bytes);
    }

    #endregion

    #region Hex String Conversion

    private static byte[] HexToBytes(string hex)
    {
        // Remove any whitespace and ensure even length
        hex = hex.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private static string BytesToHex(byte[] bytes)
    {
        var hex = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            hex.AppendFormat("{0:X2}", b);
        }
        return hex.ToString();
    }

    #endregion
}