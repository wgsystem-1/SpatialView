using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using SpatialView.Engine.Rendering.Optimization;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// WPF 벡터 렌더러 구현
/// DrawingContext를 사용하여 지오메트리를 화면에 그립니다
/// </summary>
public class VectorRenderer : IVectorRenderer
{
    private Styling.Rules.StyleEngine? _styleEngine;

    // 성능 최적화: Brush/Pen 캐싱
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();
    private readonly Dictionary<(Color, double), Pen> _penCache = new();
    private readonly Dictionary<(Color, double, double[]), Pen> _dashedPenCache = new();

    // 성능 최적화: 지오메트리 배칭
    private GeometryBatcher? _batcher;
    private bool _useBatching = true;

    /// <summary>
    /// 배칭 렌더링 사용 여부
    /// </summary>
    public bool UseBatching
    {
        get => _useBatching;
        set => _useBatching = value;
    }
    
    /// <summary>
    /// 스타일 엔진
    /// </summary>
    public Styling.Rules.StyleEngine? StyleEngine 
    { 
        get => _styleEngine;
        set => _styleEngine = value;
    }
    
    // 성능 최적화: 로그 비활성화 (필요시 활성화)
    private static readonly bool _enableLogging = false;
    
    private static void Log(string msg)
    {
        if (!_enableLogging) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[VectorRenderer] {msg}");
        }
        catch { }
    }
    
    /// <summary>
    /// 캐시된 Brush 가져오기 (성능 최적화)
    /// </summary>
    private SolidColorBrush GetCachedBrush(Color color)
    {
        if (!_brushCache.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(color);
            brush.Freeze(); // Freeze로 성능 향상
            _brushCache[color] = brush;
        }
        return brush;
    }
    
    /// <summary>
    /// 캐시된 Pen 가져오기 (성능 최적화)
    /// </summary>
    private Pen GetCachedPen(Color color, double thickness)
    {
        var key = (color, thickness);
        if (!_penCache.TryGetValue(key, out var pen))
        {
            pen = new Pen(GetCachedBrush(color), thickness);
            pen.Freeze(); // Freeze로 성능 향상
            _penCache[key] = pen;
        }
        return pen;
    }
    
    /// <summary>
    /// 캐시된 대시 Pen 가져오기 (성능 최적화)
    /// </summary>
    private Pen GetCachedDashedPen(Color color, double thickness, double[]? dashPattern)
    {
        if (dashPattern == null || dashPattern.Length == 0)
            return GetCachedPen(color, thickness);
            
        var key = (color, thickness, dashPattern);
        if (!_dashedPenCache.TryGetValue(key, out var pen))
        {
            pen = new Pen(GetCachedBrush(color), thickness);
            pen.DashStyle = new DashStyle(dashPattern, 0);
            pen.Freeze();
            _dashedPenCache[key] = pen;
        }
        return pen;
    }
    
    /// <summary>
    /// 캐시 초기화 (줌 변경 등에서 호출)
    /// </summary>
    public void ClearCache()
    {
        _brushCache.Clear();
        _penCache.Clear();
        _dashedPenCache.Clear();
    }

    /// <inheritdoc/>
    public void RenderFeatures(IEnumerable<Data.IFeature> features, RenderContext context)
    {
        if (features == null || context?.DrawingContext == null)
        {
            return;
        }

        // 해상도(맵 단위/픽셀) 및 간단 LOD(최소 픽셀 크기 필터)
        var resolution = context.ViewExtent.Width / Math.Max(1.0, context.ScreenSize.Width);
        var lodLevel = LodUtils.CalculateLODLevel(resolution);
        var minPixelSize = 0.5; // 0.5px 미만은 스킵 (라인/폴리곤)

        // 배칭 렌더링 모드 (비활성화 상태에서는 스킵)
        if (_useBatching && context.LayerStyle != null)
        {
            RenderFeaturesWithBatching(features, context, lodLevel, resolution, minPixelSize);
            return;
        }

        // 직접 렌더링 모드 (최적화됨) - LOD 체크 최소화
        foreach (var feature in features)
        {
            try
            {
                if (feature?.Geometry == null) continue;

                // 뷰포트 컬링
                var envelope = feature.Geometry.Envelope;
                if (envelope != null && !envelope.IsNull && !context.ViewExtent.Intersects(envelope))
                {
                    continue;
                }

                // 최소 픽셀 크기 필터 (포인트 제외)
                if (feature.Geometry.GeometryType != Engine.Geometry.GeometryType.Point &&
                    feature.Geometry.GeometryType != Engine.Geometry.GeometryType.MultiPoint &&
                    envelope != null)
                {
                    var w = envelope.Width / resolution;
                    var h = envelope.Height / resolution;
                    if (w < minPixelSize && h < minPixelSize)
                        continue;
                }

                // LOD 기반 최소 픽셀 크기 필터
                if (!LodUtils.ShouldRenderGeometry(feature.Geometry, context, lodLevel))
                {
                    continue;
                }

                // 레이어 스타일로 직접 렌더링
                if (context.LayerStyle != null)
                {
                    RenderGeometryWithLayerStyleFast(feature.Geometry, context);
                }
                else
                {
                    var style = _styleEngine?.GetStyle(feature, context.Zoom)
                               ?? feature.Style
                               ?? GetDefaultStyleForGeometry(feature.Geometry);
                    RenderGeometry(feature.Geometry, context, style);
                }
            }
            catch
            {
                // 개별 피처 렌더링 오류는 무시하고 계속 진행
            }
        }
    }

    /// <summary>
    /// 배칭을 사용한 피처 렌더링 (성능 최적화)
    /// </summary>
    private void RenderFeaturesWithBatching(IEnumerable<Data.IFeature> features, RenderContext context, LodUtils.LODLevel lodLevel, double resolution, double minPixelSize)
    {
        _batcher ??= new GeometryBatcher();

        var layerStyle = context.LayerStyle!;

        // 투명도 적용된 색상
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

        int processedCount = 0;
        int skippedCount = 0;

        foreach (var feature in features)
        {
            try
            {
                if (feature?.Geometry == null)
                {
                    skippedCount++;
                    continue;
                }

                // 뷰포트 컬링
                var envelope = feature.Geometry.Envelope;
                if (envelope != null && !envelope.IsNull &&
                    !ViewportCulling.IsGeometryVisible(feature.Geometry, context.ViewExtent))
                {
                    skippedCount++;
                    continue;
                }

                // 최소 픽셀 크기 필터 (포인트 제외)
                var geoEnvelope = feature.Geometry.Envelope;
                if (feature.Geometry.GeometryType != Engine.Geometry.GeometryType.Point &&
                    feature.Geometry.GeometryType != Engine.Geometry.GeometryType.MultiPoint &&
                    geoEnvelope != null)
                {
                    var w = geoEnvelope.Width / resolution;
                    var h = geoEnvelope.Height / resolution;
                    if (w < minPixelSize && h < minPixelSize)
                    {
                        skippedCount++;
                        continue;
                    }
                }

                // LOD 체크
                if (!LodUtils.ShouldRenderGeometry(feature.Geometry, context, lodLevel))
                {
                    skippedCount++;
                    continue;
                }

                // 지오메트리 타입별 배칭
                BatchGeometry(feature.Geometry, context, fillColor, strokeColor, layerStyle);
                processedCount++;
            }
            catch
            {
                skippedCount++;
            }
        }

        // 배치된 모든 지오메트리를 한번에 렌더링
        _batcher.Flush(context.DrawingContext);
    }

    /// <summary>
    /// 지오메트리를 배치에 추가
    /// </summary>
    private void BatchGeometry(
        Geometry.IGeometry geometry,
        RenderContext context,
        Color fillColor,
        Color strokeColor,
        LayerRenderStyle layerStyle)
    {
        if (_batcher == null) return;

        switch (geometry)
        {
            case Geometry.Point point:
                _batcher.AddPoint(
                    point.Coordinate.X, point.Coordinate.Y,
                    context, fillColor, strokeColor,
                    layerStyle.PointSize, layerStyle.StrokeWidth, layerStyle.Opacity);
                break;

            case Geometry.LineString lineString:
                if (lineString.Coordinates?.Length >= 2)
                {
                    _batcher.AddLine(
                        lineString.Coordinates,
                        context, strokeColor,
                        layerStyle.StrokeWidth, layerStyle.DashPattern);
                }
                break;

            case Geometry.Polygon polygon:
                if (polygon.ExteriorRing?.Coordinates?.Length >= 3)
                {
                    var holes = polygon.InteriorRings?
                        .Where(h => h?.Coordinates?.Length >= 3)
                        .Select(h => h.Coordinates)
                        .ToList();

                    _batcher.AddPolygon(
                        polygon.ExteriorRing.Coordinates,
                        holes,
                        context, fillColor, strokeColor,
                        layerStyle.StrokeWidth, layerStyle.Opacity);
                }
                break;

            case Geometry.MultiPoint multiPoint:
                foreach (var pt in multiPoint.Geometries.Cast<Geometry.Point>())
                {
                    _batcher.AddPoint(
                        pt.Coordinate.X, pt.Coordinate.Y,
                        context, fillColor, strokeColor,
                        layerStyle.PointSize, layerStyle.StrokeWidth, layerStyle.Opacity);
                }
                break;

            case Geometry.MultiLineString multiLineString:
                foreach (var line in multiLineString.Geometries.Cast<Geometry.LineString>())
                {
                    if (line.Coordinates?.Length >= 2)
                    {
                        _batcher.AddLine(
                            line.Coordinates,
                            context, strokeColor,
                            layerStyle.StrokeWidth, layerStyle.DashPattern);
                    }
                }
                break;

            case Geometry.MultiPolygon multiPolygon:
                foreach (var poly in multiPolygon.Geometries.Cast<Geometry.Polygon>())
                {
                    if (poly.ExteriorRing?.Coordinates?.Length >= 3)
                    {
                        var holes = poly.InteriorRings?
                            .Where(h => h?.Coordinates?.Length >= 3)
                            .Select(h => h.Coordinates)
                            .ToList();

                        _batcher.AddPolygon(
                            poly.ExteriorRing.Coordinates,
                            holes,
                            context, fillColor, strokeColor,
                            layerStyle.StrokeWidth, layerStyle.Opacity);
                    }
                }
                break;
        }
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
                    if (LodUtils.ShouldRenderGeometry(feature.Geometry, context, (LodUtils.LODLevel)lodLevel))
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
                    if (LodUtils.ShouldRenderGeometry(feature.Geometry, context, (LodUtils.LODLevel)lodLevel))
                    {
                        RenderFeature(feature, context, (LodUtils.LODLevel)lodLevel);
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
        if (exterior == null || exterior.Length < 3) return null;

        // PathGeometry를 사용하여 더 명확하게 홀 처리
        var pathGeometry = new PathGeometry();
        // 홀(hole)이 있는 폴리곤을 올바르게 렌더링하기 위해 EvenOdd 규칙 사용
        pathGeometry.FillRule = FillRule.EvenOdd;
        
        // 외곽 링 Figure 생성
        var exteriorFigure = new PathFigure
        {
            StartPoint = exterior[0],
            IsClosed = true,
            IsFilled = true
        };
        
        // 외곽 링의 나머지 점들 추가
        if (exterior.Length > 1)
        {
            var exteriorSegment = new PolyLineSegment(exterior.Skip(1), true);
            exteriorFigure.Segments.Add(exteriorSegment);
        }
        pathGeometry.Figures.Add(exteriorFigure);

        // 내부 링(홀) 그리기 - 각 홀은 별도의 Figure로 추가
        if (polygon.InteriorRings != null && polygon.InteriorRings.Count > 0)
        {
            foreach (var hole in polygon.InteriorRings)
            {
                if (hole?.Coordinates == null || hole.Coordinates.Length < 3) continue;
                
                var holePoints = context.ConvertToScreenPoints(hole.Coordinates);
                if (holePoints == null || holePoints.Length < 3) continue;
                
                // 홀 Figure 생성 - EvenOdd 규칙에 의해 이 영역은 비워짐
                var holeFigure = new PathFigure
                {
                    StartPoint = holePoints[0],
                    IsClosed = true,
                    IsFilled = true  // EvenOdd에서는 true여도 홀로 처리됨
                };
                
                if (holePoints.Length > 1)
                {
                    var holeSegment = new PolyLineSegment(holePoints.Skip(1), true);
                    holeFigure.Segments.Add(holeSegment);
                }
                pathGeometry.Figures.Add(holeFigure);
            }
        }
        
        pathGeometry.Freeze();
        return pathGeometry;
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
        // 모든 폴리곤을 단일 PathGeometry에 추가하여 홀 처리를 일관되게 함
        var pathGeometry = new PathGeometry();
        pathGeometry.FillRule = FillRule.EvenOdd;
        
        foreach (var poly in multiPolygon.Geometries)
        {
            if (poly?.ExteriorRing == null) continue;
            
            var exterior = context.ConvertToScreenPoints(poly.ExteriorRing.Coordinates);
            if (exterior == null || exterior.Length < 3) continue;
            
            // 외곽 링 Figure 생성
            var exteriorFigure = new PathFigure
            {
                StartPoint = exterior[0],
                IsClosed = true,
                IsFilled = true
            };
            
            if (exterior.Length > 1)
            {
                var exteriorSegment = new PolyLineSegment(exterior.Skip(1), true);
                exteriorFigure.Segments.Add(exteriorSegment);
            }
            pathGeometry.Figures.Add(exteriorFigure);
            
            // 내부 링(홀) 추가
            if (poly.InteriorRings != null && poly.InteriorRings.Count > 0)
            {
                foreach (var hole in poly.InteriorRings)
                {
                    if (hole?.Coordinates == null || hole.Coordinates.Length < 3) continue;
                    
                    var holePoints = context.ConvertToScreenPoints(hole.Coordinates);
                    if (holePoints == null || holePoints.Length < 3) continue;
                    
                    var holeFigure = new PathFigure
                    {
                        StartPoint = holePoints[0],
                        IsClosed = true,
                        IsFilled = true
                    };
                    
                    if (holePoints.Length > 1)
                    {
                        var holeSegment = new PolyLineSegment(holePoints.Skip(1), true);
                        holeFigure.Segments.Add(holeSegment);
                    }
                    pathGeometry.Figures.Add(holeFigure);
                }
            }
        }
        
        if (pathGeometry.Figures.Count == 0) return null;
        
        pathGeometry.Freeze();
        return pathGeometry;
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
        RenderFeature(feature, context, LodUtils.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 피처 렌더링
    /// </summary>
    public void RenderFeature(Data.IFeature feature, RenderContext context, LodUtils.LODLevel lodLevel)
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
    /// 레이어 스타일을 사용하여 빠른 지오메트리 렌더링 (LOD 체크 없음)
    /// </summary>
    private void RenderGeometryWithLayerStyleFast(Geometry.IGeometry geometry, RenderContext context)
    {
        if (geometry == null || context?.DrawingContext == null || context.LayerStyle == null) return;

        var layerStyle = context.LayerStyle;

        // 캐시된 Brush/Pen 사용 (투명도 적용)
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
                RenderPointFast(point, context, fillColor, strokeColor, layerStyle);
                break;

            case Geometry.LineString lineString:
                RenderLineStringFast(lineString, context, strokeColor, layerStyle);
                break;

            case Geometry.Polygon polygon:
                RenderPolygonFast(polygon, context, fillColor, strokeColor, layerStyle);
                break;

            case Geometry.MultiPoint multiPoint:
                foreach (var pt in multiPoint.Geometries)
                    RenderPointFast(pt, context, fillColor, strokeColor, layerStyle);
                break;

            case Geometry.MultiLineString multiLineString:
                foreach (var line in multiLineString.Geometries)
                    RenderLineStringFast(line, context, strokeColor, layerStyle);
                break;

            case Geometry.MultiPolygon multiPolygon:
                foreach (var poly in multiPolygon.Geometries)
                    RenderPolygonFast(poly, context, fillColor, strokeColor, layerStyle);
                break;
        }
    }

    /// <summary>
    /// 빠른 포인트 렌더링
    /// </summary>
    private void RenderPointFast(Geometry.Point point, RenderContext context, Color fillColor, Color strokeColor, LayerRenderStyle layerStyle)
    {
        var screenPoint = context.FastMapToScreen(point.Coordinate.X, point.Coordinate.Y);
        var brush = GetCachedBrush(fillColor);
        var pen = layerStyle.StrokeWidth > 0 ? GetCachedPen(strokeColor, layerStyle.StrokeWidth) : null;
        var halfSize = layerStyle.PointSize / 2;

        // 기본 원 렌더링 (심볼 타입 무시 - 성능 우선)
        context.DrawingContext.DrawEllipse(brush, pen, screenPoint, halfSize, halfSize);
    }

    /// <summary>
    /// 빠른 라인 렌더링
    /// </summary>
    private void RenderLineStringFast(Geometry.LineString lineString, RenderContext context, Color strokeColor, LayerRenderStyle layerStyle)
    {
        if (lineString.Coordinates == null || lineString.Coordinates.Length < 2) return;

        var pen = GetCachedDashedPen(strokeColor, layerStyle.StrokeWidth, layerStyle.DashPattern);

        // StreamGeometry로 효율적 렌더링
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var c = lineString.Coordinates[0];
            ctx.BeginFigure(context.FastMapToScreen(c.X, c.Y), false, false);

            for (int i = 1; i < lineString.Coordinates.Length; i++)
            {
                c = lineString.Coordinates[i];
                ctx.LineTo(context.FastMapToScreen(c.X, c.Y), true, false);
            }
        }
        geometry.Freeze();
        context.DrawingContext.DrawGeometry(null, pen, geometry);
    }

    /// <summary>
    /// 빠른 폴리곤 렌더링
    /// </summary>
    private void RenderPolygonFast(Geometry.Polygon polygon, RenderContext context, Color fillColor, Color strokeColor, LayerRenderStyle layerStyle)
    {
        if (polygon.ExteriorRing?.Coordinates == null || polygon.ExteriorRing.Coordinates.Length < 3) return;

        var brush = layerStyle.EnableFill ? GetCachedBrush(fillColor) : null;
        var pen = layerStyle.EnableStroke && layerStyle.StrokeWidth > 0 ? GetCachedPen(strokeColor, layerStyle.StrokeWidth) : null;

        // StreamGeometry로 효율적 렌더링
        var geometry = new StreamGeometry();
        geometry.FillRule = FillRule.EvenOdd;

        using (var ctx = geometry.Open())
        {
            // 외부 링
            var exteriorCoords = polygon.ExteriorRing.Coordinates;
            var c = exteriorCoords[0];
            ctx.BeginFigure(context.FastMapToScreen(c.X, c.Y), true, true);

            for (int i = 1; i < exteriorCoords.Length; i++)
            {
                c = exteriorCoords[i];
                ctx.LineTo(context.FastMapToScreen(c.X, c.Y), true, false);
            }

            // 내부 링 (홀)
            if (polygon.InteriorRings != null)
            {
                foreach (var hole in polygon.InteriorRings)
                {
                    if (hole?.Coordinates == null || hole.Coordinates.Length < 3) continue;

                    c = hole.Coordinates[0];
                    ctx.BeginFigure(context.FastMapToScreen(c.X, c.Y), true, true);

                    for (int i = 1; i < hole.Coordinates.Length; i++)
                    {
                        c = hole.Coordinates[i];
                        ctx.LineTo(context.FastMapToScreen(c.X, c.Y), true, false);
                    }
                }
            }
        }
        geometry.Freeze();
        context.DrawingContext.DrawGeometry(brush, pen, geometry);
    }

    /// <summary>
    /// 레이어 스타일을 사용하여 지오메트리 렌더링
    /// </summary>
    private void RenderGeometryWithLayerStyle(Geometry.IGeometry geometry, RenderContext context, LodUtils.LODLevel lodLevel)
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
        // 성능 최적화: FastMapToScreen 사용
        var screenPoint = context.FastMapToScreen(point.Coordinate.X, point.Coordinate.Y);
        var brush = GetCachedBrush(fillColor);
        var pen = strokeWidth > 0 ? GetCachedPen(strokeColor, strokeWidth) : null;
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
        
        // 캐시된 Pen 사용
        var pen = GetCachedDashedPen(strokeColor, strokeWidth, dashPattern);
        
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // 성능 최적화: FastMapToScreen 사용
            var c = lineString.Coordinates[0];
            ctx.BeginFigure(context.FastMapToScreen(c.X, c.Y), false, false);
            
            for (int i = 1; i < lineString.Coordinates.Length; i++)
            {
                c = lineString.Coordinates[i];
                ctx.LineTo(context.FastMapToScreen(c.X, c.Y), true, false);
            }
        }
        geometry.Freeze(); // Freeze로 성능 향상
        
        context.DrawingContext.DrawGeometry(null, pen, geometry);
    }
    
    /// <summary>
    /// 스타일을 적용하여 폴리곤 렌더링
    /// </summary>
    private void RenderPolygonWithStyle(Geometry.Polygon polygon, RenderContext context, Color fillColor, Color strokeColor, double strokeWidth, bool enableFill, bool enableStroke)
    {
        if (polygon.ExteriorRing == null || polygon.ExteriorRing.Coordinates == null || polygon.ExteriorRing.Coordinates.Length < 3) return;
        
        // 캐시된 Brush/Pen 사용
        var brush = enableFill ? GetCachedBrush(fillColor) : null;
        var pen = enableStroke && strokeWidth > 0 ? GetCachedPen(strokeColor, strokeWidth) : null;
        
        // PathGeometry를 사용하여 홀 처리를 명확하게 함
        var pathGeometry = new PathGeometry();
        pathGeometry.FillRule = FillRule.EvenOdd;
        
        // 외부 링 Figure 생성 - 성능 최적화: FastMapToScreen 사용
        var exteriorCoords = polygon.ExteriorRing.Coordinates;
        var c = exteriorCoords[0];
        var exteriorFigure = new PathFigure
        {
            StartPoint = context.FastMapToScreen(c.X, c.Y),
            IsClosed = true,
            IsFilled = true
        };
        
        // 외부 링의 나머지 점들 추가
        for (int i = 1; i < exteriorCoords.Length; i++)
        {
            c = exteriorCoords[i];
            exteriorFigure.Segments.Add(new LineSegment(context.FastMapToScreen(c.X, c.Y), true));
        }
        pathGeometry.Figures.Add(exteriorFigure);
        
        // 내부 링 (홀) - 각 홀은 별도의 Figure로 추가
        if (polygon.InteriorRings != null && polygon.InteriorRings.Count > 0)
        {
            foreach (var hole in polygon.InteriorRings)
            {
                if (hole?.Coordinates == null || hole.Coordinates.Length < 3) continue;
                
                c = hole.Coordinates[0];
                var holeFigure = new PathFigure
                {
                    StartPoint = context.FastMapToScreen(c.X, c.Y),
                    IsClosed = true,
                    IsFilled = true  // EvenOdd에서는 true여도 홀로 처리됨
                };
                
                for (int i = 1; i < hole.Coordinates.Length; i++)
                {
                    c = hole.Coordinates[i];
                    holeFigure.Segments.Add(new LineSegment(context.FastMapToScreen(c.X, c.Y), true));
                }
                pathGeometry.Figures.Add(holeFigure);
            }
        }
        
        pathGeometry.Freeze(); // Freeze로 성능 향상
        context.DrawingContext.DrawGeometry(brush, pen, pathGeometry);
    }

    /// <inheritdoc/>
    public void RenderGeometry(Geometry.IGeometry geometry, RenderContext context, Styling.IStyle? style = null)
    {
        RenderGeometry(geometry, context, style, LodUtils.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 지오메트리 렌더링
    /// </summary>
    public void RenderGeometry(Geometry.IGeometry geometry, RenderContext context, Styling.IStyle? style, LodUtils.LODLevel lodLevel)
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
        RenderPoint(point, context, style, LodUtils.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 포인트 렌더링
    /// </summary>
    public void RenderPoint(Geometry.Point point, RenderContext context, Styling.IPointStyle? style, LodUtils.LODLevel lodLevel)
    {
        if (point?.Coordinate == null || context?.DrawingContext == null) return;

        style ??= Styling.DefaultStyles.DefaultPoint;
        if (!IsStyleVisible(style, context.Zoom)) return;

        // LOD에 따른 심볼 렌더링 여부 확인
        if (!LodUtils.ShouldRenderSymbol(lodLevel, style.Size)) return;

        var screenPoint = context.MapToScreen(point.Coordinate);
        
        // 화면 범위 체크
        if (!IsPointInScreen(screenPoint, context.ScreenSize, style.Size)) return;

        var brush = new SolidColorBrush(style.Fill);
        var pen = style.StrokeWidth > 0 ? new Pen(new SolidColorBrush(style.Stroke), style.StrokeWidth) : null;

        // LOD에 따른 간소화된 렌더링
        var effectiveShape = lodLevel >= LodUtils.LODLevel.Medium ? Styling.PointShape.Circle : style.Shape;

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
        RenderLineString(lineString, context, style, LodUtils.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 라인스트링 렌더링
    /// </summary>
    public void RenderLineString(Geometry.LineString lineString, RenderContext context, Styling.ILineStyle? style, LodUtils.LODLevel lodLevel)
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
            
            if (LodUtils.ShouldSimplifyLine(lodLevel, totalLength, coordinates.Length))
            {
                var tolerance = LodUtils.GetSimplificationTolerance(lodLevel, context.Resolution);
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
        RenderPolygon(polygon, context, style, LodUtils.CalculateLODLevel(context.Zoom));
    }
    
    /// <summary>
    /// LOD를 고려한 폴리곤 렌더링
    /// </summary>
    public void RenderPolygon(Geometry.Polygon polygon, RenderContext context, Styling.IPolygonStyle? style, LodUtils.LODLevel lodLevel)
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
            var tolerance = LodUtils.GetSimplificationTolerance(lodLevel, context.Resolution);
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
        if (lodLevel <= LodUtils.LODLevel.High && polygon.InteriorRings != null)
        {
            foreach (var hole in polygon.InteriorRings)
            {
                if (hole.Coordinates?.Length >= 3)
                {
                    var holeCoords = hole.Coordinates;
                    
                    // 구멍도 단순화
                    var tolerance = LodUtils.GetSimplificationTolerance(lodLevel, context.Resolution);
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
    
    #region 라벨 렌더링
    
    /// <summary>
    /// 피처들의 라벨 렌더링
    /// </summary>
    public void RenderLabels(IEnumerable<Data.IFeature> features, RenderContext context, Styling.ILabelStyle labelStyle)
    {
        if (features == null || context?.DrawingContext == null || labelStyle == null) return;
        if (string.IsNullOrEmpty(labelStyle.LabelField)) return;
        if (!labelStyle.IsVisible) return;
        
        // 줌 레벨 체크
        if (context.Zoom < labelStyle.MinZoom || context.Zoom > labelStyle.MaxZoom) return;
        
        var featureList = features.ToList();
        if (!featureList.Any()) return;
        
        // 라벨 충돌 감지를 위한 배치된 라벨 영역 목록
        var placedLabelBounds = new List<Rect>();
        
        // 우선순위에 따라 정렬 (높은 우선순위 먼저)
        var sortedFeatures = featureList
            .OrderByDescending(f => labelStyle.Priority)
            .ToList();
        
        foreach (var feature in sortedFeatures)
        {
            try
            {
                if (feature.Geometry == null) continue;
                
                // 뷰포트 컬링
                if (!context.IsVisible(feature.Geometry)) continue;
                
                // 라벨 텍스트 가져오기
                var labelText = GetLabelText(feature, labelStyle.LabelField);
                if (string.IsNullOrWhiteSpace(labelText)) continue;
                
                // 라벨 위치 계산
                var labelPosition = CalculateLabelPosition(feature.Geometry, context, labelStyle);
                if (labelPosition == null) continue;
                
                // 라벨 크기 계산
                var labelBounds = CalculateLabelBounds(labelText, labelPosition.Value, labelStyle);
                
                // 충돌 감지 (AllowOverlap이 false인 경우)
                if (!labelStyle.AllowOverlap)
                {
                    if (IsLabelOverlapping(labelBounds, placedLabelBounds))
                        continue;
                }
                
                // 라벨 렌더링
                RenderLabel(context.DrawingContext, labelText, labelPosition.Value, labelStyle);
                
                // 배치된 라벨 영역 추가
                placedLabelBounds.Add(labelBounds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Label rendering error: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 단일 피처의 라벨 렌더링
    /// </summary>
    public void RenderFeatureLabel(Data.IFeature feature, RenderContext context, Styling.ILabelStyle labelStyle)
    {
        if (feature?.Geometry == null || context?.DrawingContext == null || labelStyle == null) return;
        if (string.IsNullOrEmpty(labelStyle.LabelField)) return;
        
        var labelText = GetLabelText(feature, labelStyle.LabelField);
        if (string.IsNullOrWhiteSpace(labelText)) return;
        
        var labelPosition = CalculateLabelPosition(feature.Geometry, context, labelStyle);
        if (labelPosition == null) return;
        
        RenderLabel(context.DrawingContext, labelText, labelPosition.Value, labelStyle);
    }
    
    /// <summary>
    /// 피처에서 라벨 텍스트 가져오기
    /// </summary>
    private string? GetLabelText(Data.IFeature feature, string fieldName)
    {
        if (feature.Attributes == null) return null;
        
        // 인덱서를 통해 속성값 가져오기
        if (!feature.Attributes.Exists(fieldName)) return null;
        
        var value = feature.Attributes[fieldName];
        return value?.ToString();
    }
    
    /// <summary>
    /// 지오메트리 타입에 따른 라벨 위치 계산
    /// </summary>
    private Point? CalculateLabelPosition(Geometry.IGeometry geometry, RenderContext context, Styling.ILabelStyle labelStyle)
    {
        Geometry.ICoordinate? centroid = null;
        
        switch (geometry)
        {
            case Geometry.Point point:
                centroid = point.Coordinate;
                break;
                
            case Geometry.LineString lineString:
                centroid = CalculateLineCentroid(lineString);
                break;
                
            case Geometry.Polygon polygon:
                centroid = CalculatePolygonCentroid(polygon);
                break;
                
            case Geometry.MultiPoint multiPoint:
                centroid = CalculateMultiPointCentroid(multiPoint);
                break;
                
            case Geometry.MultiLineString multiLineString:
                centroid = CalculateMultiLineStringCentroid(multiLineString);
                break;
                
            case Geometry.MultiPolygon multiPolygon:
                centroid = CalculateMultiPolygonCentroid(multiPolygon);
                break;
                
            default:
                if (geometry.Envelope != null)
                {
                    centroid = new Geometry.Coordinate(
                        (geometry.Envelope.MinX + geometry.Envelope.MaxX) / 2,
                        (geometry.Envelope.MinY + geometry.Envelope.MaxY) / 2);
                }
                break;
        }
        
        if (centroid == null) return null;
        
        // 화면 좌표로 변환
        var screenPoint = context.MapToScreen(centroid);
        
        // 오프셋 적용
        screenPoint.X += labelStyle.OffsetX;
        screenPoint.Y += labelStyle.OffsetY;
        
        // 배치 위치에 따른 조정
        // (실제 조정은 RenderLabel에서 텍스트 크기를 알고 난 후 수행)
        
        return screenPoint;
    }
    
    /// <summary>
    /// 라인의 중심점 계산
    /// </summary>
    private Geometry.ICoordinate? CalculateLineCentroid(Geometry.LineString lineString)
    {
        if (lineString.Coordinates == null || lineString.Coordinates.Length == 0)
            return null;
        
        // 라인의 중간 지점 찾기
        double totalLength = 0;
        var lengths = new double[lineString.Coordinates.Length - 1];
        
        for (int i = 0; i < lineString.Coordinates.Length - 1; i++)
        {
            var dx = lineString.Coordinates[i + 1].X - lineString.Coordinates[i].X;
            var dy = lineString.Coordinates[i + 1].Y - lineString.Coordinates[i].Y;
            lengths[i] = Math.Sqrt(dx * dx + dy * dy);
            totalLength += lengths[i];
        }
        
        var halfLength = totalLength / 2;
        double accumulatedLength = 0;
        
        for (int i = 0; i < lengths.Length; i++)
        {
            if (accumulatedLength + lengths[i] >= halfLength)
            {
                var ratio = (halfLength - accumulatedLength) / lengths[i];
                var x = lineString.Coordinates[i].X + ratio * (lineString.Coordinates[i + 1].X - lineString.Coordinates[i].X);
                var y = lineString.Coordinates[i].Y + ratio * (lineString.Coordinates[i + 1].Y - lineString.Coordinates[i].Y);
                return new Geometry.Coordinate(x, y);
            }
            accumulatedLength += lengths[i];
        }
        
        // 폴백: 중간 인덱스의 좌표
        var midIndex = lineString.Coordinates.Length / 2;
        return lineString.Coordinates[midIndex];
    }
    
    /// <summary>
    /// 폴리곤의 중심점 계산 (Centroid)
    /// </summary>
    private Geometry.ICoordinate? CalculatePolygonCentroid(Geometry.Polygon polygon)
    {
        if (polygon.ExteriorRing?.Coordinates == null || polygon.ExteriorRing.Coordinates.Length < 3)
            return null;
        
        var coords = polygon.ExteriorRing.Coordinates;
        double signedArea = 0;
        double cx = 0;
        double cy = 0;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            var x0 = coords[i].X;
            var y0 = coords[i].Y;
            var x1 = coords[i + 1].X;
            var y1 = coords[i + 1].Y;
            
            var a = x0 * y1 - x1 * y0;
            signedArea += a;
            cx += (x0 + x1) * a;
            cy += (y0 + y1) * a;
        }
        
        signedArea *= 0.5;
        
        if (Math.Abs(signedArea) < 1e-10)
        {
            // 면적이 너무 작으면 바운딩 박스 중심 사용
            return new Geometry.Coordinate(
                (polygon.Envelope?.MinX ?? 0 + polygon.Envelope?.MaxX ?? 0) / 2,
                (polygon.Envelope?.MinY ?? 0 + polygon.Envelope?.MaxY ?? 0) / 2);
        }
        
        cx /= (6 * signedArea);
        cy /= (6 * signedArea);
        
        return new Geometry.Coordinate(cx, cy);
    }
    
    /// <summary>
    /// MultiPoint의 중심점 계산
    /// </summary>
    private Geometry.ICoordinate? CalculateMultiPointCentroid(Geometry.MultiPoint multiPoint)
    {
        if (multiPoint.Geometries == null || !multiPoint.Geometries.Any())
            return null;
        
        double sumX = 0, sumY = 0;
        int count = 0;
        
        foreach (var point in multiPoint.Geometries)
        {
            if (point.Coordinate != null)
            {
                sumX += point.Coordinate.X;
                sumY += point.Coordinate.Y;
                count++;
            }
        }
        
        if (count == 0) return null;
        return new Geometry.Coordinate(sumX / count, sumY / count);
    }
    
    /// <summary>
    /// MultiLineString의 중심점 계산
    /// </summary>
    private Geometry.ICoordinate? CalculateMultiLineStringCentroid(Geometry.MultiLineString multiLineString)
    {
        if (multiLineString.Geometries == null || !multiLineString.Geometries.Any())
            return null;
        
        // 가장 긴 라인의 중심점 사용
        Geometry.LineString? longestLine = null;
        double maxLength = 0;
        
        foreach (var line in multiLineString.Geometries)
        {
            if (line.Coordinates == null || line.Coordinates.Length < 2) continue;
            
            double length = 0;
            for (int i = 0; i < line.Coordinates.Length - 1; i++)
            {
                var dx = line.Coordinates[i + 1].X - line.Coordinates[i].X;
                var dy = line.Coordinates[i + 1].Y - line.Coordinates[i].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }
            
            if (length > maxLength)
            {
                maxLength = length;
                longestLine = line;
            }
        }
        
        return longestLine != null ? CalculateLineCentroid(longestLine) : null;
    }
    
    /// <summary>
    /// MultiPolygon의 중심점 계산
    /// </summary>
    private Geometry.ICoordinate? CalculateMultiPolygonCentroid(Geometry.MultiPolygon multiPolygon)
    {
        if (multiPolygon.Geometries == null || !multiPolygon.Geometries.Any())
            return null;
        
        // 가장 큰 폴리곤의 중심점 사용
        Geometry.Polygon? largestPolygon = null;
        double maxArea = 0;
        
        foreach (var polygon in multiPolygon.Geometries)
        {
            if (polygon.ExteriorRing?.Coordinates == null || polygon.ExteriorRing.Coordinates.Length < 3)
                continue;
            
            var area = Math.Abs(CalculatePolygonArea(polygon));
            if (area > maxArea)
            {
                maxArea = area;
                largestPolygon = polygon;
            }
        }
        
        return largestPolygon != null ? CalculatePolygonCentroid(largestPolygon) : null;
    }
    
    /// <summary>
    /// 폴리곤 면적 계산 (Shoelace formula)
    /// </summary>
    private double CalculatePolygonArea(Geometry.Polygon polygon)
    {
        if (polygon.ExteriorRing?.Coordinates == null || polygon.ExteriorRing.Coordinates.Length < 3)
            return 0;
        
        var coords = polygon.ExteriorRing.Coordinates;
        double area = 0;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            area += coords[i].X * coords[i + 1].Y;
            area -= coords[i + 1].X * coords[i].Y;
        }
        
        return area / 2;
    }
    
    /// <summary>
    /// 라벨 바운딩 박스 계산
    /// </summary>
    private Rect CalculateLabelBounds(string text, Point position, Styling.ILabelStyle labelStyle)
    {
        var formattedText = CreateFormattedText(text, labelStyle);
        var width = formattedText.Width;
        var height = formattedText.Height;
        
        // 배치 위치에 따른 바운딩 박스 조정
        double x = position.X;
        double y = position.Y;
        
        switch (labelStyle.Placement)
        {
            case Styling.LabelPlacement.Center:
                x -= width / 2;
                y -= height / 2;
                break;
            case Styling.LabelPlacement.Top:
                x -= width / 2;
                y -= height;
                break;
            case Styling.LabelPlacement.Bottom:
                x -= width / 2;
                break;
            case Styling.LabelPlacement.Left:
                x -= width;
                y -= height / 2;
                break;
            case Styling.LabelPlacement.Right:
                y -= height / 2;
                break;
            case Styling.LabelPlacement.TopLeft:
                x -= width;
                y -= height;
                break;
            case Styling.LabelPlacement.TopRight:
                y -= height;
                break;
            case Styling.LabelPlacement.BottomLeft:
                x -= width;
                break;
            case Styling.LabelPlacement.BottomRight:
                // 기본 위치
                break;
        }
        
        // 헤일로 여백 추가
        if (labelStyle.HaloEnabled)
        {
            var haloMargin = labelStyle.HaloWidth;
            x -= haloMargin;
            y -= haloMargin;
            width += haloMargin * 2;
            height += haloMargin * 2;
        }
        
        return new Rect(x, y, width, height);
    }
    
    /// <summary>
    /// 라벨 충돌 감지
    /// </summary>
    private bool IsLabelOverlapping(Rect labelBounds, List<Rect> placedBounds)
    {
        foreach (var placed in placedBounds)
        {
            if (labelBounds.IntersectsWith(placed))
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// 라벨 렌더링
    /// </summary>
    private void RenderLabel(DrawingContext dc, string text, Point position, Styling.ILabelStyle labelStyle)
    {
        var formattedText = CreateFormattedText(text, labelStyle);
        
        // 배치 위치에 따른 좌표 조정
        var drawPosition = AdjustPositionForPlacement(position, formattedText, labelStyle.Placement);
        
        // 회전 적용
        if (Math.Abs(labelStyle.Rotation) > 0.01)
        {
            dc.PushTransform(new RotateTransform(labelStyle.Rotation, position.X, position.Y));
        }
        
        // 헤일로 (외곽선) 렌더링
        if (labelStyle.HaloEnabled && labelStyle.HaloWidth > 0)
        {
            var haloGeometry = formattedText.BuildGeometry(drawPosition);
            var haloPen = new Pen(new SolidColorBrush(labelStyle.HaloColor), labelStyle.HaloWidth * 2)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawGeometry(null, haloPen, haloGeometry);
        }
        
        // 텍스트 렌더링
        dc.DrawText(formattedText, drawPosition);
        
        // 회전 복원
        if (Math.Abs(labelStyle.Rotation) > 0.01)
        {
            dc.Pop();
        }
    }
    
    /// <summary>
    /// FormattedText 생성
    /// </summary>
    private FormattedText CreateFormattedText(string text, Styling.ILabelStyle labelStyle)
    {
        var typeface = new Typeface(
            labelStyle.FontFamily,
            labelStyle.FontStyle,
            labelStyle.FontWeight,
            FontStretches.Normal);
        
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            labelStyle.FontSize,
            new SolidColorBrush(labelStyle.FontColor),
            VisualTreeHelper.GetDpi(new System.Windows.Controls.Control()).PixelsPerDip);
        
        return formattedText;
    }
    
    /// <summary>
    /// 배치 위치에 따른 좌표 조정
    /// </summary>
    private Point AdjustPositionForPlacement(Point position, FormattedText text, Styling.LabelPlacement placement)
    {
        var width = text.Width;
        var height = text.Height;
        
        return placement switch
        {
            Styling.LabelPlacement.Center => new Point(position.X - width / 2, position.Y - height / 2),
            Styling.LabelPlacement.Top => new Point(position.X - width / 2, position.Y - height),
            Styling.LabelPlacement.Bottom => new Point(position.X - width / 2, position.Y),
            Styling.LabelPlacement.Left => new Point(position.X - width, position.Y - height / 2),
            Styling.LabelPlacement.Right => new Point(position.X, position.Y - height / 2),
            Styling.LabelPlacement.TopLeft => new Point(position.X - width, position.Y - height),
            Styling.LabelPlacement.TopRight => new Point(position.X, position.Y - height),
            Styling.LabelPlacement.BottomLeft => new Point(position.X - width, position.Y),
            Styling.LabelPlacement.BottomRight => new Point(position.X, position.Y),
            _ => new Point(position.X - width / 2, position.Y - height / 2)
        };
    }
    
    #endregion
}