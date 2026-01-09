namespace SpatialView.Engine.Geometry;

/// <summary>
/// 멀티포인트 지오메트리 - 여러 개의 포인트 컬렉션
/// </summary>
public class MultiPoint : BaseGeometry
{
    /// <summary>
    /// 빈 멀티포인트 인스턴스
    /// </summary>
    public static readonly MultiPoint Empty = new MultiPoint();
    
    private readonly List<Point> _points;
    
    /// <summary>
    /// 포인트 컬렉션 (읽기 전용)
    /// </summary>
    public IReadOnlyList<Point> Geometries => _points;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public MultiPoint() : this(new List<Point>())
    {
    }
    
    /// <summary>
    /// 포인트 배열로부터 생성
    /// </summary>
    public MultiPoint(Point[] points) : this(points?.ToList() ?? new List<Point>())
    {
    }
    
    /// <summary>
    /// 포인트 리스트로부터 생성
    /// </summary>
    public MultiPoint(List<Point> points)
    {
        _points = points ?? throw new ArgumentNullException(nameof(points));
        SRID = 0;
    }
    
    /// <summary>
    /// 좌표 배열로부터 생성
    /// </summary>
    public MultiPoint(ICoordinate[] coordinates) : this()
    {
        if (coordinates != null)
        {
            foreach (var coord in coordinates)
            {
                _points.Add(new Point(coord));
            }
        }
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.MultiPoint;
    
    /// <inheritdoc/>
    public override bool IsEmpty => _points.Count == 0;
    
    /// <inheritdoc/>
    public override bool IsValid => _points.All(p => p.IsValid);
    
    /// <inheritdoc/>
    public override int NumPoints => _points.Count;
    
    /// <inheritdoc/>
    public override int Dimension => 0;
    
    /// <summary>
    /// 지오메트리 개수
    /// </summary>
    public int NumGeometries => _points.Count;
    
    /// <summary>
    /// 특정 인덱스의 포인트 가져오기
    /// </summary>
    public Point GetGeometryN(int n)
    {
        if (n < 0 || n >= _points.Count)
            throw new ArgumentOutOfRangeException(nameof(n));
        return _points[n];
    }
    
    /// <inheritdoc/>
    public override Envelope? Envelope
    {
        get
        {
            if (IsEmpty) return null;
            var env = new Envelope();
            foreach (var point in _points)
            {
                env.ExpandToInclude(point.Coordinate);
            }
            return env;
        }
    }
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates
    {
        get
        {
            return _points.SelectMany(p => p.Coordinates).ToArray();
        }
    }
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        if (IsEmpty) return double.MaxValue;
        
        double minDist = double.MaxValue;
        foreach (var point in _points)
        {
            double dist = point.Distance(other);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }
    
    /// <inheritdoc/>
    public override double Area => 0.0;
    
    /// <inheritdoc/>
    public override double Length => 0.0;
    
    /// <inheritdoc/>
    public override Point? Centroid
    {
        get
        {
            if (IsEmpty) return null;
            
            double sumX = 0, sumY = 0, sumZ = 0;
            int validZ = 0;
            
            foreach (var point in _points)
            {
                sumX += point.X;
                sumY += point.Y;
                if (!double.IsNaN(point.Z))
                {
                    sumZ += point.Z;
                    validZ++;
                }
            }
            
            var centroid = new Coordinate(
                sumX / _points.Count,
                sumY / _points.Count);
            
            if (validZ > 0)
            {
                centroid.Z = sumZ / validZ;
            }
            
            return new Point(centroid);
        }
    }
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        var pointsCopy = _points.Select(p => (Point)p.Copy()).ToList();
        return new MultiPoint(pointsCopy) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "MULTIPOINT EMPTY";
        
        var points = _points.Select(p => 
        {
            var coords = $"{p.X:F6} {p.Y:F6}";
            if (!double.IsNaN(p.Z)) coords += $" {p.Z:F6}";
            return $"({coords})";
        });
        
        return $"MULTIPOINT ({string.Join(", ", points)})";
    }
    
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is MultiPoint other)
        {
            if (SRID != other.SRID) return false;
            if (_points.Count != other._points.Count) return false;
            
            // 순서에 관계없이 같은 포인트들을 포함하는지 확인
            var thisSet = new HashSet<Point>(_points);
            var otherSet = new HashSet<Point>(other._points);
            return thisSet.SetEquals(otherSet);
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SRID);
        // 순서에 무관하게 해시 계산
        foreach (var point in _points.OrderBy(p => p.X).ThenBy(p => p.Y))
        {
            hash.Add(point);
        }
        return hash.ToHashCode();
    }
    
    #region Spatial Relationship Operations
    
    public override bool Contains(IGeometry geometry)
    {
        if (geometry is Point point)
        {
            return _points.Any(p => p.Equals(point));
        }
        return false;
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        return _points.Any(p => p.Intersects(geometry));
    }
    
    public override bool Within(IGeometry geometry)
    {
        return _points.All(p => p.Within(geometry));
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        if (geometry is MultiPoint other)
        {
            var thisSet = new HashSet<Point>(_points);
            var otherSet = new HashSet<Point>(other._points);
            return thisSet.Overlaps(otherSet) && !thisSet.SetEquals(otherSet);
        }
        return false;
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        // MultiPoint는 다른 지오메트리와 교차할 수 없음 (차원이 0이므로)
        return false;
    }
    
    public override bool Touches(IGeometry geometry)
    {
        return _points.Any(p => p.Touches(geometry));
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        return !Intersects(geometry);
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        if (geometry is MultiPoint other)
        {
            var allPoints = new HashSet<Point>(_points);
            allPoints.UnionWith(other._points);
            return new MultiPoint(allPoints.ToList()) { SRID = SRID };
        }
        else if (geometry is Point point)
        {
            var allPoints = new List<Point>(_points);
            if (!_points.Contains(point))
                allPoints.Add(point);
            return new MultiPoint(allPoints) { SRID = SRID };
        }
        return Copy();
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        if (geometry is MultiPoint other)
        {
            var commonPoints = _points.Where(p => other._points.Contains(p)).ToList();
            return new MultiPoint(commonPoints) { SRID = SRID };
        }
        else if (geometry is Point point)
        {
            if (_points.Contains(point))
                return point.Copy();
        }
        return new MultiPoint() { SRID = SRID };
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        if (geometry is MultiPoint other)
        {
            var diffPoints = _points.Where(p => !other._points.Contains(p)).ToList();
            return new MultiPoint(diffPoints) { SRID = SRID };
        }
        else if (geometry is Point point)
        {
            var diffPoints = _points.Where(p => !p.Equals(point)).ToList();
            return new MultiPoint(diffPoints) { SRID = SRID };
        }
        return Copy();
    }
    
    public override IGeometry SymmetricDifference(IGeometry geometry)
    {
        var union = Union(geometry);
        var intersection = Intersection(geometry);
        return union.Difference(intersection);
    }
    
    public override IGeometry Buffer(double distance)
    {
        if (distance <= 0) return new MultiPoint() { SRID = SRID };
        
        // 각 점에 대해 버퍼를 생성하고 합집합
        IGeometry? result = null;
        foreach (var point in _points)
        {
            var buffer = point.Buffer(distance);
            result = result == null ? buffer : result.Union(buffer);
        }
        return result ?? new MultiPoint() { SRID = SRID };
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