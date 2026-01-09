using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Geometry3D;

/// <summary>
/// 3D 좌표를 나타내는 클래스
/// </summary>
public class Coordinate3D : ICoordinate
{
    /// <summary>
    /// X 좌표
    /// </summary>
    public double X { get; set; }
    
    /// <summary>
    /// Y 좌표
    /// </summary>
    public double Y { get; set; }
    
    /// <summary>
    /// Z 좌표 (고도/높이)
    /// </summary>
    public double Z { get; set; }
    
    /// <summary>
    /// M 좌표 (선택적 측정값)
    /// </summary>
    double ICoordinate.M { get; set; }
    
    /// <summary>
    /// M 좌표 (nullable 버전)
    /// </summary>
    public double? MValue { get; set; }
    
    /// <summary>
    /// M 속성 (호환성)
    /// </summary>
    public double M 
    { 
        get => MValue ?? double.NaN;
        set => MValue = double.IsNaN(value) ? null : value;
    }
    
    /// <summary>
    /// 차원
    /// </summary>
    public int Dimension => MValue.HasValue ? 4 : 3;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public Coordinate3D() : this(0, 0, 0) { }
    
    /// <summary>
    /// 3D 좌표 생성자
    /// </summary>
    public Coordinate3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    
    /// <summary>
    /// 4D 좌표 생성자 (M 값 포함)
    /// </summary>
    public Coordinate3D(double x, double y, double z, double m) : this(x, y, z)
    {
        MValue = m;
        ((ICoordinate)this).M = m;
    }
    
    /// <summary>
    /// 2D 좌표에서 변환
    /// </summary>
    public Coordinate3D(ICoordinate coord2D, double z = 0) : this(coord2D.X, coord2D.Y, z) { }
    
    /// <summary>
    /// 2D 동등성 비교
    /// </summary>
    public bool Equals2D(ICoordinate other)
    {
        if (other == null) return false;
        return Math.Abs(X - other.X) < double.Epsilon && 
               Math.Abs(Y - other.Y) < double.Epsilon;
    }
    
    /// <summary>
    /// 3D 동등성 비교
    /// </summary>
    public bool Equals3D(Coordinate3D other)
    {
        if (other == null) return false;
        return Equals2D(other) && Math.Abs(Z - other.Z) < double.Epsilon;
    }
    
    /// <summary>
    /// 두 점 간의 3D 거리
    /// </summary>
    public double Distance3D(Coordinate3D other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        var dz = other.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    
    /// <summary>
    /// 두 점 간의 2D 거리
    /// </summary>
    public double Distance2D(ICoordinate other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// 복사본 생성
    /// </summary>
    public ICoordinate Copy()
    {
        return MValue.HasValue ? 
            new Coordinate3D(X, Y, Z, MValue.Value) : 
            new Coordinate3D(X, Y, Z);
    }
    
    /// <summary>
    /// 2D 거리 계산
    /// </summary>
    public double Distance(ICoordinate other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        return Distance2D(other);
    }
    
    /// <summary>
    /// 3D 거리 계산
    /// </summary>
    public double Distance3D(ICoordinate other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        var dx = other.X - X;
        var dy = other.Y - Y;
        var dz = other.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    
    /// <summary>
    /// 2D 좌표로 변환
    /// </summary>
    public Coordinate To2D()
    {
        return new Coordinate(X, Y);
    }
    
    /// <summary>
    /// 좌표 정규화 (단위 벡터로 변환)
    /// </summary>
    public Coordinate3D Normalize()
    {
        var length = Math.Sqrt(X * X + Y * Y + Z * Z);
        if (length == 0) return new Coordinate3D(0, 0, 0);
        
        return new Coordinate3D(X / length, Y / length, Z / length);
    }
    
    /// <summary>
    /// 벡터 내적
    /// </summary>
    public double DotProduct(Coordinate3D other)
    {
        return X * other.X + Y * other.Y + Z * other.Z;
    }
    
    /// <summary>
    /// 벡터 외적
    /// </summary>
    public Coordinate3D CrossProduct(Coordinate3D other)
    {
        return new Coordinate3D(
            Y * other.Z - Z * other.Y,
            Z * other.X - X * other.Z,
            X * other.Y - Y * other.X
        );
    }
    
    /// <summary>
    /// 좌표 보간
    /// </summary>
    public Coordinate3D Interpolate(Coordinate3D other, double ratio)
    {
        return new Coordinate3D(
            X + (other.X - X) * ratio,
            Y + (other.Y - Y) * ratio,
            Z + (other.Z - Z) * ratio
        );
    }
    
    /// <summary>
    /// 좌표 변환 적용
    /// </summary>
    public Coordinate3D Transform(Matrix3D matrix)
    {
        var result = matrix.Transform(this);
        return new Coordinate3D(result.X, result.Y, result.Z);
    }
    
    public override bool Equals(object? obj)
    {
        if (obj is Coordinate3D coord3D)
            return Equals3D(coord3D);
        if (obj is ICoordinate coord)
            return Equals2D(coord);
        return false;
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z, MValue);
    }
    
    public override string ToString()
    {
        return MValue.HasValue ? 
            $"({X:F6}, {Y:F6}, {Z:F6}, {MValue.Value:F6})" : 
            $"({X:F6}, {Y:F6}, {Z:F6})";
    }
    
    // 연산자 오버로드
    public static Coordinate3D operator +(Coordinate3D a, Coordinate3D b)
    {
        return new Coordinate3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
    
    public static Coordinate3D operator -(Coordinate3D a, Coordinate3D b)
    {
        return new Coordinate3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }
    
    public static Coordinate3D operator *(Coordinate3D coord, double scalar)
    {
        return new Coordinate3D(coord.X * scalar, coord.Y * scalar, coord.Z * scalar);
    }
    
    public static Coordinate3D operator *(double scalar, Coordinate3D coord)
    {
        return coord * scalar;
    }
    
    public static Coordinate3D operator /(Coordinate3D coord, double scalar)
    {
        if (Math.Abs(scalar) < double.Epsilon)
            throw new DivideByZeroException();
        
        return new Coordinate3D(coord.X / scalar, coord.Y / scalar, coord.Z / scalar);
    }
}

/// <summary>
/// 3D 변환 행렬
/// </summary>
public class Matrix3D
{
    private readonly double[,] _matrix = new double[4, 4];
    
    public Matrix3D()
    {
        // 단위 행렬로 초기화
        SetIdentity();
    }
    
    /// <summary>
    /// 단위 행렬 설정
    /// </summary>
    public void SetIdentity()
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                _matrix[i, j] = i == j ? 1.0 : 0.0;
            }
        }
    }
    
    /// <summary>
    /// 이동 행렬 생성
    /// </summary>
    public static Matrix3D CreateTranslation(double dx, double dy, double dz)
    {
        var matrix = new Matrix3D();
        matrix._matrix[0, 3] = dx;
        matrix._matrix[1, 3] = dy;
        matrix._matrix[2, 3] = dz;
        return matrix;
    }
    
    /// <summary>
    /// 스케일 행렬 생성
    /// </summary>
    public static Matrix3D CreateScale(double sx, double sy, double sz)
    {
        var matrix = new Matrix3D();
        matrix._matrix[0, 0] = sx;
        matrix._matrix[1, 1] = sy;
        matrix._matrix[2, 2] = sz;
        return matrix;
    }
    
    /// <summary>
    /// X축 회전 행렬 생성
    /// </summary>
    public static Matrix3D CreateRotationX(double angle)
    {
        var matrix = new Matrix3D();
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        matrix._matrix[1, 1] = cos;
        matrix._matrix[1, 2] = -sin;
        matrix._matrix[2, 1] = sin;
        matrix._matrix[2, 2] = cos;
        
        return matrix;
    }
    
    /// <summary>
    /// Y축 회전 행렬 생성
    /// </summary>
    public static Matrix3D CreateRotationY(double angle)
    {
        var matrix = new Matrix3D();
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        matrix._matrix[0, 0] = cos;
        matrix._matrix[0, 2] = sin;
        matrix._matrix[2, 0] = -sin;
        matrix._matrix[2, 2] = cos;
        
        return matrix;
    }
    
    /// <summary>
    /// Z축 회전 행렬 생성
    /// </summary>
    public static Matrix3D CreateRotationZ(double angle)
    {
        var matrix = new Matrix3D();
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        
        matrix._matrix[0, 0] = cos;
        matrix._matrix[0, 1] = -sin;
        matrix._matrix[1, 0] = sin;
        matrix._matrix[1, 1] = cos;
        
        return matrix;
    }
    
    /// <summary>
    /// 좌표 변환
    /// </summary>
    public Coordinate3D Transform(Coordinate3D coord)
    {
        var x = _matrix[0, 0] * coord.X + _matrix[0, 1] * coord.Y + 
                _matrix[0, 2] * coord.Z + _matrix[0, 3];
        var y = _matrix[1, 0] * coord.X + _matrix[1, 1] * coord.Y + 
                _matrix[1, 2] * coord.Z + _matrix[1, 3];
        var z = _matrix[2, 0] * coord.X + _matrix[2, 1] * coord.Y + 
                _matrix[2, 2] * coord.Z + _matrix[2, 3];
        
        return new Coordinate3D(x, y, z);
    }
    
    /// <summary>
    /// 행렬 곱셈
    /// </summary>
    public static Matrix3D operator *(Matrix3D a, Matrix3D b)
    {
        var result = new Matrix3D();
        
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                double sum = 0;
                for (int k = 0; k < 4; k++)
                {
                    sum += a._matrix[i, k] * b._matrix[k, j];
                }
                result._matrix[i, j] = sum;
            }
        }
        
        return result;
    }
}

/// <summary>
/// 3D 좌표계 변환 유틸리티
/// </summary>
public static class CoordinateSystem3D
{
    /// <summary>
    /// 구면 좌표를 직교 좌표로 변환
    /// </summary>
    public static Coordinate3D SphericalToCartesian(double radius, double azimuth, double elevation)
    {
        var x = radius * Math.Cos(elevation) * Math.Cos(azimuth);
        var y = radius * Math.Cos(elevation) * Math.Sin(azimuth);
        var z = radius * Math.Sin(elevation);
        
        return new Coordinate3D(x, y, z);
    }
    
    /// <summary>
    /// 직교 좌표를 구면 좌표로 변환
    /// </summary>
    public static (double radius, double azimuth, double elevation) CartesianToSpherical(Coordinate3D coord)
    {
        var radius = Math.Sqrt(coord.X * coord.X + coord.Y * coord.Y + coord.Z * coord.Z);
        var azimuth = Math.Atan2(coord.Y, coord.X);
        var elevation = Math.Atan2(coord.Z, Math.Sqrt(coord.X * coord.X + coord.Y * coord.Y));
        
        return (radius, azimuth, elevation);
    }
    
    /// <summary>
    /// 지리좌표(경위도+고도)를 ECEF(Earth-Centered, Earth-Fixed) 좌표로 변환
    /// </summary>
    public static Coordinate3D GeographicToECEF(double longitude, double latitude, double altitude)
    {
        const double a = 6378137.0; // WGS84 장반경
        const double f = 1.0 / 298.257223563; // WGS84 편평률
        const double e2 = 2 * f - f * f; // 제1이심률의 제곱
        
        var lonRad = longitude * Math.PI / 180.0;
        var latRad = latitude * Math.PI / 180.0;
        
        var sinLat = Math.Sin(latRad);
        var cosLat = Math.Cos(latRad);
        var N = a / Math.Sqrt(1 - e2 * sinLat * sinLat);
        
        var x = (N + altitude) * cosLat * Math.Cos(lonRad);
        var y = (N + altitude) * cosLat * Math.Sin(lonRad);
        var z = (N * (1 - e2) + altitude) * sinLat;
        
        return new Coordinate3D(x, y, z);
    }
}