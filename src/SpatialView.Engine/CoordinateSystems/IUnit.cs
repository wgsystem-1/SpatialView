namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 단위 인터페이스
/// </summary>
public interface IUnit
{
    /// <summary>
    /// 단위 이름 (예: "meter", "degree")
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
}

/// <summary>
/// 선형 단위 (길이)
/// </summary>
public interface ILinearUnit : IUnit
{
    /// <summary>
    /// 미터 단위로의 변환 계수
    /// </summary>
    double MetersPerUnit { get; }
}

/// <summary>
/// 각도 단위
/// </summary>
public interface IAngularUnit : IUnit
{
    /// <summary>
    /// 라디안 단위로의 변환 계수
    /// </summary>
    double RadiansPerUnit { get; }
}