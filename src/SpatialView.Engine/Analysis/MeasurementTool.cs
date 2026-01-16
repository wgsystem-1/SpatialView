using SpatialView.Engine.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 측정 도구 - 거리 및 면적 측정 기능 제공
/// </summary>
public class MeasurementTool
{
    /// <summary>
    /// 측정 단위
    /// </summary>
    public enum MeasurementUnit
    {
        Meters,
        Kilometers,
        Miles,
        Feet,
        NauticalMiles,
        SquareMeters,
        SquareKilometers,
        Hectares,
        Acres
    }

    /// <summary>
    /// 측정 결과
    /// </summary>
    public class MeasurementResult
    {
        public double Value { get; set; }
        public MeasurementUnit Unit { get; set; }
        public string FormattedValue => FormatValue(Value, Unit);
        public List<Coordinate> Points { get; set; } = new();
        public MeasurementType Type { get; set; }

        private static string FormatValue(double value, MeasurementUnit unit)
        {
            return unit switch
            {
                MeasurementUnit.Meters => $"{value:N2} m",
                MeasurementUnit.Kilometers => $"{value:N3} km",
                MeasurementUnit.Miles => $"{value:N3} mi",
                MeasurementUnit.Feet => $"{value:N2} ft",
                MeasurementUnit.NauticalMiles => $"{value:N3} nmi",
                MeasurementUnit.SquareMeters => $"{value:N2} m²",
                MeasurementUnit.SquareKilometers => $"{value:N3} km²",
                MeasurementUnit.Hectares => $"{value:N2} ha",
                MeasurementUnit.Acres => $"{value:N2} ac",
                _ => $"{value:N2}"
            };
        }
    }

    /// <summary>
    /// 측정 유형
    /// </summary>
    public enum MeasurementType
    {
        Distance,
        Area
    }

    private readonly int _srid;
    private readonly bool _isGeographic;

    /// <summary>
    /// 측정 도구 생성
    /// </summary>
    /// <param name="srid">좌표계 SRID</param>
    public MeasurementTool(int srid = 4326)
    {
        _srid = srid;
        // 4326 (WGS84) 또는 다른 지리 좌표계인지 확인
        _isGeographic = srid == 4326 || srid == 4269 || srid == 4267;
    }

    /// <summary>
    /// 두 점 사이의 거리 측정
    /// </summary>
    public MeasurementResult MeasureDistance(Coordinate point1, Coordinate point2, MeasurementUnit unit = MeasurementUnit.Meters)
    {
        double distance;

        if (_isGeographic)
        {
            // Haversine 공식을 사용한 지리적 거리 계산
            distance = CalculateHaversineDistance(point1.Y, point1.X, point2.Y, point2.X);
        }
        else
        {
            // 평면 좌표계에서의 유클리드 거리
            distance = Math.Sqrt(Math.Pow(point2.X - point1.X, 2) + Math.Pow(point2.Y - point1.Y, 2));
        }

        // 단위 변환
        distance = ConvertDistance(distance, MeasurementUnit.Meters, unit);

        return new MeasurementResult
        {
            Value = distance,
            Unit = unit,
            Points = new List<Coordinate> { point1, point2 },
            Type = MeasurementType.Distance
        };
    }

    /// <summary>
    /// 다중 점을 따라 총 거리 측정 (폴리라인)
    /// </summary>
    public MeasurementResult MeasurePolylineDistance(IEnumerable<Coordinate> points, MeasurementUnit unit = MeasurementUnit.Meters)
    {
        var pointList = points.ToList();
        if (pointList.Count < 2)
        {
            return new MeasurementResult
            {
                Value = 0,
                Unit = unit,
                Points = pointList,
                Type = MeasurementType.Distance
            };
        }

        double totalDistance = 0;

        for (int i = 0; i < pointList.Count - 1; i++)
        {
            var result = MeasureDistance(pointList[i], pointList[i + 1], MeasurementUnit.Meters);
            totalDistance += result.Value;
        }

        // 단위 변환
        totalDistance = ConvertDistance(totalDistance, MeasurementUnit.Meters, unit);

        return new MeasurementResult
        {
            Value = totalDistance,
            Unit = unit,
            Points = pointList,
            Type = MeasurementType.Distance
        };
    }

    /// <summary>
    /// 폴리곤 면적 측정
    /// </summary>
    public MeasurementResult MeasureArea(IEnumerable<Coordinate> points, MeasurementUnit unit = MeasurementUnit.SquareMeters)
    {
        var pointList = points.ToList();
        if (pointList.Count < 3)
        {
            return new MeasurementResult
            {
                Value = 0,
                Unit = unit,
                Points = pointList,
                Type = MeasurementType.Area
            };
        }

        double area;

        if (_isGeographic)
        {
            // 지리적 좌표계에서의 면적 계산 (구면 초과 공식)
            area = CalculateSphericalArea(pointList);
        }
        else
        {
            // 평면 좌표계에서의 면적 계산 (Shoelace 공식)
            area = CalculatePlanarArea(pointList);
        }

        // 단위 변환
        area = ConvertArea(Math.Abs(area), MeasurementUnit.SquareMeters, unit);

        return new MeasurementResult
        {
            Value = area,
            Unit = unit,
            Points = pointList,
            Type = MeasurementType.Area
        };
    }

    /// <summary>
    /// Haversine 공식을 사용한 지리적 거리 계산 (미터 단위)
    /// </summary>
    private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadius = 6371000; // 미터

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadius * c;
    }

    /// <summary>
    /// 구면 초과 공식을 사용한 면적 계산 (제곱미터 단위)
    /// </summary>
    private static double CalculateSphericalArea(List<Coordinate> points)
    {
        const double EarthRadius = 6371000; // 미터

        if (points.Count < 3) return 0;

        // 폴리곤이 닫혀있지 않으면 닫기
        var closedPoints = new List<Coordinate>(points);
        if (closedPoints[0].X != closedPoints[^1].X || closedPoints[0].Y != closedPoints[^1].Y)
        {
            closedPoints.Add(closedPoints[0]);
        }

        double area = 0;

        for (int i = 0; i < closedPoints.Count - 1; i++)
        {
            var p1 = closedPoints[i];
            var p2 = closedPoints[i + 1];

            area += ToRadians(p2.X - p1.X) * (2 + Math.Sin(ToRadians(p1.Y)) + Math.Sin(ToRadians(p2.Y)));
        }

        area = Math.Abs(area * EarthRadius * EarthRadius / 2);

        return area;
    }

    /// <summary>
    /// Shoelace 공식을 사용한 평면 면적 계산
    /// </summary>
    private static double CalculatePlanarArea(List<Coordinate> points)
    {
        if (points.Count < 3) return 0;

        // 폴리곤이 닫혀있지 않으면 닫기
        var closedPoints = new List<Coordinate>(points);
        if (closedPoints[0].X != closedPoints[^1].X || closedPoints[0].Y != closedPoints[^1].Y)
        {
            closedPoints.Add(closedPoints[0]);
        }

        double area = 0;

        for (int i = 0; i < closedPoints.Count - 1; i++)
        {
            area += closedPoints[i].X * closedPoints[i + 1].Y;
            area -= closedPoints[i + 1].X * closedPoints[i].Y;
        }

        return Math.Abs(area / 2);
    }

    /// <summary>
    /// 거리 단위 변환
    /// </summary>
    public static double ConvertDistance(double value, MeasurementUnit from, MeasurementUnit to)
    {
        // 먼저 미터로 변환
        double meters = from switch
        {
            MeasurementUnit.Meters => value,
            MeasurementUnit.Kilometers => value * 1000,
            MeasurementUnit.Miles => value * 1609.344,
            MeasurementUnit.Feet => value * 0.3048,
            MeasurementUnit.NauticalMiles => value * 1852,
            _ => value
        };

        // 목표 단위로 변환
        return to switch
        {
            MeasurementUnit.Meters => meters,
            MeasurementUnit.Kilometers => meters / 1000,
            MeasurementUnit.Miles => meters / 1609.344,
            MeasurementUnit.Feet => meters / 0.3048,
            MeasurementUnit.NauticalMiles => meters / 1852,
            _ => meters
        };
    }

    /// <summary>
    /// 면적 단위 변환
    /// </summary>
    public static double ConvertArea(double value, MeasurementUnit from, MeasurementUnit to)
    {
        // 먼저 제곱미터로 변환
        double sqMeters = from switch
        {
            MeasurementUnit.SquareMeters => value,
            MeasurementUnit.SquareKilometers => value * 1_000_000,
            MeasurementUnit.Hectares => value * 10_000,
            MeasurementUnit.Acres => value * 4046.8564224,
            _ => value
        };

        // 목표 단위로 변환
        return to switch
        {
            MeasurementUnit.SquareMeters => sqMeters,
            MeasurementUnit.SquareKilometers => sqMeters / 1_000_000,
            MeasurementUnit.Hectares => sqMeters / 10_000,
            MeasurementUnit.Acres => sqMeters / 4046.8564224,
            _ => sqMeters
        };
    }

    /// <summary>
    /// 도를 라디안으로 변환
    /// </summary>
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    /// <summary>
    /// 라디안을 도로 변환
    /// </summary>
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;

    /// <summary>
    /// 세그먼트별 거리 정보 반환
    /// </summary>
    public List<MeasurementResult> GetSegmentDistances(IEnumerable<Coordinate> points, MeasurementUnit unit = MeasurementUnit.Meters)
    {
        var pointList = points.ToList();
        var results = new List<MeasurementResult>();

        for (int i = 0; i < pointList.Count - 1; i++)
        {
            results.Add(MeasureDistance(pointList[i], pointList[i + 1], unit));
        }

        return results;
    }

    /// <summary>
    /// 방위각 계산 (북쪽 기준, 시계 방향)
    /// </summary>
    public double CalculateBearing(Coordinate from, Coordinate to)
    {
        if (_isGeographic)
        {
            var lat1 = ToRadians(from.Y);
            var lat2 = ToRadians(to.Y);
            var dLon = ToRadians(to.X - from.X);

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            var bearing = ToDegrees(Math.Atan2(y, x));
            return (bearing + 360) % 360;
        }
        else
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var bearing = ToDegrees(Math.Atan2(dx, dy));
            return (bearing + 360) % 360;
        }
    }
}
