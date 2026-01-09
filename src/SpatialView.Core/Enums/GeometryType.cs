namespace SpatialView.Core.Enums;

/// <summary>
/// 지오메트리 타입
/// </summary>
public enum GeometryType
{
    Unknown = 0,
    
    // 2D 타입
    Point,
    LineString,
    Polygon,
    MultiPoint,
    MultiLineString,
    MultiPolygon,
    GeometryCollection,
    
    // Z (3D) 타입
    PointZ,
    LineStringZ,
    PolygonZ,
    MultiPointZ,
    MultiLineStringZ,
    MultiPolygonZ,
    
    // M (Measure) 타입
    PointM,
    LineStringM,
    PolygonM,
    MultiPointM,
    MultiLineStringM,
    MultiPolygonM,
    
    // ZM (3D + Measure) 타입
    PointZM,
    LineStringZM,
    PolygonZM,
    MultiPointZM,
    MultiLineStringZM,
    MultiPolygonZM,
    
    // 기타
    Line,        // LineString 별칭
    LinearRing,  // 폐곡선 LineString (폴리곤의 경계)
    
    // 비공간 (테이블)
    None
}

