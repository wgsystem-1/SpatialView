using System.Drawing;

namespace SpatialView.Engine.Styling;

/// <summary>
/// 점 심볼라이저 인터페이스
/// </summary>
public interface IPointSymbolizer : IStyle
{
    /// <summary>
    /// 심볼 타입
    /// </summary>
    PointSymbolType SymbolType { get; set; }
    
    /// <summary>
    /// 크기
    /// </summary>
    double Size { get; set; }
    
    /// <summary>
    /// 색상
    /// </summary>
    Color? Color { get; set; }
    
    /// <summary>
    /// 외곽선 색상
    /// </summary>
    Color? OutlineColor { get; set; }
    
    /// <summary>
    /// 외곽선 두께
    /// </summary>
    float OutlineWidth { get; set; }
}

/// <summary>
/// 선 심볼라이저 인터페이스
/// </summary>
public interface ILineSymbolizer : IStyle
{
    /// <summary>
    /// 선 색상
    /// </summary>
    Color? Color { get; set; }
    
    /// <summary>
    /// 선 두께
    /// </summary>
    float Width { get; set; }
    
    /// <summary>
    /// 선 스타일
    /// </summary>
    LineStyle Style { get; set; }
}

/// <summary>
/// 폴리곤 심볼라이저 인터페이스
/// </summary>
public interface IPolygonSymbolizer : IStyle
{
    /// <summary>
    /// 채우기 색상
    /// </summary>
    Color? FillColor { get; set; }
    
    /// <summary>
    /// 외곽선 색상
    /// </summary>
    Color? OutlineColor { get; set; }
    
    /// <summary>
    /// 외곽선 두께
    /// </summary>
    float OutlineWidth { get; set; }
    
    /// <summary>
    /// 외곽선 표시 여부
    /// </summary>
    bool EnableOutline { get; set; }
}

/// <summary>
/// 점 심볼 타입
/// </summary>
public enum PointSymbolType
{
    Circle,
    Square,
    Triangle,
    Diamond,
    Star,
    Cross
}

