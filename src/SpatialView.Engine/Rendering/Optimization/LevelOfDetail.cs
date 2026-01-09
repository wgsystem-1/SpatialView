using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Engine.Rendering.Optimization;

/// <summary>
/// Level of Detail (LOD) 시스템
/// 줌 레벨에 따른 지오메트리 단순화 및 피처 필터링
/// </summary>
public class LevelOfDetail
{
    private readonly Dictionary<int, LodLevel> _lodLevels;
    
    /// <summary>
    /// 기본 생성자 - 표준 LOD 레벨 생성
    /// </summary>
    public LevelOfDetail()
    {
        _lodLevels = new Dictionary<int, LodLevel>();
        InitializeDefaultLodLevels();
    }
    
    /// <summary>
    /// LOD 레벨 추가
    /// </summary>
    /// <param name="zoomLevel">줌 레벨</param>
    /// <param name="lodLevel">LOD 설정</param>
    public void AddLodLevel(int zoomLevel, LodLevel lodLevel)
    {
        _lodLevels[zoomLevel] = lodLevel;
    }
    
    /// <summary>
    /// 줌 레벨에 따른 피처 필터링
    /// </summary>
    /// <param name="features">원본 피처 목록</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <param name="viewportEnvelope">표시 영역</param>
    /// <returns>필터링된 피처 목록</returns>
    public IEnumerable<IFeature> FilterFeatures(IEnumerable<IFeature> features, 
        double zoomLevel, Envelope viewportEnvelope)
    {
        var lodLevel = GetLodLevel(zoomLevel);
        if (lodLevel == null)
            return features;
        
        var filteredFeatures = new List<IFeature>();
        
        foreach (var feature in features)
        {
            if (ShouldIncludeFeature(feature, lodLevel, viewportEnvelope))
            {
                var optimizedFeature = OptimizeFeature(feature, lodLevel);
                filteredFeatures.Add(optimizedFeature);
            }
        }
        
        return filteredFeatures;
    }
    
    /// <summary>
    /// 지오메트리 단순화
    /// </summary>
    /// <param name="geometry">원본 지오메트리</param>
    /// <param name="tolerance">단순화 허용 오차</param>
    /// <returns>단순화된 지오메트리</returns>
    public IGeometry SimplifyGeometry(IGeometry geometry, double tolerance)
    {
        if (geometry == null || tolerance <= 0)
            return geometry;
        
        return geometry.GeometryType switch
        {
            GeometryType.LineString => SimplifyLineString((LineString)geometry, tolerance),
            GeometryType.Polygon => SimplifyPolygon((Polygon)geometry, tolerance),
            GeometryType.MultiLineString => SimplifyMultiLineString((MultiLineString)geometry, tolerance),
            GeometryType.MultiPolygon => SimplifyMultiPolygon((MultiPolygon)geometry, tolerance),
            _ => geometry // Point와 MultiPoint는 단순화 불필요
        };
    }
    
    private void InitializeDefaultLodLevels()
    {
        // 줌 레벨별 기본 LOD 설정
        _lodLevels[1] = new LodLevel 
        { 
            SimplificationTolerance = 1000.0, 
            MinFeatureSize = 100.0,
            MaxFeatures = 100 
        };
        _lodLevels[5] = new LodLevel 
        { 
            SimplificationTolerance = 100.0, 
            MinFeatureSize = 50.0,
            MaxFeatures = 500 
        };
        _lodLevels[10] = new LodLevel 
        { 
            SimplificationTolerance = 10.0, 
            MinFeatureSize = 10.0,
            MaxFeatures = 2000 
        };
        _lodLevels[15] = new LodLevel 
        { 
            SimplificationTolerance = 1.0, 
            MinFeatureSize = 1.0,
            MaxFeatures = 10000 
        };
        _lodLevels[20] = new LodLevel 
        { 
            SimplificationTolerance = 0.1, 
            MinFeatureSize = 0.1,
            MaxFeatures = int.MaxValue 
        };
    }
    
    private LodLevel? GetLodLevel(double zoomLevel)
    {
        var nearestZoom = _lodLevels.Keys
            .OrderBy(z => Math.Abs(z - zoomLevel))
            .FirstOrDefault();
        
        return nearestZoom != 0 ? _lodLevels[nearestZoom] : null;
    }
    
    private bool ShouldIncludeFeature(IFeature feature, LodLevel lodLevel, Envelope viewport)
    {
        if (feature.Geometry == null || feature.BoundingBox == null)
            return false;
        
        // 뷰포트와 교차하지 않으면 제외
        if (!feature.BoundingBox.Intersects(viewport))
            return false;
        
        // 최소 크기보다 작으면 제외
        var size = Math.Max(feature.BoundingBox.Width, feature.BoundingBox.Height);
        return size >= lodLevel.MinFeatureSize;
    }
    
    private IFeature OptimizeFeature(IFeature feature, LodLevel lodLevel)
    {
        if (feature.Geometry == null || lodLevel.SimplificationTolerance <= 0)
            return feature;
        
        var simplifiedGeometry = SimplifyGeometry(feature.Geometry, lodLevel.SimplificationTolerance);
        
        // 새로운 Feature 객체 생성 (기존 Feature는 변경하지 않음)
        return new Feature(feature.Id, simplifiedGeometry, feature.Attributes);
    }
    
    private IGeometry SimplifyLineString(LineString lineString, double tolerance)
    {
        // Douglas-Peucker 알고리즘의 간단한 구현
        var coordinates = lineString.Coordinates;
        if (coordinates.Length <= 2)
            return lineString;
        
        var simplified = DouglasPeuckerSimplification(coordinates.ToList(), tolerance);
        return new LineString(simplified.ToArray());
    }
    
    private IGeometry SimplifyPolygon(Polygon polygon, double tolerance)
    {
        var exteriorRing = polygon.ExteriorRing;
        var simplifiedExterior = SimplifyLineString(exteriorRing, tolerance);
        
        // 내부 링도 단순화 (여기서는 기본 구현만)
        var interiorRings = polygon.InteriorRings
            .Select(ring => (LinearRing)SimplifyLineString(ring, tolerance))
            .ToArray();
        
        return new Polygon((LinearRing)simplifiedExterior, interiorRings);
    }
    
    private IGeometry SimplifyMultiLineString(MultiLineString multiLineString, double tolerance)
    {
        var simplifiedLines = new List<LineString>();
        
        for (int i = 0; i < multiLineString.NumGeometries; i++)
        {
            var line = multiLineString.GetGeometryN(i);
            var simplified = (LineString)SimplifyLineString(line, tolerance);
            simplifiedLines.Add(simplified);
        }
        
        return new MultiLineString(simplifiedLines.ToArray());
    }
    
    private IGeometry SimplifyMultiPolygon(MultiPolygon multiPolygon, double tolerance)
    {
        var simplifiedPolygons = multiPolygon.Geometries
            .Select(polygon => (Polygon)SimplifyPolygon(polygon, tolerance))
            .ToList();
        
        return new MultiPolygon(simplifiedPolygons);
    }
    
    /// <summary>
    /// Douglas-Peucker 단순화 알고리즘
    /// </summary>
    private List<ICoordinate> DouglasPeuckerSimplification(List<ICoordinate> points, double tolerance)
    {
        if (points.Count <= 2)
            return points;
        
        var result = new List<ICoordinate>();
        
        // 첫 번째와 마지막 점은 항상 포함
        result.Add(points[0]);
        
        // 재귀적으로 단순화
        DouglasPeuckerRecursive(points, 0, points.Count - 1, tolerance, result);
        
        result.Add(points[points.Count - 1]);
        
        return result;
    }
    
    private void DouglasPeuckerRecursive(List<ICoordinate> points, int startIndex, int endIndex, 
        double tolerance, List<ICoordinate> result)
    {
        if (endIndex <= startIndex + 1)
            return;
        
        var maxDistance = 0.0;
        var maxIndex = startIndex;
        
        // 선분과 각 점 사이의 수직 거리 계산
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            var distance = PerpendicularDistance(points[i], points[startIndex], points[endIndex]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }
        
        // 최대 거리가 허용 오차보다 크면 재귀적으로 분할
        if (maxDistance > tolerance)
        {
            DouglasPeuckerRecursive(points, startIndex, maxIndex, tolerance, result);
            result.Add(points[maxIndex]);
            DouglasPeuckerRecursive(points, maxIndex, endIndex, tolerance, result);
        }
    }
    
    private double PerpendicularDistance(ICoordinate point, ICoordinate lineStart, ICoordinate lineEnd)
    {
        var A = lineEnd.X - lineStart.X;
        var B = lineEnd.Y - lineStart.Y;
        var C = point.X - lineStart.X;
        var D = point.Y - lineStart.Y;
        
        var dot = A * C + B * D;
        var lenSq = A * A + B * B;
        
        if (lenSq == 0.0)
            return Math.Sqrt(C * C + D * D);
        
        var param = dot / lenSq;
        
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
}

/// <summary>
/// LOD 레벨 설정
/// </summary>
public class LodLevel
{
    /// <summary>
    /// 지오메트리 단순화 허용 오차
    /// </summary>
    public double SimplificationTolerance { get; set; }
    
    /// <summary>
    /// 표시할 최소 피처 크기 (맵 단위)
    /// </summary>
    public double MinFeatureSize { get; set; }
    
    /// <summary>
    /// 최대 표시 피처 수
    /// </summary>
    public int MaxFeatures { get; set; }
}