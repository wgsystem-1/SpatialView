namespace SpatialView.Engine.Geometry;

/// <summary>
/// 기본 지오메트리 인터페이스
/// </summary>
public interface IGeometry
{
    /// <summary>
    /// 지오메트리 타입
    /// </summary>
    GeometryType GeometryType { get; }
    
    /// <summary>
    /// 좌표계 ID (SRID)
    /// </summary>
    int SRID { get; set; }
    
    /// <summary>
    /// 지오메트리가 비어있는지 확인
    /// </summary>
    bool IsEmpty { get; }
    
    /// <summary>
    /// 지오메트리가 유효한지 확인
    /// </summary>
    bool IsValid { get; }
    
    /// <summary>
    /// 좌표 개수
    /// </summary>
    int NumPoints { get; }
    
    /// <summary>
    /// 최소 경계 사각형 (MBR)
    /// </summary>
    Envelope Envelope { get; }
    
    /// <summary>
    /// 지오메트리 복사
    /// </summary>
    IGeometry Copy();
    
    /// <summary>
    /// WKT (Well-Known Text) 형식으로 변환
    /// </summary>
    string ToText();
    
    /// <summary>
    /// 좌표 배열 가져오기
    /// </summary>
    ICoordinate[] Coordinates { get; }
    
    /// <summary>
    /// 다른 지오메트리와의 거리
    /// </summary>
    double Distance(IGeometry other);
    
    /// <summary>
    /// 면적 (폴리곤의 경우)
    /// </summary>
    double Area { get; }
    
    /// <summary>
    /// 길이 (라인의 경우)
    /// </summary>
    double Length { get; }
    
    /// <summary>
    /// 중심점
    /// </summary>
    ICoordinate Centroid { get; }
    
    /// <summary>
    /// 지오메트리의 차원 (0=점, 1=선, 2=면)
    /// </summary>
    int Dimension { get; }
    
    /// <summary>
    /// 경계 상자 가져오기 (Envelope와 같음)
    /// </summary>
    Envelope GetBounds();
    
    // 공간 관계 연산
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리를 포함하는지 확인
    /// </summary>
    bool Contains(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리와 교차하는지 확인
    /// </summary>
    bool Intersects(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리 내부에 있는지 확인
    /// </summary>
    bool Within(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리와 겹치는지 확인
    /// </summary>
    bool Overlaps(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리와 교차하는지 확인 (차원이 다른 경우)
    /// </summary>
    bool Crosses(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리와 접촉하는지 확인
    /// </summary>
    bool Touches(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리가 다른 지오메트리와 분리되어 있는지 확인
    /// </summary>
    bool Disjoint(IGeometry geometry);
    
    // 공간 연산
    /// <summary>
    /// 두 지오메트리의 합집합
    /// </summary>
    IGeometry Union(IGeometry geometry);
    
    /// <summary>
    /// 두 지오메트리의 교집합
    /// </summary>
    IGeometry Intersection(IGeometry geometry);
    
    /// <summary>
    /// 이 지오메트리에서 다른 지오메트리를 뺀 차집합
    /// </summary>
    IGeometry Difference(IGeometry geometry);
    
    /// <summary>
    /// 두 지오메트리의 대칭 차집합
    /// </summary>
    IGeometry SymmetricDifference(IGeometry geometry);
    
    /// <summary>
    /// 지정된 거리만큼 버퍼 생성
    /// </summary>
    IGeometry Buffer(double distance);
    
    // 유틸리티
    /// <summary>
    /// 지오메트리 복제 (깊은 복사)
    /// </summary>
    IGeometry Clone();
    
    /// <summary>
    /// 좌표 변환 적용
    /// </summary>
    /// <param name="transformation">좌표 변환</param>
    /// <returns>변환된 지오메트리</returns>
    IGeometry Transform(object transformation);
}

/// <summary>
/// 지오메트리 타입 열거형
/// </summary>
public enum GeometryType
{
    Point,
    LineString,
    LinearRing,
    Polygon,
    MultiPoint,
    MultiLineString,
    MultiPolygon,
    GeometryCollection
}