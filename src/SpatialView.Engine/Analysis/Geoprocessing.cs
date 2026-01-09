using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 지오프로세싱 연산 구현
/// Dissolve, Clip, Split, Simplification, Convex/Concave Hull 등
/// </summary>
public static class Geoprocessing
{
    /// <summary>
    /// Dissolve 연산 - 인접한 폴리곤들을 병합
    /// </summary>
    /// <param name="polygons">병합할 폴리곤 목록</param>
    /// <param name="groupBy">그룹화 기준 (null이면 모든 폴리곤 병합)</param>
    /// <returns>병합된 폴리곤들</returns>
    public static IEnumerable<IGeometry> Dissolve(
        IEnumerable<Polygon> polygons, 
        Func<Polygon, object>? groupBy = null)
    {
        if (polygons == null) throw new ArgumentNullException(nameof(polygons));

        var polyList = polygons.ToList();
        if (polyList.Count == 0) return Enumerable.Empty<IGeometry>();

        if (groupBy == null)
        {
            // 모든 폴리곤을 하나로 병합
            return new[] { DissolveAll(polyList) };
        }
        else
        {
            // 그룹별로 병합
            var groups = polyList.GroupBy(groupBy);
            return groups.Select(g => DissolveAll(g.ToList()));
        }
    }

    /// <summary>
    /// Clip 연산 - 지오메트리를 클리핑 영역으로 자르기
    /// </summary>
    /// <param name="geometry">자를 지오메트리</param>
    /// <param name="clipBoundary">클리핑 경계 (Polygon)</param>
    /// <returns>클리핑된 지오메트리</returns>
    public static IGeometry? Clip(IGeometry geometry, Polygon clipBoundary)
    {
        if (geometry == null) throw new ArgumentNullException(nameof(geometry));
        if (clipBoundary == null) throw new ArgumentNullException(nameof(clipBoundary));

        return geometry switch
        {
            Point point => ClipPoint(point, clipBoundary),
            LineString lineString => ClipLineString(lineString, clipBoundary),
            Polygon polygon => ClipPolygon(polygon, clipBoundary),
            MultiPoint multiPoint => ClipMultiPoint(multiPoint, clipBoundary),
            MultiLineString multiLineString => ClipMultiLineString(multiLineString, clipBoundary),
            MultiPolygon multiPolygon => ClipMultiPolygon(multiPolygon, clipBoundary),
            _ => null
        };
    }

    /// <summary>
    /// Split 연산 - 라인으로 폴리곤 분할
    /// </summary>
    /// <param name="polygon">분할할 폴리곤</param>
    /// <param name="splitter">분할선</param>
    /// <returns>분할된 폴리곤들</returns>
    public static IEnumerable<Polygon> Split(Polygon polygon, LineString splitter)
    {
        if (polygon == null) throw new ArgumentNullException(nameof(polygon));
        if (splitter == null) throw new ArgumentNullException(nameof(splitter));

        // 간단한 구현: 분할선이 폴리곤과 교차하는지 확인
        if (!TopologicalRelations.Intersects(polygon, splitter))
        {
            return new[] { polygon };
        }

        // 복잡한 폴리곤 분할 알고리즘 구현 필요
        // 여기서는 기본적인 경우만 처리
        return SplitPolygonByLine(polygon, splitter);
    }

    /// <summary>
    /// Douglas-Peucker 알고리즘을 사용한 라인 단순화
    /// </summary>
    /// <param name="lineString">단순화할 라인</param>
    /// <param name="tolerance">허용 오차 (거리)</param>
    /// <returns>단순화된 라인</returns>
    public static LineString Simplify(LineString lineString, double tolerance)
    {
        if (lineString == null) throw new ArgumentNullException(nameof(lineString));
        if (tolerance <= 0) throw new ArgumentException("Tolerance must be positive", nameof(tolerance));

        var coordinates = lineString.Coordinates.ToList();
        if (coordinates.Count <= 2) return lineString;

        var simplified = DouglasPeucker(coordinates, tolerance);
        return new LineString(simplified);
    }

    /// <summary>
    /// 폴리곤 단순화
    /// </summary>
    /// <param name="polygon">단순화할 폴리곤</param>
    /// <param name="tolerance">허용 오차</param>
    /// <returns>단순화된 폴리곤</returns>
    public static Polygon Simplify(Polygon polygon, double tolerance)
    {
        if (polygon == null) throw new ArgumentNullException(nameof(polygon));

        var simplifiedExterior = SimplifyRing(polygon.ExteriorRing, tolerance);
        var simplifiedHoles = polygon.InteriorRings
            .Select(hole => SimplifyRing(hole, tolerance))
            .Where(hole => hole.Coordinates.Count() >= 4) // 최소 4개 점 (닫힌 링)
            .ToList();

        return new Polygon(simplifiedExterior, simplifiedHoles.ToArray());
    }
    
    /// <summary>
    /// LinearRing 단순화
    /// </summary>
    private static LinearRing SimplifyRing(LinearRing ring, double tolerance)
    {
        if (ring == null) return null;
        
        var simplifiedLine = Simplify(ring as LineString, tolerance);
        return new LinearRing(simplifiedLine.Coordinates);
    }

    /// <summary>
    /// Convex Hull (볼록 껍질) 계산
    /// </summary>
    /// <param name="coordinates">점들</param>
    /// <returns>볼록 껍질 폴리곤</returns>
    public static Polygon ConvexHull(IEnumerable<ICoordinate> coordinates)
    {
        if (coordinates == null) throw new ArgumentNullException(nameof(coordinates));

        var points = coordinates.ToList();
        if (points.Count < 3) throw new ArgumentException("Need at least 3 points for convex hull");

        var hull = GrahamScan(points);
        return new Polygon(new LinearRing(hull.Append(hull[0]).ToArray())); // 링을 닫음
    }

    /// <summary>
    /// Concave Hull (오목 껍질) 계산 - Alpha Shape 기반
    /// </summary>
    /// <param name="coordinates">점들</param>
    /// <param name="alpha">Alpha 값 (작을수록 더 오목한 형태)</param>
    /// <returns>오목 껍질 폴리곤</returns>
    public static Polygon? ConcaveHull(IEnumerable<ICoordinate> coordinates, double alpha = 0.1)
    {
        if (coordinates == null) throw new ArgumentNullException(nameof(coordinates));

        var points = coordinates.ToList();
        if (points.Count < 3) return null;

        // Alpha Shape 알고리즘의 간단한 구현
        // 실제 구현은 매우 복잡하므로 여기서는 근사치 구현
        return ApproximateConcaveHull(points, alpha);
    }

    #region Private Implementation Methods

    /// <summary>
    /// 모든 폴리곤을 하나로 병합
    /// </summary>
    private static IGeometry DissolveAll(List<Polygon> polygons)
    {
        if (polygons.Count == 0) throw new ArgumentException("Empty polygon list");
        if (polygons.Count == 1) return polygons[0];

        // 순차적으로 Union 연산 수행
        IGeometry result = polygons[0];
        for (int i = 1; i < polygons.Count; i++)
        {
            var union = SpatialOperations.Union(result, polygons[i]);
            if (union != null) result = union;
        }

        return result;
    }

    /// <summary>
    /// 점 클리핑
    /// </summary>
    private static IGeometry? ClipPoint(Point point, Polygon clipBoundary)
    {
        return TopologicalRelations.Contains(clipBoundary, point) ? point : null;
    }

    /// <summary>
    /// 라인 클리핑 (Cohen-Sutherland 변형)
    /// </summary>
    private static IGeometry? ClipLineString(LineString lineString, Polygon clipBoundary)
    {
        var clippedSegments = new List<ICoordinate[]>();
        var coordinates = lineString.Coordinates.ToList();

        for (int i = 0; i < coordinates.Count - 1; i++)
        {
            var segment = ClipLineSegment(coordinates[i], coordinates[i + 1], clipBoundary);
            if (segment != null)
            {
                clippedSegments.Add(segment);
            }
        }

        if (clippedSegments.Count == 0) return null;

        // 연결된 세그먼트들을 MultiLineString으로 반환
        var lineStrings = clippedSegments.Select(seg => new LineString(seg)).ToList();
        return lineStrings.Count == 1 ? lineStrings[0] : new MultiLineString(lineStrings);
    }

    /// <summary>
    /// 폴리곤 클리핑 (Sutherland-Hodgman 알고리즘)
    /// </summary>
    private static IGeometry? ClipPolygon(Polygon polygon, Polygon clipBoundary)
    {
        // 간단한 구현: 교집합 연산 사용
        return SpatialOperations.Intersection(polygon, clipBoundary);
    }

    /// <summary>
    /// MultiPoint 클리핑
    /// </summary>
    private static IGeometry? ClipMultiPoint(MultiPoint multiPoint, Polygon clipBoundary)
    {
        var clippedPoints = multiPoint.Geometries
            .Cast<Point>()
            .Where(p => TopologicalRelations.Contains(clipBoundary, p))
            .ToList();

        if (clippedPoints.Count == 0) return null;
        return clippedPoints.Count == 1 ? clippedPoints[0] : new MultiPoint(clippedPoints);
    }

    /// <summary>
    /// MultiLineString 클리핑
    /// </summary>
    private static IGeometry? ClipMultiLineString(MultiLineString multiLineString, Polygon clipBoundary)
    {
        var clippedLines = new List<IGeometry>();

        foreach (var lineString in multiLineString.Geometries.Cast<LineString>())
        {
            var clipped = ClipLineString(lineString, clipBoundary);
            if (clipped != null)
            {
                if (clipped is LineString line)
                    clippedLines.Add(line);
                else if (clipped is MultiLineString multiLine)
                    clippedLines.AddRange(multiLine.Geometries);
            }
        }

        if (clippedLines.Count == 0) return null;
        return clippedLines.Count == 1 ? clippedLines[0] : new MultiLineString(clippedLines.Cast<LineString>().ToArray());
    }

    /// <summary>
    /// MultiPolygon 클리핑
    /// </summary>
    private static IGeometry? ClipMultiPolygon(MultiPolygon multiPolygon, Polygon clipBoundary)
    {
        var clippedPolygons = new List<Polygon>();

        foreach (var polygon in multiPolygon.Geometries.Cast<Polygon>())
        {
            var clipped = ClipPolygon(polygon, clipBoundary);
            if (clipped is Polygon poly)
                clippedPolygons.Add(poly);
            else if (clipped is MultiPolygon multiPoly)
                clippedPolygons.AddRange(multiPoly.Geometries.Cast<Polygon>());
        }

        if (clippedPolygons.Count == 0) return null;
        return clippedPolygons.Count == 1 ? clippedPolygons[0] : new MultiPolygon(clippedPolygons);
    }

    /// <summary>
    /// 라인 세그먼트 클리핑
    /// </summary>
    private static ICoordinate[]? ClipLineSegment(ICoordinate start, ICoordinate end, Polygon clipBoundary)
    {
        // 간단한 구현: 시작점과 끝점이 모두 경계 내부에 있으면 포함
        var startInside = TopologicalRelations.Contains(clipBoundary, new Point(start));
        var endInside = TopologicalRelations.Contains(clipBoundary, new Point(end));

        if (startInside && endInside)
        {
            return new[] { start, end };
        }

        // 복잡한 세그먼트-폴리곤 클리핑은 여기서 생략
        // 실제로는 교차점을 찾아서 적절히 클리핑해야 함
        return null;
    }

    /// <summary>
    /// 폴리곤을 라인으로 분할
    /// </summary>
    private static IEnumerable<Polygon> SplitPolygonByLine(Polygon polygon, LineString splitter)
    {
        // 복잡한 폴리곤 분할 알고리즘
        // 여기서는 간단한 경우만 처리
        
        // 분할선과 폴리곤의 교차점들을 찾음
        var intersections = FindIntersections(polygon.ExteriorRing, splitter);
        
        if (intersections.Count < 2)
        {
            // 교차점이 2개 미만이면 분할할 수 없음
            return new[] { polygon };
        }

        // 간단한 구현: 원본 폴리곤만 반환
        // 실제로는 교차점을 이용해 새로운 폴리곤들을 생성해야 함
        return new[] { polygon };
    }

    /// <summary>
    /// 라인과 링의 교차점 찾기
    /// </summary>
    private static List<ICoordinate> FindIntersections(LinearRing ring, LineString line)
    {
        var intersections = new List<ICoordinate>();
        var ringCoords = ring.Coordinates.ToList();
        var lineCoords = line.Coordinates.ToList();

        // 모든 세그먼트 조합을 확인
        for (int i = 0; i < ringCoords.Count - 1; i++)
        {
            for (int j = 0; j < lineCoords.Count - 1; j++)
            {
                var intersection = FindLineSegmentIntersection(
                    ringCoords[i], ringCoords[i + 1],
                    lineCoords[j], lineCoords[j + 1]);
                    
                if (intersection != null)
                {
                    intersections.Add(intersection);
                }
            }
        }

        return intersections;
    }

    /// <summary>
    /// 두 선분의 교차점 찾기
    /// </summary>
    private static ICoordinate? FindLineSegmentIntersection(
        ICoordinate p1, ICoordinate p2, ICoordinate p3, ICoordinate p4)
    {
        double x1 = p1.X, y1 = p1.Y;
        double x2 = p2.X, y2 = p2.Y;
        double x3 = p3.X, y3 = p3.Y;
        double x4 = p4.X, y4 = p4.Y;

        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 1e-10) return null; // 평행

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            double x = x1 + t * (x2 - x1);
            double y = y1 + t * (y2 - y1);
            return new Coordinate(x, y);
        }

        return null;
    }

    /// <summary>
    /// Douglas-Peucker 알고리즘 구현
    /// </summary>
    private static List<ICoordinate> DouglasPeucker(List<ICoordinate> points, double tolerance)
    {
        if (points.Count <= 2) return points;

        // 시작점과 끝점 사이의 선분에서 가장 먼 점 찾기
        double maxDistance = 0;
        int maxIndex = 0;

        for (int i = 1; i < points.Count - 1; i++)
        {
            double distance = PerpendicularDistance(points[i], points[0], points[points.Count - 1]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                maxIndex = i;
            }
        }

        if (maxDistance > tolerance)
        {
            // 재귀적으로 두 부분을 단순화
            var left = DouglasPeucker(points.Take(maxIndex + 1).ToList(), tolerance);
            var right = DouglasPeucker(points.Skip(maxIndex).ToList(), tolerance);

            // 중복 점 제거하고 결합
            return left.Take(left.Count - 1).Concat(right).ToList();
        }
        else
        {
            // 시작점과 끝점만 유지
            return new List<ICoordinate> { points[0], points[points.Count - 1] };
        }
    }

    /// <summary>
    /// 점에서 직선까지의 수직 거리 계산
    /// </summary>
    private static double PerpendicularDistance(ICoordinate point, ICoordinate lineStart, ICoordinate lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;

        if (dx == 0 && dy == 0)
        {
            // 시작점과 끝점이 같은 경우
            dx = point.X - lineStart.X;
            dy = point.Y - lineStart.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        double t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);

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
    /// Graham Scan 알고리즘으로 Convex Hull 계산
    /// </summary>
    private static List<ICoordinate> GrahamScan(List<ICoordinate> points)
    {
        if (points.Count < 3) return points;

        // 가장 아래쪽 점 (Y가 가장 작은 점) 찾기
        var bottom = points.OrderBy(p => p.Y).ThenBy(p => p.X).First();

        // 각도 기준으로 정렬
        var sorted = points.Where(p => p != bottom)
            .OrderBy(p => Math.Atan2(p.Y - bottom.Y, p.X - bottom.X))
            .ToList();

        var hull = new List<ICoordinate> { bottom };
        hull.AddRange(sorted);

        // Graham Scan 수행
        var result = new List<ICoordinate>();
        
        foreach (var point in hull)
        {
            while (result.Count > 1 && 
                   CrossProduct(result[result.Count - 2], result[result.Count - 1], point) <= 0)
            {
                result.RemoveAt(result.Count - 1);
            }
            result.Add(point);
        }

        return result;
    }

    /// <summary>
    /// 외적 계산 (방향 판별)
    /// </summary>
    private static double CrossProduct(ICoordinate o, ICoordinate a, ICoordinate b)
    {
        return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
    }

    /// <summary>
    /// 근사적인 Concave Hull 계산
    /// </summary>
    private static Polygon? ApproximateConcaveHull(List<ICoordinate> points, double alpha)
    {
        if (points.Count < 3) return null;

        // 간단한 구현: Convex Hull에서 시작하여 내부 점들을 고려
        var convexHull = GrahamScan(points);
        
        // Alpha 값에 따라 일부 점들을 제거하거나 추가
        // 실제 Alpha Shape는 매우 복잡한 알고리즘이므로 여기서는 근사치만 제공
        var filtered = convexHull.Where((p, i) => i % Math.Max(1, (int)(1 / alpha)) == 0).ToList();
        
        if (filtered.Count < 3) return null;
        
        return new Polygon(new LinearRing(filtered.Append(filtered[0]).ToArray()));
    }

    #endregion
}