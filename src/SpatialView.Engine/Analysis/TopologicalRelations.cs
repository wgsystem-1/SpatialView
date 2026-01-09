namespace SpatialView.Engine.Analysis;

/// <summary>
/// 위상 관계 판단 구현
/// OGC Simple Features 스펙의 DE-9IM (Dimensionally Extended 9-Intersection Model) 기반
/// </summary>
public static class TopologicalRelations
{
    /// <summary>
    /// 첫 번째 지오메트리가 두 번째 지오메트리를 포함하는지 확인
    /// </summary>
    /// <param name="container">포함하는 지오메트리</param>
    /// <param name="contained">포함되는 지오메트리</param>
    /// <returns>포함 관계 여부</returns>
    public static bool Contains(Geometry.IGeometry container, Geometry.IGeometry contained)
    {
        if (container == null || contained == null)
            return false;

        // 바운딩 박스 체크 먼저
        if (!container.GetBounds().Contains(contained.GetBounds()))
            return false;

        return (container, contained) switch
        {
            (Geometry.Point p1, Geometry.Point p2) => ContainsPointPoint(p1, p2),
            (Geometry.Polygon poly, Geometry.Point point) => ContainsPolygonPoint(poly, point),
            (Geometry.Polygon poly, Geometry.LineString line) => ContainsPolygonLineString(poly, line),
            (Geometry.Polygon p1, Geometry.Polygon p2) => ContainsPolygonPolygon(p1, p2),
            (Geometry.LineString line, Geometry.Point point) => ContainsLineStringPoint(line, point),
            _ => ContainsGeneral(container, contained)
        };
    }

    /// <summary>
    /// 첫 번째 지오메트리가 두 번째 지오메트리 내부에 있는지 확인
    /// </summary>
    /// <param name="contained">포함되는 지오메트리</param>
    /// <param name="container">포함하는 지오메트리</param>
    /// <returns>포함 관계 여부</returns>
    public static bool Within(Geometry.IGeometry contained, Geometry.IGeometry container)
    {
        return Contains(container, contained);
    }

    /// <summary>
    /// 두 지오메트리가 교차하는지 확인
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>교차 여부</returns>
    public static bool Intersects(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null || geom2 == null)
            return false;

        // 바운딩 박스 교차 테스트 먼저
        if (!geom1.GetBounds().Intersects(geom2.GetBounds()))
            return false;

        return (geom1, geom2) switch
        {
            (Geometry.Point p1, Geometry.Point p2) => IntersectsPointPoint(p1, p2),
            (Geometry.Point p, Geometry.LineString l) => IntersectsPointLineString(p, l),
            (Geometry.LineString l, Geometry.Point p) => IntersectsPointLineString(p, l),
            (Geometry.Point p, Geometry.Polygon poly) => IntersectsPointPolygon(p, poly),
            (Geometry.Polygon poly, Geometry.Point p) => IntersectsPointPolygon(p, poly),
            (Geometry.LineString l1, Geometry.LineString l2) => IntersectsLineStringLineString(l1, l2),
            (Geometry.LineString l, Geometry.Polygon poly) => IntersectsLineStringPolygon(l, poly),
            (Geometry.Polygon poly, Geometry.LineString l) => IntersectsLineStringPolygon(l, poly),
            (Geometry.Polygon p1, Geometry.Polygon p2) => IntersectsPolygonPolygon(p1, p2),
            _ => IntersectsGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// 두 지오메트리가 접촉하는지 확인 (교차하지만 내부가 겹치지 않음)
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>접촉 여부</returns>
    public static bool Touches(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (!Intersects(geom1, geom2))
            return false;

        // 접촉은 교차하지만 내부가 겹치지 않는 경우
        return !InteriorIntersects(geom1, geom2);
    }

    /// <summary>
    /// 두 지오메트리가 교차하는지 확인 (차원이 다른 지오메트리 간의 교차)
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>교차 여부</returns>
    public static bool Crosses(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null || geom2 == null)
            return false;

        return (geom1, geom2) switch
        {
            (Geometry.LineString l, Geometry.Polygon poly) => CrossesLineStringPolygon(l, poly),
            (Geometry.Polygon poly, Geometry.LineString l) => CrossesLineStringPolygon(l, poly),
            (Geometry.LineString l1, Geometry.LineString l2) => CrossesLineStringLineString(l1, l2),
            (Geometry.Point p, Geometry.LineString l) => CrossesPointLineString(p, l),
            (Geometry.LineString l, Geometry.Point p) => CrossesPointLineString(p, l),
            _ => false // Crosses는 특정 차원 조합에서만 적용
        };
    }

    /// <summary>
    /// 두 지오메트리가 겹치는지 확인 (같은 차원에서 내부가 교차)
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>겹침 여부</returns>
    public static bool Overlaps(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null || geom2 == null)
            return false;

        return (geom1, geom2) switch
        {
            (Geometry.Polygon p1, Geometry.Polygon p2) => OverlapsPolygonPolygon(p1, p2),
            (Geometry.LineString l1, Geometry.LineString l2) => OverlapsLineStringLineString(l1, l2),
            (Geometry.Point p1, Geometry.Point p2) => false, // 점은 겹칠 수 없음 (같거나 다름)
            _ => OverlapsGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// 두 지오메트리가 분리되어 있는지 확인
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>분리 여부</returns>
    public static bool Disjoint(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        return !Intersects(geom1, geom2);
    }

    /// <summary>
    /// 두 지오메트리가 동일한지 확인
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>동일 여부</returns>
    public static bool Equals(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null && geom2 == null) return true;
        if (geom1 == null || geom2 == null) return false;

        return (geom1, geom2) switch
        {
            (Geometry.Point p1, Geometry.Point p2) => EqualsPointPoint(p1, p2),
            (Geometry.LineString l1, Geometry.LineString l2) => EqualsLineStringLineString(l1, l2),
            (Geometry.Polygon p1, Geometry.Polygon p2) => EqualsPolygonPolygon(p1, p2),
            _ => EqualsGeneral(geom1, geom2)
        };
    }

    /// <summary>
    /// DE-9IM (Dimensionally Extended 9-Intersection Model) 패턴 매칭
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <param name="pattern">DE-9IM 패턴 (예: "T*F**FFF*")</param>
    /// <returns>패턴 매칭 여부</returns>
    public static bool Relate(Geometry.IGeometry geom1, Geometry.IGeometry geom2, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length != 9)
            throw new ArgumentException("DE-9IM pattern must be exactly 9 characters", nameof(pattern));

        var de9im = ComputeDE9IM(geom1, geom2);
        return MatchesPattern(de9im, pattern);
    }

    /// <summary>
    /// DE-9IM 매트릭스 계산
    /// </summary>
    /// <param name="geom1">첫 번째 지오메트리</param>
    /// <param name="geom2">두 번째 지오메트리</param>
    /// <returns>DE-9IM 매트릭스 문자열</returns>
    public static string ComputeDE9IM(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        if (geom1 == null || geom2 == null)
            return "FFFFFFFFF";

        var matrix = new char[9];

        // Interior(A) ∩ Interior(B)
        matrix[0] = GetIntersectionDimension(GetInterior(geom1), GetInterior(geom2));
        
        // Interior(A) ∩ Boundary(B)
        matrix[1] = GetIntersectionDimension(GetInterior(geom1), GetBoundary(geom2));
        
        // Interior(A) ∩ Exterior(B)
        matrix[2] = GetIntersectionDimension(GetInterior(geom1), GetExterior(geom2));
        
        // Boundary(A) ∩ Interior(B)
        matrix[3] = GetIntersectionDimension(GetBoundary(geom1), GetInterior(geom2));
        
        // Boundary(A) ∩ Boundary(B)
        matrix[4] = GetIntersectionDimension(GetBoundary(geom1), GetBoundary(geom2));
        
        // Boundary(A) ∩ Exterior(B)
        matrix[5] = GetIntersectionDimension(GetBoundary(geom1), GetExterior(geom2));
        
        // Exterior(A) ∩ Interior(B)
        matrix[6] = GetIntersectionDimension(GetExterior(geom1), GetInterior(geom2));
        
        // Exterior(A) ∩ Boundary(B)
        matrix[7] = GetIntersectionDimension(GetExterior(geom1), GetBoundary(geom2));
        
        // Exterior(A) ∩ Exterior(B)
        matrix[8] = GetIntersectionDimension(GetExterior(geom1), GetExterior(geom2));

        return new string(matrix);
    }

    #region Contains Implementations

    private static bool ContainsPointPoint(Geometry.Point p1, Geometry.Point p2)
    {
        return p1.Coordinate.Equals(p2.Coordinate);
    }

    private static bool ContainsPolygonPoint(Geometry.Polygon polygon, Geometry.Point point)
    {
        return IsPointInPolygon(point.Coordinate, polygon);
    }

    private static bool ContainsPolygonLineString(Geometry.Polygon polygon, Geometry.LineString lineString)
    {
        // 모든 점이 폴리곤 내부에 있어야 함
        return lineString.Coordinates.All(coord => IsPointInPolygon(coord, polygon));
    }

    private static bool ContainsPolygonPolygon(Geometry.Polygon container, Geometry.Polygon contained)
    {
        // 모든 외부 링의 점들이 컨테이너 내부에 있어야 함
        var allVerticesInside = contained.ExteriorRing.Coordinates.All(coord => IsPointInPolygon(coord, container));
        
        if (!allVerticesInside)
            return false;

        // 내부 링들도 확인
        return contained.InteriorRings.All(hole => 
            hole.Coordinates.All(coord => IsPointInPolygon(coord, container)));
    }

    private static bool ContainsLineStringPoint(Geometry.LineString lineString, Geometry.Point point)
    {
        return IsPointOnLineString(point.Coordinate, lineString);
    }

    private static bool ContainsGeneral(Geometry.IGeometry container, Geometry.IGeometry contained)
    {
        // 일반적인 경우 - 간단한 바운딩 박스 기반 구현
        return container.GetBounds().Contains(contained.GetBounds());
    }

    #endregion

    #region Intersects Implementations

    private static bool IntersectsPointPoint(Geometry.Point p1, Geometry.Point p2)
    {
        return p1.Coordinate.Equals(p2.Coordinate);
    }

    private static bool IntersectsPointLineString(Geometry.Point point, Geometry.LineString lineString)
    {
        return IsPointOnLineString(point.Coordinate, lineString);
    }

    private static bool IntersectsPointPolygon(Geometry.Point point, Geometry.Polygon polygon)
    {
        return IsPointInPolygonOrOnBoundary(point.Coordinate, polygon);
    }

    private static bool IntersectsLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        var coords1 = l1.Coordinates.ToList();
        var coords2 = l2.Coordinates.ToList();

        for (int i = 0; i < coords1.Count - 1; i++)
        {
            for (int j = 0; j < coords2.Count - 1; j++)
            {
                if (DoLineSegmentsIntersect(coords1[i], coords1[i + 1], coords2[j], coords2[j + 1]))
                    return true;
            }
        }

        return false;
    }

    private static bool IntersectsLineStringPolygon(Geometry.LineString lineString, Geometry.Polygon polygon)
    {
        var coords = lineString.Coordinates.ToList();
        
        // 라인스트링의 일부가 폴리곤과 교차하는지 확인
        for (int i = 0; i < coords.Count - 1; i++)
        {
            // 세그먼트가 폴리곤과 교차하는지 확인
            if (DoesLineSegmentIntersectPolygon(coords[i], coords[i + 1], polygon))
                return true;
        }

        // 라인스트링의 점이 폴리곤 내부에 있는지 확인
        return coords.Any(coord => IsPointInPolygon(coord, polygon));
    }

    private static bool IntersectsPolygonPolygon(Geometry.Polygon p1, Geometry.Polygon p2)
    {
        // 간단한 구현: 첫 번째 폴리곤의 점이 두 번째 폴리곤과 교차하는지 확인
        var coords1 = p1.ExteriorRing.Coordinates.ToList();
        
        for (int i = 0; i < coords1.Count - 1; i++)
        {
            if (DoesLineSegmentIntersectPolygon(coords1[i], coords1[i + 1], p2))
                return true;
        }

        // 반대 방향도 확인
        var coords2 = p2.ExteriorRing.Coordinates.ToList();
        for (int i = 0; i < coords2.Count - 1; i++)
        {
            if (DoesLineSegmentIntersectPolygon(coords2[i], coords2[i + 1], p1))
                return true;
        }

        // 한쪽이 다른 쪽을 완전히 포함하는 경우
        return coords1.Any(coord => IsPointInPolygon(coord, p2)) ||
               coords2.Any(coord => IsPointInPolygon(coord, p1));
    }

    private static bool IntersectsGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        return geom1.GetBounds().Intersects(geom2.GetBounds());
    }

    #endregion

    #region Crosses Implementations

    private static bool CrossesLineStringPolygon(Geometry.LineString lineString, Geometry.Polygon polygon)
    {
        var coords = lineString.Coordinates.ToList();
        bool hasInside = false;
        bool hasOutside = false;

        foreach (var coord in coords)
        {
            if (IsPointInPolygon(coord, polygon))
                hasInside = true;
            else
                hasOutside = true;

            if (hasInside && hasOutside)
                return true;
        }

        return false;
    }

    private static bool CrossesLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        // 라인이 교차하는지 확인 (차원 = 0인 교차점 존재)
        return IntersectsLineStringLineString(l1, l2);
    }

    private static bool CrossesPointLineString(Geometry.Point point, Geometry.LineString lineString)
    {
        // 포인트는 라인스트링과 "crosses" 할 수 없음 (정의상)
        return false;
    }

    #endregion

    #region Overlaps Implementations

    private static bool OverlapsPolygonPolygon(Geometry.Polygon p1, Geometry.Polygon p2)
    {
        // 두 폴리곤이 겹치려면 서로의 내부가 교차해야 함
        return InteriorIntersects(p1, p2) && !Contains(p1, p2) && !Contains(p2, p1);
    }

    private static bool OverlapsLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        // 간단한 구현: 부분적으로 겹치는지 확인
        return IntersectsLineStringLineString(l1, l2);
    }

    private static bool OverlapsGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        return IntersectsGeneral(geom1, geom2) && !Contains(geom1, geom2) && !Contains(geom2, geom1);
    }

    #endregion

    #region Equals Implementations

    private static bool EqualsPointPoint(Geometry.Point p1, Geometry.Point p2)
    {
        return p1.Coordinate.Equals(p2.Coordinate);
    }

    private static bool EqualsLineStringLineString(Geometry.LineString l1, Geometry.LineString l2)
    {
        var coords1 = l1.Coordinates.ToList();
        var coords2 = l2.Coordinates.ToList();

        if (coords1.Count != coords2.Count)
            return false;

        // 같은 방향 확인
        var forwardMatch = coords1.Zip(coords2, (c1, c2) => c1.Equals(c2)).All(x => x);
        if (forwardMatch) return true;

        // 반대 방향 확인
        coords2.Reverse();
        var reverseMatch = coords1.Zip(coords2, (c1, c2) => c1.Equals(c2)).All(x => x);
        return reverseMatch;
    }

    private static bool EqualsPolygonPolygon(Geometry.Polygon p1, Geometry.Polygon p2)
    {
        // 외부 링 비교
        if (!EqualsLineStringLineString(p1.ExteriorRing, p2.ExteriorRing))
            return false;

        // 내부 링 개수 확인
        if (p1.InteriorRings.Count != p2.InteriorRings.Count)
            return false;

        // 내부 링들 비교 (순서는 상관없음)
        var holes1 = p1.InteriorRings.ToList();
        var holes2 = p2.InteriorRings.ToList();

        foreach (var hole1 in holes1)
        {
            if (!holes2.Any(hole2 => EqualsLineStringLineString(hole1, hole2)))
                return false;
        }

        return true;
    }

    private static bool EqualsGeneral(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 간단한 구현: 타입과 바운딩 박스 비교
        return geom1.GetType() == geom2.GetType() && 
               geom1.GetBounds().Equals(geom2.GetBounds());
    }

    #endregion

    #region Helper Methods

    private static bool IsPointInPolygon(Geometry.ICoordinate point, Geometry.Polygon polygon)
    {
        // Ray casting 알고리즘
        var isInside = IsPointInLinearRing(point, polygon.ExteriorRing);
        
        // 홀을 고려
        foreach (var hole in polygon.InteriorRings)
        {
            if (IsPointInLinearRing(point, hole))
                isInside = !isInside;
        }

        return isInside;
    }

    private static bool IsPointInPolygonOrOnBoundary(Geometry.ICoordinate point, Geometry.Polygon polygon)
    {
        return IsPointInPolygon(point, polygon) || IsPointOnPolygonBoundary(point, polygon);
    }

    private static bool IsPointOnPolygonBoundary(Geometry.ICoordinate point, Geometry.Polygon polygon)
    {
        // 외부 링 확인
        if (IsPointOnLineString(point, polygon.ExteriorRing))
            return true;

        // 내부 링들 확인
        return polygon.InteriorRings.Any(hole => IsPointOnLineString(point, hole));
    }

    private static bool IsPointInLinearRing(Geometry.ICoordinate point, Geometry.LinearRing ring)
    {
        var coords = ring.Coordinates.ToList();
        bool inside = false;

        for (int i = 0, j = coords.Count - 1; i < coords.Count; j = i++)
        {
            if (((coords[i].Y > point.Y) != (coords[j].Y > point.Y)) &&
                (point.X < (coords[j].X - coords[i].X) * (point.Y - coords[i].Y) / (coords[j].Y - coords[i].Y) + coords[i].X))
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsPointOnLineString(Geometry.ICoordinate point, Geometry.LineString lineString)
    {
        var coords = lineString.Coordinates.ToList();
        
        for (int i = 0; i < coords.Count - 1; i++)
        {
            if (IsPointOnLineSegment(point, coords[i], coords[i + 1]))
                return true;
        }

        return false;
    }

    private static bool IsPointOnLineSegment(Geometry.ICoordinate point, Geometry.ICoordinate segStart, Geometry.ICoordinate segEnd)
    {
        const double tolerance = 1e-10;

        // 벡터 외적으로 동일 직선상에 있는지 확인
        var crossProduct = (point.Y - segStart.Y) * (segEnd.X - segStart.X) - 
                          (point.X - segStart.X) * (segEnd.Y - segStart.Y);

        if (Math.Abs(crossProduct) > tolerance)
            return false;

        // 점이 세그먼트 범위 내에 있는지 확인
        var dotProduct = (point.X - segStart.X) * (segEnd.X - segStart.X) + 
                        (point.Y - segStart.Y) * (segEnd.Y - segStart.Y);
        
        if (dotProduct < 0) return false;

        var squaredLength = (segEnd.X - segStart.X) * (segEnd.X - segStart.X) + 
                           (segEnd.Y - segStart.Y) * (segEnd.Y - segStart.Y);
        
        return dotProduct <= squaredLength;
    }

    private static bool DoLineSegmentsIntersect(Geometry.ICoordinate p1, Geometry.ICoordinate p2, 
                                               Geometry.ICoordinate p3, Geometry.ICoordinate p4)
    {
        var d1 = Direction(p3, p4, p1);
        var d2 = Direction(p3, p4, p2);
        var d3 = Direction(p1, p2, p3);
        var d4 = Direction(p1, p2, p4);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && 
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        return (d1 == 0 && OnSegment(p3, p4, p1)) ||
               (d2 == 0 && OnSegment(p3, p4, p2)) ||
               (d3 == 0 && OnSegment(p1, p2, p3)) ||
               (d4 == 0 && OnSegment(p1, p2, p4));
    }

    private static double Direction(Geometry.ICoordinate pi, Geometry.ICoordinate pj, Geometry.ICoordinate pk)
    {
        return (pk.X - pi.X) * (pj.Y - pi.Y) - (pj.X - pi.X) * (pk.Y - pi.Y);
    }

    private static bool OnSegment(Geometry.ICoordinate pi, Geometry.ICoordinate pj, Geometry.ICoordinate pk)
    {
        return Math.Min(pi.X, pj.X) <= pk.X && pk.X <= Math.Max(pi.X, pj.X) &&
               Math.Min(pi.Y, pj.Y) <= pk.Y && pk.Y <= Math.Max(pi.Y, pj.Y);
    }

    private static bool DoesLineSegmentIntersectPolygon(Geometry.ICoordinate segStart, Geometry.ICoordinate segEnd, Geometry.Polygon polygon)
    {
        // 외부 링과 교차 확인
        var exteriorCoords = polygon.ExteriorRing.Coordinates.ToList();
        for (int i = 0; i < exteriorCoords.Count - 1; i++)
        {
            if (DoLineSegmentsIntersect(segStart, segEnd, exteriorCoords[i], exteriorCoords[i + 1]))
                return true;
        }

        // 내부 링들과 교차 확인
        foreach (var hole in polygon.InteriorRings)
        {
            var holeCoords = hole.Coordinates.ToList();
            for (int i = 0; i < holeCoords.Count - 1; i++)
            {
                if (DoLineSegmentsIntersect(segStart, segEnd, holeCoords[i], holeCoords[i + 1]))
                    return true;
            }
        }

        return false;
    }

    private static bool InteriorIntersects(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 간단한 구현 - 실제로는 복잡한 내부 교차 계산 필요
        return Intersects(geom1, geom2);
    }

    #endregion

    #region DE-9IM Implementation

    private static GeometryComponent GetInterior(Geometry.IGeometry geometry)
    {
        return new GeometryComponent { Geometry = geometry, ComponentType = ComponentType.Interior };
    }

    private static GeometryComponent GetBoundary(Geometry.IGeometry geometry)
    {
        return new GeometryComponent { Geometry = geometry, ComponentType = ComponentType.Boundary };
    }

    private static GeometryComponent GetExterior(Geometry.IGeometry geometry)
    {
        return new GeometryComponent { Geometry = geometry, ComponentType = ComponentType.Exterior };
    }

    private static char GetIntersectionDimension(GeometryComponent comp1, GeometryComponent comp2)
    {
        // 간단한 구현 - 실제로는 복잡한 차원 계산 필요
        if (comp1.Geometry == null || comp2.Geometry == null)
            return 'F';

        var intersects = Intersects(comp1.Geometry, comp2.Geometry);
        
        if (!intersects)
            return 'F';

        // 교차하는 경우 차원 결정 (간단한 구현)
        return (comp1.Geometry, comp2.Geometry) switch
        {
            (Geometry.Point, Geometry.Point) => '0',
            (Geometry.Point, Geometry.LineString) => '0',
            (Geometry.Point, Geometry.Polygon) => '0',
            (Geometry.LineString, Geometry.LineString) => '1',
            (Geometry.LineString, Geometry.Polygon) => '1',
            (Geometry.Polygon, Geometry.Polygon) => '2',
            _ => 'T' // True이지만 차원을 정확히 모르는 경우
        };
    }

    private static bool MatchesPattern(string de9im, string pattern)
    {
        if (de9im.Length != 9 || pattern.Length != 9)
            return false;

        for (int i = 0; i < 9; i++)
        {
            var computed = de9im[i];
            var expected = pattern[i];

            if (expected == '*') continue; // 와일드카드

            if (expected == 'T' && (computed == '0' || computed == '1' || computed == '2'))
                continue; // T는 임의의 차원을 의미

            if (expected == 'F' && computed == 'F')
                continue; // False 매치

            if (expected == computed)
                continue; // 정확한 매치

            return false; // 매치 실패
        }

        return true;
    }

    #endregion

    #region Supporting Types

    private class GeometryComponent
    {
        public Geometry.IGeometry? Geometry { get; set; }
        public ComponentType ComponentType { get; set; }
    }

    private enum ComponentType
    {
        Interior,
        Boundary,
        Exterior
    }

    #endregion
}