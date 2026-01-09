namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 타원체 구현
/// </summary>
public class Ellipsoid : IEllipsoid
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public double SemiMajorAxis { get; }
    public double SemiMinorAxis { get; }
    public double Flattening { get; }
    public double InverseFlattening { get; }
    public double Eccentricity { get; }
    public double EccentricitySquared => Eccentricity * Eccentricity;
    
    /// <summary>
    /// 장반경과 역편평률로 생성
    /// </summary>
    public Ellipsoid(string name, double semiMajorAxis, double inverseFlattening, 
                     string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SemiMajorAxis = semiMajorAxis;
        InverseFlattening = inverseFlattening;
        Flattening = 1.0 / inverseFlattening;
        SemiMinorAxis = semiMajorAxis * (1.0 - Flattening);
        Eccentricity = Math.Sqrt(2 * Flattening - Flattening * Flattening);
        Authority = authority;
        AuthorityCode = authorityCode;
    }
    
    /// <summary>
    /// 장반경과 단반경으로 생성
    /// </summary>
    public static Ellipsoid FromRadii(string name, double semiMajorAxis, double semiMinorAxis,
                                      string authority = "", int authorityCode = 0)
    {
        var flattening = (semiMajorAxis - semiMinorAxis) / semiMajorAxis;
        var inverseFlattening = 1.0 / flattening;
        return new Ellipsoid(name, semiMajorAxis, inverseFlattening, authority, authorityCode);
    }
    
    /// <summary>
    /// WGS 84 타원체
    /// </summary>
    public static Ellipsoid WGS84 => new Ellipsoid("WGS 84", 6378137, 298.257223563, "EPSG", 7030);
    
    /// <summary>
    /// GRS 1980 타원체
    /// </summary>
    public static Ellipsoid GRS80 => new Ellipsoid("GRS 1980", 6378137, 298.257222101, "EPSG", 7019);
}