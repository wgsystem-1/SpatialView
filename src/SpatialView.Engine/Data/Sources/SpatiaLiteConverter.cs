using SpatialView.Engine.Geometry;
using SpatialView.Engine.Geometry.IO;
using System.Data;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// SpatiaLite와 Engine 지오메트리 간 변환 유틸리티
/// </summary>
public static class SpatiaLiteConverter
{
    /// <summary>
    /// SpatiaLite blob 데이터를 Engine 지오메트리로 변환
    /// </summary>
    /// <param name="spatialiteBlob">SpatiaLite BLOB 데이터</param>
    /// <returns>Engine 지오메트리</returns>
    public static IGeometry? ConvertFromSpatiaLite(byte[]? spatialiteBlob)
    {
        if (spatialiteBlob == null || spatialiteBlob.Length == 0)
            return null;

        try
        {
            // SpatiaLite BLOB 형식 파싱
            using var stream = new MemoryStream(spatialiteBlob);
            using var reader = new BinaryReader(stream);

            // SpatiaLite BLOB 헤더 확인 (0x00, 0x01)
            var start = reader.ReadByte();
            if (start != 0x00)
            {
                // 표준 WKB일 수도 있음
                return WkbParser.Parse(spatialiteBlob);
            }

            var endianness = reader.ReadByte();
            bool littleEndian = endianness == 0x01;

            // SRID 읽기 (4 bytes)
            var srid = ReadInt32(reader, littleEndian);

            // MBR (Minimum Bounding Rectangle) 건너뛰기 (32 bytes)
            // MinX, MinY, MaxX, MaxY (각각 8 bytes double)
            reader.ReadBytes(32);

            // MBR End marker (0x7C)
            var mbrEnd = reader.ReadByte();
            if (mbrEnd != 0x7C)
                throw new InvalidDataException("Invalid SpatiaLite BLOB format: MBR end marker not found");

            // 남은 데이터는 표준 WKB
            var wkbData = reader.ReadBytes((int)(stream.Length - stream.Position));
            
            var geometry = WkbParser.Parse(wkbData);
            
            // SRID 설정 (Engine에서 SRID 지원하는 경우)
            // TODO: Engine 지오메트리에 SRID 속성 추가 시 여기서 설정
            
            return geometry;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpatiaLite BLOB 변환 실패: {ex.Message}");
            
            // 대체 시도: 직접 WKB로 파싱
            try
            {
                return WkbParser.Parse(spatialiteBlob);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Engine 지오메트리를 SpatiaLite blob으로 변환
    /// </summary>
    /// <param name="geometry">Engine 지오메트리</param>
    /// <param name="srid">좌표계 SRID (기본값: 4326)</param>
    /// <returns>SpatiaLite BLOB 데이터</returns>
    public static byte[] ConvertToSpatiaLite(IGeometry geometry, int srid = 4326)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        try
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // SpatiaLite BLOB 헤더
            writer.Write((byte)0x00); // Start marker
            writer.Write((byte)0x01); // Little Endian

            // SRID (4 bytes, little endian)
            writer.Write(srid);

            // MBR (Minimum Bounding Rectangle) 계산 및 작성
            var envelope = geometry.GetBounds();
            writer.Write(envelope.MinX); // MinX
            writer.Write(envelope.MinY); // MinY
            writer.Write(envelope.MaxX); // MaxX
            writer.Write(envelope.MaxY); // MaxY

            // MBR End marker
            writer.Write((byte)0x7C);

            // WKB 데이터
            var wkbData = WkbParser.Write(geometry, littleEndian: true);
            writer.Write(wkbData);

            return stream.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpatiaLite BLOB 생성 실패: {ex.Message}");
            
            // 대체: 표준 WKB 반환
            return WkbParser.Write(geometry);
        }
    }

    /// <summary>
    /// DataReader에서 SpatiaLite 지오메트리 컬럼을 읽어 Engine 지오메트리로 변환
    /// </summary>
    /// <param name="reader">데이터 리더</param>
    /// <param name="columnName">지오메트리 컬럼명</param>
    /// <returns>Engine 지오메트리</returns>
    public static IGeometry? ReadGeometryFromDataReader(IDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var value = reader.GetValue(ordinal);
            
            return value switch
            {
                byte[] blob => ConvertFromSpatiaLite(blob),
                string wkt => WktParser.Parse(wkt),
                _ => null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DataReader에서 지오메트리 읽기 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Engine 지오메트리를 SpatiaLite SQL 함수 호출로 변환
    /// </summary>
    /// <param name="geometry">Engine 지오메트리</param>
    /// <param name="srid">좌표계 SRID</param>
    /// <returns>SpatiaLite SQL 문</returns>
    public static string ToSpatiaLiteSql(IGeometry geometry, int srid = 4326)
    {
        if (geometry == null)
            return "NULL";

        var wkt = WktParser.Write(geometry);
        return $"GeomFromText('{wkt}', {srid})";
    }

    /// <summary>
    /// 바운딩 박스를 SpatiaLite MbrIntersects 조건으로 변환
    /// </summary>
    /// <param name="envelope">바운딩 박스</param>
    /// <param name="geometryColumn">지오메트리 컬럼명</param>
    /// <param name="srid">좌표계 SRID</param>
    /// <returns>SQL WHERE 조건</returns>
    public static string ToMbrIntersectsCondition(Geometry.Envelope envelope, string geometryColumn, int srid = 4326)
    {
        if (envelope.IsNull)
            return "1=1"; // 필터링 없음

        return $"MbrIntersects({geometryColumn}, BuildMbr({envelope.MinX:G17}, {envelope.MinY:G17}, {envelope.MaxX:G17}, {envelope.MaxY:G17}, {srid}))";
    }

    /// <summary>
    /// WKT 문자열에서 지오메트리 타입 추출
    /// </summary>
    /// <param name="wkt">WKT 문자열</param>
    /// <returns>지오메트리 타입명</returns>
    public static string GetGeometryTypeFromWkt(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            return "UNKNOWN";

        var upperWkt = wkt.Trim().ToUpperInvariant();
        var spaceIndex = upperWkt.IndexOf(' ');
        var parenIndex = upperWkt.IndexOf('(');
        
        var endIndex = spaceIndex > 0 && parenIndex > 0 
            ? Math.Min(spaceIndex, parenIndex)
            : (spaceIndex > 0 ? spaceIndex : (parenIndex > 0 ? parenIndex : upperWkt.Length));
            
        return upperWkt.Substring(0, endIndex);
    }

    /// <summary>
    /// 지오메트리 타입에 따른 SpatiaLite 테이블 생성 SQL
    /// </summary>
    /// <param name="tableName">테이블명</param>
    /// <param name="geometryColumn">지오메트리 컬럼명</param>
    /// <param name="geometryType">지오메트리 타입</param>
    /// <param name="srid">좌표계 SRID</param>
    /// <param name="dimensions">차원 (2D=2, 3D=3)</param>
    /// <returns>CREATE TABLE SQL</returns>
    public static string CreateGeometryTableSql(string tableName, string geometryColumn, 
        string geometryType, int srid = 4326, int dimensions = 2)
    {
        var sql = $@"
CREATE TABLE {tableName} (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    {geometryColumn} GEOMETRY
);

SELECT AddGeometryColumn('{tableName}', '{geometryColumn}', {srid}, '{geometryType}', '{(dimensions == 3 ? "XYZ" : "XY")}');
SELECT CreateSpatialIndex('{tableName}', '{geometryColumn}');";

        return sql;
    }

    #region Helper Methods

    private static int ReadInt32(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(4);
        if (littleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static double ReadDouble(BinaryReader reader, bool littleEndian)
    {
        var bytes = reader.ReadBytes(8);
        if (littleEndian != BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    #endregion
}