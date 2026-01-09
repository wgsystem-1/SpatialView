namespace SpatialView.Engine.Geometry;

/// <summary>
/// 멀티폴리곤 지오메트리 - 여러 개의 폴리곤 컬렉션
/// </summary>
public class MultiPolygon : BaseGeometry
{
    /// <summary>
    /// 빈 멀티폴리곤 인스턴스
    /// </summary>
    public static readonly MultiPolygon Empty = new MultiPolygon();
    
    private readonly List<Polygon> _polygons;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public MultiPolygon() : this(new List<Polygon>())
    {
    }
    
    /// <summary>
    /// 폴리곤 배열로부터 생성
    /// </summary>
    public MultiPolygon(Polygon[] polygons) : this(polygons?.ToList() ?? new List<Polygon>())
    {
    }
    
    /// <summary>
    /// 폴리곤 리스트로부터 생성
    /// </summary>
    public MultiPolygon(List<Polygon> polygons)
    {
        _polygons = polygons ?? throw new ArgumentNullException(nameof(polygons));
        SRID = 0;
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.MultiPolygon;
    
    
    /// <inheritdoc/>
    public override bool IsEmpty => _polygons.Count == 0 || _polygons.All(p => p.IsEmpty);
    
    /// <inheritdoc/>
    public override bool IsValid => _polygons.All(p => p.IsValid);
    
    /// <inheritdoc/>
    public override int NumPoints => _polygons.Sum(p => p.NumPoints);
    
    /// <summary>
    /// 지오메트리 개수
    /// </summary>
    public int NumGeometries => _polygons.Count;
    
    /// <summary>
    /// 모든 폴리곤 가져오기
    /// </summary>
    public IReadOnlyList<Polygon> Geometries => _polygons.AsReadOnly();
    
    /// <summary>
    /// 특정 인덱스의 폴리곤 가져오기
    /// </summary>
    public Polygon GetGeometryN(int n)
    {
        if (n < 0 || n >= _polygons.Count)
            throw new ArgumentOutOfRangeException(nameof(n));
        return _polygons[n];
    }
    
    /// <inheritdoc/>
    public override Envelope? Envelope
    {
        get
        {
            if (IsEmpty) return null;
            var env = new Envelope();
            foreach (var polygon in _polygons)
            {
                if (polygon.Envelope != null)
                    env.ExpandToInclude(polygon.Envelope);
            }
            return env;
        }
    }
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates
    {
        get
        {
            return _polygons.SelectMany(p => p.Coordinates).ToArray();
        }
    }
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        if (IsEmpty) return double.MaxValue;
        
        double minDist = double.MaxValue;
        foreach (var polygon in _polygons)
        {
            double dist = polygon.Distance(other);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }
    
    /// <inheritdoc/>
    public override double Area => _polygons.Sum(p => p.Area);
    
    /// <inheritdoc/>
    public override double Length => _polygons.Sum(p => p.Length);
    
    /// <inheritdoc/>
    public override Point? Centroid
    {
        get
        {
            if (IsEmpty) return null;
            
            double totalArea = Area;
            if (totalArea == 0) return _polygons[0].Centroid;
            
            double weightedX = 0, weightedY = 0;
            
            foreach (var polygon in _polygons)
            {
                var area = polygon.Area;
                if (area > 0 && polygon.Centroid != null)
                {
                    var centroid = polygon.Centroid;
                    weightedX += centroid.Coordinate.X * area;
                    weightedY += centroid.Coordinate.Y * area;
                }
            }
            
            return new Point(new Coordinate(weightedX / totalArea, weightedY / totalArea));
        }
    }
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <summary>
    /// 점이 멀티폴리곤 내부에 있는지 확인
    /// </summary>
    public bool Contains(ICoordinate point)
    {
        return _polygons.Any(p => p.Contains(point));
    }
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        var polygonsCopy = _polygons.Select(p => (Polygon)p.Copy()).ToList();
        return new MultiPolygon(polygonsCopy) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "MULTIPOLYGON EMPTY";
        
        var polygons = _polygons.Select(poly => 
        {
            var rings = new List<string>();
            
            // 외부 링
            if (!poly.IsEmpty)
            {
                var extRing = poly.ExteriorRing;
                var extCoords = extRing.Coordinates;
                var extCoordsText = string.Join(", ", extCoords.Select(c => 
                {
                    var coord = $"{c.X:F6} {c.Y:F6}";
                    if (!double.IsNaN(c.Z)) coord += $" {c.Z:F6}";
                    return coord;
                }));
                rings.Add($"({extCoordsText})");
                
                // 내부 링
                for (int i = 0; i < poly.NumInteriorRings; i++)
                {
                    var intRing = poly.GetInteriorRingN(i);
                    var intCoords = intRing.Coordinates;
                    var intCoordsText = string.Join(", ", intCoords.Select(c => 
                    {
                        var coord = $"{c.X:F6} {c.Y:F6}";
                        if (!double.IsNaN(c.Z)) coord += $" {c.Z:F6}";
                        return coord;
                    }));
                    rings.Add($"({intCoordsText})");
                }
            }
            
            return $"({string.Join(", ", rings)})";
        });
        
        return $"MULTIPOLYGON ({string.Join(", ", polygons)})";
    }
    
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is MultiPolygon other)
        {
            if (SRID != other.SRID) return false;
            if (_polygons.Count != other._polygons.Count) return false;
            
            for (int i = 0; i < _polygons.Count; i++)
            {
                if (!_polygons[i].Equals(other._polygons[i]))
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
        foreach (var polygon in _polygons)
        {
            hash.Add(polygon);
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
        else if (geometry is MultiPolygon other)
        {
            // 모든 폴리곤이 이 멀티폴리곤에 포함되는지 확인
            return other._polygons.All(p => _polygons.Any(mp => mp.Contains(p)));
        }
        else if (geometry is Polygon polygon)
        {
            return _polygons.Any(p => p.Contains(polygon));
        }
        return false;
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        return _polygons.Any(p => p.Intersects(geometry));
    }
    
    public override bool Within(IGeometry geometry)
    {
        return _polygons.All(p => p.Within(geometry));
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        if (geometry is MultiPolygon other)
        {
            return Intersects(geometry) && !Contains(geometry) && !geometry.Contains(this);
        }
        return false;
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        // 폴리곤은 선형 지오메트리와만 교차 가능
        if (geometry.GeometryType == GeometryType.LineString || 
            geometry.GeometryType == GeometryType.MultiLineString)
        {
            return _polygons.Any(p => p.Crosses(geometry));
        }
        return false;
    }
    
    public override bool Touches(IGeometry geometry)
    {
        return _polygons.Any(p => p.Touches(geometry));
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        return !Intersects(geometry);
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        if (geometry is MultiPolygon other)
        {
            var allPolygons = new List<Polygon>(_polygons);
            allPolygons.AddRange(other._polygons);
            return new MultiPolygon(allPolygons) { SRID = SRID };
        }
        else if (geometry is Polygon polygon)
        {
            var allPolygons = new List<Polygon>(_polygons) { polygon };
            return new MultiPolygon(allPolygons) { SRID = SRID };
        }
        return Copy();
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        var resultPolygons = new List<Polygon>();
        
        foreach (var polygon in _polygons)
        {
            var intersection = polygon.Intersection(geometry);
            if (!intersection.IsEmpty)
            {
                if (intersection is Polygon p)
                {
                    resultPolygons.Add(p);
                }
                else if (intersection is MultiPolygon mp)
                {
                    resultPolygons.AddRange(mp._polygons);
                }
            }
        }
        
        return new MultiPolygon(resultPolygons) { SRID = SRID };
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        var resultPolygons = new List<Polygon>();
        
        foreach (var polygon in _polygons)
        {
            var diff = polygon.Difference(geometry);
            if (!diff.IsEmpty)
            {
                if (diff is Polygon p)
                {
                    resultPolygons.Add(p);
                }
                else if (diff is MultiPolygon mp)
                {
                    resultPolygons.AddRange(mp._polygons);
                }
            }
        }
        
        return new MultiPolygon(resultPolygons) { SRID = SRID };
    }
    
    public override IGeometry SymmetricDifference(IGeometry geometry)
    {
        var union = Union(geometry);
        var intersection = Intersection(geometry);
        return union.Difference(intersection);
    }
    
    public override IGeometry Buffer(double distance)
    {
        if (distance <= 0) return new MultiPolygon() { SRID = SRID };
        
        // 각 폴리곤에 대해 버퍼를 생성하고 합집합
        IGeometry? result = null;
        foreach (var polygon in _polygons)
        {
            var buffer = polygon.Buffer(distance);
            result = result == null ? buffer : result.Union(buffer);
        }
        return result ?? new MultiPolygon() { SRID = SRID };
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