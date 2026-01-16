using System.Windows;
using System.Windows.Media;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Engine.Rendering.Optimization;

/// <summary>
/// 지오메트리 배칭 렌더러
/// 동일한 스타일의 지오메트리를 하나의 DrawingContext 호출로 배치 렌더링
/// WPF 성능 최적화의 핵심 - Draw 호출 횟수 최소화
/// </summary>
public class GeometryBatcher : IDisposable
{
    // 스타일별 지오메트리 배치
    private readonly Dictionary<StyleKey, GeometryBatch> _polygonBatches = new();
    private readonly Dictionary<StyleKey, GeometryBatch> _lineBatches = new();
    private readonly Dictionary<StyleKey, List<(System.Windows.Point, double)>> _pointBatches = new();

    // Brush/Pen 캐시 (Frozen 상태)
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();
    private readonly Dictionary<PenKey, Pen> _penCache = new();

    // 배치 설정
    private const int MAX_FIGURES_PER_BATCH = 5000;  // 배치당 최대 Figure 수
    private const int MAX_POINTS_PER_BATCH = 10000;  // 포인트 배치당 최대 개수

    private bool _disposed;

    /// <summary>
    /// 폴리곤 추가
    /// </summary>
    public void AddPolygon(
        ICoordinate[] exteriorRing,
        IList<ICoordinate[]>? interiorRings,
        RenderContext context,
        Color fillColor,
        Color strokeColor,
        double strokeWidth,
        double opacity)
    {
        var key = new StyleKey(fillColor, strokeColor, strokeWidth, opacity);

        if (!_polygonBatches.TryGetValue(key, out var batch))
        {
            batch = new GeometryBatch();
            _polygonBatches[key] = batch;
        }

        // 배치가 가득 찼으면 새 배치 시작
        if (batch.FigureCount >= MAX_FIGURES_PER_BATCH)
        {
            batch = new GeometryBatch();
            _polygonBatches[key] = batch;
        }

        // 외곽선 추가
        var exteriorPoints = context.ConvertToScreenPoints(exteriorRing);
        if (exteriorPoints.Length >= 3)
        {
            batch.AddFigure(exteriorPoints, true, true);

            // 내부 링(홀) 추가
            if (interiorRings != null)
            {
                foreach (var hole in interiorRings)
                {
                    if (hole != null && hole.Length >= 3)
                    {
                        var holePoints = context.ConvertToScreenPoints(hole);
                        if (holePoints.Length >= 3)
                        {
                            batch.AddFigure(holePoints, true, true);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 라인 추가
    /// </summary>
    public void AddLine(
        ICoordinate[] coordinates,
        RenderContext context,
        Color strokeColor,
        double strokeWidth,
        double[]? dashPattern = null)
    {
        var key = new StyleKey(Colors.Transparent, strokeColor, strokeWidth, 1.0, dashPattern);

        if (!_lineBatches.TryGetValue(key, out var batch))
        {
            batch = new GeometryBatch();
            _lineBatches[key] = batch;
        }

        if (batch.FigureCount >= MAX_FIGURES_PER_BATCH)
        {
            batch = new GeometryBatch();
            _lineBatches[key] = batch;
        }

        var screenPoints = context.ConvertToScreenPoints(coordinates);
        if (screenPoints.Length >= 2)
        {
            batch.AddFigure(screenPoints, false, false);
        }
    }

    /// <summary>
    /// 포인트 추가
    /// </summary>
    public void AddPoint(
        double x, double y,
        RenderContext context,
        Color fillColor,
        Color strokeColor,
        double size,
        double strokeWidth,
        double opacity)
    {
        var key = new StyleKey(fillColor, strokeColor, strokeWidth, opacity);

        if (!_pointBatches.TryGetValue(key, out var batch))
        {
            batch = new List<(System.Windows.Point, double)>();
            _pointBatches[key] = batch;
        }

        var screenPoint = context.FastMapToScreen(x, y);
        batch.Add((screenPoint, size));
    }

    /// <summary>
    /// 모든 배치를 렌더링
    /// </summary>
    public void Flush(DrawingContext dc)
    {
        // 폴리곤 배치 렌더링
        foreach (var kvp in _polygonBatches)
        {
            var style = kvp.Key;
            var batch = kvp.Value;

            if (batch.FigureCount == 0) continue;

            var geometry = batch.BuildGeometry(FillRule.EvenOdd);
            if (geometry != null)
            {
                var brush = style.FillColor.A > 0 ? GetCachedBrush(ApplyOpacity(style.FillColor, style.Opacity)) : null;
                var pen = style.StrokeWidth > 0 ? GetCachedPen(ApplyOpacity(style.StrokeColor, style.Opacity), style.StrokeWidth, null) : null;

                dc.DrawGeometry(brush, pen, geometry);
            }
        }

        // 라인 배치 렌더링
        foreach (var kvp in _lineBatches)
        {
            var style = kvp.Key;
            var batch = kvp.Value;

            if (batch.FigureCount == 0) continue;

            var geometry = batch.BuildGeometry(FillRule.Nonzero);
            if (geometry != null)
            {
                var pen = GetCachedPen(style.StrokeColor, style.StrokeWidth, style.DashPattern);
                dc.DrawGeometry(null, pen, geometry);
            }
        }

        // 포인트 배치 렌더링 (원으로 표시)
        foreach (var kvp in _pointBatches)
        {
            var style = kvp.Key;
            var points = kvp.Value;

            if (points.Count == 0) continue;

            var brush = GetCachedBrush(ApplyOpacity(style.FillColor, style.Opacity));
            var pen = style.StrokeWidth > 0 ? GetCachedPen(ApplyOpacity(style.StrokeColor, style.Opacity), style.StrokeWidth, null) : null;

            // 포인트가 많으면 GeometryGroup으로 배칭
            if (points.Count > 100)
            {
                var group = new GeometryGroup();
                foreach (var (pt, size) in points)
                {
                    var halfSize = size / 2;
                    group.Children.Add(new EllipseGeometry(pt, halfSize, halfSize));
                }
                group.Freeze();
                dc.DrawGeometry(brush, pen, group);
            }
            else
            {
                // 소수의 포인트는 개별 렌더링이 더 효율적
                foreach (var (pt, size) in points)
                {
                    var halfSize = size / 2;
                    dc.DrawEllipse(brush, pen, pt, halfSize, halfSize);
                }
            }
        }

        Clear();
    }

    /// <summary>
    /// 배치 초기화
    /// </summary>
    public void Clear()
    {
        _polygonBatches.Clear();
        _lineBatches.Clear();
        _pointBatches.Clear();
    }

    /// <summary>
    /// 캐시 초기화 (줌 변경 등에서 호출)
    /// </summary>
    public void ClearCache()
    {
        _brushCache.Clear();
        _penCache.Clear();
    }

    /// <summary>
    /// 투명도 적용
    /// </summary>
    private static Color ApplyOpacity(Color color, double opacity)
    {
        return Color.FromArgb(
            (byte)(color.A * opacity),
            color.R,
            color.G,
            color.B);
    }

    /// <summary>
    /// 캐시된 Brush 가져오기
    /// </summary>
    private SolidColorBrush GetCachedBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[color] = brush;
        }
        return brush;
    }

    /// <summary>
    /// 캐시된 Pen 가져오기
    /// </summary>
    private Pen GetCachedPen(Color color, double thickness, double[]? dashPattern)
    {
        var key = new PenKey(color, thickness, dashPattern);

        if (!_penCache.TryGetValue(key, out var pen))
        {
            pen = new Pen(GetCachedBrush(color), thickness);

            if (dashPattern != null && dashPattern.Length > 0)
            {
                pen.DashStyle = new DashStyle(dashPattern, 0);
            }

            pen.Freeze();
            _penCache[key] = pen;
        }
        return pen;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Clear();
        ClearCache();
    }
}

/// <summary>
/// 스타일 키 (배치 그룹화용)
/// </summary>
internal readonly struct StyleKey : IEquatable<StyleKey>
{
    public readonly Color FillColor;
    public readonly Color StrokeColor;
    public readonly double StrokeWidth;
    public readonly double Opacity;
    public readonly double[]? DashPattern;

    public StyleKey(Color fill, Color stroke, double width, double opacity, double[]? dash = null)
    {
        FillColor = fill;
        StrokeColor = stroke;
        StrokeWidth = width;
        Opacity = opacity;
        DashPattern = dash;
    }

    public bool Equals(StyleKey other)
    {
        return FillColor == other.FillColor &&
               StrokeColor == other.StrokeColor &&
               Math.Abs(StrokeWidth - other.StrokeWidth) < 0.001 &&
               Math.Abs(Opacity - other.Opacity) < 0.001 &&
               DashPatternEquals(DashPattern, other.DashPattern);
    }

    public override bool Equals(object? obj) => obj is StyleKey other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(FillColor, StrokeColor, StrokeWidth, Opacity);
    }

    private static bool DashPatternEquals(double[]? a, double[]? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 0.001) return false;
        }
        return true;
    }
}

/// <summary>
/// Pen 캐시 키
/// </summary>
internal readonly struct PenKey : IEquatable<PenKey>
{
    public readonly Color Color;
    public readonly double Thickness;
    public readonly int DashPatternHash;

    public PenKey(Color color, double thickness, double[]? dashPattern)
    {
        Color = color;
        Thickness = thickness;
        DashPatternHash = dashPattern != null ? string.Join(",", dashPattern).GetHashCode() : 0;
    }

    public bool Equals(PenKey other)
    {
        return Color == other.Color &&
               Math.Abs(Thickness - other.Thickness) < 0.001 &&
               DashPatternHash == other.DashPatternHash;
    }

    public override bool Equals(object? obj) => obj is PenKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Color, Thickness, DashPatternHash);
}

/// <summary>
/// 지오메트리 배치 (여러 Figure를 하나의 PathGeometry로 결합)
/// </summary>
internal class GeometryBatch
{
    private readonly List<(System.Windows.Point[] Points, bool IsClosed, bool IsFilled)> _figures = new();

    public int FigureCount => _figures.Count;

    public void AddFigure(System.Windows.Point[] points, bool isClosed, bool isFilled)
    {
        if (points.Length >= 2)
        {
            _figures.Add((points, isClosed, isFilled));
        }
    }

    public StreamGeometry? BuildGeometry(FillRule fillRule)
    {
        if (_figures.Count == 0) return null;

        var geometry = new StreamGeometry();
        geometry.FillRule = fillRule;

        using (var ctx = geometry.Open())
        {
            foreach (var (points, isClosed, isFilled) in _figures)
            {
                ctx.BeginFigure(points[0], isFilled, isClosed);

                // PolyLineTo는 여러 점을 한 번에 추가 (LineTo 반복보다 효율적)
                if (points.Length > 1)
                {
                    ctx.PolyLineTo(points.AsSpan(1).ToArray(), true, false);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    public void Clear()
    {
        _figures.Clear();
    }
}
