namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 투영 좌표계 구현
/// </summary>
public class ProjectedCoordinateSystem : IProjectedCoordinateSystem
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public string WKT { get; private set; } = string.Empty;
    public int Dimension => 2;
    IUnit IProjectedCoordinateSystem.LinearUnit => LinearUnit;
    public ILinearUnit LinearUnit { get; }
    public IGeographicCoordinateSystem BaseGeographicCoordinateSystem { get; }
    public IProjection Projection { get; }
    
    public ProjectedCoordinateSystem(string name, IGeographicCoordinateSystem geographicCS,
                                    ILinearUnit linearUnit, IProjection projection,
                                    string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        BaseGeographicCoordinateSystem = geographicCS ?? throw new ArgumentNullException(nameof(geographicCS));
        LinearUnit = linearUnit ?? throw new ArgumentNullException(nameof(linearUnit));
        Projection = projection ?? throw new ArgumentNullException(nameof(projection));
        Authority = authority;
        AuthorityCode = authorityCode;
        
        GenerateWKT();
    }
    
    private void GenerateWKT()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("PROJCS[\"").Append(Name).Append("\",");
        sb.Append(BaseGeographicCoordinateSystem.WKT).Append(",");
        sb.Append("PROJECTION[\"").Append(Projection.ProjectionClass).Append("\"],");
        
        // Add projection parameters
        foreach (var param in Projection.Parameters)
        {
            sb.Append("PARAMETER[\"").Append(param.Key).Append("\",").Append(param.Value).Append("],");
        }
        
        sb.Append("UNIT[\"").Append(LinearUnit.Name).Append("\",").Append(LinearUnit.MetersPerUnit).Append("],");
        sb.Append("AUTHORITY[\"").Append(Authority).Append("\",\"").Append(AuthorityCode).Append("\"]]");
        WKT = sb.ToString();
    }
}