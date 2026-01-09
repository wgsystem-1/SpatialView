using SpatialView.Engine.Geometry.IO;
using SpatialView.Engine.Geometry;

var wkt = ""MULTIPOLYGON (((882127.6413 1482791.4594,882160.6403 1482769.9733,882174.3847 1482775.4999,882127.6413 1482791.4594)))"";
Console.WriteLine(""Parsing WKT..."");
var geom = WktParser.Parse(wkt);
Console.WriteLine($""GeomType: {geom.GetType().Name}"");
Console.WriteLine($""Envelope: {geom.Envelope}"");
if (geom is MultiPolygon mp)
{
    Console.WriteLine($""NumGeometries: {mp.NumGeometries}"");
    foreach (var poly in mp.Geometries)
    {
        Console.WriteLine($""  Polygon.IsEmpty: {poly.IsEmpty}"");
        Console.WriteLine($""  Polygon.Envelope: {poly.Envelope}"");
        Console.WriteLine($""  ExteriorRing.NumPoints: {poly.ExteriorRing?.NumPoints}"");
        Console.WriteLine($""  ExteriorRing.IsEmpty: {poly.ExteriorRing?.IsEmpty}"");
    }
}
