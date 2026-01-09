using SpatialView.Engine.Geometry;
using System.IO.Compression;

namespace SpatialView.Engine.Memory;

/// <summary>
/// 지오메트리 압축 유틸리티
/// 메모리 절약을 위한 좌표 데이터 압축
/// </summary>
public static class GeometryCompression
{
    /// <summary>
    /// 지오메트리를 압축된 바이트 배열로 변환
    /// </summary>
    public static byte[] CompressGeometry(IGeometry geometry)
    {
        using var memoryStream = new MemoryStream();
        using (var compressionStream = new DeflateStream(memoryStream, CompressionLevel.Fastest))
        using (var writer = new BinaryWriter(compressionStream))
        {
            WriteGeometry(writer, geometry);
        }
        return memoryStream.ToArray();
    }
    
    /// <summary>
    /// 압축된 바이트 배열에서 지오메트리 복원
    /// </summary>
    public static IGeometry? DecompressGeometry(byte[] compressedData, GeometryType expectedType)
    {
        using var memoryStream = new MemoryStream(compressedData);
        using var decompressionStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
        using var reader = new BinaryReader(decompressionStream);
        
        return ReadGeometry(reader, expectedType);
    }
    
    /// <summary>
    /// 좌표 배열 압축 (델타 인코딩 사용)
    /// </summary>
    public static byte[] CompressCoordinates(ICoordinate[] coordinates, double precision = 1e-6)
    {
        if (coordinates.Length == 0) return Array.Empty<byte>();
        
        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);
        
        writer.Write(coordinates.Length);
        
        // 첫 번째 좌표는 절대값으로 저장
        var first = coordinates[0];
        writer.Write(QuantizeValue(first.X, precision));
        writer.Write(QuantizeValue(first.Y, precision));
        var hasZ = !double.IsNaN(first.Z);
        writer.Write(hasZ);
        if (hasZ)
            writer.Write(QuantizeValue(first.Z, precision));
        
        // 나머지는 델타값으로 저장
        for (int i = 1; i < coordinates.Length; i++)
        {
            var prev = coordinates[i - 1];
            var curr = coordinates[i];
            
            writer.Write(QuantizeValue(curr.X - prev.X, precision));
            writer.Write(QuantizeValue(curr.Y - prev.Y, precision));
            
            var currHasZ = !double.IsNaN(curr.Z);
            var prevHasZ = !double.IsNaN(prev.Z);
            
            if (currHasZ && prevHasZ)
            {
                writer.Write(true);
                writer.Write(QuantizeValue(curr.Z - prev.Z, precision));
            }
            else if (currHasZ)
            {
                writer.Write(true);
                writer.Write(QuantizeValue(curr.Z, precision));
            }
            else
            {
                writer.Write(false);
            }
        }
        
        return memoryStream.ToArray();
    }
    
    /// <summary>
    /// 압축된 좌표 배열 복원
    /// </summary>
    public static ICoordinate[] DecompressCoordinates(byte[] compressedData, double precision = 1e-6)
    {
        if (compressedData.Length == 0) return Array.Empty<ICoordinate>();
        
        using var memoryStream = new MemoryStream(compressedData);
        using var reader = new BinaryReader(memoryStream);
        
        var count = reader.ReadInt32();
        var coordinates = new ICoordinate[count];
        
        // 첫 번째 좌표 복원
        var x = DequantizeValue(reader.ReadInt64(), precision);
        var y = DequantizeValue(reader.ReadInt64(), precision);
        var hasZ = reader.ReadBoolean();
        var z = hasZ ? DequantizeValue(reader.ReadInt64(), precision) : double.NaN;
        
        coordinates[0] = new Coordinate(x, y, z);
        
        // 나머지 델타값으로부터 복원
        for (int i = 1; i < count; i++)
        {
            var prev = coordinates[i - 1];
            var deltaX = DequantizeValue(reader.ReadInt64(), precision);
            var deltaY = DequantizeValue(reader.ReadInt64(), precision);
            
            x = prev.X + deltaX;
            y = prev.Y + deltaY;
            
            hasZ = reader.ReadBoolean();
            if (hasZ)
            {
                var deltaZ = DequantizeValue(reader.ReadInt64(), precision);
                z = !double.IsNaN(prev.Z) ? prev.Z + deltaZ : deltaZ;
            }
            else
            {
                z = double.NaN;
            }
            
            coordinates[i] = new Coordinate(x, y, z);
        }
        
        return coordinates;
    }
    
    private static void WriteGeometry(BinaryWriter writer, IGeometry geometry)
    {
        writer.Write((byte)geometry.GeometryType);
        writer.Write(geometry.SRID);
        
        switch (geometry.GeometryType)
        {
            case GeometryType.Point:
                var point = (Point)geometry;
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
                break;
                
            case GeometryType.LineString:
                var lineString = (LineString)geometry;
                var compressedCoords = CompressCoordinates(lineString.Coordinates);
                writer.Write(compressedCoords.Length);
                writer.Write(compressedCoords);
                break;
                
            case GeometryType.Polygon:
                var polygon = (Polygon)geometry;
                // 외부 링
                var exteriorCompressed = CompressCoordinates(polygon.ExteriorRing.Coordinates);
                writer.Write(exteriorCompressed.Length);
                writer.Write(exteriorCompressed);
                
                // 내부 링들
                writer.Write(polygon.NumInteriorRings);
                foreach (var ring in polygon.InteriorRings)
                {
                    var ringCompressed = CompressCoordinates(ring.Coordinates);
                    writer.Write(ringCompressed.Length);
                    writer.Write(ringCompressed);
                }
                break;
                
            // 다른 지오메트리 타입들도 유사하게 구현
            default:
                throw new NotSupportedException($"Geometry type {geometry.GeometryType} is not supported for compression");
        }
    }
    
    private static IGeometry? ReadGeometry(BinaryReader reader, GeometryType expectedType)
    {
        var type = (GeometryType)reader.ReadByte();
        if (type != expectedType) return null;
        
        var srid = reader.ReadInt32();
        
        switch (type)
        {
            case GeometryType.Point:
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var z = reader.ReadDouble();
                return new Point(x, y, z) { SRID = srid };
                
            case GeometryType.LineString:
                var coordsLength = reader.ReadInt32();
                var coordsData = reader.ReadBytes(coordsLength);
                var coords = DecompressCoordinates(coordsData);
                return new LineString(coords) { SRID = srid };
                
            case GeometryType.Polygon:
                // 외부 링
                var exteriorLength = reader.ReadInt32();
                var exteriorData = reader.ReadBytes(exteriorLength);
                var exteriorCoords = DecompressCoordinates(exteriorData);
                var exteriorRing = new LinearRing(exteriorCoords);
                
                // 내부 링들
                var numInteriorRings = reader.ReadInt32();
                var interiorRings = new LinearRing[numInteriorRings];
                for (int i = 0; i < numInteriorRings; i++)
                {
                    var ringLength = reader.ReadInt32();
                    var ringData = reader.ReadBytes(ringLength);
                    var ringCoords = DecompressCoordinates(ringData);
                    interiorRings[i] = new LinearRing(ringCoords);
                }
                
                return new Polygon(exteriorRing, interiorRings) { SRID = srid };
                
            default:
                return null;
        }
    }
    
    /// <summary>
    /// 부동소수점을 정수로 양자화
    /// </summary>
    private static long QuantizeValue(double value, double precision)
    {
        return (long)Math.Round(value / precision);
    }
    
    /// <summary>
    /// 양자화된 정수를 부동소수점으로 복원
    /// </summary>
    private static double DequantizeValue(long quantized, double precision)
    {
        return quantized * precision;
    }
}

/// <summary>
/// 압축된 지오메트리 래퍼
/// </summary>
public class CompressedGeometry
{
    private byte[]? _compressedData;
    private IGeometry? _geometry;
    private readonly GeometryType _type;
    
    public CompressedGeometry(IGeometry geometry)
    {
        _geometry = geometry;
        _type = geometry.GeometryType;
    }
    
    /// <summary>
    /// 지오메트리 가져오기 (필요시 압축 해제)
    /// </summary>
    public IGeometry? GetGeometry()
    {
        if (_geometry != null) return _geometry;
        
        if (_compressedData != null)
        {
            _geometry = GeometryCompression.DecompressGeometry(_compressedData, _type);
        }
        
        return _geometry;
    }
    
    /// <summary>
    /// 메모리 절약을 위해 압축
    /// </summary>
    public void Compress()
    {
        if (_geometry != null && _compressedData == null)
        {
            _compressedData = GeometryCompression.CompressGeometry(_geometry);
            _geometry = null; // 원본 지오메트리 해제
        }
    }
    
    /// <summary>
    /// 압축 해제
    /// </summary>
    public void Decompress()
    {
        if (_compressedData != null && _geometry == null)
        {
            _geometry = GeometryCompression.DecompressGeometry(_compressedData, _type);
        }
    }
    
    /// <summary>
    /// 현재 메모리 사용량 추정
    /// </summary>
    public long EstimatedMemoryUsage
    {
        get
        {
            if (_compressedData != null) return _compressedData.Length;
            if (_geometry != null) return _geometry.NumPoints * 24; // 대략적인 추정
            return 0;
        }
    }
}