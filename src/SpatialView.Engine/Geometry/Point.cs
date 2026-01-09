namespace SpatialView.Engine.Geometry;

/// <summary>
/// 포인트 지오메트리
/// </summary>
public class Point : BaseGeometry
{
    /// <summary>
    /// 빈 포인트 인스턴스
    /// </summary>
    public static readonly Point Empty = new Point();
    private ICoordinate _coordinate;
    
    /// <summary>
    /// 기본 생성자 (비어있는 포인트)
    /// </summary>
    public Point() : this(new Coordinate())
    {
    }
    
    /// <summary>
    /// 좌표로부터 생성
    /// </summary>
    public Point(ICoordinate coordinate)
    {
        _coordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
        SRID = 0;
    }
    
    /// <summary>
    /// X, Y 좌표로부터 생성
    /// </summary>
    public Point(double x, double y) : this(new Coordinate(x, y))
    {
    }
    
    /// <summary>
    /// X, Y, Z 좌표로부터 생성
    /// </summary>
    public Point(double x, double y, double z) : this(new Coordinate(x, y, z))
    {
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.Point;
    
    
    /// <inheritdoc/>
    public override bool IsEmpty => _coordinate == null || (double.IsNaN(_coordinate.X) && double.IsNaN(_coordinate.Y));
    
    /// <inheritdoc/>
    public override bool IsValid => !IsEmpty;
    
    /// <inheritdoc/>
    public override int NumPoints => IsEmpty ? 0 : 1;
    
    /// <inheritdoc/>
    public override Envelope? Envelope 
    { 
        get
        {
            if (IsEmpty) return null;
            return new Envelope(_coordinate, _coordinate);
        }
    }
    
    /// <summary>
    /// 포인트의 좌표
    /// </summary>
    public ICoordinate Coordinate => _coordinate;
    
    /// <summary>
    /// X 좌표
    /// </summary>
    public double X => _coordinate.X;
    
    /// <summary>
    /// Y 좌표
    /// </summary>
    public double Y => _coordinate.Y;
    
    /// <summary>
    /// Z 좌표
    /// </summary>
    public double Z => _coordinate.Z;
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates => IsEmpty ? Array.Empty<ICoordinate>() : new[] { _coordinate.Copy() };
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        if (other is Point p)
        {
            return _coordinate.Distance(p.Coordinate);
        }
        // TODO: 다른 지오메트리 타입에 대한 거리 계산
        throw new NotImplementedException($"Distance calculation from Point to {other.GeometryType} not yet implemented.");
    }
    
    /// <inheritdoc/>
    public override double Area => 0.0;
    
    /// <inheritdoc/>
    public override double Length => 0.0;
    
    /// <inheritdoc/>
    public override Point? Centroid => IsEmpty ? null : this;
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        return new Point(_coordinate.Copy()) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "POINT EMPTY";
        
        var coords = $"{X:F6} {Y:F6}";
        if (!double.IsNaN(Z)) coords += $" {Z:F6}";
        
        return $"POINT ({coords})";
    }
    
    /// <summary>
    /// 두 포인트가 같은지 비교 (2D)
    /// </summary>
    public bool Equals2D(Point other)
    {
        if (other == null) return false;
        return _coordinate.Equals2D(other.Coordinate);
    }
    
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is Point other)
        {
            return SRID == other.SRID && _coordinate.Equals(other._coordinate);
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(SRID, _coordinate);
    }
    
    /// <inheritdoc/>
    public override int Dimension => 0;
    
    #region Spatial Relationship Operations
    
    public override bool Contains(IGeometry geometry)
    {
        if (geometry is Point other)
        {
            return _coordinate.Equals2D(other._coordinate);
        }
        return false;
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        if (geometry is Point other)
        {
            return Contains(other);
        }
        else if (geometry is LineString line)
        {
            return line.Contains(this);
        }
        else if (geometry is Polygon polygon)
        {
            return polygon.Contains(this);
        }
        return false;
    }
    
    public override bool Within(IGeometry geometry)
    {
        return geometry.Contains(this);
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        // 점은 다른 지오메트리와 겹칠 수 없음 (차원이 0이므로)
        return false;
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        // 점은 다른 지오메트리와 교차할 수 없음 (차원이 0이므로)
        return false;
    }
    
    public override bool Touches(IGeometry geometry)
    {
        if (geometry is LineString line)
        {
            // 점이 라인의 끝점에 있는지 확인
            return (line.StartPoint != null && _coordinate.Equals2D(line.StartPoint)) ||
                   (line.EndPoint != null && _coordinate.Equals2D(line.EndPoint));
        }
        else if (geometry is Polygon)
        {
            // 점이 폴리곤의 경계에 있는지 확인
            return geometry.Touches(this);
        }
        return false;
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        return !Intersects(geometry);
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        if (geometry is Point other)
        {
            if (Contains(other))
                return Copy();
            return new MultiPoint(new[] { this, other }) { SRID = SRID };
        }
        else if (geometry is MultiPoint multiPoint)
        {
            return multiPoint.Union(this);
        }
        return geometry.Union(this);
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        if (Intersects(geometry))
            return Copy();
        return new Point() { SRID = SRID }; // 빈 점
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        if (geometry.Contains(this))
            return new Point() { SRID = SRID }; // 빈 점
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
        if (distance <= 0) return new Point() { SRID = SRID };
        
        // 점에서 버퍼를 만들면 원이 됨
        // TODO: 실제 원 생성 구현
        throw new NotImplementedException("점 버퍼 미구현");
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