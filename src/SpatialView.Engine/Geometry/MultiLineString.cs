namespace SpatialView.Engine.Geometry;

/// <summary>
/// 멀티라인스트링 지오메트리 - 여러 개의 라인스트링 컬렉션
/// </summary>
public class MultiLineString : BaseGeometry
{
    /// <summary>
    /// 빈 멀티라인스트링 인스턴스
    /// </summary>
    public static readonly MultiLineString Empty = new MultiLineString();
    
    private readonly List<LineString> _lineStrings;
    
    /// <summary>
    /// 라인스트링 컬렉션 (읽기 전용)
    /// </summary>
    public IReadOnlyList<LineString> Geometries => _lineStrings;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public MultiLineString() : this(new List<LineString>())
    {
    }
    
    /// <summary>
    /// 라인스트링 배열로부터 생성
    /// </summary>
    public MultiLineString(LineString[] lineStrings) : this(lineStrings?.ToList() ?? new List<LineString>())
    {
    }
    
    /// <summary>
    /// 라인스트링 리스트로부터 생성
    /// </summary>
    public MultiLineString(List<LineString> lineStrings)
    {
        _lineStrings = lineStrings ?? throw new ArgumentNullException(nameof(lineStrings));
        SRID = 0;
    }
    
    /// <inheritdoc/>
    public override GeometryType GeometryType => GeometryType.MultiLineString;
    
    /// <inheritdoc/>
    public override bool IsEmpty => _lineStrings.Count == 0 || _lineStrings.All(ls => ls.IsEmpty);
    
    /// <inheritdoc/>
    public override bool IsValid => _lineStrings.All(ls => ls.IsValid);
    
    /// <inheritdoc/>
    public override int NumPoints => _lineStrings.Sum(ls => ls.NumPoints);
    
    /// <inheritdoc/>
    public override int Dimension => 1;
    
    /// <summary>
    /// 지오메트리 개수
    /// </summary>
    public int NumGeometries => _lineStrings.Count;
    
    /// <summary>
    /// 특정 인덱스의 라인스트링 가져오기
    /// </summary>
    public LineString GetGeometryN(int n)
    {
        if (n < 0 || n >= _lineStrings.Count)
            throw new ArgumentOutOfRangeException(nameof(n));
        return _lineStrings[n];
    }
    
    /// <inheritdoc/>
    public override Envelope? Envelope
    {
        get
        {
            if (IsEmpty) return null;
            var env = new Envelope();
            foreach (var lineString in _lineStrings)
            {
                if (lineString.Envelope != null)
                    env.ExpandToInclude(lineString.Envelope);
            }
            return env;
        }
    }
    
    /// <inheritdoc/>
    public override ICoordinate[] Coordinates
    {
        get
        {
            return _lineStrings.SelectMany(ls => ls.Coordinates).ToArray();
        }
    }
    
    /// <inheritdoc/>
    public override double Distance(IGeometry other)
    {
        if (IsEmpty) return double.MaxValue;
        
        double minDist = double.MaxValue;
        foreach (var lineString in _lineStrings)
        {
            double dist = lineString.Distance(other);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }
    
    /// <inheritdoc/>
    public override double Area => 0.0;
    
    /// <inheritdoc/>
    public override double Length => _lineStrings.Sum(ls => ls.Length);
    
    /// <inheritdoc/>
    public override Point? Centroid
    {
        get
        {
            if (IsEmpty) return null;
            
            double totalLength = Length;
            if (totalLength == 0) return new Point(_lineStrings[0].StartPoint!);
            
            double weightedX = 0, weightedY = 0;
            
            foreach (var lineString in _lineStrings)
            {
                var length = lineString.Length;
                if (length > 0)
                {
                    var centroid = lineString.Centroid;
                    if (centroid != null)
                    {
                        weightedX += centroid.Coordinate.X * length;
                        weightedY += centroid.Coordinate.Y * length;
                    }
                }
            }
            
            return new Point(new Coordinate(weightedX / totalLength, weightedY / totalLength));
        }
    }
    
    /// <inheritdoc/>
    public Envelope GetBounds() => Envelope;
    
    /// <summary>
    /// 폐곡선인지 확인 (모든 라인이 폐곡선)
    /// </summary>
    public bool IsClosed => _lineStrings.Count > 0 && _lineStrings.All(ls => ls.IsClosed);
    
    /// <inheritdoc/>
    public override IGeometry Copy()
    {
        var lineStringsCopy = _lineStrings.Select(ls => (LineString)ls.Copy()).ToList();
        return new MultiLineString(lineStringsCopy) { SRID = SRID };
    }
    
    /// <inheritdoc/>
    public override string ToText()
    {
        if (IsEmpty) return "MULTILINESTRING EMPTY";
        
        var lineStrings = _lineStrings.Select(ls => 
        {
            var coords = ls.Coordinates;
            var coordsText = string.Join(", ", coords.Select(c => 
            {
                var coord = $"{c.X:F6} {c.Y:F6}";
                if (!double.IsNaN(c.Z)) coord += $" {c.Z:F6}";
                return coord;
            }));
            return $"({coordsText})";
        });
        
        return $"MULTILINESTRING ({string.Join(", ", lineStrings)})";
    }
    
    
    public override string ToString()
    {
        return ToText();
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is MultiLineString other)
        {
            if (SRID != other.SRID) return false;
            if (_lineStrings.Count != other._lineStrings.Count) return false;
            
            for (int i = 0; i < _lineStrings.Count; i++)
            {
                if (!_lineStrings[i].Equals(other._lineStrings[i]))
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
        foreach (var lineString in _lineStrings)
        {
            hash.Add(lineString);
        }
        return hash.ToHashCode();
    }
    
    #region Spatial Relationship Operations
    
    public override bool Contains(IGeometry geometry)
    {
        if (geometry is Point point)
        {
            return _lineStrings.Any(ls => ls.Contains(point));
        }
        else if (geometry is LineString lineString)
        {
            // Check if any of our linestrings contains the entire linestring
            return _lineStrings.Any(ls => ls.Contains(lineString));
        }
        else if (geometry is MultiLineString other)
        {
            // All linestrings in other must be contained in our linestrings
            return other._lineStrings.All(otherLs => 
                _lineStrings.Any(ls => ls.Contains(otherLs)));
        }
        return false;
    }
    
    public override bool Intersects(IGeometry geometry)
    {
        return _lineStrings.Any(ls => ls.Intersects(geometry));
    }
    
    public override bool Within(IGeometry geometry)
    {
        return _lineStrings.All(ls => ls.Within(geometry));
    }
    
    public override bool Overlaps(IGeometry geometry)
    {
        if (geometry is LineString || geometry is MultiLineString)
        {
            // LineStrings overlap if they have some, but not all, interior points in common
            var intersection = Intersection(geometry);
            return !intersection.IsEmpty && 
                   !Equals(geometry) && 
                   !Contains(geometry) && 
                   !geometry.Contains(this);
        }
        return false;
    }
    
    public override bool Crosses(IGeometry geometry)
    {
        return _lineStrings.Any(ls => ls.Crosses(geometry));
    }
    
    public override bool Touches(IGeometry geometry)
    {
        return _lineStrings.Any(ls => ls.Touches(geometry));
    }
    
    public override bool Disjoint(IGeometry geometry)
    {
        return !Intersects(geometry);
    }
    
    #endregion
    
    #region Spatial Operations
    
    public override IGeometry Union(IGeometry geometry)
    {
        if (geometry is MultiLineString other)
        {
            var allLineStrings = new List<LineString>(_lineStrings);
            allLineStrings.AddRange(other._lineStrings);
            return new MultiLineString(allLineStrings) { SRID = SRID };
        }
        else if (geometry is LineString lineString)
        {
            var allLineStrings = new List<LineString>(_lineStrings) { lineString };
            return new MultiLineString(allLineStrings) { SRID = SRID };
        }
        return Copy();
    }
    
    public override IGeometry Intersection(IGeometry geometry)
    {
        var resultLineStrings = new List<LineString>();
        
        foreach (var lineString in _lineStrings)
        {
            var intersection = lineString.Intersection(geometry);
            if (!intersection.IsEmpty)
            {
                if (intersection is LineString ls)
                {
                    resultLineStrings.Add(ls);
                }
                else if (intersection is MultiLineString mls)
                {
                    resultLineStrings.AddRange(mls._lineStrings);
                }
            }
        }
        
        return new MultiLineString(resultLineStrings) { SRID = SRID };
    }
    
    public override IGeometry Difference(IGeometry geometry)
    {
        var resultLineStrings = new List<LineString>();
        
        foreach (var lineString in _lineStrings)
        {
            var diff = lineString.Difference(geometry);
            if (!diff.IsEmpty)
            {
                if (diff is LineString ls)
                {
                    resultLineStrings.Add(ls);
                }
                else if (diff is MultiLineString mls)
                {
                    resultLineStrings.AddRange(mls._lineStrings);
                }
            }
        }
        
        return new MultiLineString(resultLineStrings) { SRID = SRID };
    }
    
    public override IGeometry SymmetricDifference(IGeometry geometry)
    {
        var union = Union(geometry);
        var intersection = Intersection(geometry);
        return union.Difference(intersection);
    }
    
    public override IGeometry Buffer(double distance)
    {
        if (distance <= 0) return new GeometryCollection() { SRID = SRID };
        
        // Buffer each linestring and union the results
        IGeometry? result = null;
        foreach (var lineString in _lineStrings)
        {
            var buffer = lineString.Buffer(distance);
            result = result == null ? buffer : result.Union(buffer);
        }
        return result ?? new GeometryCollection() { SRID = SRID };
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