using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Analysis.Operations;

/// <summary>
/// Boolean 연산 (Union, Intersection, Difference, SymmetricDifference)을 수행하는 클래스
/// </summary>
public class BooleanOperation
{
    private readonly double _tolerance;
    
    public BooleanOperation(double tolerance = 1e-9)
    {
        _tolerance = tolerance;
    }
    
    /// <summary>
    /// 두 지오메트리의 Union (합집합)
    /// </summary>
    public IGeometry Union(IGeometry a, IGeometry b)
    {
        if (a == null) return b;
        if (b == null) return a;
        
        // 빈 지오메트리 처리
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        
        // 동일한 타입의 지오메트리 처리
        if (a.GeometryType == b.GeometryType)
        {
            return a.GeometryType switch
            {
                GeometryType.Point => UnionPoints((Point)a, (Point)b),
                GeometryType.LineString => UnionLineStrings((LineString)a, (LineString)b),
                GeometryType.Polygon => UnionPolygons((Polygon)a, (Polygon)b),
                _ => UnionGeneral(a, b)
            };
        }
        
        return UnionGeneral(a, b);
    }
    
    /// <summary>
    /// 두 지오메트리의 Intersection (교집합)
    /// </summary>
    public IGeometry Intersection(IGeometry a, IGeometry b)
    {
        if (a == null || b == null) return new GeometryCollection();
        if (a.IsEmpty || b.IsEmpty) return new GeometryCollection();
        
        // Envelope 검사 - 교집합이 없으면 빈 결과 반환
        var envA = a.Envelope;
        var envB = b.Envelope;
        if (envA == null || envB == null || !envA.Intersects(envB))
            return new GeometryCollection();
        
        return a.GeometryType switch
        {
            GeometryType.Point when b.GeometryType == GeometryType.Point => 
                IntersectPoints((Point)a, (Point)b),
            GeometryType.Point when b.GeometryType == GeometryType.LineString => 
                IntersectPointLine((Point)a, (LineString)b),
            GeometryType.Point when b.GeometryType == GeometryType.Polygon => 
                IntersectPointPolygon((Point)a, (Polygon)b),
            GeometryType.LineString when b.GeometryType == GeometryType.LineString => 
                IntersectLineStrings((LineString)a, (LineString)b),
            GeometryType.LineString when b.GeometryType == GeometryType.Polygon => 
                IntersectLinePolygon((LineString)a, (Polygon)b),
            GeometryType.Polygon when b.GeometryType == GeometryType.Polygon => 
                IntersectPolygons((Polygon)a, (Polygon)b),
            _ => IntersectGeneral(a, b)
        };
    }
    
    /// <summary>
    /// 차집합 (A - B)
    /// </summary>
    public IGeometry Difference(IGeometry a, IGeometry b)
    {
        if (a == null || a.IsEmpty) return new GeometryCollection();
        if (b == null || b.IsEmpty) return a;
        
        // A와 B의 교집합을 구하고, A에서 제거
        var intersection = Intersection(a, b);
        if (intersection.IsEmpty) return a;
        
        return DifferenceInternal(a, b);
    }
    
    /// <summary>
    /// 대칭 차집합 ((A - B) ∪ (B - A))
    /// </summary>
    public IGeometry SymmetricDifference(IGeometry a, IGeometry b)
    {
        if (a == null || a.IsEmpty) return b ?? new GeometryCollection();
        if (b == null || b.IsEmpty) return a;
        
        var aMinusB = Difference(a, b);
        var bMinusA = Difference(b, a);
        
        return Union(aMinusB, bMinusA);
    }
    
    // Point 연산들
    private IGeometry UnionPoints(Point a, Point b)
    {
        if (a.Equals(b)) return a;
        return new MultiPoint(new[] { a, b });
    }
    
    private IGeometry IntersectPoints(Point a, Point b)
    {
        if (a.Equals(b)) return a;
        return new GeometryCollection();
    }
    
    private IGeometry IntersectPointLine(Point point, LineString line)
    {
        // 점이 선 위에 있는지 검사
        for (int i = 0; i < line.Coordinates.Length - 1; i++)
        {
            var p1 = line.Coordinates[i];
            var p2 = line.Coordinates[i + 1];
            
            if (IsPointOnSegment(point.Coordinate, p1, p2))
                return point;
        }
        
        return new GeometryCollection();
    }
    
    private IGeometry IntersectPointPolygon(Point point, Polygon polygon)
    {
        if (IsPointInPolygon(point, polygon))
            return point;
        return new GeometryCollection();
    }
    
    // LineString 연산들
    private IGeometry UnionLineStrings(LineString a, LineString b)
    {
        // 간단한 구현 - 실제로는 선분 병합 알고리즘 필요
        return new MultiLineString(new[] { a, b });
    }
    
    private IGeometry IntersectLineStrings(LineString a, LineString b)
    {
        var intersectionPoints = new List<Point>();
        var intersectionSegments = new List<LineString>();
        
        // 모든 세그먼트 쌍에 대해 교차점 찾기
        for (int i = 0; i < a.Coordinates.Length - 1; i++)
        {
            for (int j = 0; j < b.Coordinates.Length - 1; j++)
            {
                var intersection = GetSegmentIntersection(
                    a.Coordinates[i], a.Coordinates[i + 1],
                    b.Coordinates[j], b.Coordinates[j + 1]);
                
                if (intersection != null)
                {
                    if (intersection is Point p)
                        intersectionPoints.Add(p);
                    else if (intersection is LineString ls)
                        intersectionSegments.Add(ls);
                }
            }
        }
        
        // 결과 조합
        if (intersectionPoints.Count == 0 && intersectionSegments.Count == 0)
            return new GeometryCollection();
        if (intersectionPoints.Count == 1 && intersectionSegments.Count == 0)
            return intersectionPoints[0];
        if (intersectionPoints.Count == 0 && intersectionSegments.Count == 1)
            return intersectionSegments[0];
        
        var geometries = new List<IGeometry>();
        geometries.AddRange(intersectionPoints);
        geometries.AddRange(intersectionSegments);
        return new GeometryCollection(geometries.ToArray());
    }
    
    private IGeometry IntersectLinePolygon(LineString line, Polygon polygon)
    {
        var clippedSegments = new List<LineString>();
        
        // 선분별로 폴리곤과의 교차 검사
        for (int i = 0; i < line.Coordinates.Length - 1; i++)
        {
            var p1 = line.Coordinates[i];
            var p2 = line.Coordinates[i + 1];
            
            var clipped = ClipSegmentToPolygon(p1, p2, polygon);
            if (clipped.Count > 0)
                clippedSegments.AddRange(clipped);
        }
        
        if (clippedSegments.Count == 0)
            return new GeometryCollection();
        if (clippedSegments.Count == 1)
            return clippedSegments[0];
        
        return new MultiLineString(clippedSegments.ToArray());
    }
    
    // Polygon 연산들
    private IGeometry UnionPolygons(Polygon a, Polygon b)
    {
        // Sutherland-Hodgman 알고리즘의 변형 사용
        var result = new List<Polygon>();
        
        // 교집합이 없으면 MultiPolygon 반환
        if (!a.Envelope!.Intersects(b.Envelope!))
        {
            return new MultiPolygon(new[] { a, b });
        }
        
        // 간단한 경우: 한 폴리곤이 다른 폴리곤을 포함
        if (IsPolygonInsidePolygon(a, b))
            return b;
        if (IsPolygonInsidePolygon(b, a))
            return a;
        
        // 복잡한 Union - 실제 구현에서는 더 정교한 알고리즘 필요
        var unionPoly = MergePolygons(a, b);
        return (IGeometry)unionPoly ?? new MultiPolygon(new[] { a, b });
    }
    
    private IGeometry IntersectPolygons(Polygon a, Polygon b)
    {
        // Sutherland-Hodgman 클리핑 알고리즘
        var clippedCoords = new List<ICoordinate>(a.ExteriorRing.Coordinates);
        
        // B의 각 변에 대해 클리핑
        var bCoords = b.ExteriorRing.Coordinates;
        for (int i = 0; i < bCoords.Length - 1; i++)
        {
            if (clippedCoords.Count < 3) break;
            
            var edge1 = bCoords[i];
            var edge2 = bCoords[i + 1];
            clippedCoords = ClipPolygonByEdge(clippedCoords, edge1, edge2);
        }
        
        if (clippedCoords.Count < 3)
            return new GeometryCollection();
        
        // 폐합
        if (!clippedCoords[0].Equals2D(clippedCoords[clippedCoords.Count - 1]))
            clippedCoords.Add(clippedCoords[0]);
        
        return new Polygon(new LinearRing(clippedCoords.ToArray()));
    }
    
    // 일반적인 연산
    private IGeometry UnionGeneral(IGeometry a, IGeometry b)
    {
        return new GeometryCollection(new[] { a, b });
    }
    
    private IGeometry IntersectGeneral(IGeometry a, IGeometry b)
    {
        // 복잡한 케이스는 빈 컬렉션 반환
        return new GeometryCollection();
    }
    
    private IGeometry DifferenceInternal(IGeometry a, IGeometry b)
    {
        if (a.GeometryType == GeometryType.Polygon && b.GeometryType == GeometryType.Polygon)
        {
            return DifferencePolygons((Polygon)a, (Polygon)b);
        }
        
        // 다른 경우는 간단히 처리
        return a;
    }
    
    private IGeometry DifferencePolygons(Polygon a, Polygon b)
    {
        // 간단한 경우: B가 A를 완전히 포함
        if (IsPolygonInsidePolygon(a, b))
            return new GeometryCollection();
        
        // 교집합이 없으면 A 반환
        if (!a.Envelope!.Intersects(b.Envelope!))
            return a;
        
        // 실제로는 더 복잡한 알고리즘 필요
        return a;
    }
    
    // 보조 메서드들
    private bool IsPointOnSegment(ICoordinate point, ICoordinate seg1, ICoordinate seg2)
    {
        var minX = Math.Min(seg1.X, seg2.X);
        var maxX = Math.Max(seg1.X, seg2.X);
        var minY = Math.Min(seg1.Y, seg2.Y);
        var maxY = Math.Max(seg1.Y, seg2.Y);
        
        if (point.X < minX - _tolerance || point.X > maxX + _tolerance ||
            point.Y < minY - _tolerance || point.Y > maxY + _tolerance)
            return false;
        
        // 선분의 방정식에 대입
        var cross = (point.Y - seg1.Y) * (seg2.X - seg1.X) - 
                    (point.X - seg1.X) * (seg2.Y - seg1.Y);
        
        return Math.Abs(cross) < _tolerance;
    }
    
    private bool IsPointInPolygon(Point point, Polygon polygon)
    {
        var coords = polygon.ExteriorRing.Coordinates;
        var x = point.X;
        var y = point.Y;
        var inside = false;
        
        for (int i = 0, j = coords.Length - 2; i < coords.Length - 1; j = i++)
        {
            var xi = coords[i].X;
            var yi = coords[i].Y;
            var xj = coords[j].X;
            var yj = coords[j].Y;
            
            var intersect = ((yi > y) != (yj > y)) &&
                           (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
            
            if (intersect) inside = !inside;
        }
        
        // 홀 체크
        foreach (var hole in polygon.InteriorRings)
        {
            if (IsPointInRing(point, hole))
                return false;
        }
        
        return inside;
    }
    
    private bool IsPointInRing(Point point, LinearRing ring)
    {
        var coords = ring.Coordinates;
        var x = point.X;
        var y = point.Y;
        var inside = false;
        
        for (int i = 0, j = coords.Length - 2; i < coords.Length - 1; j = i++)
        {
            var xi = coords[i].X;
            var yi = coords[i].Y;
            var xj = coords[j].X;
            var yj = coords[j].Y;
            
            var intersect = ((yi > y) != (yj > y)) &&
                           (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
            
            if (intersect) inside = !inside;
        }
        
        return inside;
    }
    
    private IGeometry? GetSegmentIntersection(ICoordinate a1, ICoordinate a2, 
                                             ICoordinate b1, ICoordinate b2)
    {
        var d1x = a2.X - a1.X;
        var d1y = a2.Y - a1.Y;
        var d2x = b2.X - b1.X;
        var d2y = b2.Y - b1.Y;
        
        var cross = d1x * d2y - d1y * d2x;
        
        if (Math.Abs(cross) < _tolerance)
        {
            // 평행한 경우
            if (IsPointOnSegment(a1, b1, b2) || IsPointOnSegment(a2, b1, b2))
            {
                // 겹치는 구간 찾기
                return GetOverlappingSegment(a1, a2, b1, b2);
            }
            return null;
        }
        
        var t1 = ((b1.X - a1.X) * d2y - (b1.Y - a1.Y) * d2x) / cross;
        var t2 = ((b1.X - a1.X) * d1y - (b1.Y - a1.Y) * d1x) / cross;
        
        if (t1 >= 0 && t1 <= 1 && t2 >= 0 && t2 <= 1)
        {
            var x = a1.X + t1 * d1x;
            var y = a1.Y + t1 * d1y;
            return new Point(x, y);
        }
        
        return null;
    }
    
    private IGeometry? GetOverlappingSegment(ICoordinate a1, ICoordinate a2, 
                                            ICoordinate b1, ICoordinate b2)
    {
        // 선분이 같은 직선 상에 있는 경우 겹치는 부분 찾기
        var points = new List<ICoordinate> { a1, a2, b1, b2 };
        
        // 한 축을 기준으로 정렬
        if (Math.Abs(a2.X - a1.X) > Math.Abs(a2.Y - a1.Y))
            points.Sort((p1, p2) => p1.X.CompareTo(p2.X));
        else
            points.Sort((p1, p2) => p1.Y.CompareTo(p2.Y));
        
        // 중간 두 점이 겹치는 구간
        if ((points[1].Equals2D(a1) || points[1].Equals2D(a2)) &&
            (points[2].Equals2D(b1) || points[2].Equals2D(b2)))
        {
            return null; // 겹치지 않음
        }
        
        return new LineString(new[] { points[1], points[2] });
    }
    
    private List<LineString> ClipSegmentToPolygon(ICoordinate p1, ICoordinate p2, Polygon polygon)
    {
        var result = new List<LineString>();
        
        // 두 점 모두 폴리곤 내부
        var p1Inside = IsPointInPolygon(new Point(p1), polygon);
        var p2Inside = IsPointInPolygon(new Point(p2), polygon);
        
        if (p1Inside && p2Inside)
        {
            result.Add(new LineString(new[] { p1, p2 }));
            return result;
        }
        
        // 교차점 찾기
        var intersections = new List<ICoordinate>();
        var ringCoords = polygon.ExteriorRing.Coordinates;
        
        for (int i = 0; i < ringCoords.Length - 1; i++)
        {
            var intersection = GetSegmentIntersection(p1, p2, ringCoords[i], ringCoords[i + 1]);
            if (intersection is Point pt)
            {
                intersections.Add(pt.Coordinate);
            }
        }
        
        if (intersections.Count == 0)
            return result;
        
        // 교차점 정렬
        intersections.Sort((a, b) =>
        {
            var distA = (a.X - p1.X) * (a.X - p1.X) + (a.Y - p1.Y) * (a.Y - p1.Y);
            var distB = (b.X - p1.X) * (b.X - p1.X) + (b.Y - p1.Y) * (b.Y - p1.Y);
            return distA.CompareTo(distB);
        });
        
        // 세그먼트 생성
        if (p1Inside)
        {
            result.Add(new LineString(new[] { p1, intersections[0] }));
        }
        else if (p2Inside)
        {
            result.Add(new LineString(new[] { intersections[intersections.Count - 1], p2 }));
        }
        else if (intersections.Count >= 2)
        {
            // 폴리곤을 통과하는 경우
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                if (i + 1 < intersections.Count)
                {
                    result.Add(new LineString(new[] { intersections[i], intersections[i + 1] }));
                }
            }
        }
        
        return result;
    }
    
    private List<ICoordinate> ClipPolygonByEdge(List<ICoordinate> polygon, 
                                               ICoordinate edge1, ICoordinate edge2)
    {
        if (polygon.Count == 0) return polygon;
        
        var result = new List<ICoordinate>();
        
        for (int i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            
            var currentInside = IsPointLeftOfLine(current, edge1, edge2);
            var nextInside = IsPointLeftOfLine(next, edge1, edge2);
            
            if (currentInside)
            {
                if (nextInside)
                {
                    // 둘 다 내부
                    result.Add(next);
                }
                else
                {
                    // current는 내부, next는 외부
                    var intersection = GetLineIntersection(current, next, edge1, edge2);
                    if (intersection != null)
                        result.Add(intersection);
                }
            }
            else
            {
                if (nextInside)
                {
                    // current는 외부, next는 내부
                    var intersection = GetLineIntersection(current, next, edge1, edge2);
                    if (intersection != null)
                        result.Add(intersection);
                    result.Add(next);
                }
            }
        }
        
        return result;
    }
    
    private bool IsPointLeftOfLine(ICoordinate point, ICoordinate lineStart, ICoordinate lineEnd)
    {
        return ((lineEnd.X - lineStart.X) * (point.Y - lineStart.Y) - 
                (lineEnd.Y - lineStart.Y) * (point.X - lineStart.X)) > 0;
    }
    
    private ICoordinate? GetLineIntersection(ICoordinate p1, ICoordinate p2,
                                           ICoordinate p3, ICoordinate p4)
    {
        var intersection = GetSegmentIntersection(p1, p2, p3, p4);
        if (intersection is Point pt)
            return pt.Coordinate;
        return null;
    }
    
    private bool IsPolygonInsidePolygon(Polygon inner, Polygon outer)
    {
        // 모든 정점이 outer 안에 있는지 확인
        foreach (var coord in inner.ExteriorRing.Coordinates)
        {
            if (!IsPointInPolygon(new Point(coord), outer))
                return false;
        }
        
        return true;
    }
    
    private Polygon? MergePolygons(Polygon a, Polygon b)
    {
        // 간단한 병합 - 실제로는 더 복잡한 알고리즘 필요
        // 여기서는 null을 반환하여 MultiPolygon으로 처리
        return null;
    }
}