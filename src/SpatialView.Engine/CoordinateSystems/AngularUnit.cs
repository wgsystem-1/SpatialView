namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 각도 단위 구현
/// </summary>
public class AngularUnit : IAngularUnit
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public double RadiansPerUnit { get; }
    
    public AngularUnit(string name, double radiansPerUnit, string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        RadiansPerUnit = radiansPerUnit;
        Authority = authority;
        AuthorityCode = authorityCode;
    }
    
    /// <summary>
    /// 도 단위
    /// </summary>
    public static AngularUnit Degrees => new AngularUnit("degree", Math.PI / 180.0);
    
    /// <summary>
    /// 라디안 단위
    /// </summary>
    public static AngularUnit Radians => new AngularUnit("radian", 1.0);
}