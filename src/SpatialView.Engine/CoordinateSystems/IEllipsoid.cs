namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 타원체 인터페이스 (지구 모델)
/// </summary>
public interface IEllipsoid
{
    /// <summary>
    /// 타원체 이름 (예: "WGS 84")
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 권한자
    /// </summary>
    string Authority { get; }
    
    /// <summary>
    /// 권한 코드
    /// </summary>
    int AuthorityCode { get; }
    
    /// <summary>
    /// 장반경 (semi-major axis) - 미터 단위
    /// </summary>
    double SemiMajorAxis { get; }
    
    /// <summary>
    /// 단반경 (semi-minor axis) - 미터 단위
    /// </summary>
    double SemiMinorAxis { get; }
    
    /// <summary>
    /// 편평률 (flattening)
    /// </summary>
    double Flattening { get; }
    
    /// <summary>
    /// 역편평률 (inverse flattening)
    /// </summary>
    double InverseFlattening { get; }
    
    /// <summary>
    /// 이심률 (eccentricity)
    /// </summary>
    double Eccentricity { get; }
    
    /// <summary>
    /// 이심률 제곱 (eccentricity squared)
    /// </summary>
    double EccentricitySquared { get; }
}