namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 지리 좌표계 구현
/// </summary>
public class GeographicCoordinateSystem : IGeographicCoordinateSystem
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public string WKT { get; private set; } = string.Empty;
    public int Dimension => 2;
    IUnit IGeographicCoordinateSystem.AngularUnit => AngularUnit;
    public IAngularUnit AngularUnit { get; }
    public IEllipsoid Ellipsoid { get; }
    public IPrimeMeridian PrimeMeridian { get; }
    
    public GeographicCoordinateSystem(string name, IAngularUnit angularUnit, IEllipsoid ellipsoid,
                                     IPrimeMeridian primeMeridian, string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AngularUnit = angularUnit ?? throw new ArgumentNullException(nameof(angularUnit));
        Ellipsoid = ellipsoid ?? throw new ArgumentNullException(nameof(ellipsoid));
        PrimeMeridian = primeMeridian ?? throw new ArgumentNullException(nameof(primeMeridian));
        Authority = authority;
        AuthorityCode = authorityCode;
        
        GenerateWKT();
    }
    
    private void GenerateWKT()
    {
        // Use string concatenation to avoid quote escaping issues
        var sb = new System.Text.StringBuilder();
        sb.Append("GEOGCS[\"").Append(Name).Append("\",");
        sb.Append("DATUM[\"WGS_1984\",");
        sb.Append("SPHEROID[\"").Append(Ellipsoid.Name).Append("\",");
        sb.Append(Ellipsoid.SemiMajorAxis).Append(",").Append(Ellipsoid.InverseFlattening).Append(",");
        sb.Append("AUTHORITY[\"").Append(Ellipsoid.Authority).Append("\",\"").Append(Ellipsoid.AuthorityCode).Append("\"]]],");
        sb.Append("PRIMEM[\"").Append(PrimeMeridian.Name).Append("\",").Append(PrimeMeridian.Longitude).Append(",");
        sb.Append("AUTHORITY[\"").Append(PrimeMeridian.Authority).Append("\",\"").Append(PrimeMeridian.AuthorityCode).Append("\"]],");
        sb.Append("UNIT[\"").Append(AngularUnit.Name).Append("\",").Append(AngularUnit.RadiansPerUnit).Append("],");
        sb.Append("AUTHORITY[\"").Append(Authority).Append("\",\"").Append(AuthorityCode).Append("\"]]");
        WKT = sb.ToString();
    }
}