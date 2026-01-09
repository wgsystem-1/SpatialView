namespace SpatialView.Engine.Geometry;

/// <summary>
/// 지오메트리 컬렉션 - 여러 타입의 지오메트리 포함
/// </summary>
public class GeometryCollection : BaseGeometry
{
    /// <summary>
    /// 빈 지오메트리 컬렉션 인스턴스
    /// </summary>
    public static readonly GeometryCollection Empty = new GeometryCollection();
    
    private readonly List<IGeometry> _geometries;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public GeometryCollection() : this(new List<IGeometry>())
    {
    }
    
    /// <summary>
    /// 지오메트리 배열로부터 생성
    /// </summary>
    public GeometryCollection(IGeometry[] geometries) : this(geometries?.ToList() ?? new List<IGeometry>())
    {
    }
    
    /// <summary>
    /// 지오메트리 리스트로부터 생성
    /// </summary>
    public GeometryCollection(List<IGeometry> geometries)
    {
        _geometries = geometries ?? throw new ArgumentNullException(nameof(geometries));
        SRID = 0;
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.GeometryCollection;
    
    
    /// <inheritdoc/>
    public override bool IsEmpty => _geometries.Count == 0 || _geometries.All(g => g.IsEmpty);
    
    /// <inheritdoc/>
    public override bool IsValid => _geometries.All(g => g.IsValid);
    
    /// <inheritdoc/>
    public override int NumPoints => _geometries.Sum(g => g.NumPoints);
    
    /// <summary>
    /// 지오메트리 개수
    /// </summary>
    public int NumGeometries => _geometries.Count;
    
    /// <summary>
    /// 모든 지오메트리 가져오기
    /// </summary>
    public IReadOnlyList<IGeometry> Geometries => _geometries.AsReadOnly();
    
    /// <summary>
    /// 특정 인덱스의 지오메트리 가져오기
    /// </summary>
    public IGeometry GetGeometryN(int n)
    {
        if (n < 0 || n >= _geometries.Count)
            throw new ArgumentOutOfRangeException(nameof(n));
        return _geometries[n];
    }
    
    /// <inheritdoc/>
    public override Envelope? Envelope
    {
        get
        {
            if (IsEmpty) return null;
            var env = new Envelope();
            foreach (var geometry in _geometries)
            {
                if (geometry.Envelope != null)
                    env.ExpandToInclude(geometry.Envelope);
            }
            return env;
        }
    }
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates
    {
        get
        {
            return _geometries.SelectMany(g => g.Coordinates).ToArray();
        }
    }
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        if (IsEmpty) return double.MaxValue;
        
        double minDist = double.MaxValue;
        foreach (var geometry in _geometries)
        {
            double dist = geometry.Distance(other);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }
    
    /// <inheritdoc/>
    public override double Area
    {
        get
        {
            return _geometries.Sum(g => g.Area);
        }
    }
    
    /// <inheritdoc/>
    public override double Length
    {
        get
        {
            return _geometries.Sum(g => g.Length);
        }
    }
    
    /// <inheritdoc/>
    public override Point? Centroid
    {
        get
        {
            if (IsEmpty) return null;
            
            // 타입별 가중 평균 계산
            double totalWeight = 0;
            double weightedX = 0, weightedY = 0;
            
            foreach (var geometry in _geometries)
            {
                double weight = 0;
                ICoordinate centroid = geometry.Centroid;
                
                switch (geometry.GeometryType)
                {
                    case GeometryType.Point:
                    case GeometryType.MultiPoint:
                        weight = 1; // 포인트는 가중치 1
                        break;
                    case GeometryType.LineString:
                    case GeometryType.MultiLineString:
                        weight = geometry.Length; // 라인은 길이로 가중
                        break;
                    case GeometryType.Polygon:
                    case GeometryType.MultiPolygon:
                        weight = geometry.Area; // 폴리곤은 면적으로 가중
                        break;
                    case GeometryType.GeometryCollection:
                        weight = 1; // 컨렉션은 가중치 1
                        break;
                }
                
                if (weight > 0 && centroid != null)
                {
                    weightedX += centroid.X * weight;
                    weightedY += centroid.Y * weight;
                    totalWeight += weight;
                }
            }
            
            if (totalWeight == 0) 
            {
                var firstCentroid = _geometries[0].Centroid;
                if (firstCentroid != null)
                    return new Point(new Coordinate(firstCentroid.X, firstCentroid.Y));
                return null;
            }
            
            return new Point(new Coordinate(weightedX / totalWeight, weightedY / totalWeight));
        }
    }
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        var geometriesCopy = _geometries.Select(g => g.Copy()).ToList();
        return new GeometryCollection(geometriesCopy) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "GEOMETRYCOLLECTION EMPTY";
        
        var geometriesText = _geometries.Select(g => 
        {
            // 각 지오메트리의 WKT에서 타입 키워드 추출
            var wkt = g.ToText();
            return wkt;
        });
        
        return $"GEOMETRYCOLLECTION ({string.Join(", ", geometriesText)})";
    }
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is GeometryCollection other)
        {
            if (SRID != other.SRID) return false;
            if (_geometries.Count != other._geometries.Count) return false;
            
            for (int i = 0; i < _geometries.Count; i++)
            {
                if (!_geometries[i].Equals(other._geometries[i]))
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
        foreach (var geometry in _geometries)
        {
            hash.Add(geometry);
        }
        return hash.ToHashCode();
    }
    
    /// <inheritdoc/>
    public override int Dimension => _geometries.Count > 0 ? _geometries.Max(g => g.Dimension) : 0;
    
    #region Spatial Relationship Operations
    
    public override bool Contains(IGeometry geometry)
    {
        return _geometries.Any(g => g.Contains(geometry));
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        return _geometries.Any(g => g.Intersects(geometry));
    }
    
    public override bool Within(IGeometry geometry)
    {
        return _geometries.All(g => g.Within(geometry));
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        return _geometries.Any(g => g.Overlaps(geometry));
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        return _geometries.Any(g => g.Crosses(geometry));
    }
    
    public override bool Touches(IGeometry geometry)
    {
        return _geometries.Any(g => g.Touches(geometry));
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        return !Intersects(geometry);
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        if (geometry is GeometryCollection other)
        {
            var allGeometries = new List<IGeometry>(_geometries);
            allGeometries.AddRange(other._geometries);
            return new GeometryCollection(allGeometries) { SRID = SRID };
        }
        else
        {
            var allGeometries = new List<IGeometry>(_geometries) { geometry };
            return new GeometryCollection(allGeometries) { SRID = SRID };
        }
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        var resultGeometries = new List<IGeometry>();
        
        foreach (var geom in _geometries)
        {
            var intersection = geom.Intersection(geometry);
            if (!intersection.IsEmpty)
            {
                resultGeometries.Add(intersection);
            }
        }
        
        return new GeometryCollection(resultGeometries) { SRID = SRID };
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        var resultGeometries = new List<IGeometry>();
        
        foreach (var geom in _geometries)
        {
            var diff = geom.Difference(geometry);
            if (!diff.IsEmpty)
            {
                resultGeometries.Add(diff);
            }
        }
        
        return new GeometryCollection(resultGeometries) { SRID = SRID };
    }
    
    public override IGeometry SymmetricDifference(IGeometry geometry)
    {
        var union = Union(geometry);
        var intersection = Intersection(geometry);
        return union.Difference(intersection);
    }
    
    public override IGeometry Buffer(double distance)
    {
        if (distance <= 0) return Empty;
        
        // Buffer each geometry and union the results
        IGeometry? result = null;
        foreach (var geom in _geometries)
        {
            var buffer = geom.Buffer(distance);
            result = result == null ? buffer : result.Union(buffer);
        }
        return result ?? Empty;
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