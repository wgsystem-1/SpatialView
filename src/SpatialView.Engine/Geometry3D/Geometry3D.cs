using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Geometry3D;


/// <summary>
/// 3D 점
/// </summary>
public class Point3D : BaseGeometry, IGeometry3D
{
    private readonly Coordinate3D _coordinate;
    
    public double X => _coordinate.X;
    public double Y => _coordinate.Y;
    public double Z => _coordinate.Z;
    public override double? M => _coordinate.M;
    
    public override GeometryType GeometryType => GeometryType.Point;
    public override int Dimension => _coordinate.Dimension;
    public override bool IsEmpty => false;
    public double MinZ => Z;
    public double MaxZ => Z;
    
    public Coordinate3D Coordinate => _coordinate;
    
    public override Envelope? Envelope => new Envelope(X, Y, X, Y);
    public Envelope3D? Envelope3D => new Envelope3D(X, Y, Z, X, Y, Z);
    public override Point? Centroid => new Point(X, Y);
    public Point3D? Centroid3D => this;
    
    public Point3D(double x, double y, double z)
    {
        _coordinate = new Coordinate3D(x, y, z);
    }
    
    public Point3D(double x, double y, double z, double m)
    {
        _coordinate = new Coordinate3D(x, y, z, m);
    }
    
    public Point3D(Coordinate3D coordinate)
    {
        _coordinate = coordinate ?? throw new ArgumentNullException(nameof(coordinate));
    }
    
    public IGeometry ProjectTo2D()
    {
        return new Point(X, Y);
    }
    
    public IGeometry3D Transform(Matrix3D matrix)
    {
        return new Point3D(_coordinate.Transform(matrix));
    }

    // Explicit interface implementations
    IGeometry IGeometry3D.ProjectTo2D() => ProjectTo2D();
    IGeometry3D IGeometry3D.Transform(Matrix3D matrix) => Transform(matrix);
    
    public override ICoordinate[] Coordinates => new[] { _coordinate };
    
    public Envelope GetBounds() => Envelope!;
    
    public double Distance3D(Point3D other)
    {
        return _coordinate.Distance3D(other._coordinate);
    }
    
    public override bool Equals(object? obj)
    {
        return obj is Point3D other && _coordinate.Equals3D(other._coordinate);
    }
    
    public override int GetHashCode() => _coordinate.GetHashCode();
    
    public override string ToString() => $"POINT Z({X} {Y} {Z})";
    
    public override IGeometry Clone()
    {
        return new Point3D(_coordinate);
    }

    // Required abstract method implementations from BaseGeometry
    public override bool IsValid => true;
    public override int NumPoints => 1;
    public override IGeometry Copy() => Clone();
    public override string ToText() => ToString();
    public override double Area => 0.0;
    public override double Length => 0.0;

    public override IGeometry Transform(object transformation)
    {
        if (transformation is Matrix3D matrix3D)
            return Transform(matrix3D);
        
        throw new NotSupportedException($"Transformation type {transformation?.GetType()} not supported");
    }

    // Spatial relationship operations
    public override IGeometry Buffer(double distance) => throw new NotImplementedException("Point3D.Buffer");
    public override bool Contains(IGeometry geometry) => false;
    public override bool Crosses(IGeometry geometry) => false;
    public override IGeometry Difference(IGeometry geometry) => Clone();
    public override bool Disjoint(IGeometry geometry) => !Intersects(geometry);
    public override double Distance(IGeometry geometry) => throw new NotImplementedException("Point3D.Distance");
    public override bool Intersects(IGeometry geometry) => throw new NotImplementedException("Point3D.Intersects");
    public override IGeometry Intersection(IGeometry geometry) => throw new NotImplementedException("Point3D.Intersection");
    public override bool Overlaps(IGeometry geometry) => false;
    public override IGeometry SymmetricDifference(IGeometry geometry) => throw new NotImplementedException("Point3D.SymmetricDifference");
    public override bool Touches(IGeometry geometry) => throw new NotImplementedException("Point3D.Touches");
    public override IGeometry Union(IGeometry geometry) => throw new NotImplementedException("Point3D.Union");
    public override bool Within(IGeometry geometry) => throw new NotImplementedException("Point3D.Within");
}

/// <summary>
/// 3D 선
/// </summary>
public class LineString3D : BaseGeometry, IGeometry3D
{
    private readonly Coordinate3D[] _coordinates;
    private Envelope3D? _envelope3D;
    
    public override GeometryType GeometryType => GeometryType.LineString;
    public override int Dimension => _coordinates.Length > 0 ? _coordinates[0].Dimension : 3;
    public override bool IsEmpty => _coordinates.Length == 0;
    
    public Coordinate3D[] Coordinates3D => _coordinates;
    public override ICoordinate[] Coordinates => _coordinates.Cast<ICoordinate>().ToArray();
    
    public double MinZ => _coordinates.Length > 0 ? _coordinates.Min(c => c.Z) : 0;
    public double MaxZ => _coordinates.Length > 0 ? _coordinates.Max(c => c.Z) : 0;
    
    public LineString3D(IEnumerable<Coordinate3D> coordinates)
    {
        _coordinates = coordinates?.ToArray() ?? throw new ArgumentNullException(nameof(coordinates));
        
        if (_coordinates.Length < 2)
            throw new ArgumentException("LineString3D must have at least 2 points");
    }
    
    public override Envelope? Envelope
    {
        get
        {
            if (_coordinates.Length == 0) return null;
            
            var minX = _coordinates.Min(c => c.X);
            var minY = _coordinates.Min(c => c.Y);
            var maxX = _coordinates.Max(c => c.X);
            var maxY = _coordinates.Max(c => c.Y);
            
            return new Envelope(minX, minY, maxX, maxY);
        }
    }
    
    public Envelope3D? Envelope3D
    {
        get
        {
            if (_envelope3D == null && _coordinates.Length > 0)
            {
                var minX = _coordinates.Min(c => c.X);
                var minY = _coordinates.Min(c => c.Y);
                var minZ = _coordinates.Min(c => c.Z);
                var maxX = _coordinates.Max(c => c.X);
                var maxY = _coordinates.Max(c => c.Y);
                var maxZ = _coordinates.Max(c => c.Z);
                
                _envelope3D = new Envelope3D(minX, minY, minZ, maxX, maxY, maxZ);
            }
            return _envelope3D;
        }
    }
    
    public override Point? Centroid
    {
        get
        {
            if (_coordinates.Length == 0) return null;
            
            var totalLength = 0.0;
            var weightedX = 0.0;
            var weightedY = 0.0;
            
            for (int i = 0; i < _coordinates.Length - 1; i++)
            {
                var length = _coordinates[i].Distance2D(_coordinates[i + 1]);
                var midX = (_coordinates[i].X + _coordinates[i + 1].X) / 2.0;
                var midY = (_coordinates[i].Y + _coordinates[i + 1].Y) / 2.0;
                
                weightedX += midX * length;
                weightedY += midY * length;
                totalLength += length;
            }
            
            if (totalLength == 0) return new Point(_coordinates[0]);
            
            return new Point(weightedX / totalLength, weightedY / totalLength);
        }
    }
    
    public Point3D? Centroid3D
    {
        get
        {
            if (_coordinates.Length == 0) return null;
            
            var totalLength = 0.0;
            var weightedX = 0.0;
            var weightedY = 0.0;
            var weightedZ = 0.0;
            
            for (int i = 0; i < _coordinates.Length - 1; i++)
            {
                var length = _coordinates[i].Distance3D(_coordinates[i + 1]);
                var midX = (_coordinates[i].X + _coordinates[i + 1].X) / 2.0;
                var midY = (_coordinates[i].Y + _coordinates[i + 1].Y) / 2.0;
                var midZ = (_coordinates[i].Z + _coordinates[i + 1].Z) / 2.0;
                
                weightedX += midX * length;
                weightedY += midY * length;
                weightedZ += midZ * length;
                totalLength += length;
            }
            
            if (totalLength == 0) return new Point3D(_coordinates[0]);
            
            return new Point3D(weightedX / totalLength, weightedY / totalLength, weightedZ / totalLength);
        }
    }
    
    public IGeometry ProjectTo2D()
    {
        var coords2D = _coordinates.Select(c => c.To2D()).ToArray();
        return new LineString(coords2D);
    }
    
    public IGeometry3D Transform(Matrix3D matrix)
    {
        var transformedCoords = _coordinates.Select(c => c.Transform(matrix)).ToArray();
        return new LineString3D(transformedCoords);
    }

    // Explicit interface implementations
    IGeometry IGeometry3D.ProjectTo2D() => ProjectTo2D();
    IGeometry3D IGeometry3D.Transform(Matrix3D matrix) => Transform(matrix);
    
    public Envelope GetBounds() => Envelope!;
    
    /// <summary>
    /// 3D 길이 계산
    /// </summary>
    public double Length3D
    {
        get
        {
            var length = 0.0;
            for (int i = 0; i < _coordinates.Length - 1; i++)
            {
                length += _coordinates[i].Distance3D(_coordinates[i + 1]);
            }
            return length;
        }
    }
    
    public override string ToString() => $"LINESTRING Z({string.Join(", ", _coordinates.Select(c => $"{c.X} {c.Y} {c.Z}"))})";
    public override int GetHashCode() => _coordinates.Length > 0 ? _coordinates[0].GetHashCode() : 0;
    
    public override IGeometry Clone()
    {
        return new LineString3D(_coordinates.Select(c => new Coordinate3D(c.X, c.Y, c.Z, c.M)));
    }

    // Required abstract method implementations from BaseGeometry
    public override bool IsValid => _coordinates.Length >= 2;
    public override int NumPoints => _coordinates.Length;
    public override IGeometry Copy() => Clone();
    public override string ToText() => ToString();
    public override double Area => 0.0;
    public override double Length => Length3D;

    public override IGeometry Transform(object transformation)
    {
        if (transformation is Matrix3D matrix3D)
            return Transform(matrix3D);
        
        throw new NotSupportedException($"Transformation type {transformation?.GetType()} not supported");
    }

    // Spatial relationship operations
    public override IGeometry Buffer(double distance) => throw new NotImplementedException("LineString3D.Buffer");
    public override bool Contains(IGeometry geometry) => false;
    public override bool Crosses(IGeometry geometry) => throw new NotImplementedException("LineString3D.Crosses");
    public override IGeometry Difference(IGeometry geometry) => throw new NotImplementedException("LineString3D.Difference");
    public override bool Disjoint(IGeometry geometry) => !Intersects(geometry);
    public override double Distance(IGeometry geometry) => throw new NotImplementedException("LineString3D.Distance");
    public override bool Intersects(IGeometry geometry) => throw new NotImplementedException("LineString3D.Intersects");
    public override IGeometry Intersection(IGeometry geometry) => throw new NotImplementedException("LineString3D.Intersection");
    public override bool Overlaps(IGeometry geometry) => false;
    public override IGeometry SymmetricDifference(IGeometry geometry) => throw new NotImplementedException("LineString3D.SymmetricDifference");
    public override bool Touches(IGeometry geometry) => throw new NotImplementedException("LineString3D.Touches");
    public override IGeometry Union(IGeometry geometry) => throw new NotImplementedException("LineString3D.Union");
    public override bool Within(IGeometry geometry) => throw new NotImplementedException("LineString3D.Within");
}

/// <summary>
/// 3D 폴리곤
/// </summary>
public class Polygon3D : BaseGeometry, IGeometry3D
{
    private readonly LinearRing3D _exteriorRing;
    private readonly LinearRing3D[] _interiorRings;
    private Envelope3D? _envelope3D;
    
    public override GeometryType GeometryType => GeometryType.Polygon;
    public override int Dimension => 3;
    public override bool IsEmpty => false;
    
    public LinearRing3D ExteriorRing => _exteriorRing;
    public IReadOnlyList<LinearRing3D> InteriorRings => _interiorRings;
    
    public double MinZ
    {
        get
        {
            var minZ = _exteriorRing.MinZ;
            foreach (var hole in _interiorRings)
            {
                minZ = Math.Min(minZ, hole.MinZ);
            }
            return minZ;
        }
    }
    
    public double MaxZ
    {
        get
        {
            var maxZ = _exteriorRing.MaxZ;
            foreach (var hole in _interiorRings)
            {
                maxZ = Math.Max(maxZ, hole.MaxZ);
            }
            return maxZ;
        }
    }
    
    public Polygon3D(LinearRing3D exteriorRing, params LinearRing3D[] interiorRings)
    {
        _exteriorRing = exteriorRing ?? throw new ArgumentNullException(nameof(exteriorRing));
        _interiorRings = interiorRings ?? Array.Empty<LinearRing3D>();
    }
    
    public override Envelope? Envelope => _exteriorRing.Envelope;
    
    public Envelope3D? Envelope3D
    {
        get
        {
            _envelope3D ??= _exteriorRing.Envelope3D;
            return _envelope3D;
        }
    }
    
    public override Point? Centroid
    {
        get
        {
            // 2D 폴리곤으로 투영하여 중심점 계산
            var polygon2D = (Polygon)ProjectTo2D();
            return polygon2D.Centroid;
        }
    }
    
    public Point3D? Centroid3D
    {
        get
        {
            // 간단한 구현: 모든 정점의 평균
            var allCoords = new List<Coordinate3D>();
            allCoords.AddRange(_exteriorRing.Coordinates3D);
            
            foreach (var hole in _interiorRings)
            {
                allCoords.AddRange(hole.Coordinates3D);
            }
            
            if (allCoords.Count == 0) return null;
            
            var avgX = allCoords.Average(c => c.X);
            var avgY = allCoords.Average(c => c.Y);
            var avgZ = allCoords.Average(c => c.Z);
            
            return new Point3D(avgX, avgY, avgZ);
        }
    }
    
    public override ICoordinate[] Coordinates
    {
        get
        {
            var coords = new List<ICoordinate>();
            coords.AddRange(_exteriorRing.Coordinates);
            foreach (var hole in _interiorRings)
            {
                coords.AddRange(hole.Coordinates);
            }
            return coords.ToArray();
        }
    }
    
    public IGeometry ProjectTo2D()
    {
        var exterior2D = (LinearRing)_exteriorRing.ProjectTo2D();
        var holes2D = _interiorRings.Select(h => (LinearRing)h.ProjectTo2D()).ToArray();
        return new Polygon(exterior2D, holes2D);
    }
    
    public IGeometry3D Transform(Matrix3D matrix)
    {
        var transformedExterior = (LinearRing3D)_exteriorRing.Transform(matrix);
        var transformedHoles = _interiorRings.Select(h => (LinearRing3D)h.Transform(matrix)).ToArray();
        return new Polygon3D(transformedExterior, transformedHoles);
    }

    // Explicit interface implementations
    IGeometry IGeometry3D.ProjectTo2D() => ProjectTo2D();
    IGeometry3D IGeometry3D.Transform(Matrix3D matrix) => Transform(matrix);
    
    public Envelope GetBounds() => Envelope!;
    
    /// <summary>
    /// 면의 법선 벡터 계산
    /// </summary>
    public Coordinate3D? GetNormal()
    {
        var coords = _exteriorRing.Coordinates3D;
        if (coords.Length < 3) return null;
        
        // 첫 세 점을 사용하여 법선 계산
        var v1 = coords[1] - coords[0];
        var v2 = coords[2] - coords[0];
        var normal = v1.CrossProduct(v2);
        
        return normal.Normalize();
    }
    
    public override string ToString() => $"POLYGON Z(({string.Join(", ", _exteriorRing.Coordinates3D.Select(c => $"{c.X} {c.Y} {c.Z}"))}))";
    public override int GetHashCode() => _exteriorRing.GetHashCode();
    
    public override IGeometry Clone()
    {
        var exteriorClone = _exteriorRing.Clone() as LinearRing3D;
        var interiorClones = _interiorRings.Select(r => r.Clone() as LinearRing3D).ToArray();
        return new Polygon3D(exteriorClone!, interiorClones!);
    }

    // Required abstract method implementations from BaseGeometry
    public override bool IsValid => _exteriorRing.IsValid;
    public override int NumPoints => _exteriorRing.NumPoints + _interiorRings.Sum(r => r.NumPoints);
    public override IGeometry Copy() => Clone();
    public override string ToText() => ToString();
    
    public override double Area
    {
        get
        {
            // Simple 3D area calculation - project to 2D and calculate area
            var polygon2D = (Polygon)ProjectTo2D();
            return polygon2D.Area;
        }
    }
    
    public override double Length => _exteriorRing.Length + _interiorRings.Sum(r => r.Length);

    public override IGeometry Transform(object transformation)
    {
        if (transformation is Matrix3D matrix3D)
            return Transform(matrix3D);
        
        throw new NotSupportedException($"Transformation type {transformation?.GetType()} not supported");
    }

    // Spatial relationship operations
    public override IGeometry Buffer(double distance) => throw new NotImplementedException("Polygon3D.Buffer");
    public override bool Contains(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Contains");
    public override bool Crosses(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Crosses");
    public override IGeometry Difference(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Difference");
    public override bool Disjoint(IGeometry geometry) => !Intersects(geometry);
    public override double Distance(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Distance");
    public override bool Intersects(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Intersects");
    public override IGeometry Intersection(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Intersection");
    public override bool Overlaps(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Overlaps");
    public override IGeometry SymmetricDifference(IGeometry geometry) => throw new NotImplementedException("Polygon3D.SymmetricDifference");
    public override bool Touches(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Touches");
    public override IGeometry Union(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Union");
    public override bool Within(IGeometry geometry) => throw new NotImplementedException("Polygon3D.Within");
}

/// <summary>
/// 3D 선형 링
/// </summary>
public class LinearRing3D : LineString3D
{
    public LinearRing3D(IEnumerable<Coordinate3D> coordinates) : base(coordinates)
    {
        // 폐합 확인
        var coords = Coordinates3D;
        if (coords.Length < 4)
            throw new ArgumentException("LinearRing3D must have at least 4 points");
        
        if (!coords[0].Equals3D(coords[coords.Length - 1]))
            throw new ArgumentException("LinearRing3D must be closed");
    }
}

/// <summary>
/// 3D Envelope
/// </summary>
public class Envelope3D : Envelope
{
    public double MinZ { get; set; }
    public double MaxZ { get; set; }
    
    public double Depth => MaxZ - MinZ;
    public double Volume => Width * Height * Depth;
    
    public Envelope3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        : base(minX, minY, maxX, maxY)
    {
        MinZ = minZ;
        MaxZ = maxZ;
    }
    
    public bool Intersects3D(Envelope3D other)
    {
        return Intersects(other) &&
               !(other.MaxZ < MinZ || other.MinZ > MaxZ);
    }
    
    public bool Contains3D(double x, double y, double z)
    {
        return Contains(x, y) && z >= MinZ && z <= MaxZ;
    }
    
    public bool Contains3D(Coordinate3D coord)
    {
        return Contains3D(coord.X, coord.Y, coord.Z);
    }
    
    public Envelope3D? Intersection3D(Envelope3D other)
    {
        var intersection2D = Intersection(other);
        if (intersection2D == null) return null;
        
        var minZ = Math.Max(MinZ, other.MinZ);
        var maxZ = Math.Min(MaxZ, other.MaxZ);
        
        if (minZ > maxZ) return null;
        
        return new Envelope3D(
            intersection2D.MinX, intersection2D.MinY, minZ,
            intersection2D.MaxX, intersection2D.MaxY, maxZ
        );
    }
    
    public void ExpandToInclude(Coordinate3D coord)
    {
        ExpandToInclude(coord.X, coord.Y);
        MinZ = Math.Min(MinZ, coord.Z);
        MaxZ = Math.Max(MaxZ, coord.Z);
    }
}

/// <summary>
/// 3D 지오메트리 컬렉션
/// </summary>
public class GeometryCollection3D : BaseGeometry, IGeometry3D
{
    private readonly IGeometry3D[] _geometries;
    private Envelope3D? _envelope3D;
    
    public override GeometryType GeometryType => GeometryType.GeometryCollection;
    public override int Dimension => 3;
    public override bool IsEmpty => _geometries.Length == 0;
    
    public IReadOnlyList<IGeometry3D> Geometries => _geometries;
    
    public GeometryCollection3D(params IGeometry3D[] geometries)
    {
        _geometries = geometries ?? Array.Empty<IGeometry3D>();
    }
    
    public double MinZ => _geometries.Length > 0 ? 
        _geometries.Min(g => g.MinZ) : 0;
    
    public double MaxZ => _geometries.Length > 0 ? 
        _geometries.Max(g => g.MaxZ) : 0;
    
    public override Envelope? Envelope
    {
        get
        {
            if (_geometries.Length == 0) return null;
            
            var envelope = _geometries[0].Envelope;
            for (int i = 1; i < _geometries.Length; i++)
            {
                var geomEnv = _geometries[i].Envelope;
                if (geomEnv != null)
                    envelope?.ExpandToInclude(geomEnv);
            }
            
            return envelope;
        }
    }
    
    public Envelope3D? Envelope3D
    {
        get
        {
            if (_envelope3D == null && _geometries.Length > 0)
            {
                _envelope3D = _geometries[0].Envelope3D;
                for (int i = 1; i < _geometries.Length; i++)
                {
                    var geomEnv = _geometries[i].Envelope3D;
                    if (geomEnv != null && _envelope3D != null)
                    {
                        _envelope3D = new Envelope3D(
                            Math.Min(_envelope3D.MinX, geomEnv.MinX),
                            Math.Min(_envelope3D.MinY, geomEnv.MinY),
                            Math.Min(_envelope3D.MinZ, geomEnv.MinZ),
                            Math.Max(_envelope3D.MaxX, geomEnv.MaxX),
                            Math.Max(_envelope3D.MaxY, geomEnv.MaxY),
                            Math.Max(_envelope3D.MaxZ, geomEnv.MaxZ)
                        );
                    }
                }
            }
            return _envelope3D;
        }
    }
    
    public override Point? Centroid
    {
        get
        {
            var centroids = _geometries.Select(g => g.Centroid).Where(c => c != null).ToList();
            if (centroids.Count == 0) return null;
            
            var avgX = centroids.Average(c => c!.X);
            var avgY = centroids.Average(c => c!.Y);
            
            return new Point(avgX, avgY);
        }
    }
    
    public Point3D? Centroid3D
    {
        get
        {
            var centroids = _geometries.Select(g => g.Centroid3D).Where(c => c != null).ToList();
            if (centroids.Count == 0) return null;
            
            var avgX = centroids.Average(c => c!.X);
            var avgY = centroids.Average(c => c!.Y);
            var avgZ = centroids.Average(c => c!.Z);
            
            return new Point3D(avgX, avgY, avgZ);
        }
    }
    
    public override ICoordinate[] Coordinates
    {
        get
        {
            var coords = new List<ICoordinate>();
            foreach (var geom in _geometries)
            {
                coords.AddRange(geom.Coordinates);
            }
            return coords.ToArray();
        }
    }
    
    public IGeometry ProjectTo2D()
    {
        var geometries2D = _geometries.Select(g => g.ProjectTo2D()).ToArray();
        return new GeometryCollection(geometries2D);
    }
    
    public IGeometry3D Transform(Matrix3D matrix)
    {
        var transformed = _geometries.Select(g => g.Transform(matrix)).ToArray();
        return new GeometryCollection3D(transformed);
    }

    // Explicit interface implementations
    IGeometry IGeometry3D.ProjectTo2D() => ProjectTo2D();
    IGeometry3D IGeometry3D.Transform(Matrix3D matrix) => Transform(matrix);
    
    public Envelope GetBounds() => Envelope!;
    
    public override string ToString() => $"GEOMETRYCOLLECTION Z({string.Join(", ", _geometries.Select(g => g.ToString()))})";
    public override int GetHashCode() => _geometries.Length > 0 ? _geometries[0].GetHashCode() : 0;
    
    public override IGeometry Clone()
    {
        var clonedGeometries = _geometries.Select(g => (IGeometry3D)g.Clone()).ToArray();
        return new GeometryCollection3D(clonedGeometries!);
    }

    // Required abstract method implementations from BaseGeometry
    public override bool IsValid => _geometries.All(g => g.IsValid);
    public override int NumPoints => _geometries.Sum(g => g.NumPoints);
    public override IGeometry Copy() => Clone();
    public override string ToText() => ToString();
    public override double Area => _geometries.Sum(g => g.Area);
    public override double Length => _geometries.Sum(g => g.Length);

    public override IGeometry Transform(object transformation)
    {
        if (transformation is Matrix3D matrix3D)
            return Transform(matrix3D);
        
        throw new NotSupportedException($"Transformation type {transformation?.GetType()} not supported");
    }

    // Spatial relationship operations
    public override IGeometry Buffer(double distance) => throw new NotImplementedException("GeometryCollection3D.Buffer");
    public override bool Contains(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Contains");
    public override bool Crosses(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Crosses");
    public override IGeometry Difference(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Difference");
    public override bool Disjoint(IGeometry geometry) => !Intersects(geometry);
    public override double Distance(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Distance");
    public override bool Intersects(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Intersects");
    public override IGeometry Intersection(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Intersection");
    public override bool Overlaps(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Overlaps");
    public override IGeometry SymmetricDifference(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.SymmetricDifference");
    public override bool Touches(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Touches");
    public override IGeometry Union(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Union");
    public override bool Within(IGeometry geometry) => throw new NotImplementedException("GeometryCollection3D.Within");
}