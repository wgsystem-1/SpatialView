using SpatialView.Engine.Geometry;
using SpatialView.Engine.CoordinateSystems;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 지오메트리 계산 유틸리티 클래스
/// 거리, 면적, 길이 등의 계산 기능 제공
/// </summary>
public static class GeometryCalculator
{
    private const double EarthRadiusKm = 6371.0; // 지구 반지름 (km)
    private const double DegreesToRadians = Math.PI / 180.0;
    
    /// <summary>
    /// 두 지오메트리 간의 거리 계산
    /// </summary>
    public static double Distance(IGeometry geom1, IGeometry geom2, DistanceUnit unit = DistanceUnit.Meters)
    {
        if (geom1 == null || geom2 == null)
            return double.NaN;
        
        // 가장 가까운 점들 찾기
        var (p1, p2) = FindClosestPoints(geom1, geom2);
        if (p1 == null || p2 == null)
            return double.NaN;
        
        var distance = CalculateDistance(p1, p2);
        return ConvertDistance(distance, DistanceUnit.Meters, unit);
    }
    
    /// <summary>
    /// 지오메트리의 길이 계산
    /// </summary>
    public static double Length(IGeometry geometry, DistanceUnit unit = DistanceUnit.Meters)
    {
        if (geometry == null) return 0;
        
        return geometry.GeometryType switch
        {
            GeometryType.LineString => CalculateLineStringLength((LineString)geometry),
            GeometryType.MultiLineString => CalculateMultiLineStringLength((MultiLineString)geometry),
            GeometryType.LinearRing => CalculateLinearRingLength((LinearRing)geometry),
            GeometryType.Polygon => CalculatePolygonPerimeter((Polygon)geometry),
            GeometryType.MultiPolygon => CalculateMultiPolygonPerimeter((MultiPolygon)geometry),
            GeometryType.GeometryCollection => CalculateGeometryCollectionLength((GeometryCollection)geometry),
            _ => 0
        };
    }
    
    /// <summary>
    /// 지오메트리의 면적 계산
    /// </summary>
    public static double Area(IGeometry geometry, AreaUnit unit = AreaUnit.SquareMeters)
    {
        if (geometry == null) return 0;
        
        var area = geometry.GeometryType switch
        {
            GeometryType.Polygon => CalculatePolygonArea((Polygon)geometry),
            GeometryType.MultiPolygon => CalculateMultiPolygonArea((MultiPolygon)geometry),
            GeometryType.GeometryCollection => CalculateGeometryCollectionArea((GeometryCollection)geometry),
            _ => 0
        };
        
        return ConvertArea(area, AreaUnit.SquareMeters, unit);
    }
    
    /// <summary>
    /// 지오메트리의 둘레 계산
    /// </summary>
    public static double Perimeter(IGeometry geometry, DistanceUnit unit = DistanceUnit.Meters)
    {
        if (geometry == null) return 0;
        
        var perimeter = geometry.GeometryType switch
        {
            GeometryType.Polygon => CalculatePolygonPerimeter((Polygon)geometry),
            GeometryType.MultiPolygon => CalculateMultiPolygonPerimeter((MultiPolygon)geometry),
            _ => 0
        };
        
        return ConvertDistance(perimeter, DistanceUnit.Meters, unit);
    }
    
    /// <summary>
    /// 점에서 선까지의 거리 계산
    /// </summary>
    public static double PointToLineDistance(Point point, LineString line, DistanceUnit unit = DistanceUnit.Meters)
    {
        if (point == null || line == null || line.Coordinates.Length < 2)
            return double.NaN;
        
        var minDistance = double.MaxValue;
        
        for (int i = 0; i < line.Coordinates.Length - 1; i++)
        {
            var distance = PointToSegmentDistance(
                point.Coordinate, 
                line.Coordinates[i], 
                line.Coordinates[i + 1]);
            
            minDistance = Math.Min(minDistance, distance);
        }
        
        return ConvertDistance(minDistance, DistanceUnit.Meters, unit);
    }
    
    /// <summary>
    /// Haversine 공식을 사용한 지리적 거리 계산 (구면 거리)
    /// </summary>
    public static double GeographicDistance(ICoordinate coord1, ICoordinate coord2, DistanceUnit unit = DistanceUnit.Kilometers)
    {
        if (coord1 == null || coord2 == null)
            return double.NaN;
        
        var lat1 = coord1.Y * DegreesToRadians;
        var lat2 = coord2.Y * DegreesToRadians;
        var deltaLat = (coord2.Y - coord1.Y) * DegreesToRadians;
        var deltaLon = (coord2.X - coord1.X) * DegreesToRadians;
        
        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        var distance = EarthRadiusKm * c;
        
        return ConvertDistance(distance * 1000, DistanceUnit.Meters, unit);
    }
    
    /// <summary>
    /// 구면상의 면적 계산 (지리좌표계용)
    /// </summary>
    public static double GeographicArea(Polygon polygon, AreaUnit unit = AreaUnit.SquareKilometers)
    {
        if (polygon == null) return 0;
        
        var area = CalculateSphericalPolygonArea(polygon.ExteriorRing.Coordinates);
        
        // 홀 면적 빼기
        foreach (var hole in polygon.InteriorRings)
        {
            area -= CalculateSphericalPolygonArea(hole.Coordinates);
        }
        
        // 평방 미터로 변환
        area *= 1000000; // km² to m²
        
        return ConvertArea(area, AreaUnit.SquareMeters, unit);
    }
    
    /// <summary>
    /// 바운딩 박스의 크기 계산
    /// </summary>
    public static (double width, double height) BoundingBoxSize(Envelope envelope, DistanceUnit unit = DistanceUnit.Meters)
    {
        if (envelope == null) return (0, 0);
        
        var width = envelope.Width;
        var height = envelope.Height;
        
        // 단위 변환
        width = ConvertDistance(width, DistanceUnit.Meters, unit);
        height = ConvertDistance(height, DistanceUnit.Meters, unit);
        
        return (width, height);
    }
    
    /// <summary>
    /// 중심점 계산
    /// </summary>
    public static Point? Centroid(IGeometry geometry)
    {
        if (geometry == null) return null;
        
        return geometry.GeometryType switch
        {
            GeometryType.Point => (Point)geometry,
            GeometryType.LineString => CalculateLineStringCentroid((LineString)geometry),
            GeometryType.Polygon => CalculatePolygonCentroid((Polygon)geometry),
            GeometryType.MultiPoint => CalculateMultiPointCentroid((MultiPoint)geometry),
            GeometryType.MultiLineString => CalculateMultiLineStringCentroid((MultiLineString)geometry),
            GeometryType.MultiPolygon => CalculateMultiPolygonCentroid((MultiPolygon)geometry),
            _ => null
        };
    }
    
    // 내부 계산 메서드들
    private static double CalculateDistance(ICoordinate p1, ICoordinate p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    private static (ICoordinate?, ICoordinate?) FindClosestPoints(IGeometry geom1, IGeometry geom2)
    {
        // 간단한 구현 - 실제로는 더 정교한 알고리즘 필요
        var p1 = geom1.Centroid;
        var p2 = geom2.Centroid;
        
        if (p1 == null || p2 == null)
            return (null, null);
        
        return (p1, p2);
    }
    
    private static double CalculateLineStringLength(LineString lineString)
    {
        var length = 0.0;
        var coords = lineString.Coordinates;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            length += CalculateDistance(coords[i], coords[i + 1]);
        }
        
        return length;
    }
    
    private static double CalculateMultiLineStringLength(MultiLineString multiLineString)
    {
        return multiLineString.Geometries.Cast<LineString>().Sum(CalculateLineStringLength);
    }
    
    private static double CalculateLinearRingLength(LinearRing ring)
    {
        return CalculateLineStringLength(ring);
    }
    
    private static double CalculatePolygonPerimeter(Polygon polygon)
    {
        var perimeter = CalculateLinearRingLength(polygon.ExteriorRing);
        
        foreach (var hole in polygon.InteriorRings)
        {
            perimeter += CalculateLinearRingLength(hole);
        }
        
        return perimeter;
    }
    
    private static double CalculateMultiPolygonPerimeter(MultiPolygon multiPolygon)
    {
        return multiPolygon.Geometries.Cast<Polygon>().Sum(CalculatePolygonPerimeter);
    }
    
    private static double CalculateGeometryCollectionLength(GeometryCollection collection)
    {
        return collection.Geometries.Sum(g => Length(g));
    }
    
    private static double CalculatePolygonArea(Polygon polygon)
    {
        // Shoelace formula
        var area = CalculateRingArea(polygon.ExteriorRing.Coordinates);
        
        // 홀 면적 빼기
        foreach (var hole in polygon.InteriorRings)
        {
            area -= Math.Abs(CalculateRingArea(hole.Coordinates));
        }
        
        return Math.Abs(area);
    }
    
    private static double CalculateRingArea(ICoordinate[] coords)
    {
        var area = 0.0;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            area += coords[i].X * coords[i + 1].Y;
            area -= coords[i + 1].X * coords[i].Y;
        }
        
        return area / 2.0;
    }
    
    private static double CalculateMultiPolygonArea(MultiPolygon multiPolygon)
    {
        return multiPolygon.Geometries.Cast<Polygon>().Sum(CalculatePolygonArea);
    }
    
    private static double CalculateGeometryCollectionArea(GeometryCollection collection)
    {
        return collection.Geometries.Sum(g => Area(g));
    }
    
    private static double PointToSegmentDistance(ICoordinate point, ICoordinate seg1, ICoordinate seg2)
    {
        var dx = seg2.X - seg1.X;
        var dy = seg2.Y - seg1.Y;
        
        if (dx == 0 && dy == 0)
        {
            // seg1과 seg2가 같은 점
            return CalculateDistance(point, seg1);
        }
        
        var t = ((point.X - seg1.X) * dx + (point.Y - seg1.Y) * dy) / (dx * dx + dy * dy);
        
        if (t < 0)
        {
            return CalculateDistance(point, seg1);
        }
        else if (t > 1)
        {
            return CalculateDistance(point, seg2);
        }
        else
        {
            var projection = new Coordinate(seg1.X + t * dx, seg1.Y + t * dy);
            return CalculateDistance(point, projection);
        }
    }
    
    private static double CalculateSphericalPolygonArea(ICoordinate[] coords)
    {
        if (coords.Length < 4) return 0; // 최소 3개 점 + 폐합점
        
        var area = 0.0;
        var n = coords.Length - 1; // 폐합점 제외
        
        for (int i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var lat1 = coords[i].Y * DegreesToRadians;
            var lat2 = coords[j].Y * DegreesToRadians;
            var lon1 = coords[i].X * DegreesToRadians;
            var lon2 = coords[j].X * DegreesToRadians;
            
            area += (lon2 - lon1) * (2 + Math.Sin(lat1) + Math.Sin(lat2));
        }
        
        area = Math.Abs(area) * EarthRadiusKm * EarthRadiusKm / 2.0;
        return area;
    }
    
    private static Point? CalculateLineStringCentroid(LineString lineString)
    {
        var coords = lineString.Coordinates;
        if (coords.Length == 0) return null;
        
        var totalLength = 0.0;
        var weightedX = 0.0;
        var weightedY = 0.0;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            var segmentLength = CalculateDistance(coords[i], coords[i + 1]);
            var midX = (coords[i].X + coords[i + 1].X) / 2.0;
            var midY = (coords[i].Y + coords[i + 1].Y) / 2.0;
            
            weightedX += midX * segmentLength;
            weightedY += midY * segmentLength;
            totalLength += segmentLength;
        }
        
        if (totalLength == 0) return new Point(coords[0]);
        
        return new Point(weightedX / totalLength, weightedY / totalLength);
    }
    
    private static Point? CalculatePolygonCentroid(Polygon polygon)
    {
        var coords = polygon.ExteriorRing.Coordinates;
        if (coords.Length < 3) return null;
        
        var area = 0.0;
        var centroidX = 0.0;
        var centroidY = 0.0;
        
        for (int i = 0; i < coords.Length - 1; i++)
        {
            var j = i + 1;
            var factor = coords[i].X * coords[j].Y - coords[j].X * coords[i].Y;
            area += factor;
            centroidX += (coords[i].X + coords[j].X) * factor;
            centroidY += (coords[i].Y + coords[j].Y) * factor;
        }
        
        area /= 2.0;
        if (Math.Abs(area) < double.Epsilon) return null;
        
        centroidX /= (6.0 * area);
        centroidY /= (6.0 * area);
        
        return new Point(centroidX, centroidY);
    }
    
    private static Point? CalculateMultiPointCentroid(MultiPoint multiPoint)
    {
        var points = multiPoint.Geometries.Cast<Point>().ToList();
        if (points.Count == 0) return null;
        
        var avgX = points.Average(p => p.X);
        var avgY = points.Average(p => p.Y);
        
        return new Point(avgX, avgY);
    }
    
    private static Point? CalculateMultiLineStringCentroid(MultiLineString multiLineString)
    {
        var lineStrings = multiLineString.Geometries.Cast<LineString>().ToList();
        if (lineStrings.Count == 0) return null;
        
        var totalLength = 0.0;
        var weightedX = 0.0;
        var weightedY = 0.0;
        
        foreach (var ls in lineStrings)
        {
            var length = CalculateLineStringLength(ls);
            var centroid = CalculateLineStringCentroid(ls);
            
            if (centroid != null && length > 0)
            {
                weightedX += centroid.X * length;
                weightedY += centroid.Y * length;
                totalLength += length;
            }
        }
        
        if (totalLength == 0) return null;
        
        return new Point(weightedX / totalLength, weightedY / totalLength);
    }
    
    private static Point? CalculateMultiPolygonCentroid(MultiPolygon multiPolygon)
    {
        var polygons = multiPolygon.Geometries.Cast<Polygon>().ToList();
        if (polygons.Count == 0) return null;
        
        var totalArea = 0.0;
        var weightedX = 0.0;
        var weightedY = 0.0;
        
        foreach (var poly in polygons)
        {
            var area = CalculatePolygonArea(poly);
            var centroid = CalculatePolygonCentroid(poly);
            
            if (centroid != null && area > 0)
            {
                weightedX += centroid.X * area;
                weightedY += centroid.Y * area;
                totalArea += area;
            }
        }
        
        if (totalArea == 0) return null;
        
        return new Point(weightedX / totalArea, weightedY / totalArea);
    }
    
    // 단위 변환 메서드들
    private static double ConvertDistance(double value, DistanceUnit from, DistanceUnit to)
    {
        if (from == to) return value;
        
        // 먼저 미터로 변환
        var meters = from switch
        {
            DistanceUnit.Meters => value,
            DistanceUnit.Kilometers => value * 1000,
            DistanceUnit.Miles => value * 1609.344,
            DistanceUnit.Feet => value * 0.3048,
            DistanceUnit.NauticalMiles => value * 1852,
            _ => value
        };
        
        // 목표 단위로 변환
        return to switch
        {
            DistanceUnit.Meters => meters,
            DistanceUnit.Kilometers => meters / 1000,
            DistanceUnit.Miles => meters / 1609.344,
            DistanceUnit.Feet => meters / 0.3048,
            DistanceUnit.NauticalMiles => meters / 1852,
            _ => meters
        };
    }
    
    private static double ConvertArea(double value, AreaUnit from, AreaUnit to)
    {
        if (from == to) return value;
        
        // 먼저 평방미터로 변환
        var squareMeters = from switch
        {
            AreaUnit.SquareMeters => value,
            AreaUnit.SquareKilometers => value * 1000000,
            AreaUnit.Hectares => value * 10000,
            AreaUnit.Acres => value * 4046.86,
            AreaUnit.SquareMiles => value * 2589988.11,
            AreaUnit.SquareFeet => value * 0.092903,
            _ => value
        };
        
        // 목표 단위로 변환
        return to switch
        {
            AreaUnit.SquareMeters => squareMeters,
            AreaUnit.SquareKilometers => squareMeters / 1000000,
            AreaUnit.Hectares => squareMeters / 10000,
            AreaUnit.Acres => squareMeters / 4046.86,
            AreaUnit.SquareMiles => squareMeters / 2589988.11,
            AreaUnit.SquareFeet => squareMeters / 0.092903,
            _ => squareMeters
        };
    }
}

/// <summary>
/// 거리 단위
/// </summary>
public enum DistanceUnit
{
    Meters,
    Kilometers,
    Miles,
    Feet,
    NauticalMiles
}

/// <summary>
/// 면적 단위
/// </summary>
public enum AreaUnit
{
    SquareMeters,
    SquareKilometers,
    Hectares,
    Acres,
    SquareMiles,
    SquareFeet
}