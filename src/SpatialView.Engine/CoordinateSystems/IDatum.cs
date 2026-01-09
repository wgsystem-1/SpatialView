namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 측지 기준계 인터페이스
/// </summary>
public interface IDatum
{
    /// <summary>
    /// 기준계 이름
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 권한 기관
    /// </summary>
    string Authority { get; }
    
    /// <summary>
    /// 권한 코드
    /// </summary>
    int AuthorityCode { get; }
    
    /// <summary>
    /// 타원체
    /// </summary>
    IEllipsoid Ellipsoid { get; }
    
    /// <summary>
    /// 본초 자오선
    /// </summary>
    IPrimeMeridian PrimeMeridian { get; }
    
    /// <summary>
    /// 기준계 타입
    /// </summary>
    DatumType Type { get; }
    
    /// <summary>
    /// WGS84로의 변환 파라미터 (7-parameter transformation)
    /// [dx, dy, dz, rx, ry, rz, scale]
    /// </summary>
    double[] ToWGS84 { get; }
}

/// <summary>
/// 기준계 타입
/// </summary>
public enum DatumType
{
    Geodetic,
    Vertical,
    Engineering,
    Temporal,
    HD_Horizontal  // 수평 기준계
}