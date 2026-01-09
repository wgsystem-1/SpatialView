namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 기준타원체(Datum) 변환
/// 서로 다른 기준타원체 간의 좌표 변환을 수행합니다
/// </summary>
public class DatumTransformation
{
    /// <summary>
    /// double 배열을 HelmertTransformation으로 변환
    /// </summary>
    public static IHelmertTransformation CreateHelmertTransformation(double[] parameters)
    {
        if (parameters == null || parameters.Length != 7)
            throw new ArgumentException("Invalid transformation parameters");
            
        return new HelmertTransformation
        {
            DX = parameters[0],
            DY = parameters[1],
            DZ = parameters[2],
            RX = parameters[3],
            RY = parameters[4],
            RZ = parameters[5],
            Scale = parameters[6]
        };
    }
    
    /// <summary>
    /// 7-매개변수 Helmert 변환을 사용한 기준타원체 변환
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="z">Z 좌표</param>
    /// <param name="transformation">Helmert 변환 매개변수</param>
    /// <returns>변환된 3차원 좌표</returns>
    public static (double X, double Y, double Z) ApplyHelmertTransformation(
        double x, double y, double z, IHelmertTransformation transformation)
    {
        // Helmert 변환 매개변수
        var dx = transformation.DX;
        var dy = transformation.DY;
        var dz = transformation.DZ;
        var rx = transformation.RX * Math.PI / (180.0 * 3600.0); // 초를 라디안으로
        var ry = transformation.RY * Math.PI / (180.0 * 3600.0);
        var rz = transformation.RZ * Math.PI / (180.0 * 3600.0);
        var s = transformation.Scale * 1e-6; // ppm을 비율로
        
        // Helmert 변환 공식
        var xNew = dx + x * (1 + s) - y * rz + z * ry;
        var yNew = dy + x * rz + y * (1 + s) - z * rx;
        var zNew = dz - x * ry + y * rx + z * (1 + s);
        
        return (xNew, yNew, zNew);
    }
    
    /// <summary>
    /// 지리 좌표를 지심 직교 좌표로 변환
    /// </summary>
    /// <param name="longitude">경도 (도)</param>
    /// <param name="latitude">위도 (도)</param>
    /// <param name="height">높이 (미터)</param>
    /// <param name="ellipsoid">타원체</param>
    /// <returns>지심 직교 좌표 (X, Y, Z)</returns>
    public static (double X, double Y, double Z) GeographicToGeocentric(
        double longitude, double latitude, double height, IEllipsoid ellipsoid)
    {
        var lonRad = longitude * Math.PI / 180.0;
        var latRad = latitude * Math.PI / 180.0;
        
        var a = ellipsoid.SemiMajorAxis;
        var e2 = ellipsoid.EccentricitySquared;
        
        var n = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
        
        var x = (n + height) * Math.Cos(latRad) * Math.Cos(lonRad);
        var y = (n + height) * Math.Cos(latRad) * Math.Sin(lonRad);
        var z = (n * (1 - e2) + height) * Math.Sin(latRad);
        
        return (x, y, z);
    }
    
    /// <summary>
    /// 지심 직교 좌표를 지리 좌표로 변환
    /// </summary>
    /// <param name="x">X 좌표</param>
    /// <param name="y">Y 좌표</param>
    /// <param name="z">Z 좌표</param>
    /// <param name="ellipsoid">타원체</param>
    /// <returns>지리 좌표 (경도, 위도, 높이)</returns>
    public static (double Longitude, double Latitude, double Height) GeocentricToGeographic(
        double x, double y, double z, IEllipsoid ellipsoid)
    {
        var a = ellipsoid.SemiMajorAxis;
        var b = ellipsoid.SemiMinorAxis;
        var e2 = ellipsoid.EccentricitySquared;
        
        var ep2 = e2 / (1 - e2);
        var r2 = x * x + y * y;
        var r = Math.Sqrt(r2);
        var e2b = e2 * b;
        var f = 54 * b * b * z * z;
        var g = r2 + (1 - e2) * z * z - e2 * (a * a - b * b);
        var c = (e2 * e2 * f * r2) / (g * g * g);
        var s = Math.Pow(1 + c + Math.Sqrt(c * c + 2 * c), 1.0 / 3.0);
        var p = f / (3 * (s + 1.0 / s + 1) * (s + 1.0 / s + 1) * g * g);
        var q = Math.Sqrt(1 + 2 * e2 * e2 * p);
        var r0 = -(p * e2 * r) / (1 + q) + Math.Sqrt(0.5 * a * a * (1 + 1.0 / q) -
                  p * (1 - e2) * z * z / (q * (1 + q)) - 0.5 * p * r2);
        var u = Math.Sqrt((r - e2 * r0) * (r - e2 * r0) + z * z);
        var v = Math.Sqrt((r - e2 * r0) * (r - e2 * r0) + (1 - e2) * z * z);
        var z0 = b * b * z / (a * v);
        
        var height = u * (1 - b * b / (a * v));
        var latitude = Math.Atan((z + ep2 * z0) / r) * 180.0 / Math.PI;
        var longitude = Math.Atan2(y, x) * 180.0 / Math.PI;
        
        return (longitude, latitude, height);
    }
}

/// <summary>
/// 기준타원체 변환 팩토리
/// 다양한 기준타원체 간 변환을 생성하고 관리합니다
/// </summary>
public class DatumTransformationFactory
{
    private readonly Dictionary<string, WellKnownDatum> _wellKnownDatums;
    
    /// <summary>
    /// 생성자
    /// </summary>
    public DatumTransformationFactory()
    {
        _wellKnownDatums = new Dictionary<string, WellKnownDatum>();
        InitializeWellKnownDatums();
    }
    
    /// <summary>
    /// 기준타원체 변환 생성
    /// </summary>
    /// <param name="sourceDatum">소스 기준타원체</param>
    /// <param name="targetDatum">대상 기준타원체</param>
    /// <returns>기준타원체 변환</returns>
    public IDatumTransformation CreateTransformation(IDatum sourceDatum, IDatum targetDatum)
    {
        if (sourceDatum == null)
            throw new ArgumentNullException(nameof(sourceDatum));
        
        if (targetDatum == null)
            throw new ArgumentNullException(nameof(targetDatum));
        
        // 동일한 기준타원체인 경우 항등 변환
        if (string.Equals(sourceDatum.Name, targetDatum.Name, StringComparison.OrdinalIgnoreCase))
        {
            return new IdentityDatumTransformation(sourceDatum, targetDatum);
        }
        
        // WGS84를 중간 기준타원체로 사용하는 2단계 변환
        if (sourceDatum.ToWGS84 != null && targetDatum.ToWGS84 != null)
        {
            return new TwoStepDatumTransformation(sourceDatum, targetDatum);
        }
        
        // 직접 변환이 가능한 경우
        if (sourceDatum.ToWGS84 != null || targetDatum.ToWGS84 != null)
        {
            return new DirectDatumTransformation(sourceDatum, targetDatum);
        }
        
        throw new NotSupportedException($"Cannot create transformation between {sourceDatum.Name} and {targetDatum.Name}");
    }
    
    /// <summary>
    /// 잘 알려진 기준타원체들 초기화
    /// </summary>
    private void InitializeWellKnownDatums()
    {
        // WGS84
        _wellKnownDatums["WGS84"] = new WellKnownDatum
        {
            Name = "WGS84",
            Ellipsoid = WellKnownEllipsoids.WGS84,
            HelmertTransformation = null // WGS84 자체이므로 변환 매개변수 없음
        };
        
        // GRS80 (Korea 2000 등에서 사용)
        _wellKnownDatums["Korea2000"] = new WellKnownDatum
        {
            Name = "Korea 2000",
            Ellipsoid = WellKnownEllipsoids.GRS80,
            HelmertTransformation = new HelmertTransformation
            {
                DX = 0.0, DY = 0.0, DZ = 0.0, // Korea 2000은 WGS84와 거의 동일
                RX = 0.0, RY = 0.0, RZ = 0.0,
                Scale = 0.0
            }
        };
        
        // Tokyo Datum (일본 측지계)
        _wellKnownDatums["Tokyo"] = new WellKnownDatum
        {
            Name = "Tokyo",
            Ellipsoid = WellKnownEllipsoids.Bessel1841,
            HelmertTransformation = new HelmertTransformation
            {
                DX = -146.414, DY = 507.337, DZ = 680.507,
                RX = -1.1622, RY = -2.9344, RZ = -1.4555,
                Scale = -8.15
            }
        };
        
        // NAD83 (북미)
        _wellKnownDatums["NAD83"] = new WellKnownDatum
        {
            Name = "North American Datum 1983",
            Ellipsoid = WellKnownEllipsoids.GRS80,
            HelmertTransformation = new HelmertTransformation
            {
                DX = 0.0, DY = 0.0, DZ = 0.0, // NAD83은 WGS84와 거의 동일
                RX = 0.0, RY = 0.0, RZ = 0.0,
                Scale = 0.0
            }
        };
        
        // NAD27 (구 북미 측지계)
        _wellKnownDatums["NAD27"] = new WellKnownDatum
        {
            Name = "North American Datum 1927",
            Ellipsoid = WellKnownEllipsoids.Clarke1866,
            HelmertTransformation = new HelmertTransformation
            {
                DX = -8.0, DY = 160.0, DZ = 176.0,
                RX = 0.0, RY = 0.0, RZ = 0.0,
                Scale = 0.0
            }
        };
        
        // European Datum 1950
        _wellKnownDatums["ED50"] = new WellKnownDatum
        {
            Name = "European Datum 1950",
            Ellipsoid = WellKnownEllipsoids.International1924,
            HelmertTransformation = new HelmertTransformation
            {
                DX = -87.0, DY = -98.0, DZ = -121.0,
                RX = 0.0, RY = 0.0, RZ = 0.0,
                Scale = 0.0
            }
        };
    }
    
    /// <summary>
    /// 이름으로 기준타원체 조회
    /// </summary>
    /// <param name="datumName">기준타원체 이름</param>
    /// <returns>기준타원체 또는 null</returns>
    public WellKnownDatum? GetWellKnownDatum(string datumName)
    {
        return _wellKnownDatums.TryGetValue(datumName, out var datum) ? datum : null;
    }
    
    /// <summary>
    /// 모든 잘 알려진 기준타원체 목록
    /// </summary>
    /// <returns>기준타원체 목록</returns>
    public IEnumerable<WellKnownDatum> GetAllWellKnownDatums()
    {
        return _wellKnownDatums.Values;
    }
}

/// <summary>
/// 기준타원체 변환 인터페이스
/// </summary>
public interface IDatumTransformation
{
    /// <summary>
    /// 소스 기준타원체
    /// </summary>
    IDatum SourceDatum { get; }
    
    /// <summary>
    /// 대상 기준타원체
    /// </summary>
    IDatum TargetDatum { get; }
    
    /// <summary>
    /// 변환 정확도 (미터)
    /// </summary>
    double Accuracy { get; }
    
    /// <summary>
    /// 지리 좌표 변환
    /// </summary>
    /// <param name="longitude">경도</param>
    /// <param name="latitude">위도</param>
    /// <param name="height">높이 (기본값 0)</param>
    /// <returns>변환된 지리 좌표</returns>
    (double Longitude, double Latitude, double Height) Transform(double longitude, double latitude, double height = 0.0);
}

/// <summary>
/// 항등 기준타원체 변환 (동일한 기준타원체)
/// </summary>
public class IdentityDatumTransformation : IDatumTransformation
{
    public IDatum SourceDatum { get; }
    public IDatum TargetDatum { get; }
    public double Accuracy => 0.0;
    
    public IdentityDatumTransformation(IDatum sourceDatum, IDatum targetDatum)
    {
        SourceDatum = sourceDatum;
        TargetDatum = targetDatum;
    }
    
    public (double Longitude, double Latitude, double Height) Transform(double longitude, double latitude, double height = 0.0)
    {
        return (longitude, latitude, height);
    }
}

/// <summary>
/// 직접 기준타원체 변환 (한 단계 변환)
/// </summary>
public class DirectDatumTransformation : IDatumTransformation
{
    public IDatum SourceDatum { get; }
    public IDatum TargetDatum { get; }
    public double Accuracy => 1.0; // 1미터 정확도
    
    public DirectDatumTransformation(IDatum sourceDatum, IDatum targetDatum)
    {
        SourceDatum = sourceDatum;
        TargetDatum = targetDatum;
    }
    
    public (double Longitude, double Latitude, double Height) Transform(double longitude, double latitude, double height = 0.0)
    {
        // WGS84로의 변환이 정의된 경우
        if (SourceDatum.ToWGS84 != null)
        {
            // 소스 -> 지심 직교 좌표
            var (x, y, z) = DatumTransformation.GeographicToGeocentric(longitude, latitude, height, SourceDatum.Ellipsoid);
            
            // Helmert 변환 적용
            var transform = DatumTransformation.CreateHelmertTransformation(SourceDatum.ToWGS84);
            var (xNew, yNew, zNew) = DatumTransformation.ApplyHelmertTransformation(x, y, z, transform);
            
            // 지심 직교 좌표 -> 대상 지리 좌표
            return DatumTransformation.GeocentricToGeographic(xNew, yNew, zNew, TargetDatum.Ellipsoid);
        }
        
        // 대상에서 WGS84로의 변환이 정의된 경우 (역변환)
        if (TargetDatum.ToWGS84 != null)
        {
            // 대상 -> 지심 직교 좌표
            var (x, y, z) = DatumTransformation.GeographicToGeocentric(longitude, latitude, height, SourceDatum.Ellipsoid);
            
            // 역 Helmert 변환 적용 (매개변수 부호 반전)
            var toWgs84 = TargetDatum.ToWGS84;
            var reverseTransform = new HelmertTransformation
            {
                DX = -toWgs84[0],  // dx
                DY = -toWgs84[1],  // dy
                DZ = -toWgs84[2],  // dz
                RX = -toWgs84[3],  // rx
                RY = -toWgs84[4],  // ry
                RZ = -toWgs84[5],  // rz
                Scale = -toWgs84[6]  // scale
            };
            
            var (xNew, yNew, zNew) = DatumTransformation.ApplyHelmertTransformation(x, y, z, reverseTransform);
            
            // 지심 직교 좌표 -> 대상 지리 좌표
            return DatumTransformation.GeocentricToGeographic(xNew, yNew, zNew, TargetDatum.Ellipsoid);
        }
        
        throw new InvalidOperationException("No transformation parameters available");
    }
}

/// <summary>
/// 2단계 기준타원체 변환 (WGS84를 경유하는 변환)
/// </summary>
public class TwoStepDatumTransformation : IDatumTransformation
{
    public IDatum SourceDatum { get; }
    public IDatum TargetDatum { get; }
    public double Accuracy => 2.0; // 2미터 정확도 (2단계 변환으로 인한 오차 누적)
    
    public TwoStepDatumTransformation(IDatum sourceDatum, IDatum targetDatum)
    {
        SourceDatum = sourceDatum;
        TargetDatum = targetDatum;
    }
    
    public (double Longitude, double Latitude, double Height) Transform(double longitude, double latitude, double height = 0.0)
    {
        if (SourceDatum.ToWGS84 == null || TargetDatum.ToWGS84 == null)
            throw new InvalidOperationException("Both datums must have WGS84 transformation parameters for two-step transformation");
        
        // 1단계: 소스 -> WGS84
        var (x1, y1, z1) = DatumTransformation.GeographicToGeocentric(longitude, latitude, height, SourceDatum.Ellipsoid);
        var sourceTransform = DatumTransformation.CreateHelmertTransformation(SourceDatum.ToWGS84);
        var (x2, y2, z2) = DatumTransformation.ApplyHelmertTransformation(x1, y1, z1, sourceTransform);
        
        // 2단계: WGS84 -> 대상 (역변환)
        var toWgs84 = TargetDatum.ToWGS84;
        var reverseTransform = new HelmertTransformation
        {
            DX = -toWgs84[0],  // dx
            DY = -toWgs84[1],  // dy
            DZ = -toWgs84[2],  // dz
            RX = -toWgs84[3],  // rx
            RY = -toWgs84[4],  // ry
            RZ = -toWgs84[5],  // rz
            Scale = -toWgs84[6]  // scale
        };
        
        var (x3, y3, z3) = DatumTransformation.ApplyHelmertTransformation(x2, y2, z2, reverseTransform);
        
        // 지심 직교 좌표 -> 대상 지리 좌표
        return DatumTransformation.GeocentricToGeographic(x3, y3, z3, TargetDatum.Ellipsoid);
    }
}

/// <summary>
/// 잘 알려진 기준타원체 정의
/// </summary>
public class WellKnownDatum : IDatum
{
    public string Name { get; set; } = string.Empty;
    public string Authority { get; set; } = "EPSG";
    public int AuthorityCode { get; set; }
    public IEllipsoid Ellipsoid { get; set; } = null!;
    public IPrimeMeridian PrimeMeridian { get; set; } = null!;
    public DatumType Type { get; set; } = DatumType.HD_Horizontal;
    public IHelmertTransformation? HelmertTransformation { get; set; }
    
    /// <inheritdoc/>
    public double[] ToWGS84 
    { 
        get 
        {
            if (HelmertTransformation == null)
                return new double[7]; // 널 변환 (모든 값 0)
            
            // 7-parameter transformation: [dx, dy, dz, rx, ry, rz, scale]
            return new double[] 
            {
                HelmertTransformation.DeltaX,
                HelmertTransformation.DeltaY,
                HelmertTransformation.DeltaZ,
                HelmertTransformation.RotationX,
                HelmertTransformation.RotationY,
                HelmertTransformation.RotationZ,
                HelmertTransformation.ScaleFactor
            };
        }
    }
}

/// <summary>
/// Helmert 변환 매개변수 구현
/// </summary>
public class HelmertTransformation : IHelmertTransformation
{
    public double DeltaX { get; set; }
    public double DeltaY { get; set; }
    public double DeltaZ { get; set; }
    public double RotationX { get; set; }
    public double RotationY { get; set; }
    public double RotationZ { get; set; }
    public double ScaleFactor { get; set; }
    public bool IsReversible { get; set; } = true;
    
    public (double x, double y, double z) Transform(double x, double y, double z)
    {
        // Simplified 7-parameter Helmert transformation
        var newX = x + DeltaX + (ScaleFactor * x) + (RotationZ * y) - (RotationY * z);
        var newY = y + DeltaY - (RotationZ * x) + (ScaleFactor * y) + (RotationX * z);
        var newZ = z + DeltaZ + (RotationY * x) - (RotationX * y) + (ScaleFactor * z);
        return (newX, newY, newZ);
    }
    
    public (double x, double y, double z) InverseTransform(double x, double y, double z)
    {
        if (!IsReversible)
            throw new InvalidOperationException("Transformation is not reversible");
            
        // Simplified inverse transformation
        var newX = x - DeltaX - (ScaleFactor * x) - (RotationZ * y) + (RotationY * z);
        var newY = y - DeltaY + (RotationZ * x) - (ScaleFactor * y) - (RotationX * z);
        var newZ = z - DeltaZ - (RotationY * x) + (RotationX * y) - (ScaleFactor * z);
        return (newX, newY, newZ);
    }
    
    // Legacy properties for backward compatibility
    public double DX { get => DeltaX; set => DeltaX = value; }
    public double DY { get => DeltaY; set => DeltaY = value; }
    public double DZ { get => DeltaZ; set => DeltaZ = value; }
    public double RX { get => RotationX; set => RotationX = value; }
    public double RY { get => RotationY; set => RotationY = value; }
    public double RZ { get => RotationZ; set => RotationZ = value; }
    public double Scale { get => ScaleFactor; set => ScaleFactor = value; }
}

/// <summary>
/// 잘 알려진 타원체 정의
/// </summary>
public static class WellKnownEllipsoids
{
    public static readonly IEllipsoid WGS84 = new Ellipsoid("WGS 84", 6378137.0, 298.257223563);
    public static readonly IEllipsoid GRS80 = new Ellipsoid("GRS 1980", 6378137.0, 298.257222101);
    public static readonly IEllipsoid Clarke1866 = new Ellipsoid("Clarke 1866", 6378206.4, 294.978698214);
    public static readonly IEllipsoid Bessel1841 = new Ellipsoid("Bessel 1841", 6377397.155, 299.1528128);
    public static readonly IEllipsoid International1924 = new Ellipsoid("International 1924", 6378388.0, 297.0);
    public static readonly IEllipsoid Airy1830 = new Ellipsoid("Airy 1830", 6377563.396, 299.3249646);
}