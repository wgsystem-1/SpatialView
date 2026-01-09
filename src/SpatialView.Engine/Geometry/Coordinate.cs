namespace SpatialView.Engine.Geometry;

/// <summary>
/// 좌표 구현 클래스
/// </summary>
public class Coordinate : ICoordinate, IComparable<Coordinate>, IEquatable<Coordinate>
{
    private const double NullOrdinate = double.NaN;
    
    /// <inheritdoc/>
    public double X { get; set; }
    
    /// <inheritdoc/>
    public double Y { get; set; }
    
    /// <inheritdoc/>
    public double Z { get; set; }
    
    /// <inheritdoc/>
    public double M { get; set; }
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public Coordinate() : this(0.0, 0.0)
    {
    }
    
    /// <summary>
    /// 2D 좌표 생성자
    /// </summary>
    public Coordinate(double x, double y) : this(x, y, NullOrdinate)
    {
    }
    
    /// <summary>
    /// 3D 좌표 생성자
    /// </summary>
    public Coordinate(double x, double y, double z) : this(x, y, z, NullOrdinate)
    {
    }
    
    /// <summary>
    /// 4D 좌표 생성자 (XYZM)
    /// </summary>
    public Coordinate(double x, double y, double z, double m)
    {
        X = x;
        Y = y;
        Z = z;
        M = m;
    }
    
    /// <summary>
    /// 복사 생성자
    /// </summary>
    public Coordinate(ICoordinate c) : this(c.X, c.Y, c.Z, c.M)
    {
    }
    
    /// <inheritdoc/>
    public double Distance(ICoordinate other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <inheritdoc/>
    public double Distance3D(ICoordinate other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = double.IsNaN(Z) || double.IsNaN(other.Z) ? 0 : Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    
    /// <inheritdoc/>
    public bool Equals2D(ICoordinate other)
    {
        if (other == null) return false;
        return X == other.X && Y == other.Y;
    }
    
    /// <inheritdoc/>
    public ICoordinate Copy()
    {
        return new Coordinate(this);
    }
    
    /// <summary>
    /// Z 값이 유효한지 확인
    /// </summary>
    public bool HasZ => !double.IsNaN(Z);
    
    /// <summary>
    /// M 값이 유효한지 확인
    /// </summary>
    public bool HasM => !double.IsNaN(M);
    
    /// <inheritdoc/>
    public int CompareTo(Coordinate? other)
    {
        if (other == null) return 1;
        
        if (X < other.X) return -1;
        if (X > other.X) return 1;
        
        if (Y < other.Y) return -1;
        if (Y > other.Y) return 1;
        
        return 0;
    }
    
    /// <inheritdoc/>
    public bool Equals(Coordinate? other)
    {
        if (other == null) return false;
        return X == other.X && Y == other.Y && Z == other.Z && M == other.M;
    }
    
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Coordinate other && Equals(other);
    }
    
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z, M);
    }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        var coords = $"({X:F6}, {Y:F6}";
        if (HasZ) coords += $", {Z:F6}";
        if (HasM) coords += $", {M:F6}";
        coords += ")";
        return coords;
    }
}