using System.Windows.Media;

namespace SpatialView.Engine.Styling;

/// <summary>
/// 기본 스타일 인터페이스
/// </summary>
public interface IStyle
{
    /// <summary>
    /// 스타일 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 스타일이 사용 가능한지 확인
    /// </summary>
    bool IsVisible { get; set; }
    
    /// <summary>
    /// 최소 표시 배율
    /// </summary>
    double MinZoom { get; set; }
    
    /// <summary>
    /// 최대 표시 배율
    /// </summary>
    double MaxZoom { get; set; }
}

/// <summary>
/// 포인트 스타일
/// </summary>
public interface IPointStyle : IStyle
{
    /// <summary>
    /// 색상
    /// </summary>
    Color Fill { get; set; }
    
    /// <summary>
    /// 크기 (픽셀)
    /// </summary>
    double Size { get; set; }
    
    /// <summary>
    /// 두께선 색상
    /// </summary>
    Color Stroke { get; set; }
    
    /// <summary>
    /// 두께선 두께
    /// </summary>
    double StrokeWidth { get; set; }
    
    /// <summary>
    /// 모양 (Circle, Square, Triangle 등)
    /// </summary>
    PointShape Shape { get; set; }
}

/// <summary>
/// 라인 스타일
/// </summary>
public interface ILineStyle : IStyle
{
    /// <summary>
    /// 선 색상
    /// </summary>
    Color Stroke { get; set; }
    
    /// <summary>
    /// 선 두께
    /// </summary>
    double StrokeWidth { get; set; }
    
    /// <summary>
    /// 선 스타일 (Solid, Dash, Dot 등)
    /// </summary>
    LineStyle LineStyle { get; set; }
    
    /// <summary>
    /// 대시 패턴
    /// </summary>
    double[]? DashArray { get; set; }
    
    /// <summary>
    /// 선 끝 모양
    /// </summary>
    PenLineCap LineCap { get; set; }
    
    /// <summary>
    /// 선 연결 모양
    /// </summary>
    PenLineJoin LineJoin { get; set; }
}

/// <summary>
/// 폴리곤 스타일
/// </summary>
public interface IPolygonStyle : IStyle
{
    /// <summary>
    /// 내부 색상
    /// </summary>
    Color Fill { get; set; }
    
    /// <summary>
    /// 투명도 (0.0 ~ 1.0)
    /// </summary>
    double Opacity { get; set; }
    
    /// <summary>
    /// 두께선 색상
    /// </summary>
    Color Stroke { get; set; }
    
    /// <summary>
    /// 두께선 두께
    /// </summary>
    double StrokeWidth { get; set; }
    
    /// <summary>
    /// 두께선 스타일
    /// </summary>
    LineStyle StrokeStyle { get; set; }
    
    /// <summary>
    /// 대시 패턴
    /// </summary>
    double[]? DashArray { get; set; }
}

/// <summary>
/// 포인트 모양
/// </summary>
public enum PointShape
{
    Circle,
    Square,
    Triangle,
    Diamond,
    Cross,
    X
}

/// <summary>
/// 선 스타일
/// </summary>
public enum LineStyle
{
    Solid,
    Dash,
    Dot,
    DashDot,
    DashDotDot
}