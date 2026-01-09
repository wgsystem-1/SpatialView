namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 잘 알려진 좌표계 정의
/// </summary>
public static class WellKnownCoordinateSystems
{
    /// <summary>
    /// WGS 84 지리 좌표계 (EPSG:4326)
    /// </summary>
    public static IGeographicCoordinateSystem WGS84 { get; } = new GeographicCoordinateSystem(
        "WGS 84",
        new AngularUnit("degree", 0.0174532925199433),
        new Ellipsoid("WGS 84", 6378137, 298.257223563),
        new PrimeMeridian("Greenwich", 0),
        "EPSG",
        4326
    );
    
    /// <summary>
    /// Web Mercator 투영 좌표계 (EPSG:3857)
    /// </summary>
    public static IProjectedCoordinateSystem WebMercator { get; } = new ProjectedCoordinateSystem(
        "WGS 84 / Pseudo-Mercator",
        WGS84,
        new LinearUnit("meter", 1.0),
        new Projection("Popular Visualisation Pseudo Mercator", "Mercator_1SP", new Dictionary<string, double>
        {
            { "False_Easting", 0 },
            { "False_Northing", 0 },
            { "Central_Meridian", 0 },
            { "Scale_Factor", 1.0 },
            { "Latitude_Of_Origin", 0 }
        }),
        "EPSG",
        3857
    );
    
    /// <summary>
    /// Korea 2000 / Middle Belt (EPSG:5186)
    /// </summary>
    public static IProjectedCoordinateSystem KoreaMiddleBelt { get; } = new ProjectedCoordinateSystem(
        "Korea 2000 / Middle Belt",
        new GeographicCoordinateSystem(
            "Korea 2000",
            new AngularUnit("degree", 0.0174532925199433),
            new Ellipsoid("GRS 1980", 6378137, 298.257222101),
            new PrimeMeridian("Greenwich", 0),
            "EPSG",
            4737
        ),
        new LinearUnit("meter", 1.0),
        new Projection("Korea Middle Belt", "Transverse_Mercator", new Dictionary<string, double>
        {
            { "False_Easting", 200000 },
            { "False_Northing", 500000 },
            { "Central_Meridian", 127.0 },
            { "Scale_Factor", 1.0 },
            { "Latitude_Of_Origin", 38.0 }
        }),
        "EPSG",
        5186
    );
}