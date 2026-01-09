namespace SpatialView.Engine.CoordinateSystems.Transformation;

/// <summary>
/// 항등 변환 (같은 좌표계)
/// </summary>
public class IdentityTransformation : ICoordinateTransformation
{
    public ICoordinateSystem SourceCoordinateSystem { get; }
    public ICoordinateSystem TargetCoordinateSystem { get; }
    
    public IdentityTransformation(ICoordinateSystem coordinateSystem)
    {
        SourceCoordinateSystem = coordinateSystem ?? throw new ArgumentNullException(nameof(coordinateSystem));
        TargetCoordinateSystem = coordinateSystem;
    }
    
    public Geometry.ICoordinate Transform(Geometry.ICoordinate sourceCoordinate)
    {
        return sourceCoordinate.Copy();
    }
    
    public Geometry.ICoordinate[] Transform(Geometry.ICoordinate[] sourceCoordinates)
    {
        return sourceCoordinates.Select(c => c.Copy()).ToArray();
    }
    
    public Geometry.IGeometry Transform(Geometry.IGeometry geometry)
    {
        var copy = geometry.Copy();
        copy.SRID = TargetCoordinateSystem.AuthorityCode;
        return copy;
    }
}