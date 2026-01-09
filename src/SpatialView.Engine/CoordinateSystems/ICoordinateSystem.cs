namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 좌표계 인터페이스
/// </summary>
public interface ICoordinateSystem
{
    /// <summary>
    /// 좌표계 이름
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 좌표계 권한자 (예: "EPSG")
    /// </summary>
    string Authority { get; }
    
    /// <summary>
    /// 좌표계 권한 코드 (예: 4326)
    /// </summary>
    int AuthorityCode { get; }
    
    /// <summary>
    /// WKT (Well-Known Text) 표현
    /// </summary>
    string WKT { get; }
    
    /// <summary>
    /// 좌표계의 차원 수
    /// </summary>
    int Dimension { get; }
}

/// <summary>
/// 지리 좌표계 인터페이스
/// </summary>
public interface IGeographicCoordinateSystem : ICoordinateSystem
{
    /// <summary>
    /// 각도 단위
    /// </summary>
    IUnit AngularUnit { get; }
    
    /// <summary>
    /// 타원체
    /// </summary>
    IEllipsoid Ellipsoid { get; }
    
    /// <summary>
    /// 원점 자오선
    /// </summary>
    IPrimeMeridian PrimeMeridian { get; }
}

/// <summary>
/// 투영 좌표계 인터페이스
/// </summary>
public interface IProjectedCoordinateSystem : ICoordinateSystem
{
    /// <summary>
    /// 선형 단위
    /// </summary>
    IUnit LinearUnit { get; }
    
    /// <summary>
    /// 기반 지리 좌표계
    /// </summary>
    IGeographicCoordinateSystem BaseGeographicCoordinateSystem { get; }
    
    /// <summary>
    /// 투영 방법
    /// </summary>
    IProjection Projection { get; }
}