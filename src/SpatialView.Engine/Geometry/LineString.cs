namespace SpatialView.Engine.Geometry;

/// <summary>
/// 라인스트링 지오메트리 (연결된 선)
/// </summary>
public class LineString : BaseGeometry
{
    /// <summary>
    /// 빈 라인스트링 인스턴스
    /// </summary>
    public static readonly LineString Empty = new LineString();
    private readonly List<ICoordinate> _coordinates;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public LineString() : this(new List<ICoordinate>())
    {
    }
    
    /// <summary>
    /// 좌표 배열로부터 생성
    /// </summary>
    public LineString(ICoordinate[] coordinates) : this(coordinates?.ToList() ?? new List<ICoordinate>())
    {
    }
    
    /// <summary>
    /// 좌표 리스트로부터 생성
    /// </summary>
    public LineString(List<ICoordinate> coordinates)
    {
        _coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
        SRID = 0;
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.LineString;
    
    
    /// <inheritdoc/>
    public override bool IsEmpty => _coordinates.Count == 0;
    
    /// <inheritdoc/>
    public override bool IsValid
    {
        get
        {
            // 최소 2개의 점이 필요
            if (_coordinates.Count < 2) return false;
            
            // 중복된 연속 점이 없어야 함
            for (int i = 1; i < _coordinates.Count; i++)
            {
                if (_coordinates[i].Equals2D(_coordinates[i - 1]))
                    return false;
            }
            return true;
        }
    }
    
    /// <inheritdoc/>
    public override int NumPoints => _coordinates.Count;
    
    /// <inheritdoc/>
    public override Envelope? Envelope
    {
        get
        {
            if (IsEmpty) return null;
            var env = new Envelope();
            foreach (var coord in _coordinates)
            {
                env.ExpandToInclude(coord);
            }
            return env;
        }
    }
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates => _coordinates.Select(c => c.Copy()).ToArray();
    
    /// <summary>
    /// 시작점
    /// </summary>
    public ICoordinate? StartPoint => IsEmpty ? null : _coordinates[0];
    
    /// <summary>
    /// 끝점
    /// </summary>
    public ICoordinate? EndPoint => IsEmpty ? null : _coordinates[_coordinates.Count - 1];
    
    /// <summary>
    /// 폐곡선인지 확인 (시작점과 끝점이 같음)
    /// </summary>
    public bool IsClosed => !IsEmpty && StartPoint != null && EndPoint != null && StartPoint.Equals2D(EndPoint);
    
    /// <summary>
    /// 링인지 확인 (폐곡선이고 유효함)
    /// </summary>
    public bool IsRing => IsClosed && IsValid;
    
    /// <summary>
    /// 특정 인덱스의 좌표 가져오기
    /// </summary>
    public ICoordinate GetCoordinateN(int n)
    {
        if (n < 0 || n >= _coordinates.Count)
            throw new ArgumentOutOfRangeException(nameof(n));
        return _coordinates[n];
    }
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        if (other is Point point)
        {
            return DistanceToPoint(point.Coordinate);
        }
        else if (other is LineString line)
        {
            return DistanceToLine(line);
        }
        // TODO: 다른 지오메트리 타입
        throw new NotImplementedException($"Distance calculation from LineString to {other.GeometryType} not yet implemented.");
    }
    
    private double DistanceToPoint(ICoordinate point)
    {
        double minDist = double.MaxValue;
        
        for (int i = 0; i < _coordinates.Count - 1; i++)
        {
            var dist = SegmentDistance(_coordinates[i], _coordinates[i + 1], point);
            if (dist < minDist) minDist = dist;
        }
        
        return minDist;
    }
    
    private double DistanceToLine(LineString other)
    {
        double minDist = double.MaxValue;
        
        // 모든 세그먼트 쌍 비교
        for (int i = 0; i < _coordinates.Count - 1; i++)
        {
            for (int j = 0; j < other._coordinates.Count - 1; j++)
            {
                var dist = SegmentToSegmentDistance(
                    _coordinates[i], _coordinates[i + 1],
                    other._coordinates[j], other._coordinates[j + 1]);
                if (dist < minDist) minDist = dist;
            }
        }
        
        return minDist;
    }
    
    private static double SegmentDistance(ICoordinate p1, ICoordinate p2, ICoordinate p)
    {
        // 점에서 선분까지의 최단거리
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        
        if (dx != 0 || dy != 0)
        {
            var t = ((p.X - p1.X) * dx + (p.Y - p1.Y) * dy) / (dx * dx + dy * dy);
            
            if (t > 1)
            {
                dx = p.X - p2.X;
                dy = p.Y - p2.Y;
            }
            else if (t > 0)
            {
                dx = p.X - (p1.X + t * dx);
                dy = p.Y - (p1.Y + t * dy);
            }
            else
            {
                dx = p.X - p1.X;
                dy = p.Y - p1.Y;
            }
        }
        else
        {
            dx = p.X - p1.X;
            dy = p.Y - p1.Y;
        }
        
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    private static double SegmentToSegmentDistance(ICoordinate p1, ICoordinate p2, ICoordinate p3, ICoordinate p4)
    {
        // 간단한 구현: 모든 끝점 간 거리와 점-선분 거리의 최소값
        double min = p1.Distance(p3);
        min = Math.Min(min, p1.Distance(p4));
        min = Math.Min(min, p2.Distance(p3));
        min = Math.Min(min, p2.Distance(p4));
        min = Math.Min(min, SegmentDistance(p1, p2, p3));
        min = Math.Min(min, SegmentDistance(p1, p2, p4));
        min = Math.Min(min, SegmentDistance(p3, p4, p1));
        min = Math.Min(min, SegmentDistance(p3, p4, p2));
        return min;
    }
    
    /// <inheritdoc/>
    public override double Area => 0.0;
    
    /// <inheritdoc/>
    public override double Length
    {
        get
        {
            double length = 0.0;
            for (int i = 1; i < _coordinates.Count; i++)
            {
                length += _coordinates[i - 1].Distance(_coordinates[i]);
            }
            return length;
        }
    }
    
    /// <inheritdoc/>
    public override Point? Centroid
    {
        get
        {
            if (IsEmpty) return null;
            
            double totalLength = Length;
            if (totalLength == 0) return new Point(_coordinates[0].Copy());
            
            double sumX = 0, sumY = 0;
            // double runningLength = 0; // Not used currently
            
            for (int i = 0; i < _coordinates.Count - 1; i++)
            {
                var p1 = _coordinates[i];
                var p2 = _coordinates[i + 1];
                var segmentLength = p1.Distance(p2);
                
                if (segmentLength > 0)
                {
                    var midX = (p1.X + p2.X) / 2;
                    var midY = (p1.Y + p2.Y) / 2;
                    
                    sumX += midX * segmentLength;
                    sumY += midY * segmentLength;
                }
            }
            
            return new Point(new Coordinate(sumX / totalLength, sumY / totalLength));
        }
    }
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        return new LineString(_coordinates.Select(c => c.Copy()).ToList()) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "LINESTRING EMPTY";
        
        var coords = string.Join(", ", _coordinates.Select(c => 
        {
            var coord = $"{c.X:F6} {c.Y:F6}";
            if (!double.IsNaN(c.Z)) coord += $" {c.Z:F6}";
            return coord;
        }));
        
        return $"LINESTRING ({coords})";
    }
    
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is LineString other)
        {
            if (SRID != other.SRID) return false;
            if (_coordinates.Count != other._coordinates.Count) return false;
            
            for (int i = 0; i < _coordinates.Count; i++)
            {
                if (!_coordinates[i].Equals(other._coordinates[i]))
                    return false;
            }
            return true;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SRID);
        foreach (var coord in _coordinates)
        {
            hash.Add(coord);
        }
        return hash.ToHashCode();
    }
    
    /// <inheritdoc/>
    public override int Dimension => 1;
    
    #region Spatial Relationship Operations
    
    public override bool Contains(IGeometry geometry)
    {
        if (geometry is Point point)
        {
            // 점이 라인 위에 있는지 확인
            return DistanceToPoint(point.Coordinate) < double.Epsilon;
        }
        else if (geometry is LineString other)
        {
            // 다른 라인의 모든 점이 이 라인 위에 있는지 확인
            return other._coordinates.All(coord => DistanceToPoint(coord) < double.Epsilon);
        }
        return false;
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        if (geometry is Point point)
        {
            return Contains(point);
        }
        else if (geometry is LineString line)
        {
            // 두 라인이 교차하는지 확인
            for (int i = 0; i < _coordinates.Count - 1; i++)
            {
                for (int j = 0; j < line._coordinates.Count - 1; j++)
                {
                    if (SegmentsIntersect(_coordinates[i], _coordinates[i + 1],
                                         line._coordinates[j], line._coordinates[j + 1]))
                        return true;
                }
            }
            return false;
        }
        else if (geometry is Polygon polygon)
        {
            return geometry.Intersects(this);
        }
        return false;
    }
    
    public override bool Within(IGeometry geometry)
    {
        if (geometry is Polygon)
        {
            // 모든 점이 폴리곤 내부에 있는지 확인
            return _coordinates.All(coord => geometry.Contains(new Point(coord)));
        }
        return false;
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        if (geometry is LineString other)
        {
            // 부분적으로 겹치는지 확인
            return Intersects(geometry) && !Contains(geometry) && !geometry.Contains(this);
        }
        return false;
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        if (geometry is LineString)
        {
            // 두 라인이 교차하는지 확인
            return Intersects(geometry);
        }
        else if (geometry is Polygon)
        {
            // 라인이 폴리곤을 가로지르는지 확인
            return Intersects(geometry) && !Within(geometry);
        }
        return false;
    }
    
    public override bool Touches(IGeometry geometry)
    {
        if (geometry is Point point)
        {
            // 점이 끝점에만 닿는지 확인
            return (StartPoint != null && StartPoint.Equals2D(point.Coordinate)) ||
                   (EndPoint != null && EndPoint.Equals2D(point.Coordinate));
        }
        else if (geometry is LineString other)
        {
            // 끝점끼리만 만나는지 확인
            var thisStart = StartPoint;
            var thisEnd = EndPoint;
            var otherStart = other.StartPoint;
            var otherEnd = other.EndPoint;
            
            if (thisStart == null || thisEnd == null || otherStart == null || otherEnd == null)
                return false;
                
            return (thisStart.Equals2D(otherStart) || thisStart.Equals2D(otherEnd) ||
                    thisEnd.Equals2D(otherStart) || thisEnd.Equals2D(otherEnd)) &&
                   !Overlaps(other);
        }
        return false;
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        return !Intersects(geometry);
    }
    
    private static bool SegmentsIntersect(ICoordinate p1, ICoordinate p2, ICoordinate p3, ICoordinate p4)
    {
        double d1 = Direction(p3, p4, p1);
        double d2 = Direction(p3, p4, p2);
        double d3 = Direction(p1, p2, p3);
        double d4 = Direction(p1, p2, p4);
        
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;
            
        if (d1 == 0 && OnSegment(p3, p1, p4)) return true;
        if (d2 == 0 && OnSegment(p3, p2, p4)) return true;
        if (d3 == 0 && OnSegment(p1, p3, p2)) return true;
        if (d4 == 0 && OnSegment(p1, p4, p2)) return true;
        
        return false;
    }
    
    private static double Direction(ICoordinate pi, ICoordinate pj, ICoordinate pk)
    {
        return (pk.X - pi.X) * (pj.Y - pi.Y) - (pj.X - pi.X) * (pk.Y - pi.Y);
    }
    
    private static bool OnSegment(ICoordinate pi, ICoordinate pj, ICoordinate pk)
    {
        if (Math.Min(pi.X, pk.X) <= pj.X && pj.X <= Math.Max(pi.X, pk.X) &&
            Math.Min(pi.Y, pk.Y) <= pj.Y && pj.Y <= Math.Max(pi.Y, pk.Y))
            return true;
        return false;
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        if (geometry is LineString other)
        {
            // 간단한 구현: MultiLineString으로 결합
            return new MultiLineString(new[] { this, other }) { SRID = SRID };
        }
        return Copy();
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        // TODO: 실제 교차점 계산 구현
        if (Intersects(geometry))
        {
            return Copy(); // 임시 구현
        }
        return new LineString() { SRID = SRID };
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        // TODO: 실제 차집합 계산 구현
        if (!Intersects(geometry))
        {
            return Copy();
        }
        return new LineString() { SRID = SRID };
    }
    
    public override IGeometry SymmetricDifference(IGeometry geometry)
    {
        var union = Union(geometry);
        var intersection = Intersection(geometry);
        return union.Difference(intersection);
    }
    
    public override IGeometry Buffer(double distance)
    {
        // TODO: 실제 버퍼 계산 구현
        throw new NotImplementedException("LineString Buffer not yet implemented");
    }
    
    public override IGeometry Clone()
    {
        return Copy();
    }
    
    public override IGeometry Transform(object transformation)
    {
        // For now, just return a copy - actual transformation logic will be implemented later
        return Clone();
    }
    
    #endregion
}