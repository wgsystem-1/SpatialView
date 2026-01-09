using System.Windows.Media;

namespace SpatialView.Engine.Styling;

/// <summary>
/// 기본 스타일 정의
/// </summary>
public static class DefaultStyles
{
    /// <summary>
    /// 기본 포인트 스타일
    /// </summary>
    public static IPointStyle DefaultPoint => new PointStyle
    {
        Name = "Default Point",
        Fill = Colors.Blue,
        Size = 6,
        Stroke = Colors.Black,
        StrokeWidth = 1,
        Shape = PointShape.Circle,
        IsVisible = true,
        MinZoom = 0,
        MaxZoom = double.MaxValue
    };
    
    /// <summary>
    /// 기본 라인 스타일
    /// </summary>
    public static ILineStyle DefaultLine => new DefaultLineStyle
    {
        Name = "Default Line",
        Stroke = Colors.Blue,
        StrokeWidth = 2,
        LineStyle = Styling.LineStyle.Solid,
        LineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
        IsVisible = true,
        MinZoom = 0,
        MaxZoom = double.MaxValue
    };
    
    /// <summary>
    /// 기본 폴리곤 스타일
    /// </summary>
    public static IPolygonStyle DefaultPolygon => new PolygonStyle
    {
        Name = "Default Polygon",
        Fill = Color.FromArgb(128, 0, 0, 255), // 반투명 파랑
        Opacity = 0.5,
        Stroke = Colors.Blue,
        StrokeWidth = 1,
        StrokeStyle = Styling.LineStyle.Solid,
        IsVisible = true,
        MinZoom = 0,
        MaxZoom = double.MaxValue
    };
}

/// <summary>
/// 포인트 스타일 구현
/// </summary>
public class PointStyle : IPointStyle
{
    public string Name { get; set; } = "Point Style";
    public bool IsVisible { get; set; } = true;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public Color Fill { get; set; } = Colors.Blue;
    public double Size { get; set; } = 6;
    public Color Stroke { get; set; } = Colors.Black;
    public double StrokeWidth { get; set; } = 1;
    public PointShape Shape { get; set; } = PointShape.Circle;
}

/// <summary>
/// 라인 스타일 구현
/// </summary>
public class DefaultLineStyle : ILineStyle
{
    public string Name { get; set; } = "Line Style";
    public bool IsVisible { get; set; } = true;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public Color Stroke { get; set; } = Colors.Blue;
    public double StrokeWidth { get; set; } = 2;
    public Styling.LineStyle LineStyle { get; set; } = Styling.LineStyle.Solid;
    public double[]? DashArray { get; set; }
    public PenLineCap LineCap { get; set; } = PenLineCap.Round;
    public PenLineJoin LineJoin { get; set; } = PenLineJoin.Round;
}

/// <summary>
/// 폴리곤 스타일 구현
/// </summary>
public class PolygonStyle : IPolygonStyle
{
    public string Name { get; set; } = "Polygon Style";
    public bool IsVisible { get; set; } = true;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public Color Fill { get; set; } = Color.FromArgb(128, 0, 0, 255);
    public double Opacity { get; set; } = 0.5;
    public Color Stroke { get; set; } = Colors.Blue;
    public double StrokeWidth { get; set; } = 1;
    public Styling.LineStyle StrokeStyle { get; set; } = Styling.LineStyle.Solid;
    public double[]? DashArray { get; set; }
}