using System.Drawing;
using System.Drawing.Drawing2D;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Geometry3D;
using SpatialView.Engine.Styling;
using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;
using GeomPoint = SpatialView.Engine.Geometry.Point;

namespace SpatialView.Engine.Rendering3D;

/// <summary>
/// 3D 렌더러
/// </summary>
public class Renderer3D
{
    private readonly Camera3D _camera;
    private readonly Renderer3DOptions _options;
    private Graphics? _graphics;
    private int _width;
    private int _height;
    private readonly Dictionary<int, double> _depthBuffer;
    
    public Camera3D Camera => _camera;
    public Renderer3DOptions Options => _options;
    
    public Renderer3D(Camera3D camera, Renderer3DOptions? options = null)
    {
        _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        _options = options ?? new Renderer3DOptions();
        _depthBuffer = new Dictionary<int, double>();
    }
    
    /// <summary>
    /// 렌더링 시작
    /// </summary>
    public void BeginRender(Graphics graphics, int width, int height)
    {
        _graphics = graphics;
        _width = width;
        _height = height;
        
        // 카메라 종횡비 업데이트
        _camera.AspectRatio = (double)width / height;
        
        // 깊이 버퍼 초기화
        _depthBuffer.Clear();
        
        // 안티앨리어싱 설정
        if (_options.EnableAntialiasing)
        {
            _graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            _graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        
        // 배경색 설정
        if (_options.BackgroundColor.HasValue)
        {
            _graphics.Clear(_options.BackgroundColor.Value);
        }
    }
    
    /// <summary>
    /// 3D 지오메트리 렌더링
    /// </summary>
    public void RenderGeometry3D(IGeometry3D geometry, IStyle style)
    {
        if (_graphics == null || geometry == null || style == null) return;
        
        if (geometry is IGeometry baseGeom)
        {
            switch (baseGeom.GeometryType)
            {
            case GeometryType.Point:
                RenderPoint3D((Point3D)geometry, style);
                break;
            case GeometryType.LineString:
                RenderLineString3D((LineString3D)geometry, style);
                break;
            case GeometryType.Polygon:
                RenderPolygon3D((Polygon3D)geometry, style);
                break;
            case GeometryType.GeometryCollection:
                RenderGeometryCollection3D((GeometryCollection3D)geometry, style);
                break;
            }
        }
    }
    
    /// <summary>
    /// 3D 피처 렌더링
    /// </summary>
    public void RenderFeature3D(IFeature feature, IStyle style)
    {
        if (feature.Geometry is IGeometry3D geom3D)
        {
            RenderGeometry3D(geom3D, style);
        }
        else if (feature.Geometry != null && feature.Attributes != null)
        {
            // 2D 지오메트리를 3D로 변환 (Z값이 속성에 있는 경우)
            var zValue = GetZValue(feature.Attributes);
            if (zValue.HasValue)
            {
                var converted3D = ConvertTo3D(feature.Geometry, zValue.Value);
                if (converted3D != null)
                {
                    RenderGeometry3D(converted3D, style);
                }
            }
        }
    }
    
    /// <summary>
    /// 3D 레이어 렌더링
    /// </summary>
    public void RenderLayer3D(ILayer layer)
    {
        if (!layer.Visible || layer.Style == null) return;
        
        var features = layer.GetFeatures(layer.GetExtent());
        
        // 깊이 정렬이 필요한 경우
        if (_options.DepthSort)
        {
            features = SortFeaturesByDepth(features);
        }
        
        foreach (var feature in features)
        {
            RenderFeature3D(feature, layer.Style);
        }
    }
    
    private void RenderPoint3D(Point3D point, IStyle style)
    {
        var screenCoords = _camera.WorldToScreen(point.Coordinate, _width, _height);
        if (!screenCoords.HasValue) return;
        
        var (x, y, depth) = screenCoords.Value;
        
        // 깊이 테스트
        if (!PassDepthTest((int)x, (int)y, depth)) return;
        
        // 2D 점으로 렌더링
        var point2D = new PointF((float)x, (float)y);
        
        if (style is IPointSymbolizer symbolizer)
        {
            RenderPointSymbol(point2D, symbolizer, depth);
        }
    }
    
    private void RenderLineString3D(LineString3D lineString, IStyle style)
    {
        if (lineString.Coordinates3D.Length < 2) return;
        
        var screenPoints = new List<PointF>();
        var depths = new List<double>();
        
        // 모든 점을 스크린 좌표로 변환
        foreach (var coord in lineString.Coordinates3D)
        {
            var screenCoords = _camera.WorldToScreen(coord, _width, _height);
            if (screenCoords.HasValue)
            {
                var (x, y, depth) = screenCoords.Value;
                screenPoints.Add(new PointF((float)x, (float)y));
                depths.Add(depth);
            }
        }
        
        if (screenPoints.Count < 2) return;
        
        // 선분별로 렌더링
        using var pen = CreatePen(style);
        
        for (int i = 0; i < screenPoints.Count - 1; i++)
        {
            // 선분의 중간 깊이로 깊이 테스트
            var midDepth = (depths[i] + depths[i + 1]) / 2;
            var midX = (int)((screenPoints[i].X + screenPoints[i + 1].X) / 2);
            var midY = (int)((screenPoints[i].Y + screenPoints[i + 1].Y) / 2);
            
            if (PassDepthTest(midX, midY, midDepth))
            {
                _graphics!.DrawLine(pen, screenPoints[i], screenPoints[i + 1]);
            }
        }
    }
    
    private void RenderPolygon3D(Polygon3D polygon, IStyle style)
    {
        // 외곽선을 스크린 좌표로 변환
        var screenPoints = ConvertToScreenPoints(polygon.ExteriorRing.Coordinates3D);
        if (screenPoints.Count < 3) return;
        
        // 폴리곤의 평균 깊이 계산
        var avgDepth = screenPoints.Average(p => p.depth);
        var centerX = (int)screenPoints.Average(p => p.point.X);
        var centerY = (int)screenPoints.Average(p => p.point.Y);
        
        // 깊이 테스트
        if (!PassDepthTest(centerX, centerY, avgDepth)) return;
        
        using var path = new GraphicsPath();
        path.AddPolygon(screenPoints.Select(p => p.point).ToArray());
        
        // 홀 처리
        foreach (var hole in polygon.InteriorRings)
        {
            var holePoints = ConvertToScreenPoints(hole.Coordinates3D);
            if (holePoints.Count >= 3)
            {
                path.AddPolygon(holePoints.Select(p => p.point).ToArray());
            }
        }
        
        // 채우기
        if (_options.EnableShading && style is IPolygonSymbolizer polyStyle)
        {
            using var brush = CreateBrushWithShading(polyStyle, polygon);
            _graphics!.FillPath(brush, path);
        }
        else
        {
            using var brush = CreateBrush(style);
            _graphics!.FillPath(brush, path);
        }
        
        // 외곽선
        if (style is IPolygonSymbolizer ps && ps.EnableOutline)
        {
            using var pen = CreatePen(style);
            _graphics!.DrawPath(pen, path);
        }
    }
    
    private void RenderGeometryCollection3D(GeometryCollection3D collection, IStyle style)
    {
        foreach (var geometry in collection.Geometries)
        {
            RenderGeometry3D(geometry, style);
        }
    }
    
    private List<(PointF point, double depth)> ConvertToScreenPoints(Coordinate3D[] coords)
    {
        var result = new List<(PointF, double)>();
        
        foreach (var coord in coords)
        {
            var screenCoords = _camera.WorldToScreen(coord, _width, _height);
            if (screenCoords.HasValue)
            {
                var (x, y, depth) = screenCoords.Value;
                result.Add((new PointF((float)x, (float)y), depth));
            }
        }
        
        return result;
    }
    
    private bool PassDepthTest(int x, int y, double depth)
    {
        if (!_options.EnableDepthTest) return true;
        
        var key = y * _width + x;
        
        if (_depthBuffer.TryGetValue(key, out var existingDepth))
        {
            if (depth <= existingDepth)
            {
                _depthBuffer[key] = depth;
                return true;
            }
            return false;
        }
        
        _depthBuffer[key] = depth;
        return true;
    }
    
    private void RenderPointSymbol(PointF point, IPointSymbolizer symbolizer, double depth)
    {
        var size = (float)(symbolizer.Size * (1 - depth * 0.5)); // 거리에 따른 크기 조정
        
        using var brush = new SolidBrush(symbolizer.Color ?? Color.Red);
        
        switch (symbolizer.SymbolType)
        {
            case PointSymbolType.Circle:
                _graphics!.FillEllipse(brush, point.X - size/2, point.Y - size/2, size, size);
                break;
            case PointSymbolType.Square:
                _graphics!.FillRectangle(brush, point.X - size/2, point.Y - size/2, size, size);
                break;
            case PointSymbolType.Triangle:
                var points = new[]
                {
                    new PointF(point.X, point.Y - size/2),
                    new PointF(point.X - size/2, point.Y + size/2),
                    new PointF(point.X + size/2, point.Y + size/2)
                };
                _graphics!.FillPolygon(brush, points);
                break;
        }
    }
    
    private Brush CreateBrushWithShading(IPolygonSymbolizer style, Polygon3D polygon)
    {
        // 면의 법선 벡터 계산
        var normal = polygon.GetNormal();
        if (normal == null) return CreateBrush(style);
        
        // 빛의 방향 (간단히 위에서 아래로)
        var lightDirection = new Coordinate3D(0.3, -0.3, -0.8).Normalize();
        
        // 램버트 음영
        var dot = Math.Max(0, -normal.DotProduct(lightDirection));
        var shade = 0.3 + 0.7 * dot; // 최소 30% 밝기
        
        var baseColor = style.FillColor ?? Color.LightGray;
        var shadedColor = Color.FromArgb(
            baseColor.A,
            (int)(baseColor.R * shade),
            (int)(baseColor.G * shade),
            (int)(baseColor.B * shade)
        );
        
        return new SolidBrush(shadedColor);
    }
    
    private Pen CreatePen(IStyle style)
    {
        if (style is ILineSymbolizer lineStyle)
        {
            return new Pen(lineStyle.Color ?? Color.Black, lineStyle.Width);
        }
        else if (style is IPolygonSymbolizer polyStyle)
        {
            return new Pen(polyStyle.OutlineColor ?? Color.Black, polyStyle.OutlineWidth);
        }
        
        return new Pen(Color.Black, 1);
    }
    
    private Brush CreateBrush(IStyle style)
    {
        if (style is IPolygonSymbolizer polyStyle)
        {
            return new SolidBrush(polyStyle.FillColor ?? Color.LightGray);
        }
        
        return new SolidBrush(Color.Gray);
    }
    
    private double? GetZValue(IAttributeTable attributes)
    {
        // 일반적인 Z 필드 이름들
        var zFields = new[] { "Z", "z", "Height", "height", "Elevation", "elevation", "altitude" };
        
        foreach (var field in zFields)
        {
            if (attributes.Exists(field))
            {
                var value = attributes[field];
                if (value != null && double.TryParse(value.ToString(), out var z))
                {
                    return z;
                }
            }
        }
        
        return null;
    }
    
    private IGeometry3D? ConvertTo3D(IGeometry geometry, double z)
    {
        switch (geometry.GeometryType)
        {
            case GeometryType.Point:
                var point = (GeomPoint)geometry;
                return new Point3D(point.X, point.Y, z);
                
            case GeometryType.LineString:
                var line = (LineString)geometry;
                var coords3D = line.Coordinates.Select(c => new Coordinate3D(c.X, c.Y, z)).ToArray();
                return new LineString3D(coords3D);
                
            case GeometryType.Polygon:
                var poly = (Polygon)geometry;
                var exterior3D = new LinearRing3D(
                    poly.ExteriorRing.Coordinates.Select(c => new Coordinate3D(c.X, c.Y, z))
                );
                var holes3D = poly.InteriorRings.Select(h => 
                    new LinearRing3D(h.Coordinates.Select(c => new Coordinate3D(c.X, c.Y, z)))
                ).ToArray();
                return new Polygon3D(exterior3D, holes3D);
                
            default:
                return null;
        }
    }
    
    private IEnumerable<IFeature> SortFeaturesByDepth(IEnumerable<IFeature> features)
    {
        var featureDepths = new List<(IFeature feature, double depth)>();
        
        foreach (var feature in features)
        {
            var geom = feature.Geometry;
            if (geom == null) continue;
            
            var centroid = geom.Centroid;
            if (centroid == null) continue;
            
            var z = GetZValue(feature.Attributes) ?? 0;
            var worldPoint = new Coordinate3D(centroid.X, centroid.Y, z);
            var screenCoords = _camera.WorldToScreen(worldPoint, _width, _height);
            
            if (screenCoords.HasValue)
            {
                featureDepths.Add((feature, screenCoords.Value.z));
            }
        }
        
        // 깊이 순으로 정렬 (뒤에서 앞으로)
        return featureDepths.OrderByDescending(f => f.depth).Select(f => f.feature);
    }
}

/// <summary>
/// 3D 렌더러 옵션
/// </summary>
public class Renderer3DOptions
{
    /// <summary>
    /// 깊이 테스트 활성화
    /// </summary>
    public bool EnableDepthTest { get; set; } = true;
    
    /// <summary>
    /// 깊이 정렬 활성화
    /// </summary>
    public bool DepthSort { get; set; } = true;
    
    /// <summary>
    /// 음영 처리 활성화
    /// </summary>
    public bool EnableShading { get; set; } = true;
    
    /// <summary>
    /// 안티앨리어싱 활성화
    /// </summary>
    public bool EnableAntialiasing { get; set; } = true;
    
    /// <summary>
    /// 와이어프레임 모드
    /// </summary>
    public bool WireframeMode { get; set; } = false;
    
    /// <summary>
    /// 배경색
    /// </summary>
    public Color? BackgroundColor { get; set; } = Color.White;
    
    /// <summary>
    /// 안개 효과
    /// </summary>
    public bool EnableFog { get; set; } = false;
    
    /// <summary>
    /// 안개 시작 거리
    /// </summary>
    public double FogStart { get; set; } = 100;
    
    /// <summary>
    /// 안개 끝 거리
    /// </summary>
    public double FogEnd { get; set; } = 1000;
    
    /// <summary>
    /// 안개 색상
    /// </summary>
    public Color FogColor { get; set; } = Color.LightGray;
}