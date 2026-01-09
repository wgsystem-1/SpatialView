using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Engine.Rendering.Optimization;

/// <summary>
/// 뷰포트 컬링 시스템
/// 화면 밖 객체 렌더링 제외 및 LOD 기반 간소화
/// </summary>
public class ViewportCulling
{
    private readonly Dictionary<int, CullingStatistics> _statistics;
    private readonly object _lockObject = new();

    public ViewportCulling()
    {
        _statistics = new Dictionary<int, CullingStatistics>();
    }

    /// <summary>
    /// 뷰포트 기반 피처 필터링
    /// </summary>
    /// <param name="features">원본 피처들</param>
    /// <param name="viewport">뷰포트 영역</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <param name="bufferRatio">뷰포트 확장 비율 (0.1 = 10% 확장)</param>
    /// <returns>컬링된 피처들</returns>
    public IEnumerable<IFeature> CullFeatures(
        IEnumerable<IFeature> features, 
        Envelope viewport, 
        double zoomLevel,
        double bufferRatio = 0.1)
    {
        if (features == null) yield break;
        if (viewport == null) throw new ArgumentNullException(nameof(viewport));

        var stats = new CullingStatistics();
        var expandedViewport = ExpandViewport(viewport, bufferRatio);

        foreach (var feature in features)
        {
            stats.TotalFeatures++;

            if (feature?.Geometry == null)
            {
                stats.NullGeometryFeatures++;
                continue;
            }

            // 기본 바운딩 박스 체크
            var bounds = feature.Geometry.GetBounds();
            if (bounds == null || !expandedViewport.Intersects(bounds))
            {
                stats.CulledFeatures++;
                continue;
            }

            // LOD 기반 간소화 적용
            var simplifiedFeature = ApplyLevelOfDetail(feature, zoomLevel, viewport);
            if (simplifiedFeature != null)
            {
                stats.VisibleFeatures++;
                yield return simplifiedFeature;
            }
            else
            {
                stats.SimplifiedAwayFeatures++;
            }
        }

        // 통계 저장
        lock (_lockObject)
        {
            _statistics[GetStatisticsKey(zoomLevel)] = stats;
        }
    }

    /// <summary>
    /// 공간 인덱스 기반 고성능 컬링
    /// </summary>
    /// <param name="spatialIndex">공간 인덱스</param>
    /// <param name="viewport">뷰포트 영역</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <param name="bufferRatio">뷰포트 확장 비율</param>
    /// <returns>컬링된 피처들</returns>
    public IEnumerable<IFeature> CullFeaturesWithIndex(
        ISpatialIndex spatialIndex,
        Envelope viewport,
        double zoomLevel,
        double bufferRatio = 0.1)
    {
        if (spatialIndex == null) throw new ArgumentNullException(nameof(spatialIndex));
        if (viewport == null) throw new ArgumentNullException(nameof(viewport));

        var expandedViewport = ExpandViewport(viewport, bufferRatio);
        var candidateFeatures = spatialIndex.Query(expandedViewport);

        return CullFeatures(candidateFeatures, viewport, zoomLevel, 0); // 버퍼는 이미 적용됨
    }

    /// <summary>
    /// 레이어별 컬링 처리
    /// </summary>
    /// <param name="layers">레이어들</param>
    /// <param name="viewport">뷰포트 영역</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <returns>컬링된 레이어 피처들</returns>
    public Dictionary<ILayer, IEnumerable<IFeature>> CullLayers(
        IEnumerable<ILayer> layers,
        Envelope viewport,
        double zoomLevel)
    {
        var result = new Dictionary<ILayer, IEnumerable<IFeature>>();

        foreach (var layer in layers)
        {
            if (layer?.IsVisible != true || !IsLayerVisibleAtZoom(layer, zoomLevel))
                continue;

            var layerFeatures = GetLayerFeatures(layer, viewport);
            var culledFeatures = CullFeatures(layerFeatures, viewport, zoomLevel);
            
            result[layer] = culledFeatures.ToList(); // 지연 실행 방지
        }

        return result;
    }

    /// <summary>
    /// LOD 기반 지오메트리 간소화
    /// </summary>
    /// <param name="feature">원본 피처</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <param name="viewport">뷰포트 영역</param>
    /// <returns>간소화된 피처 또는 null (제거된 경우)</returns>
    private IFeature? ApplyLevelOfDetail(IFeature feature, double zoomLevel, Envelope viewport)
    {
        if (feature?.Geometry == null) return null;

        var lodLevel = CalculateLODLevel(zoomLevel);
        var pixelTolerance = CalculatePixelTolerance(zoomLevel, viewport);

        switch (feature.Geometry)
        {
            case Point point:
                return ApplyPointLOD(feature, point, lodLevel, pixelTolerance);
                
            case LineString lineString:
                return ApplyLineStringLOD(feature, lineString, lodLevel, pixelTolerance);
                
            case Polygon polygon:
                return ApplyPolygonLOD(feature, polygon, lodLevel, pixelTolerance);
                
            case MultiPoint multiPoint:
                return ApplyMultiPointLOD(feature, multiPoint, lodLevel, pixelTolerance);
                
            case MultiLineString multiLineString:
                return ApplyMultiLineStringLOD(feature, multiLineString, lodLevel, pixelTolerance);
                
            case MultiPolygon multiPolygon:
                return ApplyMultiPolygonLOD(feature, multiPolygon, lodLevel, pixelTolerance);
                
            default:
                return feature;
        }
    }

    /// <summary>
    /// 포인트 LOD 처리
    /// </summary>
    private IFeature? ApplyPointLOD(IFeature feature, Point point, int lodLevel, double pixelTolerance)
    {
        // 높은 줌 레벨에서는 모든 포인트 표시
        if (lodLevel >= 15) return feature;

        // 낮은 줌 레벨에서는 중요한 포인트만 표시
        if (lodLevel < 10)
        {
            var importance = GetFeatureImportance(feature);
            if (importance < GetMinimumImportanceThreshold(lodLevel))
                return null;
        }

        return feature;
    }

    /// <summary>
    /// 라인스트링 LOD 처리
    /// </summary>
    private IFeature? ApplyLineStringLOD(IFeature feature, LineString lineString, int lodLevel, double pixelTolerance)
    {
        if (lodLevel >= 15) return feature;

        // Douglas-Peucker 간소화 적용
        var tolerance = CalculateSimplificationTolerance(lodLevel, pixelTolerance);
        var simplifiedGeometry = SimplifyLineString(lineString, tolerance);

        if (simplifiedGeometry == null || simplifiedGeometry.Coordinates.Count() < 2)
            return null;

        return CreateSimplifiedFeature(feature, simplifiedGeometry);
    }

    /// <summary>
    /// 폴리곤 LOD 처리
    /// </summary>
    private IFeature? ApplyPolygonLOD(IFeature feature, Polygon polygon, int lodLevel, double pixelTolerance)
    {
        var bounds = polygon.GetBounds();
        if (bounds == null) return null;

        // 폴리곤이 너무 작으면 포인트로 대체
        var minDimension = Math.Min(bounds.Width, bounds.Height);
        var pixelSize = CalculatePixelSize(pixelTolerance);
        
        if (minDimension < pixelSize * 2)
        {
            if (lodLevel < 12)
            {
                // 중심점으로 대체
                var centroid = polygon.Centroid;
                return centroid != null ? CreateSimplifiedFeature(feature, centroid) : null;
            }
            else
            {
                return null; // 너무 작아서 제거
            }
        }

        if (lodLevel >= 15) return feature;

        // 폴리곤 간소화
        var tolerance = CalculateSimplificationTolerance(lodLevel, pixelTolerance);
        var simplifiedGeometry = SimplifyPolygon(polygon, tolerance);

        if (simplifiedGeometry == null)
            return null;

        return CreateSimplifiedFeature(feature, simplifiedGeometry);
    }

    /// <summary>
    /// 멀티포인트 LOD 처리
    /// </summary>
    private IFeature? ApplyMultiPointLOD(IFeature feature, MultiPoint multiPoint, int lodLevel, double pixelTolerance)
    {
        if (lodLevel >= 15) return feature;

        var filteredPoints = new List<Point>();
        var importance = GetFeatureImportance(feature);
        var threshold = GetMinimumImportanceThreshold(lodLevel);

        foreach (var point in multiPoint.Geometries.Cast<Point>())
        {
            if (importance >= threshold)
            {
                filteredPoints.Add(point);
            }
        }

        if (filteredPoints.Count == 0) return null;
        if (filteredPoints.Count == 1) return CreateSimplifiedFeature(feature, filteredPoints[0]);

        return CreateSimplifiedFeature(feature, new MultiPoint(filteredPoints));
    }

    /// <summary>
    /// 멀티라인스트링 LOD 처리
    /// </summary>
    private IFeature? ApplyMultiLineStringLOD(IFeature feature, MultiLineString multiLineString, int lodLevel, double pixelTolerance)
    {
        if (lodLevel >= 15) return feature;

        var tolerance = CalculateSimplificationTolerance(lodLevel, pixelTolerance);
        var simplifiedLines = new List<LineString>();

        foreach (var lineString in multiLineString.Geometries.Cast<LineString>())
        {
            var simplified = SimplifyLineString(lineString, tolerance);
            if (simplified?.Coordinates.Count() >= 2)
            {
                simplifiedLines.Add(simplified);
            }
        }

        if (simplifiedLines.Count == 0) return null;
        if (simplifiedLines.Count == 1) return CreateSimplifiedFeature(feature, simplifiedLines[0]);

        return CreateSimplifiedFeature(feature, new MultiLineString(simplifiedLines));
    }

    /// <summary>
    /// 멀티폴리곤 LOD 처리
    /// </summary>
    private IFeature? ApplyMultiPolygonLOD(IFeature feature, MultiPolygon multiPolygon, int lodLevel, double pixelTolerance)
    {
        if (lodLevel >= 15) return feature;

        var tolerance = CalculateSimplificationTolerance(lodLevel, pixelTolerance);
        var simplifiedPolygons = new List<Polygon>();
        var pixelSize = CalculatePixelSize(pixelTolerance);

        foreach (var polygon in multiPolygon.Geometries.Cast<Polygon>())
        {
            var bounds = polygon.GetBounds();
            if (bounds != null)
            {
                var minDimension = Math.Min(bounds.Width, bounds.Height);
                if (minDimension >= pixelSize * 2)
                {
                    var simplified = SimplifyPolygon(polygon, tolerance);
                    if (simplified != null)
                    {
                        simplifiedPolygons.Add(simplified);
                    }
                }
            }
        }

        if (simplifiedPolygons.Count == 0) return null;
        if (simplifiedPolygons.Count == 1) return CreateSimplifiedFeature(feature, simplifiedPolygons[0]);

        return CreateSimplifiedFeature(feature, new MultiPolygon(simplifiedPolygons));
    }

    #region Helper Methods

    /// <summary>
    /// 뷰포트 확장
    /// </summary>
    private static Envelope ExpandViewport(Envelope viewport, double bufferRatio)
    {
        var bufferX = viewport.Width * bufferRatio;
        var bufferY = viewport.Height * bufferRatio;
        
        return new Envelope(
            viewport.MinX - bufferX,
            viewport.MinY - bufferY,
            viewport.MaxX + bufferX,
            viewport.MaxY + bufferY
        );
    }

    /// <summary>
    /// LOD 레벨 계산
    /// </summary>
    private static int CalculateLODLevel(double zoomLevel)
    {
        return Math.Max(0, Math.Min(20, (int)Math.Round(zoomLevel)));
    }

    /// <summary>
    /// 픽셀 허용 오차 계산
    /// </summary>
    private static double CalculatePixelTolerance(double zoomLevel, Envelope viewport)
    {
        // 뷰포트 크기에 기반한 픽셀 크기 추정
        var metersPerPixel = viewport.Width / 1024.0; // 1024픽셀 가정
        return metersPerPixel * Math.Pow(2, 15 - zoomLevel);
    }

    /// <summary>
    /// 간소화 허용 오차 계산
    /// </summary>
    private static double CalculateSimplificationTolerance(int lodLevel, double pixelTolerance)
    {
        var baseTolerance = pixelTolerance;
        var lodFactor = Math.Pow(2, Math.Max(0, 15 - lodLevel));
        return baseTolerance * lodFactor;
    }

    /// <summary>
    /// 픽셀 크기 계산
    /// </summary>
    private static double CalculatePixelSize(double pixelTolerance)
    {
        return pixelTolerance;
    }

    /// <summary>
    /// 피처 중요도 계산
    /// </summary>
    private static double GetFeatureImportance(IFeature feature)
    {
        if (feature?.Attributes == null) return 0.5;

        // 속성 기반 중요도 계산
        var importance = 0.5;

        // 이름이 있으면 중요도 증가
        if (feature.Attributes.Exists("name") || feature.Attributes.Exists("NAME"))
            importance += 0.3;

        // 특정 타입이면 중요도 조정
        if (feature.Attributes.Exists("type") || feature.Attributes.Exists("TYPE"))
        {
            var type = feature.Attributes["type"]?.ToString() ?? feature.Attributes["TYPE"]?.ToString();
            importance += type?.ToLowerInvariant() switch
            {
                "city" => 0.4,
                "town" => 0.3,
                "village" => 0.2,
                "highway" => 0.3,
                "primary" => 0.2,
                _ => 0.1
            };
        }

        return Math.Min(1.0, importance);
    }

    /// <summary>
    /// 최소 중요도 임계값
    /// </summary>
    private static double GetMinimumImportanceThreshold(int lodLevel)
    {
        return lodLevel switch
        {
            < 5 => 0.9,
            < 8 => 0.7,
            < 10 => 0.5,
            < 12 => 0.3,
            _ => 0.1
        };
    }

    /// <summary>
    /// 라인스트링 간소화
    /// </summary>
    private static LineString? SimplifyLineString(LineString lineString, double tolerance)
    {
        var coords = lineString.Coordinates.ToList();
        if (coords.Count < 3) return lineString;

        var simplified = DouglasPeuckerSimplify(coords, tolerance);
        return simplified.Count >= 2 ? new LineString(simplified) : null;
    }

    /// <summary>
    /// 폴리곤 간소화
    /// </summary>
    private static Polygon? SimplifyPolygon(Polygon polygon, double tolerance)
    {
        var exteriorCoords = polygon.ExteriorRing.Coordinates.ToList();
        var simplifiedExterior = DouglasPeuckerSimplify(exteriorCoords, tolerance);
        
        if (simplifiedExterior.Count < 4) return null;

        var simplifiedHoles = new List<LinearRing>();
        foreach (var hole in polygon.InteriorRings)
        {
            var holeCoords = hole.Coordinates.ToList();
            var simplifiedHole = DouglasPeuckerSimplify(holeCoords, tolerance);
            
            if (simplifiedHole.Count >= 4)
            {
                simplifiedHoles.Add(new LinearRing(simplifiedHole));
            }
        }

        return new Polygon(new LinearRing(simplifiedExterior), simplifiedHoles.ToArray());
    }

    /// <summary>
    /// Douglas-Peucker 간소화 알고리즘
    /// </summary>
    private static List<ICoordinate> DouglasPeuckerSimplify(List<ICoordinate> points, double tolerance)
    {
        if (points.Count <= 2) return points;

        var maxDistance = 0.0;
        var maxIndex = 0;

        for (int i = 1; i < points.Count - 1; i++)
        {
            var distance = PerpendicularDistance(points[i], points[0], points[points.Count - 1]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        if (maxDistance > tolerance)
        {
            var left = DouglasPeuckerSimplify(points.Take(maxIndex + 1).ToList(), tolerance);
            var right = DouglasPeuckerSimplify(points.Skip(maxIndex).ToList(), tolerance);
            
            return left.Take(left.Count - 1).Concat(right).ToList();
        }
        else
        {
            return new List<ICoordinate> { points[0], points[points.Count - 1] };
        }
    }

    /// <summary>
    /// 점에서 직선까지의 수직 거리
    /// </summary>
    private static double PerpendicularDistance(ICoordinate point, ICoordinate lineStart, ICoordinate lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;

        if (dx == 0 && dy == 0)
        {
            dx = point.X - lineStart.X;
            dy = point.Y - lineStart.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        var t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

        if (t > 1)
        {
            dx = point.X - lineEnd.X;
            dy = point.Y - lineEnd.Y;
        }
        else if (t > 0)
        {
            dx = point.X - (lineStart.X + dx * t);
            dy = point.Y - (lineStart.Y + dy * t);
        }
        else
        {
            dx = point.X - lineStart.X;
            dy = point.Y - lineStart.Y;
        }

        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 간소화된 피처 생성
    /// </summary>
    private static IFeature CreateSimplifiedFeature(IFeature original, IGeometry newGeometry)
    {
        return new Feature(original.Id, newGeometry, original.Attributes);
    }

    /// <summary>
    /// 레이어가 현재 줌에서 보이는지 확인
    /// </summary>
    private static bool IsLayerVisibleAtZoom(ILayer layer, double zoomLevel)
    {
        // ILayer에 MinZoom, MaxZoom 속성이 있다고 가정
        if (layer is VectorLayer vectorLayer)
        {
            return zoomLevel >= vectorLayer.MinimumZoom && 
                   zoomLevel <= vectorLayer.MaximumZoom;
        }
        
        return true;
    }

    /// <summary>
    /// 레이어 피처들 가져오기
    /// </summary>
    private static IEnumerable<IFeature> GetLayerFeatures(ILayer layer, Envelope viewport)
    {
        if (layer is VectorLayer vectorLayer && vectorLayer.DataSource != null)
        {
            return vectorLayer.DataSource.GetFeatures(viewport) ?? Enumerable.Empty<IFeature>();
        }
        
        return Enumerable.Empty<IFeature>();
    }

    /// <summary>
    /// 통계 키 생성
    /// </summary>
    private static int GetStatisticsKey(double zoomLevel)
    {
        return (int)Math.Round(zoomLevel);
    }

    #endregion

    /// <summary>
    /// 컬링 통계 가져오기
    /// </summary>
    public CullingStatistics? GetStatistics(double zoomLevel)
    {
        lock (_lockObject)
        {
            _statistics.TryGetValue(GetStatisticsKey(zoomLevel), out var stats);
            return stats;
        }
    }

    /// <summary>
    /// 모든 통계 가져오기
    /// </summary>
    public Dictionary<int, CullingStatistics> GetAllStatistics()
    {
        lock (_lockObject)
        {
            return new Dictionary<int, CullingStatistics>(_statistics);
        }
    }

    /// <summary>
    /// 통계 초기화
    /// </summary>
    public void ClearStatistics()
    {
        lock (_lockObject)
        {
            _statistics.Clear();
        }
    }
}

/// <summary>
/// 컬링 통계 정보
/// </summary>
public class CullingStatistics
{
    public int TotalFeatures { get; set; }
    public int VisibleFeatures { get; set; }
    public int CulledFeatures { get; set; }
    public int SimplifiedAwayFeatures { get; set; }
    public int NullGeometryFeatures { get; set; }

    public double CullingRatio => TotalFeatures > 0 ? (double)CulledFeatures / TotalFeatures : 0;
    public double SimplificationRatio => TotalFeatures > 0 ? (double)SimplifiedAwayFeatures / TotalFeatures : 0;
    public double VisibilityRatio => TotalFeatures > 0 ? (double)VisibleFeatures / TotalFeatures : 0;

    public override string ToString()
    {
        return $"Total: {TotalFeatures}, Visible: {VisibleFeatures}, Culled: {CulledFeatures}, " +
               $"Simplified: {SimplifiedAwayFeatures}, Culling: {CullingRatio:P1}";
    }
}

/// <summary>
/// 공간 인덱스 인터페이스
/// </summary>
public interface ISpatialIndex
{
    IEnumerable<IFeature> Query(Envelope envelope);
    void Insert(IFeature feature);
    void Remove(IFeature feature);
    void Clear();
}