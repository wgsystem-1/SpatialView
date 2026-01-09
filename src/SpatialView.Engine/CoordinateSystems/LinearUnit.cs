namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 선형 단위 구현
/// </summary>
public class LinearUnit : ILinearUnit
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public double MetersPerUnit { get; }
    
    public LinearUnit(string name, double metersPerUnit, string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MetersPerUnit = metersPerUnit;
        Authority = authority;
        AuthorityCode = authorityCode;
    }
    
    /// <summary>
    /// 미터 단위
    /// </summary>
    public static LinearUnit Meter => new LinearUnit("meter", 1.0);
    
    /// <summary>
    /// 킬로미터 단위
    /// </summary>
    public static LinearUnit Kilometer => new LinearUnit("kilometer", 1000.0);
    
    /// <summary>
    /// 피트 단위
    /// </summary>
    public static LinearUnit Foot => new LinearUnit("foot", 0.3048);
}