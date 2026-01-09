using System.Net.Http;
using System.Drawing;
using System.Xml.Linq;
using System.Globalization;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.CoordinateSystems;

namespace SpatialView.Engine.Web;

/// <summary>
/// WMS (Web Map Service) 클라이언트
/// </summary>
public class WmsClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WmsClientOptions _options;
    private WmsCapabilities? _capabilities;
    
    public WmsCapabilities? Capabilities => _capabilities;
    
    public WmsClient(string serviceUrl, WmsClientOptions? options = null)
    {
        _options = options ?? new WmsClientOptions();
        _httpClient = new HttpClient { BaseAddress = new Uri(serviceUrl) };
        
        if (_options.Timeout.HasValue)
            _httpClient.Timeout = _options.Timeout.Value;
    }
    
    /// <summary>
    /// GetCapabilities 요청
    /// </summary>
    public async Task<WmsCapabilities> GetCapabilitiesAsync()
    {
        var url = BuildUrl(new Dictionary<string, string>
        {
            ["SERVICE"] = "WMS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetCapabilities"
        });
        
        var response = await _httpClient.GetStringAsync(url);
        _capabilities = ParseCapabilities(response);
        return _capabilities;
    }
    
    /// <summary>
    /// GetMap 요청 - 맵 이미지 가져오기
    /// </summary>
    public async Task<Image?> GetMapAsync(GetMapRequest request)
    {
        var parameters = new Dictionary<string, string>
        {
            ["SERVICE"] = "WMS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetMap",
            ["LAYERS"] = string.Join(",", request.Layers),
            ["STYLES"] = string.Join(",", request.Styles ?? Enumerable.Repeat("", request.Layers.Count())),
            ["CRS"] = request.Crs ?? "EPSG:4326",
            ["BBOX"] = FormatBBox(request.BoundingBox),
            ["WIDTH"] = request.Width.ToString(),
            ["HEIGHT"] = request.Height.ToString(),
            ["FORMAT"] = request.Format ?? "image/png",
            ["TRANSPARENT"] = request.Transparent ? "TRUE" : "FALSE"
        };
        
        if (request.BackgroundColor != null)
            parameters["BGCOLOR"] = ColorToHex(request.BackgroundColor.Value);
        
        if (!string.IsNullOrEmpty(request.Exceptions))
            parameters["EXCEPTIONS"] = request.Exceptions;
        
        var url = BuildUrl(parameters);
        
        try
        {
            var imageData = await _httpClient.GetByteArrayAsync(url);
            using var ms = new MemoryStream(imageData);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// GetFeatureInfo 요청 - 피처 정보 조회
    /// </summary>
    public async Task<string?> GetFeatureInfoAsync(GetFeatureInfoRequest request)
    {
        var parameters = new Dictionary<string, string>
        {
            ["SERVICE"] = "WMS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetFeatureInfo",
            ["LAYERS"] = string.Join(",", request.QueryLayers),
            ["STYLES"] = "",
            ["CRS"] = request.Crs ?? "EPSG:4326",
            ["BBOX"] = FormatBBox(request.BoundingBox),
            ["WIDTH"] = request.Width.ToString(),
            ["HEIGHT"] = request.Height.ToString(),
            ["QUERY_LAYERS"] = string.Join(",", request.QueryLayers),
            ["INFO_FORMAT"] = request.InfoFormat ?? "text/xml",
            ["I"] = request.X.ToString(), // WMS 1.3.0에서는 I/J
            ["J"] = request.Y.ToString(),
            ["FEATURE_COUNT"] = request.FeatureCount.ToString()
        };
        
        var url = BuildUrl(parameters);
        
        try
        {
            return await _httpClient.GetStringAsync(url);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// GetLegendGraphic 요청 - 범례 이미지 가져오기
    /// </summary>
    public async Task<Image?> GetLegendGraphicAsync(string layer, 
        int width = 20, int height = 20, string format = "image/png")
    {
        var parameters = new Dictionary<string, string>
        {
            ["SERVICE"] = "WMS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetLegendGraphic",
            ["LAYER"] = layer,
            ["FORMAT"] = format,
            ["WIDTH"] = width.ToString(),
            ["HEIGHT"] = height.ToString()
        };
        
        var url = BuildUrl(parameters);
        
        try
        {
            var imageData = await _httpClient.GetByteArrayAsync(url);
            using var ms = new MemoryStream(imageData);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }
    
    private WmsCapabilities ParseCapabilities(string xml)
    {
        var doc = XDocument.Parse(xml);
        var capabilities = new WmsCapabilities();
        
        // 서비스 정보
        var service = doc.Descendants("Service").FirstOrDefault();
        if (service != null)
        {
            capabilities.ServiceName = service.Element("Name")?.Value;
            capabilities.ServiceTitle = service.Element("Title")?.Value;
            capabilities.ServiceAbstract = service.Element("Abstract")?.Value;
        }
        
        // 레이어 정보
        var capability = doc.Descendants("Capability").FirstOrDefault();
        if (capability != null)
        {
            var rootLayer = capability.Element("Layer");
            if (rootLayer != null)
            {
                capabilities.Layers = ParseLayers(rootLayer);
            }
            
            // 지원 형식
            var getMap = capability.Descendants("GetMap").FirstOrDefault();
            if (getMap != null)
            {
                capabilities.Formats = getMap.Descendants("Format")
                    .Select(f => f.Value)
                    .ToList();
            }
        }
        
        return capabilities;
    }
    
    private List<WmsLayer> ParseLayers(XElement layerElement)
    {
        var layers = new List<WmsLayer>();
        
        // 현재 레이어
        var layer = new WmsLayer
        {
            Name = layerElement.Element("Name")?.Value,
            Title = layerElement.Element("Title")?.Value,
            Abstract = layerElement.Element("Abstract")?.Value,
            Queryable = bool.Parse(layerElement.Attribute("queryable")?.Value ?? "false")
        };
        
        // CRS/SRS
        var crsElements = layerElement.Elements("CRS")
            .Concat(layerElement.Elements("SRS"));
        layer.SupportedCrs = crsElements.Select(e => e.Value).ToList();
        
        // BoundingBox
        var bbox = layerElement.Elements("BoundingBox").FirstOrDefault();
        if (bbox != null)
        {
            layer.BoundingBox = new Envelope(
                double.Parse(bbox.Attribute("minx")?.Value ?? "0", CultureInfo.InvariantCulture),
                double.Parse(bbox.Attribute("miny")?.Value ?? "0", CultureInfo.InvariantCulture),
                double.Parse(bbox.Attribute("maxx")?.Value ?? "0", CultureInfo.InvariantCulture),
                double.Parse(bbox.Attribute("maxy")?.Value ?? "0", CultureInfo.InvariantCulture)
            );
        }
        
        if (!string.IsNullOrEmpty(layer.Name))
            layers.Add(layer);
        
        // 하위 레이어
        foreach (var childLayer in layerElement.Elements("Layer"))
        {
            layers.AddRange(ParseLayers(childLayer));
        }
        
        return layers;
    }
    
    private string BuildUrl(Dictionary<string, string> parameters)
    {
        var queryString = string.Join("&", parameters.Select(kvp => 
            $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        
        var baseUrl = _httpClient.BaseAddress!.ToString();
        var separator = baseUrl.Contains("?") ? "&" : "?";
        
        return $"{baseUrl}{separator}{queryString}";
    }
    
    private string FormatBBox(Envelope bbox)
    {
        // WMS 1.3.0에서는 CRS에 따라 축 순서가 다름
        var culture = CultureInfo.InvariantCulture;
        return $"{bbox.MinX.ToString(culture)},{bbox.MinY.ToString(culture)}," +
               $"{bbox.MaxX.ToString(culture)},{bbox.MaxY.ToString(culture)}";
    }
    
    private string ColorToHex(Color color)
    {
        return $"0x{color.R:X2}{color.G:X2}{color.B:X2}";
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// WMS 클라이언트 옵션
/// </summary>
public class WmsClientOptions
{
    public string Version { get; set; } = "1.3.0";
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// WMS Capabilities
/// </summary>
public class WmsCapabilities
{
    public string? ServiceName { get; set; }
    public string? ServiceTitle { get; set; }
    public string? ServiceAbstract { get; set; }
    public List<WmsLayer> Layers { get; set; } = new();
    public List<string> Formats { get; set; } = new();
}

/// <summary>
/// WMS 레이어
/// </summary>
public class WmsLayer
{
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Abstract { get; set; }
    public bool Queryable { get; set; }
    public Envelope? BoundingBox { get; set; }
    public List<string> SupportedCrs { get; set; } = new();
}

/// <summary>
/// GetMap 요청
/// </summary>
public class GetMapRequest
{
    public IEnumerable<string> Layers { get; set; } = new List<string>();
    public IEnumerable<string>? Styles { get; set; }
    public string? Crs { get; set; } = "EPSG:4326";
    public Envelope BoundingBox { get; set; } = null!;
    public int Width { get; set; } = 256;
    public int Height { get; set; } = 256;
    public string? Format { get; set; } = "image/png";
    public bool Transparent { get; set; } = true;
    public Color? BackgroundColor { get; set; }
    public string? Exceptions { get; set; } = "XML";
}

/// <summary>
/// GetFeatureInfo 요청
/// </summary>
public class GetFeatureInfoRequest
{
    public IEnumerable<string> QueryLayers { get; set; } = new List<string>();
    public string? Crs { get; set; } = "EPSG:4326";
    public Envelope BoundingBox { get; set; } = null!;
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string? InfoFormat { get; set; } = "text/xml";
    public int FeatureCount { get; set; } = 1;
}