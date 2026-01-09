using System.Globalization;
using System.Text.RegularExpressions;

namespace SpatialView.Engine.Geometry.IO;

/// <summary>
/// Well-Known Text (WKT) 형식의 지오메트리 파서
/// OGC Simple Features 스펙을 따른 WKT 파싱 지원
/// </summary>
public static class WktParser
{
    private static readonly Regex WktRegex = new(
        @"^(\w+)\s*(?:\((.+)\))?\s*$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpatialView_render.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    /// <summary>
    /// WKT 문자열을 지오메트리로 파싱
    /// </summary>
    /// <param name="wkt">WKT 문자열</param>
    /// <returns>파싱된 지오메트리</returns>
    /// <exception cref="ArgumentException">잘못된 WKT 형식인 경우</exception>
    public static IGeometry Parse(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            return Point.Empty;

        try
        {
            wkt = wkt.Trim();
            var match = WktRegex.Match(wkt);
            
            Log($"WktParser.Parse: wkt(first 100)={wkt.Substring(0, Math.Min(100, wkt.Length))}, match.Success={match.Success}");
            
            if (!match.Success)
            {
                Log($"WktParser.Parse: Invalid WKT format - {wkt}");
                return Point.Empty;
            }

            var geometryType = match.Groups[1].Value.ToUpperInvariant();
            var coordinates = match.Groups[2].Value;
            
            if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            {
                return geometryType switch
                {
                    "POINT" => Point.Empty,
                    "LINESTRING" => LineString.Empty,
                    "POLYGON" => Polygon.Empty,
                    "MULTIPOINT" => MultiPoint.Empty,
                    "MULTILINESTRING" => MultiLineString.Empty,
                    "MULTIPOLYGON" => MultiPolygon.Empty,
                    "GEOMETRYCOLLECTION" => GeometryCollection.Empty,
                    _ => Point.Empty
                };
            }

            Log($"WktParser.Parse: geometryType={geometryType}, coordinates(first 100)={coordinates.Substring(0, Math.Min(100, coordinates.Length))}");

            return geometryType switch
            {
                "POINT" => ParsePoint(coordinates),
                "LINESTRING" => ParseLineString(coordinates),
                "POLYGON" => ParsePolygon(coordinates),
                "MULTIPOINT" => ParseMultiPoint(coordinates),
                "MULTILINESTRING" => ParseMultiLineString(coordinates),
                "MULTIPOLYGON" => ParseMultiPolygon(coordinates),
                "GEOMETRYCOLLECTION" => ParseGeometryCollection(coordinates),
                _ => Point.Empty
            };
        }
        catch (Exception ex)
        {
            Log($"WktParser.Parse ERROR: {ex.Message} for WKT: {wkt}");
            return Point.Empty;
        }
    }

    /// <summary>
    /// 지오메트리를 WKT 문자열로 변환
    /// </summary>
    public static string Write(IGeometry geometry)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        return geometry switch
        {
            Point point => WritePoint(point),
            LineString lineString => WriteLineString(lineString),
            Polygon polygon => WritePolygon(polygon),
            MultiPoint multiPoint => WriteMultiPoint(multiPoint),
            MultiLineString multiLineString => WriteMultiLineString(multiLineString),
            MultiPolygon multiPolygon => WriteMultiPolygon(multiPolygon),
            GeometryCollection collection => WriteGeometryCollection(collection),
            _ => throw new ArgumentException($"Unsupported geometry type: {geometry.GetType().Name}", nameof(geometry))
        };
    }

    #region Point Parsing

    private static Point ParsePoint(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return Point.Empty;

        var coords = ParseCoordinateSequence(coordinates);
        if (coords.Count == 0)
            return Point.Empty;

        var coord = coords[0];
        return !double.IsNaN(coord.Z) ? new Point(coord.X, coord.Y, coord.Z) : new Point(coord.X, coord.Y);
    }

    private static string WritePoint(Point point)
    {
        if (point.IsEmpty)
            return "POINT EMPTY";

        var coord = point.Coordinate;
        var coordStr = !double.IsNaN(coord.Z) 
            ? $"{coord.X.ToString(CultureInfo.InvariantCulture)} {coord.Y.ToString(CultureInfo.InvariantCulture)} {coord.Z.ToString(CultureInfo.InvariantCulture)}"
            : $"{coord.X.ToString(CultureInfo.InvariantCulture)} {coord.Y.ToString(CultureInfo.InvariantCulture)}";
            
        return $"POINT ({coordStr})";
    }

    #endregion

    #region LineString Parsing

    private static LineString ParseLineString(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return LineString.Empty;

        var coords = ParseCoordinateSequence(coordinates);
        return new LineString(coords);
    }

    private static string WriteLineString(LineString lineString)
    {
        if (lineString.IsEmpty)
            return "LINESTRING EMPTY";

        var coordsStr = string.Join(", ", lineString.Coordinates.Select(c =>
            !double.IsNaN(c.Z)
                ? $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)} {c.Z.ToString(CultureInfo.InvariantCulture)}"
                : $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}"));

        return $"LINESTRING ({coordsStr})";
    }

    #endregion

    #region Polygon Parsing

    private static Polygon ParsePolygon(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return Polygon.Empty;

        var rings = ParseRings(coordinates);
        if (rings.Count == 0)
            return Polygon.Empty;

        // First ring is exterior, others are holes
        var exterior = new LinearRing(rings[0]);
        var holes = rings.Skip(1).Select(ring => new LinearRing(ring)).ToArray();

        return new Polygon(exterior, holes);
    }

    private static string WritePolygon(Polygon polygon)
    {
        if (polygon.IsEmpty)
            return "POLYGON EMPTY";

        var rings = new List<string>();
        
        // Exterior ring
        var exteriorCoords = string.Join(", ", polygon.ExteriorRing.Coordinates.Select(c =>
            !double.IsNaN(c.Z)
                ? $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)} {c.Z.ToString(CultureInfo.InvariantCulture)}"
                : $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}"));
        rings.Add($"({exteriorCoords})");

        // Interior rings (holes)
        foreach (var hole in polygon.InteriorRings)
        {
            var holeCoords = string.Join(", ", hole.Coordinates.Select(c =>
                !double.IsNaN(c.Z)
                    ? $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)} {c.Z.ToString(CultureInfo.InvariantCulture)}"
                    : $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}"));
            rings.Add($"({holeCoords})");
        }

        return $"POLYGON ({string.Join(", ", rings)})";
    }

    #endregion

    #region Multi* Parsing

    private static MultiPoint ParseMultiPoint(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return MultiPoint.Empty;

        var points = new List<Point>();
        var pointStrings = ParseGeometryParts(coordinates);

        foreach (var pointStr in pointStrings)
        {
            // Remove optional parentheses around individual points
            var cleanPointStr = pointStr.Trim().Trim('(', ')');
            var coord = ParseCoordinateSequence(cleanPointStr).FirstOrDefault();
            if (coord != null)
            {
                points.Add(!double.IsNaN(coord.Z) 
                    ? new Point(coord.X, coord.Y, coord.Z) 
                    : new Point(coord.X, coord.Y));
            }
        }

        return new MultiPoint(points);
    }

    private static MultiLineString ParseMultiLineString(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return MultiLineString.Empty;

        var lineStrings = new List<LineString>();
        var lineStringParts = ParseGeometryParts(coordinates);

        foreach (var lineStr in lineStringParts)
        {
            var coords = ParseCoordinateSequence(lineStr.Trim('(', ')'));
            lineStrings.Add(new LineString(coords));
        }

        return new MultiLineString(lineStrings);
    }

    private static MultiPolygon ParseMultiPolygon(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return MultiPolygon.Empty;

        var polygons = new List<Polygon>();
        var polygonParts = ParsePolygonParts(coordinates);
        Log($"ParseMultiPolygon: polygonParts.Count={polygonParts.Count}");

        foreach (var polygonStr in polygonParts)
        {
            Log($"ParseMultiPolygon: polygonStr(first 100)={polygonStr.Substring(0, Math.Min(100, polygonStr.Length))}");
            var rings = ParseRings(polygonStr);
            Log($"ParseMultiPolygon: rings.Count={rings.Count}");
            
            if (rings.Count > 0)
            {
                var exterior = rings[0];
                Log($"ParseMultiPolygon: exterior coords count={exterior.Count}");
                var holes = rings.Skip(1).ToList();
                var exteriorRing = new LinearRing(exterior);
                Log($"ParseMultiPolygon: exteriorRing.NumPoints={exteriorRing.NumPoints}, IsEmpty={exteriorRing.IsEmpty}");
                var holeRings = holes.Select(h => new LinearRing(h)).ToArray();
                var polygon = new Polygon(exteriorRing, holeRings);
                Log($"ParseMultiPolygon: polygon.IsEmpty={polygon.IsEmpty}, Envelope={polygon.Envelope}");
                polygons.Add(polygon);
            }
        }

        var result = new MultiPolygon(polygons);
        Log($"ParseMultiPolygon: result.NumGeometries={result.NumGeometries}, Envelope={result.Envelope}");
        return result;
    }

    private static GeometryCollection ParseGeometryCollection(string coordinates)
    {
        if (string.IsNullOrWhiteSpace(coordinates) || coordinates.ToUpperInvariant() == "EMPTY")
            return GeometryCollection.Empty;

        var geometries = new List<IGeometry>();
        var geometryStrings = ParseGeometryCollectionParts(coordinates);

        foreach (var geomStr in geometryStrings)
        {
            geometries.Add(Parse(geomStr));
        }

        return new GeometryCollection(geometries);
    }

    private static string WriteMultiPoint(MultiPoint multiPoint)
    {
        if (multiPoint.IsEmpty)
            return "MULTIPOINT EMPTY";

        var pointStrs = multiPoint.Geometries.Cast<Point>().Select(p =>
        {
            var coord = p.Coordinate;
            return !double.IsNaN(coord.Z)
                ? $"({coord.X.ToString(CultureInfo.InvariantCulture)} {coord.Y.ToString(CultureInfo.InvariantCulture)} {coord.Z.ToString(CultureInfo.InvariantCulture)})"
                : $"({coord.X.ToString(CultureInfo.InvariantCulture)} {coord.Y.ToString(CultureInfo.InvariantCulture)})";
        });

        return $"MULTIPOINT ({string.Join(", ", pointStrs)})";
    }

    private static string WriteMultiLineString(MultiLineString multiLineString)
    {
        if (multiLineString.IsEmpty)
            return "MULTILINESTRING EMPTY";

        var lineStrs = multiLineString.Geometries.Cast<LineString>().Select(ls =>
        {
            var coordsStr = string.Join(", ", ls.Coordinates.Select(c =>
                !double.IsNaN(c.Z)
                    ? $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)} {c.Z.ToString(CultureInfo.InvariantCulture)}"
                    : $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}"));
            return $"({coordsStr})";
        });

        return $"MULTILINESTRING ({string.Join(", ", lineStrs)})";
    }

    private static string WriteMultiPolygon(MultiPolygon multiPolygon)
    {
        if (multiPolygon.IsEmpty)
            return "MULTIPOLYGON EMPTY";

        var polygonStrs = multiPolygon.Geometries.Cast<Polygon>().Select(p =>
        {
            var rings = new List<string>();
            
            // Exterior ring
            var exteriorCoords = string.Join(", ", p.ExteriorRing.Coordinates.Select(c =>
                !double.IsNaN(c.Z)
                    ? $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)} {c.Z.ToString(CultureInfo.InvariantCulture)}"
                    : $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}"));
            rings.Add($"({exteriorCoords})");

            // Interior rings
            foreach (var hole in p.InteriorRings)
            {
                var holeCoords = string.Join(", ", hole.Coordinates.Select(c =>
                    !double.IsNaN(c.Z)
                        ? $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)} {c.Z.ToString(CultureInfo.InvariantCulture)}"
                        : $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}"));
                rings.Add($"({holeCoords})");
            }

            return $"({string.Join(", ", rings)})";
        });

        return $"MULTIPOLYGON ({string.Join(", ", polygonStrs)})";
    }

    private static string WriteGeometryCollection(GeometryCollection collection)
    {
        if (collection.IsEmpty)
            return "GEOMETRYCOLLECTION EMPTY";

        var geomStrs = collection.Geometries.Select(Write);
        return $"GEOMETRYCOLLECTION ({string.Join(", ", geomStrs)})";
    }

    #endregion

    #region Helper Methods

    private static List<ICoordinate> ParseCoordinateSequence(string coordinates)
    {
        var coords = new List<ICoordinate>();
        
        if (string.IsNullOrWhiteSpace(coordinates))
            return coords;

        var coordPairs = coordinates.Split(',');
        foreach (var pair in coordPairs)
        {
            var values = pair.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length >= 2)
            {
                if (double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    if (values.Length >= 3 && double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                    {
                        coords.Add(new Coordinate(x, y, z));
                    }
                    else
                    {
                        coords.Add(new Coordinate(x, y));
                    }
                }
            }
        }

        return coords;
    }

    private static List<List<ICoordinate>> ParseRings(string coordinates)
    {
        var rings = new List<List<ICoordinate>>();
        var ringStrings = ParseGeometryParts(coordinates);

        foreach (var ringStr in ringStrings)
        {
            var coords = ParseCoordinateSequence(ringStr.Trim('(', ')'));
            if (coords.Count > 0)
                rings.Add(coords);
        }

        return rings;
    }

    private static List<string> ParseGeometryParts(string input)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(')
            {
                if (depth == 0)
                    start = i;
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    parts.Add(input.Substring(start + 1, i - start - 1));
                }
            }
        }

        return parts;
    }

    private static List<string> ParsePolygonParts(string input)
    {
        var parts = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return parts;

        var depth = 0;
        var start = 0;
        var currentInput = input.Trim();

        // 바깥쪽의 불필요한 괄호들 제거 (재귀 대신 루프로)
        while (currentInput.StartsWith("((") && currentInput.EndsWith("))"))
        {
            var inner = currentInput.Substring(1, currentInput.Length - 2).Trim();
            // 만약 괄호를 벗겼는데 중간에 다른 그룹이 있다면 (예: ((A),(B))) 다시 복구
            var tempDepth = 0;
            var splitFound = false;
            for (int i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '(') tempDepth++;
                else if (inner[i] == ')') tempDepth--;
                else if (inner[i] == ',' && tempDepth == 0)
                {
                    splitFound = true;
                    break;
                }
            }
            if (splitFound) break;
            currentInput = inner;
        }

        for (int i = 0; i < currentInput.Length; i++)
        {
            char c = currentInput[i];
            if (c == '(')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    parts.Add(currentInput.Substring(start, i - start + 1));
                }
            }
        }

        return parts;
    }

    private static List<string> ParseGeometryCollectionParts(string input)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        var inGeometry = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            
            if (c == '(')
            {
                if (!inGeometry)
                {
                    // Find the start of this geometry type
                    var j = i - 1;
                    while (j >= start && char.IsWhiteSpace(input[j])) j--;
                    while (j >= start && (char.IsLetter(input[j]) || char.IsWhiteSpace(input[j]))) j--;
                    start = j + 1;
                    inGeometry = true;
                }
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0 && inGeometry)
                {
                    parts.Add(input.Substring(start, i - start + 1).Trim());
                    inGeometry = false;
                    start = i + 1;
                }
            }
        }

        return parts;
    }

    #endregion
}