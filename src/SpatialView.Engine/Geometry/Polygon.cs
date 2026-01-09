namespace SpatialView.Engine.Geometry;

/// <summary>
/// 폴리곤 지오메트리
/// </summary>
public class Polygon : BaseGeometry
{
    /// <summary>
    /// 빈 폴리곤 인스턴스
    /// </summary>
    public static readonly Polygon Empty = new Polygon();
    private LinearRing? _exteriorRing;
    private List<LinearRing> _interiorRings;
    
    /// <summary>
    /// 기본 생성자 (비어있는 폴리곤)
    /// </summary>
    public Polygon() : this(null!, null)
    {
    }
    
    /// <summary>
    /// 외부 링만 있는 폴리곤 생성
    /// </summary>
    public Polygon(LinearRing? exteriorRing) : this(exteriorRing!, null)
    {
    }
    
    /// <summary>
    /// 외부 링과 내부 링(홀)을 가진 폴리곤 생성
    /// </summary>
    public Polygon(LinearRing? exteriorRing, LinearRing[]? interiorRings)
    {
        _exteriorRing = exteriorRing!;
        _interiorRings = interiorRings?.ToList() ?? new List<LinearRing>();
        SRID = 0;
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.Polygon;
    
    
    /// <inheritdoc/>
    public override bool IsEmpty => _exteriorRing == null || _exteriorRing.IsEmpty;
    
    /// <inheritdoc/>
    public override bool IsValid
    {
        get
        {
            if (IsEmpty) return true;
            
            // 외부 링이 유효해야 함
            if (!_exteriorRing.IsValid) return false;
            
            // 외부 링은 반시계방향이어야 함
            if (!_exteriorRing.IsCounterClockwise()) return false;
            
            // 모든 내부 링 검사
            foreach (var hole in _interiorRings)
            {
                if (!hole.IsValid) return false;
                
                // 내부 링은 시계방향이어야 함
                if (!hole.IsClockwise()) return false;
                
                // TODO: 내부 링은 외부 링 내부에 있어야 함
                // TODO: 내부 링들은 서로 겹치지 않아야 함
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// 외부 링
    /// </summary>
    public LinearRing? ExteriorRing => _exteriorRing;
    
    /// <summary>
    /// 내부 링(홀) 개수
    /// </summary>
    public int NumInteriorRings => _interiorRings.Count;
    
    /// <summary>
    /// 모든 내부 링 가져오기
    /// </summary>
    public IReadOnlyList<LinearRing> InteriorRings => _interiorRings.AsReadOnly();
    
    /// <summary>
    /// 특정 인덱스의 내부 링 가져오기
    /// </summary>
    public LinearRing GetInteriorRingN(int n)
    {
        if (n < 0 || n >= _interiorRings.Count)
            throw new ArgumentOutOfRangeException(nameof(n));
        return _interiorRings[n];
    }
    
    /// <inheritdoc/>
    public override int NumPoints
    {
        get
        {
            int count = IsEmpty ? 0 : _exteriorRing.NumPoints;
            foreach (var ring in _interiorRings)
            {
                count += ring.NumPoints;
            }
            return count;
        }
    }
    
    /// <inheritdoc/>
    public override Envelope? Envelope
    {
        get
        {
            if (IsEmpty) return null;
            return _exteriorRing.Envelope;
        }
    }
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates
    {
        get
        {
            var coords = new List<ICoordinate>();
            if (!IsEmpty)
            {
                coords.AddRange(_exteriorRing.Coordinates);
                foreach (var ring in _interiorRings)
                {
                    coords.AddRange(ring.Coordinates);
                }
            }
            return coords.ToArray();
        }
    }
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        // TODO: 폴리곤과 다른 지오메트리 간의 거리 계산
        throw new NotImplementedException($"Distance calculation from Polygon to {other.GeometryType} not yet implemented.");
    }
    
    /// <inheritdoc/>
    public override double Area
    {
        get
        {
            if (IsEmpty) return 0.0;
            
            double area = _exteriorRing.Area;
            foreach (var hole in _interiorRings)
            {
                area -= hole.Area;
            }
            return area;
        }
    }
    
    /// <inheritdoc/>
    public override double Length
    {
        get
        {
            if (IsEmpty) return 0.0;
            
            double length = _exteriorRing.Length;
            foreach (var ring in _interiorRings)
            {
                length += ring.Length;
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
            
            // 간단한 centroid 계산 (외부 링만 고려)
            // TODO: 정확한 centroid 계산 (홀 고려)
            return _exteriorRing.Centroid;
        }
    }
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <summary>
    /// 점이 폴리곤 내부에 있는지 확인
    /// </summary>
    public bool Contains(ICoordinate point)
    {
        if (IsEmpty) return false;
        
        // 외부 링에 포함되어 있지 않으면 false
        if (!IsPointInRing(point, _exteriorRing)) return false;
        
        // 내부 링(홀)에 포함되어 있으면 false
        foreach (var hole in _interiorRings)
        {
            if (IsPointInRing(point, hole)) return false;
        }
        
        return true;
    }
    
    private bool IsPointInRing(ICoordinate point, LinearRing ring)
    {
        // Ray casting algorithm
        var coords = ring.Coordinates;
        int n = coords.Length - 1; // 마지막 점은 첫 번째와 같음
        bool inside = false;
        
        double p1x = coords[0].X;
        double p1y = coords[0].Y;
        
        for (int i = 1; i <= n; i++)
        {
            double p2x = coords[i % n].X;
            double p2y = coords[i % n].Y;
            
            if (point.Y > Math.Min(p1y, p2y))
            {
                if (point.Y <= Math.Max(p1y, p2y))
                {
                    if (point.X <= Math.Max(p1x, p2x))
                    {
                        if (p1y != p2y)
                        {
                            double xinters = (point.Y - p1y) * (p2x - p1x) / (p2y - p1y) + p1x;
                        }
                        if (p1x == p2x || point.X <= (point.Y - p1y) * (p2x - p1x) / (p2y - p1y) + p1x)
                        {
                            inside = !inside;
                        }
                    }
                }
            }
            p1x = p2x;
            p1y = p2y;
        }
        
        return inside;
    }
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        var exteriorCopy = _exteriorRing?.Copy() as LinearRing;
        var interiorCopies = _interiorRings.Select(r => r.Copy() as LinearRing).ToArray()!;
        return new Polygon(exteriorCopy, interiorCopies) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "POLYGON EMPTY";
        
        var sb = new System.Text.StringBuilder();
        sb.Append("POLYGON (");
        
        // 외부 링
        sb.Append("(");
        var extCoords = _exteriorRing.Coordinates;
        for (int i = 0; i < extCoords.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{extCoords[i].X:F6} {extCoords[i].Y:F6}");
            if (!double.IsNaN(extCoords[i].Z)) sb.Append($" {extCoords[i].Z:F6}");
        }
        sb.Append(")");
        
        // 내부 링
        foreach (var ring in _interiorRings)
        {
            sb.Append(", (");
            var coords = ring.Coordinates;
            for (int i = 0; i < coords.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"{coords[i].X:F6} {coords[i].Y:F6}");
                if (!double.IsNaN(coords[i].Z)) sb.Append($" {coords[i].Z:F6}");
            }
            sb.Append(")");
        }
        
        sb.Append(")");
        return sb.ToString();
    }
    
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is Polygon other)
        {
            if (SRID != other.SRID) return false;
            
            // 외부 링 비교
            if (IsEmpty && other.IsEmpty) return true;
            if (IsEmpty || other.IsEmpty) return false;
            if (!_exteriorRing.Equals(other._exteriorRing)) return false;
            
            // 내부 링 개수 비교
            if (_interiorRings.Count != other._interiorRings.Count) return false;
            
            // 내부 링 비교
            for (int i = 0; i < _interiorRings.Count; i++)
            {
                if (!_interiorRings[i].Equals(other._interiorRings[i]))
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
        hash.Add(_exteriorRing);
        foreach (var ring in _interiorRings)
        {
            hash.Add(ring);
        }
        return hash.ToHashCode();
    }
    
    /// <inheritdoc/>
    public override int Dimension => 2;
    
    #region Spatial Relationship Operations
    
    public override bool Contains(IGeometry geometry)
    {
        if (geometry is Point point)
        {
            return Contains(point.Coordinate);
        }
        // TODO: Implement Contains for other geometry types
        return false;
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        // TODO: Implement Intersects
        return !Disjoint(geometry);
    }
    
    public override bool Within(IGeometry geometry)
    {
        // TODO: Implement Within
        return geometry.Contains(this);
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        // TODO: Implement Overlaps
        if (geometry is Polygon)
        {
            return Intersects(geometry) && !Contains(geometry) && !geometry.Contains(this);
        }
        return false;
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        // TODO: Implement Crosses
        // Polygons can only cross lines
        if (geometry.GeometryType == GeometryType.LineString || 
            geometry.GeometryType == GeometryType.MultiLineString)
        {
            return Intersects(geometry) && !Contains(geometry);
        }
        return false;
    }
    
    public override bool Touches(IGeometry geometry)
    {
        // TODO: Implement Touches
        return Intersects(geometry) && !Overlaps(geometry);
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        // TODO: Implement Disjoint properly
        return !Envelope!.Intersects(geometry.Envelope);
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        // TODO: Implement Union
        throw new NotImplementedException("Polygon Union not yet implemented");
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        // TODO: Implement Intersection
        throw new NotImplementedException("Polygon Intersection not yet implemented");
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        // TODO: Implement Difference
        throw new NotImplementedException("Polygon Difference not yet implemented");
    }
    
    public override IGeometry SymmetricDifference(IGeometry geometry)
    {
        // TODO: Implement SymmetricDifference
        var union = Union(geometry);
        var intersection = Intersection(geometry);
        return union.Difference(intersection);
    }
    
    public override IGeometry Buffer(double distance)
    {
        // TODO: Implement Buffer
        throw new NotImplementedException("Polygon Buffer not yet implemented");
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