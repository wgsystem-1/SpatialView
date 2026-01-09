namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 기준 자오선 인터페이스
/// </summary>
public interface IPrimeMeridian
{
    /// <summary>
    /// 자오선 이름 (예: "Greenwich")
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
    /// 그리니치로부터의 각도 (도 단위)
    /// </summary>
    double Longitude { get; }
}