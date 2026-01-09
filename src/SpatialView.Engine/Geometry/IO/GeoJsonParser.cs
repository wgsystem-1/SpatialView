using System.Text.Json;
using SpatialView.Engine.Data;

namespace SpatialView.Engine.Geometry.IO;

/// <summary>
/// GeoJSON 파서
/// </summary>
public static class GeoJsonParser
{
    /// <summary>
    /// GeoJSON 문자열에서 지오메트리 파싱
    /// </summary>
    public static IGeometry? ParseGeometry(string geoJson)
    {
        try
        {
            using var document = JsonDocument.Parse(geoJson);
            return ParseGeometry(document.RootElement);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GeoJSON 지오메트리 파싱 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSON 요소에서 지오메트리 파싱
    /// </summary>
    public static IGeometry? ParseGeometry(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeElement))
            return null;

        var type = typeElement.GetString();
        if (string.IsNullOrEmpty(type))
            return null;

        if (!element.TryGetProperty("coordinates", out var coordsElement))
            return null;

        return type switch
        {
            "Point" => ParsePoint(coordsElement),
            "LineString" => ParseLineString(coordsElement),
            "Polygon" => ParsePolygon(coordsElement),
            "MultiPoint" => ParseMultiPoint(coordsElement),
            "MultiLineString" => ParseMultiLineString(coordsElement),
            "MultiPolygon" => ParseMultiPolygon(coordsElement),
            "GeometryCollection" => ParseGeometryCollection(element),
            _ => null
        };
    }

    /// <summary>
    /// 지오메트리를 GeoJSON 문자열로 변환
    /// </summary>
    public static string WriteGeometry(IGeometry geometry)
    {
        var geoJson = CreateGeometryObject(geometry);
        return JsonSerializer.Serialize(geoJson, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    /// <summary>
    /// 피처를 GeoJSON 문자열로 변환
    /// </summary>
    public static string WriteFeature(IFeature feature)
    {
        var featureObj = new Dictionary<string, object?>
        {
            ["type"] = "Feature",
            ["id"] = feature.Id,
            ["geometry"] = CreateGeometryObject(feature.Geometry),
            ["properties"] = feature.Attributes.ToDictionary()
        };

        return JsonSerializer.Serialize(featureObj, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    /// <summary>
    /// 피처 컬렉션을 GeoJSON 문자열로 변환
    /// </summary>
    public static string WriteFeatureCollection(IEnumerable<IFeature> features)
    {
        var featureObjects = features.Select(f => new Dictionary<string, object?>
        {
            ["type"] = "Feature",
            ["id"] = f.Id,
            ["geometry"] = CreateGeometryObject(f.Geometry),
            ["properties"] = f.Attributes.ToDictionary()
        }).ToList();

        var collection = new Dictionary<string, object>
        {
            ["type"] = "FeatureCollection",
            ["features"] = featureObjects
        };

        return JsonSerializer.Serialize(collection, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    #region Geometry Parsing

    private static Point? ParsePoint(JsonElement coords)
    {
        var coord = ParseCoordinate(coords);
        return coord != null ? new Point(coord) : null;
    }

    private static LineString? ParseLineString(JsonElement coords)
    {
        var coordinates = ParseCoordinateArray(coords);
        return coordinates.Count >= 2 ? new LineString(coordinates) : null;
    }

    private static Polygon? ParsePolygon(JsonElement coords)
    {
        if (coords.ValueKind != JsonValueKind.Array)
            return null;

        var rings = new List<LinearRing>();
        
        foreach (var ringElement in coords.EnumerateArray())
        {
            var ringCoords = ParseCoordinateArray(ringElement);
            if (ringCoords.Count >= 4)
            {
                rings.Add(new LinearRing(ringCoords));
            }
        }

        if (rings.Count == 0)
            return null;

        var exteriorRing = rings[0];
        var interiorRings = rings.Skip(1).ToList();
        
        return new Polygon(exteriorRing, interiorRings.ToArray());
    }

    private static MultiPoint? ParseMultiPoint(JsonElement coords)
    {
        var points = new List<Point>();
        
        foreach (var pointElement in coords.EnumerateArray())
        {
            var coord = ParseCoordinate(pointElement);
            if (coord != null)
            {
                points.Add(new Point(coord));
            }
        }

        return points.Count > 0 ? new MultiPoint(points) : null;
    }

    private static MultiLineString? ParseMultiLineString(JsonElement coords)
    {
        var lineStrings = new List<LineString>();
        
        foreach (var lineElement in coords.EnumerateArray())
        {
            var lineCoords = ParseCoordinateArray(lineElement);
            if (lineCoords.Count >= 2)
            {
                lineStrings.Add(new LineString(lineCoords));
            }
        }

        return lineStrings.Count > 0 ? new MultiLineString(lineStrings) : null;
    }

    private static MultiPolygon? ParseMultiPolygon(JsonElement coords)
    {
        var polygons = new List<Polygon>();
        
        foreach (var polyElement in coords.EnumerateArray())
        {
            var polygon = ParsePolygonFromArray(polyElement);
            if (polygon != null)
            {
                polygons.Add(polygon);
            }
        }

        return polygons.Count > 0 ? new MultiPolygon(polygons) : null;
    }

    private static Polygon? ParsePolygonFromArray(JsonElement coords)
    {
        if (coords.ValueKind != JsonValueKind.Array)
            return null;

        var rings = new List<LinearRing>();
        
        foreach (var ringElement in coords.EnumerateArray())
        {
            var ringCoords = ParseCoordinateArray(ringElement);
            if (ringCoords.Count >= 4)
            {
                rings.Add(new LinearRing(ringCoords));
            }
        }

        if (rings.Count == 0)
            return null;

        var exteriorRing = rings[0];
        var interiorRings = rings.Skip(1).ToList();
        
        return new Polygon(exteriorRing, interiorRings.ToArray());
    }

    private static GeometryCollection? ParseGeometryCollection(JsonElement element)
    {
        if (!element.TryGetProperty("geometries", out var geomsElement))
            return null;

        var geometries = new List<IGeometry>();
        
        foreach (var geomElement in geomsElement.EnumerateArray())
        {
            var geometry = ParseGeometry(geomElement);
            if (geometry != null)
            {
                geometries.Add(geometry);
            }
        }

        return geometries.Count > 0 ? new GeometryCollection(geometries) : null;
    }

    private static ICoordinate? ParseCoordinate(JsonElement coords)
    {
        if (coords.ValueKind != JsonValueKind.Array)
            return null;

        var values = coords.EnumerateArray().ToList();
        if (values.Count < 2)
            return null;

        if (!values[0].TryGetDouble(out var x) || !values[1].TryGetDouble(out var y))
            return null;

        // Z 좌표는 선택적
        double z = double.NaN;
        if (values.Count > 2 && values[2].TryGetDouble(out var zValue))
        {
            z = zValue;
        }

        return new Coordinate(x, y, z);
    }

    private static List<ICoordinate> ParseCoordinateArray(JsonElement coords)
    {
        var coordinates = new List<ICoordinate>();
        
        if (coords.ValueKind != JsonValueKind.Array)
            return coordinates;

        foreach (var coordElement in coords.EnumerateArray())
        {
            var coord = ParseCoordinate(coordElement);
            if (coord != null)
            {
                coordinates.Add(coord);
            }
        }

        return coordinates;
    }

    #endregion

    #region Geometry Writing

    private static Dictionary<string, object?> CreateGeometryObject(IGeometry geometry)
    {
        return geometry switch
        {
            Point point => CreatePointObject(point),
            LineString line => CreateLineStringObject(line),
            Polygon polygon => CreatePolygonObject(polygon),
            MultiPoint multiPoint => CreateMultiPointObject(multiPoint),
            MultiLineString multiLine => CreateMultiLineStringObject(multiLine),
            MultiPolygon multiPolygon => CreateMultiPolygonObject(multiPolygon),
            GeometryCollection collection => CreateGeometryCollectionObject(collection),
            _ => throw new NotSupportedException($"Geometry type {geometry.GetType()} not supported")
        };
    }

    private static Dictionary<string, object?> CreatePointObject(Point point)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "Point",
            ["coordinates"] = CreateCoordinateArray(point.Coordinate)
        };
    }

    private static Dictionary<string, object?> CreateLineStringObject(LineString line)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "LineString",
            ["coordinates"] = line.Coordinates.Select(CreateCoordinateArray).ToList()
        };
    }

    private static Dictionary<string, object?> CreatePolygonObject(Polygon polygon)
    {
        var rings = new List<List<double[]>>
        {
            polygon.ExteriorRing.Coordinates.Select(CreateCoordinateArray).ToList()
        };

        foreach (var interiorRing in polygon.InteriorRings)
        {
            rings.Add(interiorRing.Coordinates.Select(CreateCoordinateArray).ToList());
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "Polygon",
            ["coordinates"] = rings
        };
    }

    private static Dictionary<string, object?> CreateMultiPointObject(MultiPoint multiPoint)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "MultiPoint",
            ["coordinates"] = multiPoint.Geometries
                .Cast<Point>()
                .Select(p => CreateCoordinateArray(p.Coordinate))
                .ToList()
        };
    }

    private static Dictionary<string, object?> CreateMultiLineStringObject(MultiLineString multiLine)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "MultiLineString",
            ["coordinates"] = multiLine.Geometries
                .Cast<LineString>()
                .Select(l => l.Coordinates.Select(CreateCoordinateArray).ToList())
                .ToList()
        };
    }

    private static Dictionary<string, object?> CreateMultiPolygonObject(MultiPolygon multiPolygon)
    {
        var polygons = new List<List<List<double[]>>>();

        foreach (var polygon in multiPolygon.Geometries.Cast<Polygon>())
        {
            var rings = new List<List<double[]>>
            {
                polygon.ExteriorRing.Coordinates.Select(CreateCoordinateArray).ToList()
            };

            foreach (var interiorRing in polygon.InteriorRings)
            {
                rings.Add(interiorRing.Coordinates.Select(CreateCoordinateArray).ToList());
            }

            polygons.Add(rings);
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "MultiPolygon",
            ["coordinates"] = polygons
        };
    }

    private static Dictionary<string, object?> CreateGeometryCollectionObject(GeometryCollection collection)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "GeometryCollection",
            ["geometries"] = collection.Geometries.Select(CreateGeometryObject).ToList()
        };
    }

    private static double[] CreateCoordinateArray(ICoordinate coord)
    {
        return !double.IsNaN(coord.Z) ? 
            new[] { coord.X, coord.Y, coord.Z } : 
            new[] { coord.X, coord.Y };
    }

    #endregion
}

/// <summary>
/// AttributeTable 확장 메서드
/// </summary>
public static class AttributeTableExtensions
{
    /// <summary>
    /// AttributeTable을 Dictionary로 변환
    /// </summary>
    public static Dictionary<string, object?> ToDictionary(this IAttributeTable attributes)
    {
        var dict = new Dictionary<string, object?>();
        
        foreach (var name in attributes.GetNames())
        {
            dict[name] = attributes[name];
        }
        
        return dict;
    }
}