namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 투영법 인터페이스
/// </summary>
public interface IProjection
{
    /// <summary>
    /// 투영법 이름 (예: "Transverse_Mercator")
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
    /// 투영 파라미터 목록
    /// </summary>
    IDictionary<string, double> Parameters { get; }
    
    /// <summary>
    /// 투영 클래스 (예: "Transverse Mercator")
    /// </summary>
    string ProjectionClass { get; }
}