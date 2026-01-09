namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 투영법 구현
/// </summary>
public class Projection : IProjection
{
    public string Name { get; }
    public string Authority { get; }
    public int AuthorityCode { get; }
    public IDictionary<string, double> Parameters { get; }
    public string ProjectionClass { get; }
    
    public Projection(string name, string projectionClass, IDictionary<string, double> parameters,
                     string authority = "", int authorityCode = 0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ProjectionClass = projectionClass ?? throw new ArgumentNullException(nameof(projectionClass));
        Parameters = parameters ?? new Dictionary<string, double>();
        Authority = authority;
        AuthorityCode = authorityCode;
    }
}