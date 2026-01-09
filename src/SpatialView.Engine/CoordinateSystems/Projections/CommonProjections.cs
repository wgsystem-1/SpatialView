namespace SpatialView.Engine.CoordinateSystems.Projections;

/// <summary>
/// 일반적인 지도투영법 구현
/// 주요 투영법들의 수학적 변환을 제공합니다
/// </summary>
public static class CommonProjections
{
    /// <summary>
    /// Web Mercator 투영법
    /// 웹 지도에서 널리 사용되는 구면 메르카토르 투영
    /// </summary>
    public static class WebMercator
    {
        public const int SRID = 3857;
        public const double EarthRadius = 6378137.0; // WGS84 장반경
        public const double MaxLatitude = 85.05112878; // 유효 위도 범위
        
        /// <summary>
        /// 위경도를 Web Mercator 좌표로 변환
        /// </summary>
        /// <param name="longitude">경도 (도)</param>
        /// <param name="latitude">위도 (도)</param>
        /// <returns>Web Mercator 좌표 (X, Y)</returns>
        public static (double X, double Y) FromGeographic(double longitude, double latitude)
        {
            // 위도 범위 제한
            latitude = Math.Max(Math.Min(latitude, MaxLatitude), -MaxLatitude);
            
            // 경도를 라디안으로 변환
            var lonRad = longitude * Math.PI / 180.0;
            
            // 위도를 라디안으로 변환
            var latRad = latitude * Math.PI / 180.0;
            
            // Web Mercator 변환
            var x = EarthRadius * lonRad;
            var y = EarthRadius * Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0));
            
            return (x, y);
        }
        
        /// <summary>
        /// Web Mercator 좌표를 위경도로 변환
        /// </summary>
        /// <param name="x">X 좌표 (미터)</param>
        /// <param name="y">Y 좌표 (미터)</param>
        /// <returns>위경도 (경도, 위도)</returns>
        public static (double Longitude, double Latitude) ToGeographic(double x, double y)
        {
            var longitude = (x / EarthRadius) * 180.0 / Math.PI;
            var latitude = (2.0 * Math.Atan(Math.Exp(y / EarthRadius)) - Math.PI / 2.0) * 180.0 / Math.PI;
            
            return (longitude, latitude);
        }
    }
    
    /// <summary>
    /// UTM (Universal Transverse Mercator) 투영법
    /// 전 세계를 60개 구역으로 나누어 사용하는 투영법
    /// </summary>
    public static class UTM
    {
        public const double ScaleFactor = 0.9996;
        public const double FalseEasting = 500000.0;
        public const double FalseNorthingSouth = 10000000.0; // 남반구용
        
        /// <summary>
        /// 경도로부터 UTM 구역 번호 계산
        /// </summary>
        /// <param name="longitude">경도 (도)</param>
        /// <returns>UTM 구역 번호 (1-60)</returns>
        public static int GetZoneNumber(double longitude)
        {
            return (int)Math.Floor((longitude + 180) / 6) + 1;
        }
        
        /// <summary>
        /// UTM 구역의 중앙 자오선 계산
        /// </summary>
        /// <param name="zoneNumber">UTM 구역 번호</param>
        /// <returns>중앙 자오선 (도)</returns>
        public static double GetCentralMeridian(int zoneNumber)
        {
            return (zoneNumber - 1) * 6.0 - 180.0 + 3.0;
        }
        
        /// <summary>
        /// WGS84 위경도를 UTM 좌표로 변환
        /// </summary>
        /// <param name="longitude">경도 (도)</param>
        /// <param name="latitude">위도 (도)</param>
        /// <param name="zoneNumber">UTM 구역 번호 (null이면 자동 계산)</param>
        /// <param name="isNorthern">북반구 여부 (null이면 위도로 판단)</param>
        /// <returns>UTM 좌표 (X, Y, Zone, IsNorthern)</returns>
        public static (double X, double Y, int Zone, bool IsNorthern) FromWGS84(
            double longitude, double latitude, int? zoneNumber = null, bool? isNorthern = null)
        {
            var zone = zoneNumber ?? GetZoneNumber(longitude);
            var northern = isNorthern ?? latitude >= 0;
            var centralMeridian = GetCentralMeridian(zone);
            
            // WGS84 타원체 매개변수
            const double a = 6378137.0; // 장반경
            const double e2 = 0.00669437999014; // 이심률 제곱
            var e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
            
            // 라디안 변환
            var latRad = latitude * Math.PI / 180.0;
            var lonRad = longitude * Math.PI / 180.0;
            var lonOriginRad = centralMeridian * Math.PI / 180.0;
            
            // 계산용 변수들
            var eccPrimeSquared = e2 / (1 - e2);
            var n = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
            var t = Math.Tan(latRad) * Math.Tan(latRad);
            var c = eccPrimeSquared * Math.Cos(latRad) * Math.Cos(latRad);
            var aa = Math.Cos(latRad) * (lonRad - lonOriginRad);
            
            var m = a * ((1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * latRad
                      - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * latRad)
                      + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * latRad)
                      - (35 * e2 * e2 * e2 / 3072) * Math.Sin(6 * latRad));
            
            // UTM 좌표 계산
            var x = ScaleFactor * n * (aa + (1 - t + c) * aa * aa * aa / 6
                                      + (5 - 18 * t + t * t + 72 * c - 58 * eccPrimeSquared) * aa * aa * aa * aa * aa / 120)
                    + FalseEasting;
            
            var y = ScaleFactor * (m + n * Math.Tan(latRad) * (aa * aa / 2
                                                              + (5 - t + 9 * c + 4 * c * c) * aa * aa * aa * aa / 24
                                                              + (61 - 58 * t + t * t + 600 * c - 330 * eccPrimeSquared) * aa * aa * aa * aa * aa * aa / 720));
            
            // 남반구인 경우 y 좌표 조정
            if (!northern)
                y += FalseNorthingSouth;
            
            return (x, y, zone, northern);
        }
        
        /// <summary>
        /// UTM 좌표를 WGS84 위경도로 변환
        /// </summary>
        /// <param name="x">X 좌표 (미터)</param>
        /// <param name="y">Y 좌표 (미터)</param>
        /// <param name="zoneNumber">UTM 구역 번호</param>
        /// <param name="isNorthern">북반구 여부</param>
        /// <returns>WGS84 위경도 (경도, 위도)</returns>
        public static (double Longitude, double Latitude) ToWGS84(double x, double y, int zoneNumber, bool isNorthern)
        {
            const double a = 6378137.0; // WGS84 장반경
            const double e2 = 0.00669437999014; // 이심률 제곱
            var e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
            
            // 남반구인 경우 y 좌표 조정
            if (!isNorthern)
                y -= FalseNorthingSouth;
            
            var x1 = x - FalseEasting;
            var y1 = y;
            
            var lonOrigin = GetCentralMeridian(zoneNumber) * Math.PI / 180.0;
            var eccPrimeSquared = e2 / (1 - e2);
            
            var m = y1 / ScaleFactor;
            var mu = m / (a * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));
            
            var phi1Rad = mu + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu)
                             + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu)
                             + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu);
            
            var n1 = a / Math.Sqrt(1 - e2 * Math.Sin(phi1Rad) * Math.Sin(phi1Rad));
            var t1 = Math.Tan(phi1Rad) * Math.Tan(phi1Rad);
            var c1 = eccPrimeSquared * Math.Cos(phi1Rad) * Math.Cos(phi1Rad);
            var r1 = a * (1 - e2) / Math.Pow(1 - e2 * Math.Sin(phi1Rad) * Math.Sin(phi1Rad), 1.5);
            var d = x1 / (n1 * ScaleFactor);
            
            var latitude = phi1Rad - (n1 * Math.Tan(phi1Rad) / r1) * (d * d / 2 - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * eccPrimeSquared) * d * d * d * d / 24
                                                                     + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * eccPrimeSquared - 3 * c1 * c1) * d * d * d * d * d * d / 720);
            
            var longitude = (d - (1 + 2 * t1 + c1) * d * d * d / 6 + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * eccPrimeSquared + 24 * t1 * t1) * d * d * d * d * d / 120) / Math.Cos(phi1Rad);
            
            longitude = lonOrigin + longitude;
            
            return (longitude * 180.0 / Math.PI, latitude * 180.0 / Math.PI);
        }
    }
    
    /// <summary>
    /// Lambert Conformal Conic 투영법
    /// 중위도 지역에서 널리 사용되는 정각원추투영
    /// </summary>
    public static class LambertConformalConic
    {
        /// <summary>
        /// Lambert Conformal Conic 투영 매개변수
        /// </summary>
        public class Parameters
        {
            public double CentralMeridian { get; set; } = 0.0;
            public double LatitudeOfOrigin { get; set; } = 0.0;
            public double StandardParallel1 { get; set; } = 33.0;
            public double StandardParallel2 { get; set; } = 45.0;
            public double FalseEasting { get; set; } = 0.0;
            public double FalseNorthing { get; set; } = 0.0;
            public double SemiMajorAxis { get; set; } = 6378137.0; // WGS84
            public double InverseFlattening { get; set; } = 298.257223563; // WGS84
        }
        
        /// <summary>
        /// 위경도를 Lambert Conformal Conic 좌표로 변환
        /// </summary>
        /// <param name="longitude">경도 (도)</param>
        /// <param name="latitude">위도 (도)</param>
        /// <param name="parameters">투영 매개변수</param>
        /// <returns>투영 좌표 (X, Y)</returns>
        public static (double X, double Y) FromGeographic(double longitude, double latitude, Parameters parameters)
        {
            var lonRad = longitude * Math.PI / 180.0;
            var latRad = latitude * Math.PI / 180.0;
            var lonOriginRad = parameters.CentralMeridian * Math.PI / 180.0;
            var latOriginRad = parameters.LatitudeOfOrigin * Math.PI / 180.0;
            var lat1Rad = parameters.StandardParallel1 * Math.PI / 180.0;
            var lat2Rad = parameters.StandardParallel2 * Math.PI / 180.0;
            
            var a = parameters.SemiMajorAxis;
            var f = 1.0 / parameters.InverseFlattening;
            var e = Math.Sqrt(2 * f - f * f);
            
            // 투영 상수 계산
            var m1 = Math.Cos(lat1Rad) / Math.Sqrt(1 - e * e * Math.Sin(lat1Rad) * Math.Sin(lat1Rad));
            var m2 = Math.Cos(lat2Rad) / Math.Sqrt(1 - e * e * Math.Sin(lat2Rad) * Math.Sin(lat2Rad));
            
            var t1 = Math.Tan(Math.PI / 4 - lat1Rad / 2) / Math.Pow((1 - e * Math.Sin(lat1Rad)) / (1 + e * Math.Sin(lat1Rad)), e / 2);
            var t2 = Math.Tan(Math.PI / 4 - lat2Rad / 2) / Math.Pow((1 - e * Math.Sin(lat2Rad)) / (1 + e * Math.Sin(lat2Rad)), e / 2);
            var t = Math.Tan(Math.PI / 4 - latRad / 2) / Math.Pow((1 - e * Math.Sin(latRad)) / (1 + e * Math.Sin(latRad)), e / 2);
            var t0 = Math.Tan(Math.PI / 4 - latOriginRad / 2) / Math.Pow((1 - e * Math.Sin(latOriginRad)) / (1 + e * Math.Sin(latOriginRad)), e / 2);
            
            var n = (Math.Log(m1) - Math.Log(m2)) / (Math.Log(t1) - Math.Log(t2));
            var ff = m1 / (n * Math.Pow(t1, n));
            var rho = a * ff * Math.Pow(t, n);
            var rho0 = a * ff * Math.Pow(t0, n);
            
            var theta = n * (lonRad - lonOriginRad);
            
            var x = rho * Math.Sin(theta) + parameters.FalseEasting;
            var y = rho0 - rho * Math.Cos(theta) + parameters.FalseNorthing;
            
            return (x, y);
        }
    }
    
    /// <summary>
    /// Albers Equal Area Conic 투영법
    /// 면적을 보존하는 원추투영법
    /// </summary>
    public static class AlbersEqualArea
    {
        /// <summary>
        /// Albers Equal Area 투영 매개변수
        /// </summary>
        public class Parameters
        {
            public double CentralMeridian { get; set; } = 0.0;
            public double LatitudeOfOrigin { get; set; } = 0.0;
            public double StandardParallel1 { get; set; } = 29.5;
            public double StandardParallel2 { get; set; } = 45.5;
            public double FalseEasting { get; set; } = 0.0;
            public double FalseNorthing { get; set; } = 0.0;
            public double SemiMajorAxis { get; set; } = 6378137.0; // WGS84
            public double InverseFlattening { get; set; } = 298.257223563; // WGS84
        }
        
        /// <summary>
        /// 위경도를 Albers Equal Area 좌표로 변환
        /// </summary>
        /// <param name="longitude">경도 (도)</param>
        /// <param name="latitude">위도 (도)</param>
        /// <param name="parameters">투영 매개변수</param>
        /// <returns>투영 좌표 (X, Y)</returns>
        public static (double X, double Y) FromGeographic(double longitude, double latitude, Parameters parameters)
        {
            var lonRad = longitude * Math.PI / 180.0;
            var latRad = latitude * Math.PI / 180.0;
            var lonOriginRad = parameters.CentralMeridian * Math.PI / 180.0;
            var latOriginRad = parameters.LatitudeOfOrigin * Math.PI / 180.0;
            var lat1Rad = parameters.StandardParallel1 * Math.PI / 180.0;
            var lat2Rad = parameters.StandardParallel2 * Math.PI / 180.0;
            
            var a = parameters.SemiMajorAxis;
            var f = 1.0 / parameters.InverseFlattening;
            var e = Math.Sqrt(2 * f - f * f);
            var e2 = e * e;
            
            // q 함수 계산
            var q = (1 - e2) * (Math.Sin(latRad) / (1 - e2 * Math.Sin(latRad) * Math.Sin(latRad)) - 
                               (1 / (2 * e)) * Math.Log((1 - e * Math.Sin(latRad)) / (1 + e * Math.Sin(latRad))));
            var q1 = (1 - e2) * (Math.Sin(lat1Rad) / (1 - e2 * Math.Sin(lat1Rad) * Math.Sin(lat1Rad)) - 
                                (1 / (2 * e)) * Math.Log((1 - e * Math.Sin(lat1Rad)) / (1 + e * Math.Sin(lat1Rad))));
            var q2 = (1 - e2) * (Math.Sin(lat2Rad) / (1 - e2 * Math.Sin(lat2Rad) * Math.Sin(lat2Rad)) - 
                                (1 / (2 * e)) * Math.Log((1 - e * Math.Sin(lat2Rad)) / (1 + e * Math.Sin(lat2Rad))));
            var q0 = (1 - e2) * (Math.Sin(latOriginRad) / (1 - e2 * Math.Sin(latOriginRad) * Math.Sin(latOriginRad)) - 
                                (1 / (2 * e)) * Math.Log((1 - e * Math.Sin(latOriginRad)) / (1 + e * Math.Sin(latOriginRad))));
            
            var m1 = Math.Cos(lat1Rad) / Math.Sqrt(1 - e2 * Math.Sin(lat1Rad) * Math.Sin(lat1Rad));
            var m2 = Math.Cos(lat2Rad) / Math.Sqrt(1 - e2 * Math.Sin(lat2Rad) * Math.Sin(lat2Rad));
            
            var n = (m1 * m1 - m2 * m2) / (q2 - q1);
            var c = m1 * m1 + n * q1;
            var rho = a * Math.Sqrt(c - n * q) / n;
            var rho0 = a * Math.Sqrt(c - n * q0) / n;
            
            var theta = n * (lonRad - lonOriginRad);
            
            var x = rho * Math.Sin(theta) + parameters.FalseEasting;
            var y = rho0 - rho * Math.Cos(theta) + parameters.FalseNorthing;
            
            return (x, y);
        }
    }
    
    /// <summary>
    /// 투영법 유틸리티
    /// </summary>
    public static class ProjectionUtils
    {
        /// <summary>
        /// 도를 라디안으로 변환
        /// </summary>
        /// <param name="degrees">도</param>
        /// <returns>라디안</returns>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
        
        /// <summary>
        /// 라디안을 도로 변환
        /// </summary>
        /// <param name="radians">라디안</param>
        /// <returns>도</returns>
        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }
        
        /// <summary>
        /// 경도를 정규화 (-180 ~ 180)
        /// </summary>
        /// <param name="longitude">경도</param>
        /// <returns>정규화된 경도</returns>
        public static double NormalizeLongitude(double longitude)
        {
            while (longitude > 180.0)
                longitude -= 360.0;
            while (longitude < -180.0)
                longitude += 360.0;
            return longitude;
        }
        
        /// <summary>
        /// 위도를 정규화 (-90 ~ 90)
        /// </summary>
        /// <param name="latitude">위도</param>
        /// <returns>정규화된 위도</returns>
        public static double NormalizeLatitude(double latitude)
        {
            return Math.Max(-90.0, Math.Min(90.0, latitude));
        }
        
        /// <summary>
        /// 두 지점 간 거리 계산 (Haversine 공식)
        /// </summary>
        /// <param name="lat1">지점1 위도</param>
        /// <param name="lon1">지점1 경도</param>
        /// <param name="lat2">지점2 위도</param>
        /// <param name="lon2">지점2 경도</param>
        /// <returns>거리 (미터)</returns>
        public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // 지구 반지름 (미터)
            
            var lat1Rad = DegreesToRadians(lat1);
            var lat2Rad = DegreesToRadians(lat2);
            var deltaLat = DegreesToRadians(lat2 - lat1);
            var deltaLon = DegreesToRadians(lon2 - lon1);
            
            var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }
    }
}