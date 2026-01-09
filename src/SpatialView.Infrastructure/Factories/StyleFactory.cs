using System.Windows.Media;
using SpatialView.Core.Factories;
using SpatialView.Core.Styling;
using SpatialView.Infrastructure.GisEngine;

namespace SpatialView.Infrastructure.Factories;

/// <summary>
/// SpatialView 독립 스타일 생성 팩토리 구현 (SharpMap 제거됨)
/// </summary>
public class StyleFactory : IStyleFactory
{
    public IVectorStyle CreateVectorStyle()
    {
        // SpatialView 독립 기본 스타일
        return new SpatialViewVectorStyle();
    }
    
    public IVectorStyle CreateVectorStyle(Color fillColor, Color outlineColor)
    {
        return new SpatialViewVectorStyle
        {
            Fill = fillColor,
            Outline = outlineColor,
            EnableOutline = true
        };
    }
    
    public IVectorStyle CreateHighlightStyle()
    {
        return new SpatialViewVectorStyle
        {
            Fill = Color.FromArgb(128, 255, 255, 0), // Semi-transparent yellow
            Outline = Color.FromArgb(255, 255, 215, 0), // Gold outline
            OutlineWidth = 3,
            EnableOutline = true
        };
    }
    
    public IVectorStyle CreateSelectionStyle()
    {
        return new SpatialViewVectorStyle
        {
            Fill = Color.FromArgb(100, 0, 162, 232), // Semi-transparent blue
            Outline = Color.FromArgb(255, 0, 122, 204), // Blue outline
            OutlineWidth = 2,
            EnableOutline = true
        };
    }
    
    public IPointSymbol CreatePointSymbol(PointSymbolType type, float size)
    {
        // Since SharpMap doesn't directly support our point symbol interface,
        // we'll create a basic implementation
        return new BasicPointSymbol
        {
            SymbolType = type,
            Size = size,
            Rotation = 0,
            Offset = new System.Windows.Point(0, 0)
        };
    }
    
    /// <summary>
    /// Basic point symbol implementation
    /// </summary>
    private class BasicPointSymbol : IPointSymbol
    {
        public PointSymbolType SymbolType { get; set; }
        public float Size { get; set; }
        public float Rotation { get; set; }
        public System.Windows.Point Offset { get; set; }
    }
}