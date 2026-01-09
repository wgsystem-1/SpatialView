using System.Collections.Concurrent;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 기본 공간 연산 구현
/// OGC Simple Features 스펙을 따른 공간 분석 기능 제공
/// </summary>
public static class SpatialOperations
{
    /// <summary>
    /// 지오메트리 주위에 버퍼 생성
    /// </summary>
    /// <param name="geometry">입력 지오메트리</param>
    /// <param name="distance">버퍼 거리</param>
    /// <param name="segments">원호 세그먼트 수 (기본값: 8)</param>
    /// <returns>버퍼 폴리곤</returns>
    public static Geometry.IGeometry Buffer(Geometry.IGeometry geometry, double distance, int segments = 8)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));
        
        if (Math.Abs(distance) < double.Epsilon)
            return geometry;

        return geometry switch
        {
            Geometry.Point point => BufferPoint(point, distance, segments),
            Geometry.LineString lineString => BufferLineString(lineString, distance, segments),
            Geometry.Polygon polygon => BufferPolygon(polygon, distance, segments),
            Geometry.MultiPoint multiPoint => BufferMultiGeometry(multiPoint, distance, segments),
            Geometry.MultiLineString multiLineString => BufferMultiGeometry(multiLineString, distance, segments),
            Geometry.MultiPolygon multiPolygon => BufferMultiGeometry(multiPolygon, distance, segments),
            _ => throw new NotSupportedException($"Buffer operation not supported for {geometry.GetType().Name}")
        };
    }

    /// <summary>
    /// 두 지오메트리의 교집합 계산
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>교집합 지오메트리</returns>
    public static Geometry.IGeometry? Intersection(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null || geom2 == null)
            return null;

        // 바운딩 박스 교차 테스트 먼저 수행
        if (!geom1.GetBounds().Intersects(geom2.GetBounds()))
            return Geometry.GeometryCollection.Empty;

        return (geom1, geom2) switch
        {
            (Geometry.Point p1, Geometry.Point p2) => IntersectionPointPoint(p1, p2),
            (Geometry.Point p, Geometry.LineString l) => IntersectionPointLineString(p, l),
            (Geometry.LineString l, Geometry.Point p) => IntersectionPointLineString(p, l),
            (Geometry.Point p, Geometry.Polygon poly) => IntersectionPointPolygon(p, poly),
            (Geometry.Polygon poly, Geometry.Point p) => IntersectionPointPolygon(p, poly),
            (Geometry.LineString l1, Geometry.LineString l2) => IntersectionLineStringLineString(l1, l2),
            (Geometry.LineString l, Geometry.Polygon poly) => IntersectionLineStringPolygon(l, poly),
            (Geometry.Polygon poly, Geometry.LineString l) => IntersectionLineStringPolygon(l, poly),
            (Geometry.Polygon p1, Geometry.Polygon p2) => IntersectionPolygonPolygon(p1, p2),
            _ => IntersectionGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// 두 지오메트리의 합집합 계산
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>합집합 지오메트리</returns>
    public static Geometry.IGeometry Union(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null) return geom2;
        if (geom2 == null) return geom1;

        return (geom1, geom2) switch
        {
            (Geometry.Point p1, Geometry.Point p2) => UnionPointPoint(p1, p2),
            (Geometry.LineString l1, Geometry.LineString l2) => UnionLineStringLineString(l1, l2),
            (Geometry.Polygon p1, Geometry.Polygon p2) => UnionPolygonPolygon(p1, p2),
            _ => UnionGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// 두 지오메트리의 차집합 계산 (A - B)
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리 (A)</param>
    /// <param name="geom2">두 번째 지오메트리 (B)</param>
    /// <returns>차집합 지오메트리</returns>
    public static Geometry.IGeometry? Difference(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null) return null;
        if (geom2 == null) return geom1;

        // 바운딩 박스 교차 테스트
        if (!geom1.GetBounds().Intersects(geom2.GetBounds()))
            return geom1;

        return (geom1, geom2) switch
        {
            (Geometry.Polygon p1, Geometry.Polygon p2) => DifferencePolygonPolygon(p1, p2),
            _ => DifferenceGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// 두 지오메트리의 대칭 차집합 계산 ((A - B) ∪ (B - A))
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>대칭 차집합 지오메트리</returns>
    public static Geometry.IGeometry? SymmetricDifference(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null) return geom2;
        if (geom2 == null) return geom1;

        var diff1 = Difference(geom1, geom2);
        var diff2 = Difference(geom2, geom1);

        if (diff1 == null) return diff2;
        if (diff2 == null) return diff1;

        return Union(diff1, diff2);
    }

    /// <summary>
    /// 두 지오메트리 간의 거리 계산
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>최단 거리</returns>
    public static double Distance(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null || geom2 == null)
            return double.PositiveInfinity;

        return (geom1, geom2) switch
        {
            (Geometry.Point p1, Geometry.Point p2) => DistancePointPoint(p1, p2),
            (Geometry.Point p, Geometry.LineString l) => DistancePointLineString(p, l),
            (Geometry.LineString l, Geometry.Point p) => DistancePointLineString(p, l),
            (Geometry.Point p, Geometry.Polygon poly) => DistancePointPolygon(p, poly),
            (Geometry.Polygon poly, Geometry.Point p) => DistancePointPolygon(p, poly),
            (Geometry.LineString l1, Geometry.LineString l2) => DistanceLineStringLineString(l1, l2),
            _ => DistanceGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// 지오메트리의 면적 계산
    /// </summary>
    /// <param name="geometry">면적을 계산할 지오메트리</param>
    /// <returns>면적 (폴리곤이 아닌 경우 0)</returns>
    public static double Area(Geometry.IGeometry geometry)
    {
        return geometry switch
        {
            Geometry.Polygon polygon => CalculatePolygonArea(polygon),
            Geometry.MultiPolygon multiPolygon => multiPolygon.Geometries.Cast<Geometry.Polygon>().Sum(CalculatePolygonArea),
            _ => 0.0
        };
    }

    /// <summary>
    /// 지오메트리의 둘레/길이 계산
    /// </summary>
    /// <param name="geometry">길이를 계산할 지오메트리</param>
    /// <returns>둘레 또는 길이</returns>
    public static double Length(Geometry.IGeometry geometry)
    {
        return geometry switch
        {
            Geometry.LineString lineString => CalculateLineStringLength(lineString),
            Geometry.Polygon polygon => CalculatePolygonPerimeter(polygon),
            Geometry.MultiLineString multiLineString => multiLineString.Geometries.Cast<Geometry.LineString>().Sum(CalculateLineStringLength),
            Geometry.MultiPolygon multiPolygon => multiPolygon.Geometries.Cast<Geometry.Polygon>().Sum(CalculatePolygonPerimeter),
            _ => 0.0
        };
    }

    #region Point Buffer Implementation

    private static Geometry.Polygon BufferPoint(Geometry.Point point, double distance, int segments)
    {
        var coordinates = new List<Geometry.ICoordinate>();
        var angleStep = 2 * Math.PI / segments;

        for (int i = 0; i <= segments; i++)
        {
            var angle = i * angleStep;
            var x = point.Coordinate.X + distance * Math.Cos(angle);
            var y = point.Coordinate.Y + distance * Math.Sin(angle);
            coordinates.Add(new Geometry.Coordinate(x, y));
        }

        return new Geometry.Polygon(new Geometry.LinearRing(coordinates));
    }

    #endregion

    #region LineString Buffer Implementation

    private static Geometry.IGeometry BufferLineString(Geometry.LineString lineString, double distance, int segments)
    {
        if (lineString.Coordinates.Length < 2)
            return Geometry.GeometryCollection.Empty;

        var bufferPolygons = new List<Geometry.Polygon>();
        var coords = lineString.Coordinates.ToList();

        // 각 선분에 대해 버퍼 생성
        for (int i = 0; i < coords.Count - 1; i++)
        {
            var segmentBuffer = BufferLineSegment(coords[i], coords[i + 1], distance, segments);
            bufferPolygons.Add(segmentBuffer);
        }

        // 끝점에 반원 캡 추가
        var startCap = BufferLineCap(coords[0], coords[1], distance, segments, true);
        var endCap = BufferLineCap(coords[coords.Count - 1], coords[coords.Count - 2], distance, segments, false);
        
        bufferPolygons.Add(startCap);
        bufferPolygons.Add(endCap);

        // 모든 버퍼 폴리곤을 합집합으로 결합
        return UnionPolygons(bufferPolygons);
    }

    private static Geometry.Polygon BufferLineSegment(Geometry.ICoordinate start, Geometry.ICoordinate end, double distance, int segments)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length < double.Epsilon)
            return BufferPoint(new Geometry.Point(start.X, start.Y), distance, segments);

        // 정규화된 방향 벡터
        var nx = dx / length;
        var ny = dy / length;

        // 수직 벡터 (왼쪽으로 90도 회전)
        var px = -ny * distance;
        var py = nx * distance;

        var coordinates = new List<Geometry.ICoordinate>
        {
            new Geometry.Coordinate(start.X + px, start.Y + py),
            new Geometry.Coordinate(end.X + px, end.Y + py),
            new Geometry.Coordinate(end.X - px, end.Y - py),
            new Geometry.Coordinate(start.X - px, start.Y - py),
            new Geometry.Coordinate(start.X + px, start.Y + py) // 닫힌 링
        };

        return new Geometry.Polygon(new Geometry.LinearRing(coordinates));
    }

    private static Geometry.Polygon BufferLineCap(Geometry.ICoordinate point, Geometry.ICoordinate direction, double distance, int segments, bool isStart)
    {
        var dx = direction.X - point.X;
        var dy = direction.Y - point.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length < double.Epsilon)
            return Geometry.Polygon.Empty;

        var nx = dx / length;
        var ny = dy / length;

        var coordinates = new List<Geometry.ICoordinate>();
        var startAngle = isStart ? Math.Atan2(ny, nx) + Math.PI : Math.Atan2(ny, nx);
        var angleStep = Math.PI / segments;

        for (int i = 0; i <= segments; i++)
        {
            var angle = startAngle + i * angleStep;
            var x = point.X + distance * Math.Cos(angle);
            var y = point.Y + distance * Math.Sin(angle);
            coordinates.Add(new Geometry.Coordinate(x, y));
        }

        // 링 닫기
        if (coordinates.Count > 0 && !coordinates.First().Equals(coordinates.Last()))
        {
            coordinates.Add(coordinates.First());
        }

        return new Geometry.Polygon(new Geometry.LinearRing(coordinates));
    }

    #endregion

    #region Polygon Buffer Implementation

    private static Geometry.IGeometry BufferPolygon(Geometry.Polygon polygon, double distance, int segments)
    {
        if (distance > 0)
        {
            // 확장 버퍼 - 외부 링을 확장하고 내부 링을 축소
            var expandedExterior = BufferLinearRing(polygon.ExteriorRing, distance, segments, true);
            var contractedHoles = new List<Geometry.LinearRing>();

            foreach (var hole in polygon.InteriorRings)
            {
                var contracted = BufferLinearRing(hole, -distance, segments, false);
                if (contracted != null && !contracted.IsEmpty)
                    contractedHoles.Add(contracted);
            }

            return new Geometry.Polygon(
                new Geometry.LinearRing(expandedExterior.Coordinates), 
                contractedHoles.ToArray());
        }
        else
        {
            // 축소 버퍼 - 외부 링을 축소
            var contractedExterior = BufferLinearRing(polygon.ExteriorRing, distance, segments, true);
            if (contractedExterior == null || contractedExterior.IsEmpty)
                return Geometry.Polygon.Empty;

            // 내부 링은 확장되어 외부 링과 병합될 수 있음
            return new Geometry.Polygon(new Geometry.LinearRing(contractedExterior.Coordinates));
        }
    }

    private static Geometry.LinearRing? BufferLinearRing(Geometry.LinearRing ring, double distance, int segments, bool isExterior)
    {
        var coords = ring.Coordinates.ToList();
        if (coords.Count < 4) return null;

        var offsetCoords = new List<Geometry.ICoordinate>();

        for (int i = 0; i < coords.Count - 1; i++) // -1 because last coordinate is same as first
        {
            var prev = coords[i == 0 ? coords.Count - 2 : i - 1];
            var curr = coords[i];
            var next = coords[i + 1];

            var offsetPoint = CalculateOffsetPoint(prev, curr, next, distance, isExterior);
            if (offsetPoint != null)
                offsetCoords.Add(offsetPoint);
        }

        if (offsetCoords.Count < 3) return null;

        // 링 닫기
        if (!offsetCoords.First().Equals(offsetCoords.Last()))
            offsetCoords.Add(offsetCoords.First());

        return new Geometry.LinearRing(offsetCoords);
    }

    private static Geometry.ICoordinate? CalculateOffsetPoint(Geometry.ICoordinate prev, Geometry.ICoordinate curr, Geometry.ICoordinate next, double distance, bool isExterior)
    {
        // 벡터 계산
        var v1x = curr.X - prev.X;
        var v1y = curr.Y - prev.Y;
        var v2x = next.X - curr.X;
        var v2y = next.Y - curr.Y;

        var len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
        var len2 = Math.Sqrt(v2x * v2x + v2y * v2y);

        if (len1 < double.Epsilon || len2 < double.Epsilon)
            return null;

        // 정규화
        v1x /= len1; v1y /= len1;
        v2x /= len2; v2y /= len2;

        // 각도 이등분선 계산
        var bisX = (v1x + v2x) / 2;
        var bisY = (v1y + v2y) / 2;
        var bisLen = Math.Sqrt(bisX * bisX + bisY * bisY);

        if (bisLen < double.Epsilon)
        {
            // 180도 각도인 경우 수직 벡터 사용
            bisX = -v1y;
            bisY = v1x;
            bisLen = 1.0;
        }

        bisX /= bisLen;
        bisY /= bisLen;

        // 외부/내부에 따라 방향 조정
        if (!isExterior)
        {
            bisX = -bisX;
            bisY = -bisY;
        }

        // 오프셋 거리 계산 (각도에 따른 보정)
        var angle = Math.Abs(Math.Atan2(v1x * v2y - v1y * v2x, v1x * v2x + v1y * v2y));
        var offsetDistance = distance / Math.Sin(angle / 2);

        return new Geometry.Coordinate(
            curr.X + offsetDistance * bisX,
            curr.Y + offsetDistance * bisY);
    }

    #endregion

    #region Multi-Geometry Buffer Implementation

    private static Geometry.IGeometry BufferMultiGeometry(Geometry.IGeometry multiGeometry, double distance, int segments)
    {
        var geometries = multiGeometry switch
        {
            Geometry.MultiPoint mp => mp.Geometries,
            Geometry.MultiLineString mls => mls.Geometries,
            Geometry.MultiPolygon mpoly => mpoly.Geometries,
            Geometry.GeometryCollection gc => gc.Geometries,
            _ => throw new ArgumentException("Not a multi-geometry type")
        };

        var buffers = geometries
            .Select(geom => Buffer(geom, distance, segments))
            .Where(buffer => buffer != null && !buffer.IsEmpty)
            .ToList();

        return buffers.Count switch
        {
            0 => Geometry.GeometryCollection.Empty,
            1 => buffers[0],
            _ => UnionGeometries(buffers)
        };
    }

    #endregion

    #region Intersection Implementations

    private static Geometry.IGeometry? IntersectionPointPoint(Geometry.Point p1, Geometry.Point p2)
    {
        return p1.Coordinate.Equals(p2.Coordinate) ? p1 : null;
    }

    private static Geometry.IGeometry? IntersectionPointLineString(Geometry.Point point, Geometry.LineString lineString)
    {
        var coords = lineString.Coordinates.ToList();
        for (int i = 0; i < coords.Count - 1; i++)
        {
            if (IsPointOnLineSegment(point.Coordinate, coords[i], coords[i + 1]))
                return point;
        }
        return null;
    }

    private static Geometry.IGeometry? IntersectionPointPolygon(Geometry.Point point, Geometry.Polygon polygon)
    {
        return TopologicalRelations.Contains(polygon, point) ? point : null;
    }

    private static Geometry.IGeometry? IntersectionLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        var intersectionPoints = new List<Geometry.Point>();
        var coords1 = l1.Coordinates.ToList();
        var coords2 = l2.Coordinates.ToList();

        for (int i = 0; i < coords1.Count - 1; i++)
        {
            for (int j = 0; j < coords2.Count - 1; j++)
            {
                var intersection = LineSegmentIntersection(coords1[i], coords1[i + 1], coords2[j], coords2[j + 1]);
                if (intersection != null)
                {
                    intersectionPoints.Add(new Geometry.Point(intersection.X, intersection.Y));
                }
            }
        }

        return intersectionPoints.Count switch
        {
            0 => null,
            1 => intersectionPoints[0],
            _ => new Geometry.MultiPoint(intersectionPoints)
        };
    }

    private static Geometry.IGeometry? IntersectionLineStringPolygon(Geometry.LineString lineString, Geometry.Polygon polygon)
    {
        // 간단한 구현: 라인스트링의 각 세그먼트가 폴리곤과 교차하는 부분을 찾음
        var intersectedSegments = new List<Geometry.LineString>();
        var coords = lineString.Coordinates.ToList();

        for (int i = 0; i < coords.Count - 1; i++)
        {
            var start = coords[i];
            var end = coords[i + 1];
            
            // 세그먼트가 폴리곤 내부에 있는지 확인
            var startInside = TopologicalRelations.Contains(polygon, new Geometry.Point(start.X, start.Y));
            var endInside = TopologicalRelations.Contains(polygon, new Geometry.Point(end.X, end.Y));

            if (startInside && endInside)
            {
                // 전체 세그먼트가 내부에 있음
                intersectedSegments.Add(new Geometry.LineString(new[] { start, end }));
            }
            else if (startInside || endInside)
            {
                // 부분적으로 교차함 - 교차점 찾기 필요
                var clippedSegment = ClipLineSegmentToPolygon(start, end, polygon);
                if (clippedSegment != null)
                    intersectedSegments.Add(clippedSegment);
            }
        }

        return intersectedSegments.Count switch
        {
            0 => null,
            1 => intersectedSegments[0],
            _ => new Geometry.MultiLineString(intersectedSegments)
        };
    }

    private static Geometry.IGeometry? IntersectionPolygonPolygon(Geometry.Polygon p1, Geometry.Polygon p2)
    {
        // 복잡한 폴리곤 교집합 - 간단한 바운딩 박스 기반 구현
        var bounds1 = p1.GetBounds();
        var bounds2 = p2.GetBounds();
        var intersection = bounds1.Intersection(bounds2);

        if (intersection == null || intersection.IsNull)
            return null;

        // 실제 폴리곤 교집합은 매우 복잡하므로 여기서는 간단한 근사치 반환
        var coords = new List<Geometry.ICoordinate>
        {
            new Geometry.Coordinate(intersection.MinX, intersection.MinY),
            new Geometry.Coordinate(intersection.MaxX, intersection.MinY),
            new Geometry.Coordinate(intersection.MaxX, intersection.MaxY),
            new Geometry.Coordinate(intersection.MinX, intersection.MaxY),
            new Geometry.Coordinate(intersection.MinX, intersection.MinY)
        };

        return new Geometry.Polygon(new Geometry.LinearRing(coords));
    }

    private static Geometry.IGeometry? IntersectionGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 일반적인 교집합 - 간단한 구현
        var bounds1 = geom1.GetBounds();
        var bounds2 = geom2.GetBounds();
        var intersection = bounds1.Intersection(bounds2);

        if (intersection == null || intersection.IsNull)
            return null;

        return new Geometry.GeometryCollection(new[] { geom1, geom2 });
    }

    #endregion

    #region Union Implementations

    private static Geometry.IGeometry UnionPointPoint(Geometry.Point p1, Geometry.Point p2)
    {
        return p1.Coordinate.Equals(p2.Coordinate) ? p1 : new Geometry.MultiPoint(new[] { p1, p2 });
    }

    private static Geometry.IGeometry UnionLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        // 간단한 구현: 두 라인을 MultiLineString으로 결합
        return new Geometry.MultiLineString(new[] { l1, l2 });
    }

    private static Geometry.IGeometry UnionPolygonPolygon(Geometry.Polygon p1, Geometry.Polygon p2)
    {
        // 복잡한 폴리곤 합집합 - 간단한 구현으로 MultiPolygon 반환
        return new Geometry.MultiPolygon(new[] { p1, p2 });
    }

    private static Geometry.IGeometry UnionGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        return new Geometry.GeometryCollection(new[] { geom1, geom2 });
    }

    private static Geometry.IGeometry UnionPolygons(IEnumerable<Geometry.Polygon> polygons)
    {
        var polygonList = polygons.ToList();
        return polygonList.Count switch
        {
            0 => Geometry.GeometryCollection.Empty,
            1 => polygonList[0],
            _ => new Geometry.MultiPolygon(polygonList)
        };
    }

    private static Geometry.IGeometry UnionGeometries(IEnumerable<Geometry.IGeometry> geometries)
    {
        var geometryList = geometries.ToList();
        return geometryList.Count switch
        {
            0 => Geometry.GeometryCollection.Empty,
            1 => geometryList[0],
            _ => new Geometry.GeometryCollection(geometryList)
        };
    }

    #endregion

    #region Difference Implementations

    private static Geometry.IGeometry? DifferencePolygonPolygon(Geometry.Polygon p1, Geometry.Polygon p2)
    {
        // 복잡한 폴리곤 차집합 - 간단한 구현
        var bounds1 = p1.GetBounds();
        var bounds2 = p2.GetBounds();

        if (!bounds1.Intersects(bounds2))
            return p1; // 교차하지 않으면 원본 반환

        // 실제로는 매우 복잡한 알고리즘 필요 - 여기서는 원본 반환
        return p1;
    }

    private static Geometry.IGeometry? DifferenceGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 일반적인 차집합 - 간단한 구현
        return geom1;
    }

    #endregion

    #region Distance Calculations

    private static double DistancePointPoint(Geometry.Point p1, Geometry.Point p2)
    {
        var dx = p1.Coordinate.X - p2.Coordinate.X;
        var dy = p1.Coordinate.Y - p2.Coordinate.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistancePointLineString(Geometry.Point point, Geometry.LineString lineString)
    {
        var minDistance = double.PositiveInfinity;
        var coords = lineString.Coordinates.ToList();

        for (int i = 0; i < coords.Count - 1; i++)
        {
            var distance = DistancePointToLineSegment(point.Coordinate, coords[i], coords[i + 1]);
            minDistance = Math.Min(minDistance, distance);
        }

        return minDistance;
    }

    private static double DistancePointPolygon(Geometry.Point point, Geometry.Polygon polygon)
    {
        if (TopologicalRelations.Contains(polygon, point))
            return 0.0; // 포인트가 폴리곤 내부에 있음

        // 외부 링과의 거리 계산
        var distance = DistancePointLineString(point, polygon.ExteriorRing);

        // 내부 링들과의 거리도 확인
        foreach (var hole in polygon.InteriorRings)
        {
            var holeDistance = DistancePointLineString(point, hole);
            distance = Math.Min(distance, holeDistance);
        }

        return distance;
    }

    private static double DistanceLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        var minDistance = double.PositiveInfinity;
        var coords1 = l1.Coordinates.ToList();
        var coords2 = l2.Coordinates.ToList();

        for (int i = 0; i < coords1.Count - 1; i++)
        {
            for (int j = 0; j < coords2.Count - 1; j++)
            {
                var distance = DistanceLineSegmentToLineSegment(coords1[i], coords1[i + 1], coords2[j], coords2[j + 1]);
                minDistance = Math.Min(minDistance, distance);
            }
        }

        return minDistance;
    }

    private static double DistanceGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 간단한 구현: 바운딩 박스 간 거리
        var bounds1 = geom1.GetBounds();
        var bounds2 = geom2.GetBounds();

        if (bounds1.Intersects(bounds2))
            return 0.0;

        var dx = Math.Max(0, Math.Max(bounds1.MinX - bounds2.MaxX, bounds2.MinX - bounds1.MaxX));
        var dy = Math.Max(0, Math.Max(bounds1.MinY - bounds2.MaxY, bounds2.MinY - bounds1.MaxY));

        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistancePointToLineSegment(Geometry.ICoordinate point, Geometry.ICoordinate lineStart, Geometry.ICoordinate lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < double.Epsilon)
        {
            // 선분이 점인 경우
            var px = point.X - lineStart.X;
            var py = point.Y - lineStart.Y;
            return Math.Sqrt(px * px + py * py);
        }

        var t = Math.Max(0, Math.Min(1, ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / lengthSquared));
        var projectionX = lineStart.X + t * dx;
        var projectionY = lineStart.Y + t * dy;
        var distanceX = point.X - projectionX;
        var distanceY = point.Y - projectionY;

        return Math.Sqrt(distanceX * distanceX + distanceY * distanceY);
    }

    private static double DistanceLineSegmentToLineSegment(Geometry.ICoordinate a1, Geometry.ICoordinate a2, Geometry.ICoordinate b1, Geometry.ICoordinate b2)
    {
        // 두 선분 간의 최단 거리 계산
        var distances = new[]
        {
            DistancePointToLineSegment(a1, b1, b2),
            DistancePointToLineSegment(a2, b1, b2),
            DistancePointToLineSegment(b1, a1, a2),
            DistancePointToLineSegment(b2, a1, a2)
        };

        return distances.Min();
    }

    #endregion

    #region Area and Length Calculations

    private static double CalculatePolygonArea(Geometry.Polygon polygon)
    {
        var area = CalculateRingArea(polygon.ExteriorRing);
        
        // 구멍 면적 빼기
        foreach (var hole in polygon.InteriorRings)
        {
            area -= CalculateRingArea(hole);
        }

        return Math.Abs(area);
    }

    private static double CalculateRingArea(Geometry.LinearRing ring)
    {
        var coords = ring.Coordinates.ToList();
        if (coords.Count < 4) return 0.0;

        double area = 0.0;
        for (int i = 0; i < coords.Count - 1; i++)
        {
            var j = (i + 1) % (coords.Count - 1);
            area += coords[i].X * coords[j].Y;
            area -= coords[j].X * coords[i].Y;
        }

        return area / 2.0;
    }

    private static double CalculateLineStringLength(Geometry.LineString lineString)
    {
        var coords = lineString.Coordinates.ToList();
        if (coords.Count < 2) return 0.0;

        double length = 0.0;
        for (int i = 0; i < coords.Count - 1; i++)
        {
            var dx = coords[i + 1].X - coords[i].X;
            var dy = coords[i + 1].Y - coords[i].Y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }

        return length;
    }

    private static double CalculatePolygonPerimeter(Geometry.Polygon polygon)
    {
        var perimeter = CalculateLineStringLength(polygon.ExteriorRing);
        
        // 구멍들의 둘레 더하기
        foreach (var hole in polygon.InteriorRings)
        {
            perimeter += CalculateLineStringLength(hole);
        }

        return perimeter;
    }

    #endregion

    #region Helper Methods

    private static bool IsPointOnLineSegment(Geometry.ICoordinate point, Geometry.ICoordinate lineStart, Geometry.ICoordinate lineEnd)
    {
        var distance = DistancePointToLineSegment(point, lineStart, lineEnd);
        return distance < 1e-10; // 허용 오차
    }

    private static Geometry.ICoordinate? LineSegmentIntersection(Geometry.ICoordinate p1, Geometry.ICoordinate p2, Geometry.ICoordinate p3, Geometry.ICoordinate p4)
    {
        var x1 = p1.X; var y1 = p1.Y;
        var x2 = p2.X; var y2 = p2.Y;
        var x3 = p3.X; var y3 = p3.Y;
        var x4 = p4.X; var y4 = p4.Y;

        var denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denominator) < 1e-10) return null; // 평행선

        var t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denominator;
        var u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denominator;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            var x = x1 + t * (x2 - x1);
            var y = y1 + t * (y2 - y1);
            return new Geometry.Coordinate(x, y);
        }

        return null;
    }

    private static Geometry.LineString? ClipLineSegmentToPolygon(Geometry.ICoordinate start, Geometry.ICoordinate end, Geometry.Polygon polygon)
    {
        // 간단한 구현 - 실제로는 복잡한 클리핑 알고리즘 필요
        var startInside = TopologicalRelations.Contains(polygon, new Geometry.Point(start.X, start.Y));
        var endInside = TopologicalRelations.Contains(polygon, new Geometry.Point(end.X, end.Y));

        if (startInside && endInside)
            return new Geometry.LineString(new[] { start, end });
        
        if (!startInside && !endInside)
            return null;

        // 부분적 교차 - 교차점 찾기 (복잡한 구현 필요)
        return new Geometry.LineString(new[] { start, end });
    }

    #endregion
}

/// <summary>
/// 병렬 공간 연산을 위한 확장
/// </summary>
public static class ParallelSpatialOperations
{
    /// <summary>
    /// 다중 지오메트리에 대한 병렬 버퍼 연산
    /// </summary>
    /// <param name="geometries">입력 지오메트리 컬렉션</param>
    /// <param name="distance">버퍼 거리</param>
    /// <param name="segments">원호 세그먼트 수</param>
    /// <returns>버퍼 결과 컬렉션</returns>
    public static IEnumerable<Geometry.IGeometry> BufferParallel(IEnumerable<Geometry.IGeometry> geometries, double distance, int segments = 8)
    {
        return geometries.AsParallel().Select(geom => SpatialOperations.Buffer(geom, distance, segments));
    }

    /// <summary>
    /// 다중 지오메트리 간의 병렬 거리 계산
    /// </summary>
    /// <param name="geometries1">첫 번째 지오메트리 컬렉션</param>
    /// <param name="geometries2">두 번째 지오메트리 컬렉션</param>
    /// <returns>거리 행렬</returns>
    public static double[,] DistanceMatrixParallel(IList<Geometry.IGeometry> geometries1, IList<Geometry.IGeometry> geometries2)
    {
        var result = new double[geometries1.Count, geometries2.Count];
        var indices = new List<(int i, int j)>();

        for (int i = 0; i < geometries1.Count; i++)
        {
            for (int j = 0; j < geometries2.Count; j++)
            {
                indices.Add((i, j));
            }
        }

        Parallel.ForEach(indices, pair =>
        {
            var (i, j) = pair;
            result[i, j] = SpatialOperations.Distance(geometries1[i], geometries2[j]);
        });

        return result;
    }
}