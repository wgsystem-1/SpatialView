using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// WPF 벡터 렌더러 구현
/// DrawingContext를 사용하여 지오메트리를 화면에 그립니다
/// </summary>
public class VectorRenderer : IVectorRenderer
{
    private Styling.Rules.StyleEngine? _styleEngine;
    
    /// <summary>
    /// 스타일 엔진
    /// </summary>
    public Styling.Rules.StyleEngine? StyleEngine 
    { 
        get => _styleEngine;
        set => _styleEngine = value;
    }
    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpatialView_render.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    /// <inheritdoc/>
    public void RenderFeatures(IEnumerable<Data.IFeature> features, RenderContext context)
    {
        if (features == null || context?.DrawingContext == null)
        {
            Log($"VectorRenderer.RenderFeatures: features={features != null}, context={context != null}, dc={context?.DrawingContext != null}");
            return;
        }

        var featureList = features.ToList();
        Log($"VectorRenderer.RenderFeatures: 입력 피처 수={featureList.Count}, ViewExtent={context.ViewExtent}, Zoom={context.Zoom}");

        // 현재 줌 레벨에 따른 LOD 계산
        var lodLevel = LevelOfDetail.CalculateLODLevel(context.Zoom);
        Log($"VectorRenderer.RenderFeatures: LOD 레벨={lodLevel}");
        
        // 뷰포트 컬링을 통해 보이는 피처만 필터링
        var visibleFeatures = featureList.Where(f => 
            f.Geometry == null || 
            f.Geometry.Envelope == null || 
            f.Geometry.Envelope.IsNull ||
            ViewportCulling.IsGeometryVisible(f.Geometry, context.ViewExtent)).ToList();
        
        Log($"VectorRenderer.RenderFeatures: 컬링 후 피처 수={visibleFeatures.Count} (원본={featureList.Count})");

        if (!visibleFeatures.Any())
        {
            Log($"VectorRenderer.RenderFeatures: 보이는 피처 없음!");
            return;
        }

        // 안정성을 위해 모든 피처를 순차 렌더링 (병렬 렌더링 일시 중단)
        Log($"VectorRenderer.RenderFeatures: 순차 렌더링 시작");
        int renderedCount = 0;
        foreach (var feature in visibleFeatures)
        {
            try
            {
                if (feature.Geometry == null) continue;

                // LOD에 따른 지오메트리 렌더링 여부 확인
                if (LevelOfDetail.ShouldRenderGeometry(feature.Geometry, context, (LevelOfDetail.LODLevel)lodLevel))
                {
                    RenderFeature(feature, context, (LevelOfDetail.LODLevel)lodLevel);
                    renderedCount++;
                }
            }
            catch (Exception ex)
            {
                // 개별 피처 렌더링 오류는 로그만 남기고 계속 진행
                if (renderedCount < 1) // 처음 발생한 에러만 로그
                    Log($"Feature rendering error: {ex.Message}");
            }
        }
        Log($"VectorRenderer.RenderFeatures: 순차 렌더링 완료 (그려진 피처: {renderedCount})");
    }

    /// <summary>
    /// 대용량 피처 병렬 렌더링
    /// </summary>
    private void RenderFeaturesParallel(IList<Data.IFeature> features, RenderContext context, int lodLevel)
    {
        try
        {
            // CPU 코어 수에 따른 청크 크기 계산
            var chunkSize = Math.Max(100, features.Count / Environment.ProcessorCount);
            var chunks = features.Chunk(chunkSize);

            // 병렬로 렌더링 데이터 준비
            var renderTasks = chunks.Select(chunk => Task.Run(() =>
            {
                var renderItems = new List<RenderItem>();
                
                foreach (var feature in chunk)
                {
                    if (LevelOfDetail.ShouldRenderGeometry(feature.Geometry, context, (LevelOfDetail.LODLevel)lodLevel))
                    {
                        var style = GetFeatureStyle(feature, context);
                        var screenGeometry = TransformGeometry(feature.Geometry, context);
                        
                        if (screenGeometry != null)
                        {
                            renderItems.Add(new RenderItem 
                            { 
                                Geometry = screenGeometry,
                                Style = style,
                                Feature = feature 
                            });
                        }
                    }
                }

                return renderItems;
            }));

            // 모든 병렬 태스크 완료 대기
            var results = Task.WhenAll(renderTasks).Result;
            
            // 메인 스레드에서 실제 렌더링 (DrawingContext는 메인 스레드에서만 사용 가능)
            foreach (var renderItems in results)
            {
                foreach (var item in renderItems)
                {
                    DrawGeometry(item.Geometry, item.Style, context);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Parallel rendering failed: {ex.Message}");
            
            // 폴백: 순차 렌더링
            foreach (var feature in features)
            {
                try
                {
                    if (LevelOfDetail.ShouldRenderGeometry(feature.Geometry, context, (LevelOfDetail.LODLevel)lodLevel))
                    {
                        RenderFeature(feature, context, (LevelOfDetail.LODLevel)lodLevel);
                    }
                }
                catch (Exception renderEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Feature rendering error: {renderEx.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 지오메트리를 화면 좌표로 변환
    /// </summary>
    private System.Windows.Media.Geometry? TransformGeometry(Geometry.IGeometry geometry, RenderContext context)
    {
        if (geometry == null) return null;

        try
        {
            return geometry switch
            {
                Geometry.Point point => CreatePointGeometry(point, context),
                Geometry.LineString lineString => CreateLineStringGeometry(lineString, context),
                Geometry.Polygon polygon => CreatePolygonGeometry(polygon, context),
                Geometry.MultiPoint multiPoint => CreateMultiPointGeometry(multiPoint, context),
                Geometry.MultiLineString multiLineString => CreateMultiLineStringGeometry(multiLineString, context),
                Geometry.MultiPolygon multiPolygon => CreateMultiPolygonGeometry(multiPolygon, context),
                _ => null
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Geometry transformation error: {ex.Message}");
            return null;
        }
    }

    private System.Windows.Media.Geometry CreatePointGeometry(Geometry.Point point, RenderContext context)
    {
        var screenPoint = context.MapToScreen(point.Coordinate);
        return new EllipseGeometry(screenPoint, 3, 3);
    }

    private System.Windows.Media.Geometry? CreateLineStringGeometry(Geometry.LineString lineString, RenderContext context)
    {
        var points = context.ConvertToScreenPoints(lineString.Coordinates);
        if (points == null || points.Length == 0) return null;

        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            gc.BeginFigure(points[0], false, false);
            gc.PolyLineTo(points.Skip(1).ToList(), true, true);
        }
        geometry.Freeze();
        return geometry;
    }

    private System.Windows.Media.Geometry? CreatePolygonGeometry(Geometry.Polygon polygon, RenderContext context)
    {
        if (polygon.ExteriorRing == null) return null;
        var exterior = context.ConvertToScreenPoints(polygon.ExteriorRing.Coordinates);
        if (exterior == null || exterior.Length == 0) return null;

        var geometry = new StreamGeometry();
        using (var gc = geometry.Open())
        {
            gc.BeginFigure(exterior[0], true, true);
            gc.PolyLineTo(exterior.Skip(1).ToList(), true, true);

            if (polygon.InteriorRings != null)
            {
                foreach (var hole in polygon.InteriorRings)
                {
                    var holePoints = context.ConvertToScreenPoints(hole.Coordinates);
                    if (holePoints != null && holePoints.Length > 0)
                    {
                        gc.BeginFigure(holePoints[0], true, true);
                        gc.PolyLineTo(holePoints.Skip(1).ToList(), true, true);
                    }
                }
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private System.Windows.Media.Geometry? CreateMultiPointGeometry(Geometry.MultiPoint multiPoint, RenderContext context)
    {
        var group = new GeometryGroup();
        foreach (var point in multiPoint.Geometries)
        {
            var geom = CreatePointGeometry(point, context);
            if (geom != null) group.Children.Add(geom);
        }
        return group.Children.Count > 0 ? group : null;
    }

    private System.Windows.Media.Geometry? CreateMultiLineStringGeometry(Geometry.MultiLineString multiLineString, RenderContext context)
    {
        var group = new GeometryGroup();
        foreach (var line in multiLineString.Geometries)
        {
            var geom = CreateLineStringGeometry(line, context);
            if (geom != null) group.Children.Add(geom);
        }
        return group.Children.Count > 0 ? group : null;
    }

    private System.Windows.Media.Geometry? CreateMultiPolygonGeometry(Geometry.MultiPolygon multiPolygon, RenderContext context)
    {
        var group = new GeometryGroup();
        foreach (var poly in multiPolygon.Geometries)
        {
            var geom = CreatePolygonGeometry(poly, context);
            if (geom != null) group.Children.Add(geom);
        }
        return group.Children.Count > 0 ? group : null;
    }

    /// <summary>
    /// 피처 스타일 가져오기
    /// </summary>
    private object GetFeatureStyle(Data.IFeature feature, RenderContext context)
    {
        return _styleEngine?.GetStyle(feature, 1.0) ?? GetDefaultStyle(feature.Geometry?.GetType());
    }

    /// <summary>
    /// 기본 스타일 가져오기
    /// </summary>
    private Styling.IStyle GetDefaultStyle(Type? geometryType)
    {
        if (geometryType == typeof(Geometry.Point) || geometryType == typeof(Geometry.MultiPoint))
            return Styling.DefaultStyles.DefaultPoint;
        if (geometryType == typeof(Geometry.LineString) || geometryType == typeof(Geometry.MultiLineString))
            return Styling.DefaultStyles.DefaultLine;
        if (geometryType == typeof(Geometry.Polygon) || geometryType == typeof(Geometry.MultiPolygon))
            return Styling.DefaultStyles.DefaultPolygon;
            
        return Styling.DefaultStyles.DefaultPoint;
    }

    /// <summary>
    /// 화면 지오메트리를 실제로 그리기
    /// </summary>
    private void DrawGeometry(System.Windows.Media.Geometry geometry, object style, RenderContext context)
    {
        if (style is Styling.IStyle s)
        {
            Brush? brush = null;
            Pen? pen = null;

            if (s is Styling.IPolygonStyle polyStyle)
            {
                brush = CreateBrush(polyStyle);
                pen = CreatePolygonPen(polyStyle);
            }
            else if (s is Styling.ILineStyle lineStyle)
            {
                pen = CreatePen(lineStyle);
            }
            else if (s is Styling.IPointStyle pointStyle)
            {
                brush = new SolidColorBrush(pointStyle.Fill);
                pen = pointStyle.StrokeWidth > 0 ? new Pen(new SolidColorBrush(pointStyle.Stroke), pointStyle.StrokeWidth) : null;
            }

            context.DrawingContext.DrawGeometry(brush, pen, geometry);
        }
        else
        {
            context.DrawingContext.DrawGeometry(null, new Pen(Brushes.Black, 1), geometry);
        }
    }

    /// <inheritdoc/>
    public void RenderFeature(Data.IFeature feature, RenderContext context)
    {
        RenderFeature(feature, context, LevelOfDetail.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 피처 렌더링
    /// </summary>
    public void RenderFeature(Data.IFeature feature, RenderContext context, LevelOfDetail.LODLevel lodLevel)
    {
        if (feature?.Geometry == null || context?.DrawingContext == null) return;

        // 뷰포트 컬링 - 보이지 않는 지오메트리는 건너뜀
        if (!context.IsVisible(feature.Geometry)) return;

        // 레이어 스타일이 있으면 사용, 없으면 기존 로직
        if (context.LayerStyle != null)
        {
            RenderGeometryWithLayerStyle(feature.Geometry, context, lodLevel);
        }
        else
        {
            // 스타일 가져오기 - StyleEngine이 있으면 사용, 없으면 피처의 스타일 또는 기본 스타일
            var style = _styleEngine?.GetStyle(feature, context.Zoom) 
                       ?? feature.Style 
                       ?? GetDefaultStyleForGeometry(feature.Geometry);
            
            RenderGeometry(feature.Geometry, context, style, lodLevel);
        }
    }
    
    /// <summary>
    /// 레이어 스타일을 사용하여 지오메트리 렌더링
    /// </summary>
    private void RenderGeometryWithLayerStyle(Geometry.IGeometry geometry, RenderContext context, LevelOfDetail.LODLevel lodLevel)
    {
        if (geometry == null || context?.DrawingContext == null || context.LayerStyle == null) return;
        
        var layerStyle = context.LayerStyle;
        
        // 투명도 적용된 색상 생성
        var fillColor = Color.FromArgb(
            (byte)(layerStyle.FillColor.A * layerStyle.Opacity),
            layerStyle.FillColor.R,
            layerStyle.FillColor.G,
            layerStyle.FillColor.B);
        
        var strokeColor = Color.FromArgb(
            (byte)(layerStyle.StrokeColor.A * layerStyle.Opacity),
            layerStyle.StrokeColor.R,
            layerStyle.StrokeColor.G,
            layerStyle.StrokeColor.B);
        
        switch (geometry)
        {
            case Geometry.Point point:
                RenderPointWithStyle(point, context, fillColor, strokeColor, layerStyle.PointSize, layerStyle.StrokeWidth, layerStyle.SymbolType);
                break;
                
            case Geometry.LineString lineString:
                RenderLineStringWithStyle(lineString, context, strokeColor, layerStyle.StrokeWidth, layerStyle.DashPattern);
                break;
                
            case Geometry.Polygon polygon:
                RenderPolygonWithStyle(polygon, context, fillColor, strokeColor, layerStyle.StrokeWidth, layerStyle.EnableFill, layerStyle.EnableStroke);
                break;
                
            case Geometry.MultiPoint multiPoint:
                foreach (var pt in multiPoint.Geometries.Cast<Geometry.Point>())
                    RenderPointWithStyle(pt, context, fillColor, strokeColor, layerStyle.PointSize, layerStyle.StrokeWidth, layerStyle.SymbolType);
                break;
                
            case Geometry.MultiLineString multiLineString:
                foreach (var line in multiLineString.Geometries.Cast<Geometry.LineString>())
                    RenderLineStringWithStyle(line, context, strokeColor, layerStyle.StrokeWidth, layerStyle.DashPattern);
                break;
                
            case Geometry.MultiPolygon multiPolygon:
                foreach (var poly in multiPolygon.Geometries.Cast<Geometry.Polygon>())
                    RenderPolygonWithStyle(poly, context, fillColor, strokeColor, layerStyle.StrokeWidth, layerStyle.EnableFill, layerStyle.EnableStroke);
                break;
        }
    }
    
    /// <summary>
    /// 스타일을 적용하여 포인트 렌더링
    /// </summary>
    private void RenderPointWithStyle(Geometry.Point point, RenderContext context, Color fillColor, Color strokeColor, double size, double strokeWidth, PointSymbolType symbolType)
    {
        var screenPoint = context.MapToScreen(point.Coordinate);
        var brush = new SolidColorBrush(fillColor);
        var pen = strokeWidth > 0 ? new Pen(new SolidColorBrush(strokeColor), strokeWidth) : null;
        var halfSize = size / 2;
        
        switch (symbolType)
        {
            case PointSymbolType.Circle:
                context.DrawingContext.DrawEllipse(brush, pen, screenPoint, halfSize, halfSize);
                break;
            case PointSymbolType.Square:
                context.DrawingContext.DrawRectangle(brush, pen, 
                    new Rect(screenPoint.X - halfSize, screenPoint.Y - halfSize, size, size));
                break;
            case PointSymbolType.Diamond:
                var diamondGeometry = new StreamGeometry();
                using (var ctx = diamondGeometry.Open())
                {
                    ctx.BeginFigure(new Point(screenPoint.X, screenPoint.Y - halfSize), true, true);
                    ctx.LineTo(new Point(screenPoint.X + halfSize, screenPoint.Y), true, false);
                    ctx.LineTo(new Point(screenPoint.X, screenPoint.Y + halfSize), true, false);
                    ctx.LineTo(new Point(screenPoint.X - halfSize, screenPoint.Y), true, false);
                }
                context.DrawingContext.DrawGeometry(brush, pen, diamondGeometry);
                break;
            case PointSymbolType.Triangle:
                var triangleGeometry = new StreamGeometry();
                using (var ctx = triangleGeometry.Open())
                {
                    ctx.BeginFigure(new Point(screenPoint.X, screenPoint.Y - halfSize), true, true);
                    ctx.LineTo(new Point(screenPoint.X + halfSize, screenPoint.Y + halfSize), true, false);
                    ctx.LineTo(new Point(screenPoint.X - halfSize, screenPoint.Y + halfSize), true, false);
                }
                context.DrawingContext.DrawGeometry(brush, pen, triangleGeometry);
                break;
            case PointSymbolType.Cross:
                var crossPen = new Pen(new SolidColorBrush(fillColor), strokeWidth > 0 ? strokeWidth : 2);
                context.DrawingContext.DrawLine(crossPen, 
                    new Point(screenPoint.X - halfSize, screenPoint.Y), 
                    new Point(screenPoint.X + halfSize, screenPoint.Y));
                context.DrawingContext.DrawLine(crossPen, 
                    new Point(screenPoint.X, screenPoint.Y - halfSize), 
                    new Point(screenPoint.X, screenPoint.Y + halfSize));
                break;
            case PointSymbolType.Star:
                // 간단한 별 모양 (5각 별)
                var starGeometry = CreateStarGeometry(screenPoint, halfSize);
                context.DrawingContext.DrawGeometry(brush, pen, starGeometry);
                break;
            default:
                context.DrawingContext.DrawEllipse(brush, pen, screenPoint, halfSize, halfSize);
                break;
        }
    }
    
    /// <summary>
    /// 별 모양 지오메트리 생성
    /// </summary>
    private StreamGeometry CreateStarGeometry(Point center, double radius)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var innerRadius = radius * 0.4;
            var points = new Point[10];
            for (int i = 0; i < 10; i++)
            {
                var angle = Math.PI / 2 + i * Math.PI / 5;
                var r = i % 2 == 0 ? radius : innerRadius;
                points[i] = new Point(center.X + r * Math.Cos(angle), center.Y - r * Math.Sin(angle));
            }
            ctx.BeginFigure(points[0], true, true);
            for (int i = 1; i < 10; i++)
                ctx.LineTo(points[i], true, false);
        }
        return geometry;
    }
    
    /// <summary>
    /// 스타일을 적용하여 라인 렌더링
    /// </summary>
    private void RenderLineStringWithStyle(Geometry.LineString lineString, RenderContext context, Color strokeColor, double strokeWidth, double[]? dashPattern)
    {
        if (lineString.Coordinates == null || lineString.Coordinates.Length < 2) return;
        
        var pen = new Pen(new SolidColorBrush(strokeColor), strokeWidth);
        if (dashPattern != null && dashPattern.Length > 0)
        {
            pen.DashStyle = new DashStyle(dashPattern, 0);
        }
        pen.StartLineCap = PenLineCap.Round;
        pen.EndLineCap = PenLineCap.Round;
        pen.LineJoin = PenLineJoin.Round;
        
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var firstPoint = context.MapToScreen(lineString.Coordinates[0]);
            ctx.BeginFigure(firstPoint, false, false);
            
            for (int i = 1; i < lineString.Coordinates.Length; i++)
            {
                ctx.LineTo(context.MapToScreen(lineString.Coordinates[i]), true, false);
            }
        }
        
        context.DrawingContext.DrawGeometry(null, pen, geometry);
    }
    
    /// <summary>
    /// 스타일을 적용하여 폴리곤 렌더링
    /// </summary>
    private void RenderPolygonWithStyle(Geometry.Polygon polygon, RenderContext context, Color fillColor, Color strokeColor, double strokeWidth, bool enableFill, bool enableStroke)
    {
        if (polygon.ExteriorRing == null || polygon.ExteriorRing.Coordinates == null || polygon.ExteriorRing.Coordinates.Length < 3) return;
        
        var brush = enableFill ? new SolidColorBrush(fillColor) : null;
        var pen = enableStroke && strokeWidth > 0 ? new Pen(new SolidColorBrush(strokeColor), strokeWidth) : null;
        
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // 외부 링
            var exteriorCoords = polygon.ExteriorRing.Coordinates;
            var firstPoint = context.MapToScreen(exteriorCoords[0]);
            ctx.BeginFigure(firstPoint, true, true);
            
            for (int i = 1; i < exteriorCoords.Length; i++)
            {
                ctx.LineTo(context.MapToScreen(exteriorCoords[i]), true, false);
            }
            
            // 내부 링 (홀)
            if (polygon.InteriorRings != null)
            {
                foreach (var hole in polygon.InteriorRings)
                {
                    if (hole?.Coordinates == null || hole.Coordinates.Length < 3) continue;
                    
                    var holeFirst = context.MapToScreen(hole.Coordinates[0]);
                    ctx.BeginFigure(holeFirst, true, true);
                    
                    for (int i = 1; i < hole.Coordinates.Length; i++)
                    {
                        ctx.LineTo(context.MapToScreen(hole.Coordinates[i]), true, false);
                    }
                }
            }
        }
        
        geometry.FillRule = FillRule.EvenOdd;
        context.DrawingContext.DrawGeometry(brush, pen, geometry);
    }

    /// <inheritdoc/>
    public void RenderGeometry(Geometry.IGeometry geometry, RenderContext context, Styling.IStyle? style = null)
    {
        RenderGeometry(geometry, context, style, LevelOfDetail.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 지오메트리 렌더링
    /// </summary>
    public void RenderGeometry(Geometry.IGeometry geometry, RenderContext context, Styling.IStyle? style, LevelOfDetail.LODLevel lodLevel)
    {
        if (geometry == null || context?.DrawingContext == null) return;

        // 줌 레벨에 따른 스타일 필터링
        if (style != null && !IsStyleVisible(style, context.Zoom)) return;

        switch (geometry)
        {
            case Geometry.Point point:
                RenderPoint(point, context, style as Styling.IPointStyle, lodLevel);
                break;
                
            case Geometry.LineString lineString:
                RenderLineString(lineString, context, style as Styling.ILineStyle, lodLevel);
                break;
                
            case Geometry.Polygon polygon:
                RenderPolygon(polygon, context, style as Styling.IPolygonStyle, lodLevel);
                break;
                
            case Geometry.MultiPoint multiPoint:
                foreach (var pt in multiPoint.Geometries.Cast<Geometry.Point>())
                    RenderPoint(pt, context, style as Styling.IPointStyle, lodLevel);
                break;
                
            case Geometry.MultiLineString multiLineString:
                foreach (var line in multiLineString.Geometries.Cast<Geometry.LineString>())
                    RenderLineString(line, context, style as Styling.ILineStyle, lodLevel);
                break;
                
            case Geometry.MultiPolygon multiPolygon:
                foreach (var poly in multiPolygon.Geometries.Cast<Geometry.Polygon>())
                    RenderPolygon(poly, context, style as Styling.IPolygonStyle, lodLevel);
                break;
                
            default:
                RenderMultiGeometry(geometry, context, style);
                break;
        }
    }

    /// <inheritdoc/>
    public void RenderPoint(Geometry.Point point, RenderContext context, Styling.IPointStyle? style = null)
    {
        RenderPoint(point, context, style, LevelOfDetail.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 포인트 렌더링
    /// </summary>
    public void RenderPoint(Geometry.Point point, RenderContext context, Styling.IPointStyle? style, LevelOfDetail.LODLevel lodLevel)
    {
        if (point?.Coordinate == null || context?.DrawingContext == null) return;

        style ??= Styling.DefaultStyles.DefaultPoint;
        if (!IsStyleVisible(style, context.Zoom)) return;

        // LOD에 따른 심볼 렌더링 여부 확인
        if (!LevelOfDetail.ShouldRenderSymbol(lodLevel, style.Size)) return;

        var screenPoint = context.MapToScreen(point.Coordinate);
        
        // 화면 범위 체크
        if (!IsPointInScreen(screenPoint, context.ScreenSize, style.Size)) return;

        var brush = new SolidColorBrush(style.Fill);
        var pen = style.StrokeWidth > 0 ? new Pen(new SolidColorBrush(style.Stroke), style.StrokeWidth) : null;

        // LOD에 따른 간소화된 렌더링
        var effectiveShape = lodLevel >= LevelOfDetail.LODLevel.Medium ? Styling.PointShape.Circle : style.Shape;

        switch (effectiveShape)
        {
            case Styling.PointShape.Circle:
                context.DrawingContext.DrawEllipse(brush, pen, screenPoint, style.Size / 2, style.Size / 2);
                break;
                
            case Styling.PointShape.Square:
                var rect = new Rect(screenPoint.X - style.Size / 2, screenPoint.Y - style.Size / 2, 
                                  style.Size, style.Size);
                context.DrawingContext.DrawRectangle(brush, pen, rect);
                break;
                
            case Styling.PointShape.Triangle:
                DrawTriangle(context.DrawingContext, screenPoint, style.Size, brush, pen);
                break;
                
            case Styling.PointShape.Diamond:
                DrawDiamond(context.DrawingContext, screenPoint, style.Size, brush, pen);
                break;
                
            case Styling.PointShape.Cross:
                DrawCross(context.DrawingContext, screenPoint, style.Size, pen);
                break;
                
            case Styling.PointShape.X:
                DrawX(context.DrawingContext, screenPoint, style.Size, pen);
                break;
        }
    }

    /// <inheritdoc/>
    public void RenderLineString(Geometry.LineString lineString, RenderContext context, Styling.ILineStyle? style = null)
    {
        RenderLineString(lineString, context, style, LevelOfDetail.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 라인스트링 렌더링
    /// </summary>
    public void RenderLineString(Geometry.LineString lineString, RenderContext context, Styling.ILineStyle? style, LevelOfDetail.LODLevel lodLevel)
    {
        if (lineString?.Coordinates == null || lineString.Coordinates.Length < 2 || 
            context?.DrawingContext == null) return;

        style ??= Styling.DefaultStyles.DefaultLine;
        if (!IsStyleVisible(style, context.Zoom)) return;

        var coordinates = lineString.Coordinates;
        
        // LOD에 따른 라인 단순화
        if (coordinates.Length > 2)
        {
            var screenPoints = context.ConvertToScreenPoints(coordinates);
            var totalLength = CalculateLineLength(screenPoints);
            
            if (LevelOfDetail.ShouldSimplifyLine(lodLevel, totalLength, coordinates.Length))
            {
                var tolerance = LevelOfDetail.GetSimplificationTolerance(lodLevel, context.Resolution);
                coordinates = SimplifyLineString(coordinates, tolerance);
            }
        }
        
        var finalScreenPoints = context.ConvertToScreenPoints(coordinates);
        
        // 화면 범위와 교차하는지 체크
        if (!IsLineInScreen(finalScreenPoints, context.ScreenSize)) return;

        var pen = CreatePen(style);
        if (pen == null) return;

        // 선분 그리기
        for (int i = 0; i < finalScreenPoints.Length - 1; i++)
        {
            context.DrawingContext.DrawLine(pen, finalScreenPoints[i], finalScreenPoints[i + 1]);
        }
    }

    /// <inheritdoc/>
    public void RenderPolygon(Geometry.Polygon polygon, RenderContext context, Styling.IPolygonStyle? style = null)
    {
        RenderPolygon(polygon, context, style, LevelOfDetail.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 폴리곤 렌더링
    /// </summary>
    public void RenderPolygon(Geometry.Polygon polygon, RenderContext context, Styling.IPolygonStyle? style, LevelOfDetail.LODLevel lodLevel)
    {
        if (polygon?.ExteriorRing?.Coordinates == null || 
            polygon.ExteriorRing.Coordinates.Length < 3 ||
            context?.DrawingContext == null) return;

        style ??= Styling.DefaultStyles.DefaultPolygon;
        if (!IsStyleVisible(style, context.Zoom)) return;

        var exteriorCoords = polygon.ExteriorRing.Coordinates;
        
        // LOD에 따른 폴리곤 단순화
        if (exteriorCoords.Length > 3)
        {
            var tolerance = LevelOfDetail.GetSimplificationTolerance(lodLevel, context.Resolution);
            if (tolerance > 0)
            {
                exteriorCoords = SimplifyLineString(exteriorCoords, tolerance);
                if (exteriorCoords.Length < 3) return; // 단순화 후 유효하지 않은 폴리곤
            }
        }

        // 외곽선 변환
        var exteriorPoints = context.ConvertToScreenPoints(exteriorCoords);
        
        // 화면 범위와 교차하는지 체크
        if (!IsPolygonInScreen(exteriorPoints, context.ScreenSize)) return;

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = exteriorPoints[0],
            IsClosed = true
        };

        // 외곽선 그리기
        for (int i = 1; i < exteriorPoints.Length; i++)
        {
            figure.Segments.Add(new LineSegment(exteriorPoints[i], true));
        }

        // 구멍 처리 (높은 LOD에서만)
        if (lodLevel <= LevelOfDetail.LODLevel.High && polygon.InteriorRings != null)
        {
            foreach (var hole in polygon.InteriorRings)
            {
                if (hole.Coordinates?.Length >= 3)
                {
                    var holeCoords = hole.Coordinates;
                    
                    // 구멍도 단순화
                    var tolerance = LevelOfDetail.GetSimplificationTolerance(lodLevel, context.Resolution);
                    if (tolerance > 0 && holeCoords.Length > 3)
                    {
                        holeCoords = SimplifyLineString(holeCoords, tolerance);
                        if (holeCoords.Length < 3) continue;
                    }
                    
                    var holePoints = context.ConvertToScreenPoints(holeCoords);
                    var holeFigure = new PathFigure
                    {
                        StartPoint = holePoints[0],
                        IsClosed = true
                    };

                    for (int i = 1; i < holePoints.Length; i++)
                    {
                        holeFigure.Segments.Add(new LineSegment(holePoints[i], true));
                    }

                    geometry.Figures.Add(holeFigure);
                }
            }
        }

        geometry.Figures.Add(figure);

        var brush = CreateBrush(style);
        var pen = CreatePolygonPen(style);

        context.DrawingContext.DrawGeometry(brush, pen, geometry);
    }

    /// <inheritdoc/>
    public void RenderMultiGeometry(Geometry.IGeometry multiGeometry, RenderContext context, Styling.IStyle? style = null)
    {
        if (multiGeometry is Geometry.GeometryCollection collection)
        {
            foreach (var geom in collection.Geometries)
            {
                RenderGeometry(geom, context, style);
            }
        }
    }

    #region 헬퍼 메서드

    /// <summary>
    /// 지오메트리 타입에 따른 기본 스타일 반환
    /// </summary>
    private Styling.IStyle GetDefaultStyleForGeometry(Geometry.IGeometry geometry)
    {
        return geometry switch
        {
            Geometry.Point or Geometry.MultiPoint => Styling.DefaultStyles.DefaultPoint,
            Geometry.LineString or Geometry.MultiLineString => Styling.DefaultStyles.DefaultLine,
            Geometry.Polygon or Geometry.MultiPolygon => Styling.DefaultStyles.DefaultPolygon,
            _ => Styling.DefaultStyles.DefaultPoint
        };
    }

    /// <summary>
    /// 스타일이 현재 줌 레벨에서 보이는지 확인
    /// </summary>
    private static bool IsStyleVisible(Styling.IStyle style, double zoom)
    {
        return style.IsVisible && zoom >= style.MinZoom && zoom <= style.MaxZoom;
    }

    /// <summary>
    /// 점이 화면 범위 내에 있는지 확인
    /// </summary>
    private static bool IsPointInScreen(Point point, Size screenSize, double symbolSize)
    {
        var margin = symbolSize / 2 + 10; // 여유 공간
        return point.X >= -margin && point.X <= screenSize.Width + margin &&
               point.Y >= -margin && point.Y <= screenSize.Height + margin;
    }

    /// <summary>
    /// 선이 화면과 교차하는지 확인
    /// </summary>
    private static bool IsLineInScreen(Point[] points, Size screenSize)
    {
        var screenRect = new Rect(0, 0, screenSize.Width, screenSize.Height);
        
        for (int i = 0; i < points.Length - 1; i++)
        {
            if (screenRect.Contains(points[i]) || screenRect.Contains(points[i + 1]))
                return true;
                
            // 선분이 화면을 가로지르는지 체크 (간단한 방법)
            if (LineIntersectsRect(points[i], points[i + 1], screenRect))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// 폴리곤이 화면과 교차하는지 확인
    /// </summary>
    private static bool IsPolygonInScreen(Point[] points, Size screenSize)
    {
        var screenRect = new Rect(0, 0, screenSize.Width, screenSize.Height);
        
        // 점 중 하나라도 화면 안에 있으면 true
        foreach (var point in points)
        {
            if (screenRect.Contains(point)) return true;
        }
        
        // 폴리곤이 화면을 완전히 둘러싸는지 체크
        return IsPointInPolygon(new Point(screenSize.Width / 2, screenSize.Height / 2), points);
    }

    /// <summary>
    /// 선분과 사각형 교차 체크
    /// </summary>
    private static bool LineIntersectsRect(Point p1, Point p2, Rect rect)
    {
        return LineIntersectsLine(p1, p2, rect.TopLeft, rect.TopRight) ||
               LineIntersectsLine(p1, p2, rect.TopRight, rect.BottomRight) ||
               LineIntersectsLine(p1, p2, rect.BottomRight, rect.BottomLeft) ||
               LineIntersectsLine(p1, p2, rect.BottomLeft, rect.TopLeft);
    }

    /// <summary>
    /// 두 선분의 교차 체크
    /// </summary>
    private static bool LineIntersectsLine(Point p1, Point p2, Point p3, Point p4)
    {
        var d1 = Direction(p3, p4, p1);
        var d2 = Direction(p3, p4, p2);
        var d3 = Direction(p1, p2, p3);
        var d4 = Direction(p1, p2, p4);
        
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && 
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
            
        return false;
    }

    /// <summary>
    /// 방향 계산 (외적)
    /// </summary>
    private static double Direction(Point a, Point b, Point c)
    {
        return (c.X - a.X) * (b.Y - a.Y) - (b.X - a.X) * (c.Y - a.Y);
    }

    /// <summary>
    /// 점이 폴리곤 내부에 있는지 확인 (Ray Casting)
    /// </summary>
    private static bool IsPointInPolygon(Point point, Point[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// 라인 스타일로부터 Pen 생성
    /// </summary>
    private static Pen? CreatePen(Styling.ILineStyle style)
    {
        if (style.StrokeWidth <= 0) return null;

        var pen = new Pen(new SolidColorBrush(style.Stroke), style.StrokeWidth)
        {
            StartLineCap = style.LineCap,
            EndLineCap = style.LineCap,
            LineJoin = style.LineJoin
        };

        // 대시 패턴 적용
        switch (style.LineStyle)
        {
            case Styling.LineStyle.Dash:
                pen.DashStyle = DashStyles.Dash;
                break;
            case Styling.LineStyle.Dot:
                pen.DashStyle = DashStyles.Dot;
                break;
            case Styling.LineStyle.DashDot:
                pen.DashStyle = DashStyles.DashDot;
                break;
            case Styling.LineStyle.DashDotDot:
                pen.DashStyle = DashStyles.DashDotDot;
                break;
            default:
                pen.DashStyle = DashStyles.Solid;
                break;
        }

        // 사용자 정의 대시 패턴
        if (style.DashArray?.Length > 0)
        {
            pen.DashStyle = new DashStyle(style.DashArray, 0);
        }

        return pen;
    }

    /// <summary>
    /// 폴리곤 스타일로부터 브러시 생성
    /// </summary>
    private static Brush? CreateBrush(Styling.IPolygonStyle style)
    {
        var brush = new SolidColorBrush(style.Fill);
        
        if (style.Opacity < 1.0)
        {
            brush.Opacity = Math.Max(0, Math.Min(1, style.Opacity));
        }
        
        return brush;
    }

    /// <summary>
    /// 폴리곤 스타일로부터 외곽선 Pen 생성
    /// </summary>
    private static Pen? CreatePolygonPen(Styling.IPolygonStyle style)
    {
        if (style.StrokeWidth <= 0) return null;

        var pen = new Pen(new SolidColorBrush(style.Stroke), style.StrokeWidth);

        // 대시 패턴 적용
        switch (style.StrokeStyle)
        {
            case Styling.LineStyle.Dash:
                pen.DashStyle = DashStyles.Dash;
                break;
            case Styling.LineStyle.Dot:
                pen.DashStyle = DashStyles.Dot;
                break;
            case Styling.LineStyle.DashDot:
                pen.DashStyle = DashStyles.DashDot;
                break;
            case Styling.LineStyle.DashDotDot:
                pen.DashStyle = DashStyles.DashDotDot;
                break;
            default:
                pen.DashStyle = DashStyles.Solid;
                break;
        }

        // 사용자 정의 대시 패턴
        if (style.DashArray?.Length > 0)
        {
            pen.DashStyle = new DashStyle(style.DashArray, 0);
        }

        return pen;
    }

    /// <summary>
    /// 삼각형 그리기
    /// </summary>
    private static void DrawTriangle(DrawingContext dc, Point center, double size, Brush brush, Pen? pen)
    {
        var halfSize = size / 2;
        var height = size * 0.866; // √3/2
        
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(center.X, center.Y - height / 2),
            IsClosed = true
        };
        
        figure.Segments.Add(new LineSegment(new Point(center.X - halfSize, center.Y + height / 2), true));
        figure.Segments.Add(new LineSegment(new Point(center.X + halfSize, center.Y + height / 2), true));
        
        geometry.Figures.Add(figure);
        dc.DrawGeometry(brush, pen, geometry);
    }

    /// <summary>
    /// 다이아몬드 그리기
    /// </summary>
    private static void DrawDiamond(DrawingContext dc, Point center, double size, Brush brush, Pen? pen)
    {
        var halfSize = size / 2;
        
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new Point(center.X, center.Y - halfSize),
            IsClosed = true
        };
        
        figure.Segments.Add(new LineSegment(new Point(center.X + halfSize, center.Y), true));
        figure.Segments.Add(new LineSegment(new Point(center.X, center.Y + halfSize), true));
        figure.Segments.Add(new LineSegment(new Point(center.X - halfSize, center.Y), true));
        
        geometry.Figures.Add(figure);
        dc.DrawGeometry(brush, pen, geometry);
    }

    /// <summary>
    /// 십자가 그리기
    /// </summary>
    private static void DrawCross(DrawingContext dc, Point center, double size, Pen? pen)
    {
        if (pen == null) return;
        
        var halfSize = size / 2;
        
        // 수직선
        dc.DrawLine(pen, 
            new Point(center.X, center.Y - halfSize), 
            new Point(center.X, center.Y + halfSize));
            
        // 수평선
        dc.DrawLine(pen,
            new Point(center.X - halfSize, center.Y),
            new Point(center.X + halfSize, center.Y));
    }

    /// <summary>
    /// X 그리기
    /// </summary>
    private static void DrawX(DrawingContext dc, Point center, double size, Pen? pen)
    {
        if (pen == null) return;
        
        var halfSize = size / 2;
        
        // 대각선 1
        dc.DrawLine(pen,
            new Point(center.X - halfSize, center.Y - halfSize),
            new Point(center.X + halfSize, center.Y + halfSize));
            
        // 대각선 2
        dc.DrawLine(pen,
            new Point(center.X + halfSize, center.Y - halfSize),
            new Point(center.X - halfSize, center.Y + halfSize));
    }
    
    /// <summary>
    /// 선의 총 길이 계산 (픽셀 단위)
    /// </summary>
    private static double CalculateLineLength(Point[] points)
    {
        if (points.Length < 2) return 0;
        
        double totalLength = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            var dx = points[i + 1].X - points[i].X;
            var dy = points[i + 1].Y - points[i].Y;
            totalLength += Math.Sqrt(dx * dx + dy * dy);
        }
        
        return totalLength;
    }
    
    /// <summary>
    /// Douglas-Peucker 알고리즘을 사용한 라인 단순화
    /// </summary>
    private static Geometry.ICoordinate[] SimplifyLineString(Geometry.ICoordinate[] coordinates, double tolerance)
    {
        if (coordinates.Length <= 2 || tolerance <= 0)
            return coordinates;
            
        var simplified = DouglasPeucker(coordinates, tolerance);
        return simplified.Length >= 2 ? simplified : coordinates;
    }
    
    /// <summary>
    /// Douglas-Peucker 알고리즘 구현
    /// </summary>
    private static Geometry.ICoordinate[] DouglasPeucker(Geometry.ICoordinate[] points, double tolerance)
    {
        if (points.Length <= 2)
            return points;
        
        // 시작점과 끝점 사이의 가장 먼 점 찾기
        double maxDistance = 0;
        int maxIndex = 0;
        
        var start = points[0];
        var end = points[points.Length - 1];
        
        for (int i = 1; i < points.Length - 1; i++)
        {
            double distance = PerpendicularDistance(points[i], start, end);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }
        
        // 최대 거리가 허용치보다 크면 재귀적으로 단순화
        if (maxDistance > tolerance)
        {
            // 첫 번째 부분 단순화
            var firstPart = new Geometry.ICoordinate[maxIndex + 1];
            Array.Copy(points, 0, firstPart, 0, maxIndex + 1);
            var simplified1 = DouglasPeucker(firstPart, tolerance);
            
            // 두 번째 부분 단순화
            var secondPart = new Geometry.ICoordinate[points.Length - maxIndex];
            Array.Copy(points, maxIndex, secondPart, 0, points.Length - maxIndex);
            var simplified2 = DouglasPeucker(secondPart, tolerance);
            
            // 결과 결합 (중복 점 제거)
            var result = new List<Geometry.ICoordinate>(simplified1);
            result.AddRange(simplified2.Skip(1)); // 첫 번째 점은 중복이므로 제외
            
            return result.ToArray();
        }
        else
        {
            // 허용치 이하이면 시작점과 끝점만 반환
            return new[] { start, end };
        }
    }
    
    /// <summary>
    /// 점에서 선분까지의 수직 거리 계산
    /// </summary>
    private static double PerpendicularDistance(Geometry.ICoordinate point, Geometry.ICoordinate lineStart, Geometry.ICoordinate lineEnd)
    {
        var A = lineEnd.X - lineStart.X;
        var B = lineEnd.Y - lineStart.Y;
        var C = point.X - lineStart.X;
        var D = point.Y - lineStart.Y;
        
        var dot = A * C + B * D;
        var lengthSquared = A * A + B * B;
        
        if (lengthSquared == 0) // 선분의 길이가 0
            return Math.Sqrt(C * C + D * D);
        
        var param = dot / lengthSquared;
        
        double xx, yy;
        if (param < 0)
        {
            xx = lineStart.X;
            yy = lineStart.Y;
        }
        else if (param > 1)
        {
            xx = lineEnd.X;
            yy = lineEnd.Y;
        }
        else
        {
            xx = lineStart.X + param * A;
            yy = lineStart.Y + param * B;
        }
        
        var dx = point.X - xx;
        var dy = point.Y - yy;
        
        return Math.Sqrt(dx * dx + dy * dy);
    }

    #endregion
}