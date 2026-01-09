using System.Windows.Media;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// 병렬 렌더링을 위한 렌더링 아이템
/// </summary>
public class RenderItem
{
    /// <summary>
    /// 화면 좌표로 변환된 지오메트리
    /// </summary>
    public System.Windows.Media.Geometry? Geometry { get; set; }
    
    /// <summary>
    /// 적용할 스타일
    /// </summary>
    public object? Style { get; set; }
    
    /// <summary>
    /// 원본 피처 (필요한 경우 참조)
    /// </summary>
    public Data.IFeature? Feature { get; set; }
    
    /// <summary>
    /// 렌더링 우선순위 (낮을수록 먼저 렌더링)
    /// </summary>
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// Z-Index (높을수록 위에 렌더링)
    /// </summary>
    public int ZIndex { get; set; } = 0;
}