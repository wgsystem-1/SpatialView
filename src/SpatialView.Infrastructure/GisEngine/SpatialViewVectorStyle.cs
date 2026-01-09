using System.Windows.Media;
using SpatialView.Core.Styling;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// SpatialView 독립 벡터 스타일 구현 (SharpMap 제거됨)
/// </summary>
public class SpatialViewVectorStyle : IVectorStyle
{
    public Color Fill { get; set; } = Colors.Blue;
    public Color Outline { get; set; } = Colors.Black;
    public float OutlineWidth { get; set; } = 1.0f;
    public float PointSize { get; set; } = 10.0f;
    public bool EnableOutline { get; set; } = true;
    public float LineWidth { get; set; } = 1.0f;
    public float Opacity { get; set; } = 1.0f;
    public IPointSymbol? PointSymbol { get; set; }
    public float[]? LineDashPattern { get; set; }
    
    // 추가 속성들 (Core에 없지만 유용한 속성들)
    public Color LineColor { get; set; } = Colors.Black;
    public bool EnableFill { get; set; } = true;
    public PointSymbolType PointSymbolType { get; set; } = PointSymbolType.Circle;
    
    // 텍스트 스타일 (향후 구현)
    public Color TextColor { get; set; } = Colors.Black;
    public string FontFamily { get; set; } = "Arial";
    public double FontSize { get; set; } = 12.0;
    public bool IsBold { get; set; } = false;
    public bool IsItalic { get; set; } = false;
    
    /// <summary>
    /// 스타일 복사
    /// </summary>
    public IVectorStyle Clone()
    {
        return new SpatialViewVectorStyle
        {
            Fill = this.Fill,
            Outline = this.Outline,
            OutlineWidth = this.OutlineWidth,
            PointSize = this.PointSize,
            EnableOutline = this.EnableOutline,
            LineWidth = this.LineWidth,
            Opacity = this.Opacity,
            PointSymbol = this.PointSymbol,
            LineDashPattern = this.LineDashPattern?.ToArray(), // 배열 복사
            
            // 추가 속성들
            LineColor = this.LineColor,
            EnableFill = this.EnableFill,
            PointSymbolType = this.PointSymbolType,
            TextColor = this.TextColor,
            FontFamily = this.FontFamily,
            FontSize = this.FontSize,
            IsBold = this.IsBold,
            IsItalic = this.IsItalic
        };
    }
}