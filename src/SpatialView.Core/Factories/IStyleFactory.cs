using System.Windows.Media;
using SpatialView.Core.Styling;

namespace SpatialView.Core.Factories;

/// <summary>
/// 스타일 생성 팩토리 인터페이스
/// </summary>
public interface IStyleFactory
{
    /// <summary>
    /// 기본 벡터 스타일 생성
    /// </summary>
    IVectorStyle CreateVectorStyle();
    
    /// <summary>
    /// 색상을 지정한 벡터 스타일 생성
    /// </summary>
    IVectorStyle CreateVectorStyle(System.Windows.Media.Color fillColor, System.Windows.Media.Color outlineColor);
    
    /// <summary>
    /// 하이라이트 스타일 생성
    /// </summary>
    IVectorStyle CreateHighlightStyle();
    
    /// <summary>
    /// 선택 스타일 생성
    /// </summary>
    IVectorStyle CreateSelectionStyle();
    
    /// <summary>
    /// 포인트 심볼 생성
    /// </summary>
    IPointSymbol CreatePointSymbol(PointSymbolType type, float size);
}