using System.Net.Http;
using System.Drawing;
using System.Text.Json;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;
using GeomPoint = SpatialView.Engine.Geometry.Point;

namespace SpatialView.Engine.Web;

/// <summary>
/// 웹 맵 서비스 클라이언트
/// </summary>
public class WebMapClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WebMapOptions _options;
    private readonly Dictionary<string, Image> _tileCache;
    private readonly SemaphoreSlim _downloadSemaphore;
    
    public WebMapClient(WebMapOptions? options = null)
    {
        _options = options ?? new WebMapOptions();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
        
        if (_options.Timeout.HasValue)
            _httpClient.Timeout = _options.Timeout.Value;
        
        _tileCache = new Dictionary<string, Image>();
        _downloadSemaphore = new SemaphoreSlim(_options.MaxConcurrentDownloads);
    }
    
    /// <summary>
    /// 타일 이미지 가져오기
    /// </summary>
    public async Task<Image?> GetTileAsync(int x, int y, int zoom, TileProvider provider = TileProvider.OpenStreetMap)
    {
        var url = GetTileUrl(x, y, zoom, provider);
        var cacheKey = $"{provider}_{zoom}_{x}_{y}";
        
        // 캐시 확인
        lock (_tileCache)
        {
            if (_tileCache.TryGetValue(cacheKey, out var cachedImage))
                return cachedImage;
        }
        
        await _downloadSemaphore.WaitAsync();
        try
        {
            var imageData = await _httpClient.GetByteArrayAsync(url);
            using var ms = new MemoryStream(imageData);
            var image = Image.FromStream(ms);
            
            // 캐시에 저장
            if (_options.EnableCache)
            {
                lock (_tileCache)
                {
                    if (_tileCache.Count > _options.MaxCacheSize)
                    {
                        // 가장 오래된 항목 제거
                        var oldestKey = _tileCache.Keys.First();
                        _tileCache[oldestKey]?.Dispose();
                        _tileCache.Remove(oldestKey);
                    }
                    
                    _tileCache[cacheKey] = image;
                }
            }
            
            return image;
        }
        catch (Exception ex)
        {
            // 로깅 또는 예외 처리
            Console.WriteLine($"Failed to download tile: {ex.Message}");
            return null;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }
    
    /// <summary>
    /// GeoJSON 데이터 가져오기
    /// </summary>
    public async Task<FeatureCollection?> GetGeoJsonAsync(string url)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(url);
            return ParseGeoJson(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get GeoJSON: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 지오코딩 (주소를 좌표로 변환)
    /// </summary>
    public async Task<Coordinate?> GeocodeAsync(string address)
    {
        var encodedAddress = Uri.EscapeDataString(address);
        var url = $"https://nominatim.openstreetmap.org/search?q={encodedAddress}&format=json&limit=1";
        
        try
        {
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.GetArrayLength() > 0)
            {
                var result = root[0];
                var lat = double.Parse(result.GetProperty("lat").GetString()!);
                var lon = double.Parse(result.GetProperty("lon").GetString()!);
                
                return new Coordinate(lon, lat);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Geocoding failed: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// 역지오코딩 (좌표를 주소로 변환)
    /// </summary>
    public async Task<string?> ReverseGeocodeAsync(double longitude, double latitude)
    {
        var url = $"https://nominatim.openstreetmap.org/reverse?lat={latitude}&lon={longitude}&format=json";
        
        try
        {
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("display_name", out var displayName))
            {
                return displayName.GetString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Reverse geocoding failed: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// 경로 찾기
    /// </summary>
    public async Task<LineString?> GetRouteAsync(Coordinate start, Coordinate end, RouteProfile profile = RouteProfile.Driving)
    {
        var profileStr = profile switch
        {
            RouteProfile.Driving => "driving",
            RouteProfile.Walking => "foot",
            RouteProfile.Cycling => "bicycle",
            _ => "driving"
        };
        
        var url = $"https://router.project-osrm.org/route/v1/{profileStr}/{start.X},{start.Y};{end.X},{end.Y}?geometries=geojson";
        
        try
        {
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.GetProperty("code").GetString() == "Ok")
            {
                var route = root.GetProperty("routes")[0];
                var geometry = route.GetProperty("geometry");
                
                return ParseLineStringFromGeoJson(geometry);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Routing failed: {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// 타일 좌표를 지리 좌표로 변환
    /// </summary>
    public static Envelope TileToEnvelope(int x, int y, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var lonMin = x / n * 360.0 - 180.0;
        var lonMax = (x + 1) / n * 360.0 - 180.0;
        var latMax = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180.0 / Math.PI;
        var latMin = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (y + 1) / n))) * 180.0 / Math.PI;
        
        return new Envelope(lonMin, latMin, lonMax, latMax);
    }
    
    /// <summary>
    /// 지리 좌표를 타일 좌표로 변환
    /// </summary>
    public static (int x, int y) CoordinateToTile(double longitude, double latitude, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var x = (int)((longitude + 180.0) / 360.0 * n);
        var y = (int)((1.0 - Math.Log(Math.Tan(latitude * Math.PI / 180.0) + 
                      1.0 / Math.Cos(latitude * Math.PI / 180.0)) / Math.PI) / 2.0 * n);
        
        return (x, y);
    }
    
    /// <summary>
    /// 영역에 필요한 타일 목록 가져오기
    /// </summary>
    public static List<(int x, int y)> GetTilesForEnvelope(Envelope envelope, int zoom)
    {
        var tiles = new List<(int x, int y)>();
        
        var (minX, minY) = CoordinateToTile(envelope.MinX, envelope.MaxY, zoom);
        var (maxX, maxY) = CoordinateToTile(envelope.MaxX, envelope.MinY, zoom);
        
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                tiles.Add((x, y));
            }
        }
        
        return tiles;
    }
    
    private string GetTileUrl(int x, int y, int zoom, TileProvider provider)
    {
        return provider switch
        {
            TileProvider.OpenStreetMap => $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png",
            TileProvider.CartoDB => $"https://a.basemaps.cartocdn.com/light_all/{zoom}/{x}/{y}.png",
            TileProvider.Stamen => $"https://stamen-tiles.a.ssl.fastly.net/terrain/{zoom}/{x}/{y}.jpg",
            TileProvider.Custom => string.Format(_options.CustomTileUrlTemplate!, zoom, x, y),
            _ => throw new NotSupportedException($"Provider {provider} not supported")
        };
    }
    
    private FeatureCollection ParseGeoJson(string json)
    {
        var features = new List<Feature>();
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (root.GetProperty("type").GetString() == "FeatureCollection")
        {
            var featuresArray = root.GetProperty("features");
            
            foreach (var featureElement in featuresArray.EnumerateArray())
            {
                var feature = ParseGeoJsonFeature(featureElement);
                if (feature != null)
                    features.Add(feature);
            }
        }
        
        return new FeatureCollection(features);
    }
    
    private Feature? ParseGeoJsonFeature(JsonElement featureElement)
    {
        if (featureElement.GetProperty("type").GetString() != "Feature")
            return null;
        
        var geometryElement = featureElement.GetProperty("geometry");
        var geometry = ParseGeoJsonGeometry(geometryElement);
        
        if (geometry == null) return null;
        
        var properties = new AttributeTable();
        if (featureElement.TryGetProperty("properties", out var propertiesElement))
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                properties[property.Name] = property.Value.GetString();
            }
        }
        
        var id = featureElement.TryGetProperty("id", out var idElement) ? 
            idElement.GetString() : Guid.NewGuid().ToString();
        
        return new Feature(id!, geometry, properties);
    }
    
    private IGeometry? ParseGeoJsonGeometry(JsonElement geometryElement)
    {
        var type = geometryElement.GetProperty("type").GetString();
        var coordinates = geometryElement.GetProperty("coordinates");
        
        return type switch
        {
            "Point" => ParsePoint(coordinates),
            "LineString" => ParseLineString(coordinates),
            "Polygon" => ParsePolygon(coordinates),
            "MultiPoint" => ParseMultiPoint(coordinates),
            "MultiLineString" => ParseMultiLineString(coordinates),
            "MultiPolygon" => ParseMultiPolygon(coordinates),
            _ => null
        };
    }
    
    private GeomPoint ParsePoint(JsonElement coordinates)
    {
        var x = coordinates[0].GetDouble();
        var y = coordinates[1].GetDouble();
        return new GeomPoint(x, y);
    }
    
    private LineString ParseLineString(JsonElement coordinates)
    {
        var points = new List<Coordinate>();
        foreach (var coord in coordinates.EnumerateArray())
        {
            var x = coord[0].GetDouble();
            var y = coord[1].GetDouble();
            points.Add(new Coordinate(x, y));
        }
        return new LineString(points.ToArray());
    }
    
    private LineString? ParseLineStringFromGeoJson(JsonElement geometry)
    {
        if (geometry.GetProperty("type").GetString() == "LineString")
        {
            return ParseLineString(geometry.GetProperty("coordinates"));
        }
        return null;
    }
    
    private Polygon ParsePolygon(JsonElement coordinates)
    {
        var rings = new List<LinearRing>();
        foreach (var ring in coordinates.EnumerateArray())
        {
            var points = new List<Coordinate>();
            foreach (var coord in ring.EnumerateArray())
            {
                var x = coord[0].GetDouble();
                var y = coord[1].GetDouble();
                points.Add(new Coordinate(x, y));
            }
            rings.Add(new LinearRing(points.ToArray()));
        }
        
        return new Polygon(rings[0], rings.Skip(1).ToArray());
    }
    
    private MultiPoint ParseMultiPoint(JsonElement coordinates)
    {
        var points = new List<GeomPoint>();
        foreach (var coord in coordinates.EnumerateArray())
        {
            var x = coord[0].GetDouble();
            var y = coord[1].GetDouble();
            points.Add(new GeomPoint(x, y));
        }
        return new MultiPoint(points.ToArray());
    }
    
    private MultiLineString ParseMultiLineString(JsonElement coordinates)
    {
        var lineStrings = new List<LineString>();
        foreach (var line in coordinates.EnumerateArray())
        {
            var points = new List<Coordinate>();
            foreach (var coord in line.EnumerateArray())
            {
                var x = coord[0].GetDouble();
                var y = coord[1].GetDouble();
                points.Add(new Coordinate(x, y));
            }
            lineStrings.Add(new LineString(points.ToArray()));
        }
        return new MultiLineString(lineStrings.ToArray());
    }
    
    private MultiPolygon ParseMultiPolygon(JsonElement coordinates)
    {
        var polygons = new List<Polygon>();
        foreach (var polygon in coordinates.EnumerateArray())
        {
            var rings = new List<LinearRing>();
            foreach (var ring in polygon.EnumerateArray())
            {
                var points = new List<Coordinate>();
                foreach (var coord in ring.EnumerateArray())
                {
                    var x = coord[0].GetDouble();
                    var y = coord[1].GetDouble();
                    points.Add(new Coordinate(x, y));
                }
                rings.Add(new LinearRing(points.ToArray()));
            }
            polygons.Add(new Polygon(rings[0], rings.Skip(1).ToArray()));
        }
        return new MultiPolygon(polygons.ToArray());
    }
    
    public void ClearCache()
    {
        lock (_tileCache)
        {
            foreach (var image in _tileCache.Values)
            {
                image?.Dispose();
            }
            _tileCache.Clear();
        }
    }
    
    public void Dispose()
    {
        ClearCache();
        _httpClient?.Dispose();
        _downloadSemaphore?.Dispose();
    }
}

/// <summary>
/// 웹 맵 옵션
/// </summary>
public class WebMapOptions
{
    /// <summary>
    /// 사용자 에이전트
    /// </summary>
    public string UserAgent { get; set; } = "SpatialView.Engine/1.0";
    
    /// <summary>
    /// 타임아웃
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 캐시 활성화
    /// </summary>
    public bool EnableCache { get; set; } = true;
    
    /// <summary>
    /// 최대 캐시 크기 (타일 수)
    /// </summary>
    public int MaxCacheSize { get; set; } = 100;
    
    /// <summary>
    /// 최대 동시 다운로드 수
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 4;
    
    /// <summary>
    /// 사용자 정의 타일 URL 템플릿
    /// </summary>
    public string? CustomTileUrlTemplate { get; set; }
}

/// <summary>
/// 타일 제공자
/// </summary>
public enum TileProvider
{
    OpenStreetMap,
    CartoDB,
    Stamen,
    Custom
}

/// <summary>
/// 경로 프로필
/// </summary>
public enum RouteProfile
{
    Driving,
    Walking,
    Cycling
}