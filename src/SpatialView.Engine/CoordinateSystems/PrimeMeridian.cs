namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 기준 자오선 구현
/// </summary>
public class PrimeMeridian : IPrimeMeridian
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public double Longitude { get; }
    
    public PrimeMeridian(string name, double longitude, string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Longitude = longitude;
        Authority = authority;
        AuthorityCode = authorityCode;
    }
    
    /// <summary>
    /// 그리니치 자오선
    /// </summary>
    public static PrimeMeridian Greenwich => new PrimeMeridian("Greenwich", 0, "EPSG", 8901);
    
    /// <summary>
    /// 파리 자오선
    /// </summary>
    public static PrimeMeridian Paris => new PrimeMeridian("Paris", 2.5969213, "EPSG", 8903);
}