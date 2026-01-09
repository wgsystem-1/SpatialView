namespace SpatialView.Engine.CoordinateSystems;

/// <summary>
/// 좌표계 팩토리
/// </summary>
public class CoordinateSystemFactory
{
    
    /// <summary>
    /// WKT에서 좌표계 생성
    /// </summary>
    public ICoordinateSystem CreateFromWkt(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new ArgumentException("WKT cannot be null or empty", nameof(wkt));
        
        // 간단한 파싱을 통해 좌표계 타입 결정
        if (wkt.Contains("GEOGCS", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: WKT에서 실제 값을 파싱해야 함
            // 현재는 기본값 사용
            var angularUnit = new AngularUnit("Degree", 0.0174532925199433);
            var ellipsoid = new Ellipsoid("WGS 84", 6378137, 298.257223563, "EPSG", 7030);
            var primeMeridian = new PrimeMeridian("Greenwich", 0, "EPSG", 8901);
            
            return new GeographicCoordinateSystem(
                ExtractName(wkt),
                angularUnit,
                ellipsoid,
                primeMeridian,
                "EPSG",
                4326
            );
        }
        else if (wkt.Contains("PROJCS", StringComparison.OrdinalIgnoreCase))
        {
            // TODO: WKT에서 실제 값을 파싱해야 함
            // 현재는 기본값 사용
            var angularUnit = new AngularUnit("Degree", 0.0174532925199433);
            var ellipsoid = new Ellipsoid("WGS 84", 6378137, 298.257223563, "EPSG", 7030);
            var primeMeridian = new PrimeMeridian("Greenwich", 0, "EPSG", 8901);
            var geoCS = new GeographicCoordinateSystem("WGS 84", angularUnit, ellipsoid, primeMeridian, "EPSG", 4326);
            var linearUnit = new LinearUnit("Meter", 1.0);
            var projection = new Projection("Transverse_Mercator", "Transverse_Mercator", new Dictionary<string, double>());
            
            return new ProjectedCoordinateSystem(
                ExtractName(wkt),
                geoCS,
                linearUnit,
                projection,
                "EPSG",
                0
            );
        }
        else
        {
            throw new NotSupportedException($"Unsupported coordinate system type in WKT: {wkt}");
        }
    }
    
    /// <summary>
    /// EPSG 코드에서 좌표계 생성
    /// </summary>
    public ICoordinateSystem CreateFromEPSG(int epsgCode)
    {
        var entry = EPSGDatabase.Instance.GetBySRID(epsgCode);
        if (entry == null)
        {
            throw new ArgumentException($"Unknown EPSG code: {epsgCode}");
        }
        
        return CreateFromWkt(entry.WKT);
    }
    
    /// <summary>
    /// SRID에서 좌표계 생성 (CreateFromEPSG와 동일)
    /// </summary>
    public ICoordinateSystem CreateFromSRID(int srid)
    {
        return CreateFromEPSG(srid);
    }
    
    /// <summary>
    /// WKT에서 좌표계 생성 (CreateFromWkt와 동일, 호환성용)
    /// </summary>
    public ICoordinateSystem CreateFromWKT(string wkt)
    {
        return CreateFromWkt(wkt);
    }
    
    /// <summary>
    /// WKT에서 이름 추출
    /// </summary>
    private string ExtractName(string wkt)
    {
        var start = wkt.IndexOf('"');
        if (start < 0) return "Unknown";
        
        var end = wkt.IndexOf('"', start + 1);
        if (end < 0) return "Unknown";
        
        return wkt.Substring(start + 1, end - start - 1);
    }
}