using SpatialView.Engine.Data;

namespace SpatialView.Engine.Styling;

/// <summary>
/// 스타일 규칙 인터페이스
/// </summary>
public interface IStyleRule
{
    /// <summary>
    /// 규칙 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 규칙 설명
    /// </summary>
    string? Description { get; set; }
    
    /// <summary>
    /// 최소 표시 배율
    /// </summary>
    double MinZoom { get; set; }
    
    /// <summary>
    /// 최대 표시 배율
    /// </summary>
    double MaxZoom { get; set; }
    
    /// <summary>
    /// 피처에 규칙이 적용되는지 확인
    /// </summary>
    /// <param name="feature">피처</param>
    /// <returns>적용 여부</returns>
    bool Matches(IFeature feature);
    
    /// <summary>
    /// 피처에 대한 스타일 가져오기
    /// </summary>
    /// <param name="feature">피처</param>
    /// <returns>스타일</returns>
    IStyle? GetStyle(IFeature feature);
}