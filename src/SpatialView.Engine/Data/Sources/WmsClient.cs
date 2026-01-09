using SpatialView.Engine.Geometry;
using System.Net.Http;
using System.Text;
using System.Xml.Linq;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// WMS (Web Map Service) 클라이언트
/// OGC WMS 1.1.1 및 1.3.0 지원
/// </summary>
public class WmsClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _version;
    private bool _disposed = false;

    /// <summary>
    /// 요청 타임아웃 (밀리초)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 사용자 정의 헤더
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; } = new();

    /// <summary>
    /// 프록시 설정
    /// </summary>
    public string? ProxyUrl { get; set; }

    public WmsClient(string baseUrl, string version = "1.3.0")
    {
        _baseUrl = baseUrl?.TrimEnd('?', '&') ?? throw new ArgumentNullException(nameof(baseUrl));
        _version = version ?? throw new ArgumentNullException(nameof(version));
        
        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(ProxyUrl))
        {
            handler.Proxy = new System.Net.WebProxy(ProxyUrl);
        }
        
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
        };

        // 기본 헤더 설정
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpatialView GIS Engine WMS Client");
    }

    /// <summary>
    /// GetCapabilities 요청
    /// </summary>
    public async Task<WmsCapabilities?> GetCapabilitiesAsync()
    {
        try
        {
            var url = BuildGetCapabilitiesUrl();
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var xml = await response.Content.ReadAsStringAsync();
            return ParseCapabilities(xml);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCapabilities failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// GetMap 요청
    /// </summary>
    public async Task<byte[]?> GetMapAsync(WmsGetMapRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        try
        {
            var url = BuildGetMapUrl(request);
            
            foreach (var header in CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.Remove(header.Key);
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType?.StartsWith("image/") == true)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                // 오류 응답일 가능성
                var errorText = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"WMS Error Response: {errorText}");
                return null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetMap failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// GetFeatureInfo 요청
    /// </summary>
    public async Task<string?> GetFeatureInfoAsync(WmsGetFeatureInfoRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        try
        {
            var url = BuildGetFeatureInfoUrl(request);
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFeatureInfo failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 비동기 타일 다운로드 (여러 타일)
    /// </summary>
    public async Task<Dictionary<string, byte[]>> GetMultipleMapTilesAsync(
        IEnumerable<WmsGetMapRequest> requests,
        int maxConcurrency = 4)
    {
        var results = new Dictionary<string, byte[]>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync();
            try
            {
                var tileData = await GetMapAsync(request);
                if (tileData != null)
                {
                    var key = $"{request.BoundingBox.MinX},{request.BoundingBox.MinY},{request.BoundingBox.MaxX},{request.BoundingBox.MaxY}";
                    lock (results)
                    {
                        results[key] = tileData;
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    #region Private Methods

    private string BuildGetCapabilitiesUrl()
    {
        var sb = new StringBuilder(_baseUrl);
        sb.Append(_baseUrl.Contains('?') ? "&" : "?");
        sb.Append("SERVICE=WMS");
        sb.Append("&REQUEST=GetCapabilities");
        sb.Append($"&VERSION={_version}");
        
        return sb.ToString();
    }

    private string BuildGetMapUrl(WmsGetMapRequest request)
    {
        var sb = new StringBuilder(_baseUrl);
        sb.Append(_baseUrl.Contains('?') ? "&" : "?");
        sb.Append("SERVICE=WMS");
        sb.Append("&REQUEST=GetMap");
        sb.Append($"&VERSION={_version}");
        sb.Append($"&LAYERS={string.Join(",", request.Layers)}");
        sb.Append($"&STYLES={string.Join(",", request.Styles)}");
        sb.Append($"&FORMAT={request.Format}");
        sb.Append($"&WIDTH={request.Width}");
        sb.Append($"&HEIGHT={request.Height}");
        
        // 좌표계 설정 (버전에 따라 다름)
        if (_version == "1.3.0")
        {
            sb.Append($"&CRS={request.SRS}");
            // 1.3.0에서는 일부 좌표계의 경우 축 순서가 바뀜
            if (IsAxisOrderFlipped(request.SRS))
            {
                sb.Append($"&BBOX={request.BoundingBox.MinY},{request.BoundingBox.MinX},{request.BoundingBox.MaxY},{request.BoundingBox.MaxX}");
            }
            else
            {
                sb.Append($"&BBOX={request.BoundingBox.MinX},{request.BoundingBox.MinY},{request.BoundingBox.MaxX},{request.BoundingBox.MaxY}");
            }
        }
        else
        {
            sb.Append($"&SRS={request.SRS}");
            sb.Append($"&BBOX={request.BoundingBox.MinX},{request.BoundingBox.MinY},{request.BoundingBox.MaxX},{request.BoundingBox.MaxY}");
        }

        // 선택적 매개변수들
        if (request.Transparent.HasValue)
        {
            sb.Append($"&TRANSPARENT={request.Transparent.Value.ToString().ToUpper()}");
        }

        if (!string.IsNullOrEmpty(request.BackgroundColor))
        {
            sb.Append($"&BGCOLOR={request.BackgroundColor}");
        }

        if (request.Time != null)
        {
            sb.Append($"&TIME={request.Time}");
        }

        if (request.Elevation != null)
        {
            sb.Append($"&ELEVATION={request.Elevation}");
        }

        // 커스텀 매개변수들
        foreach (var param in request.CustomParameters)
        {
            sb.Append($"&{param.Key}={param.Value}");
        }

        return sb.ToString();
    }

    private string BuildGetFeatureInfoUrl(WmsGetFeatureInfoRequest request)
    {
        var sb = new StringBuilder(BuildGetMapUrl(request.GetMapRequest));
        sb.Append("&REQUEST=GetFeatureInfo");
        sb.Append($"&INFO_FORMAT={request.InfoFormat}");
        sb.Append($"&I={request.X}"); // 픽셀 X 좌표
        sb.Append($"&J={request.Y}"); // 픽셀 Y 좌표
        sb.Append($"&QUERY_LAYERS={string.Join(",", request.QueryLayers)}");

        if (request.FeatureCount.HasValue)
        {
            sb.Append($"&FEATURE_COUNT={request.FeatureCount.Value}");
        }

        return sb.ToString();
    }

    private static bool IsAxisOrderFlipped(string srs)
    {
        // WMS 1.3.0에서 축 순서가 뒤바뀌는 좌표계들
        var flippedSrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EPSG:4326", "EPSG:4269", "EPSG:4267", "EPSG:4152", "EPSG:4150"
        };

        return flippedSrs.Contains(srs);
    }

    private static WmsCapabilities? ParseCapabilities(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            
            if (root == null) return null;

            var ns = root.GetDefaultNamespace();
            var capabilities = new WmsCapabilities();

            // 서비스 정보
            var service = root.Element(ns + "Service");
            if (service != null)
            {
                capabilities.ServiceInfo = ParseServiceInfo(service, ns);
            }

            // 기능 정보
            var capability = root.Element(ns + "Capability");
            if (capability != null)
            {
                capabilities.Capability = ParseCapability(capability, ns);
            }

            return capabilities;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Parse capabilities failed: {ex.Message}");
            return null;
        }
    }

    private static WmsServiceInfo ParseServiceInfo(XElement service, XNamespace ns)
    {
        return new WmsServiceInfo
        {
            Name = service.Element(ns + "Name")?.Value ?? string.Empty,
            Title = service.Element(ns + "Title")?.Value ?? string.Empty,
            Abstract = service.Element(ns + "Abstract")?.Value,
            Keywords = service.Element(ns + "KeywordList")?.Elements(ns + "Keyword")
                .Select(k => k.Value).ToList() ?? new List<string>(),
            OnlineResource = service.Element(ns + "OnlineResource")?.Attribute("href")?.Value,
            ContactInformation = ParseContactInfo(service.Element(ns + "ContactInformation"), ns),
            Fees = service.Element(ns + "Fees")?.Value,
            AccessConstraints = service.Element(ns + "AccessConstraints")?.Value
        };
    }

    private static WmsContactInfo? ParseContactInfo(XElement? contact, XNamespace ns)
    {
        if (contact == null) return null;

        var person = contact.Element(ns + "ContactPersonPrimary");
        var address = contact.Element(ns + "ContactAddress");

        return new WmsContactInfo
        {
            Person = person?.Element(ns + "ContactPerson")?.Value,
            Organization = person?.Element(ns + "ContactOrganization")?.Value,
            Position = contact.Element(ns + "ContactPosition")?.Value,
            AddressType = address?.Element(ns + "AddressType")?.Value,
            Address = address?.Element(ns + "Address")?.Value,
            City = address?.Element(ns + "City")?.Value,
            StateOrProvince = address?.Element(ns + "StateOrProvince")?.Value,
            PostCode = address?.Element(ns + "PostCode")?.Value,
            Country = address?.Element(ns + "Country")?.Value,
            VoiceTelephone = contact.Element(ns + "ContactVoiceTelephone")?.Value,
            FacsimileTelephone = contact.Element(ns + "ContactFacsimileTelephone")?.Value,
            EmailAddress = contact.Element(ns + "ContactElectronicMailAddress")?.Value
        };
    }

    private static WmsCapability ParseCapability(XElement capability, XNamespace ns)
    {
        var result = new WmsCapability();

        // Request 정보
        var request = capability.Element(ns + "Request");
        if (request != null)
        {
            result.Requests = ParseRequests(request, ns);
        }

        // Layer 정보
        var rootLayer = capability.Element(ns + "Layer");
        if (rootLayer != null)
        {
            result.RootLayer = ParseLayer(rootLayer, ns);
        }

        return result;
    }

    private static Dictionary<string, WmsRequestInfo> ParseRequests(XElement request, XNamespace ns)
    {
        var requests = new Dictionary<string, WmsRequestInfo>();

        foreach (var req in request.Elements())
        {
            var requestInfo = new WmsRequestInfo
            {
                Formats = req.Elements(ns + "Format").Select(f => f.Value).ToList()
            };

            var dcpType = req.Element(ns + "DCPType");
            var http = dcpType?.Element(ns + "HTTP");
            var get = http?.Element(ns + "Get");
            var onlineResource = get?.Element(ns + "OnlineResource");

            requestInfo.GetUrl = onlineResource?.Attribute("href")?.Value;

            requests[req.Name.LocalName] = requestInfo;
        }

        return requests;
    }

    private static WmsLayer ParseLayer(XElement layer, XNamespace ns)
    {
        var result = new WmsLayer
        {
            Name = layer.Element(ns + "Name")?.Value,
            Title = layer.Element(ns + "Title")?.Value ?? string.Empty,
            Abstract = layer.Element(ns + "Abstract")?.Value,
            Queryable = bool.TryParse(layer.Attribute("queryable")?.Value, out var q) ? q : false,
            Opaque = bool.TryParse(layer.Attribute("opaque")?.Value, out var o) ? o : false,
            NoSubsets = bool.TryParse(layer.Attribute("noSubsets")?.Value, out var ns_val) ? ns_val : false,
            FixedWidth = int.TryParse(layer.Attribute("fixedWidth")?.Value, out var fw) ? fw : null,
            FixedHeight = int.TryParse(layer.Attribute("fixedHeight")?.Value, out var fh) ? fh : null
        };

        // Keywords
        result.Keywords = layer.Element(ns + "KeywordList")?.Elements(ns + "Keyword")
            .Select(k => k.Value).ToList() ?? new List<string>();

        // SRS/CRS
        result.SRS = layer.Elements(ns + "SRS").Concat(layer.Elements(ns + "CRS"))
            .Select(s => s.Value).ToList();

        // BoundingBox
        var bbox = layer.Element(ns + "LatLonBoundingBox") ?? layer.Element(ns + "EX_GeographicBoundingBox");
        if (bbox != null)
        {
            result.LatLonBoundingBox = ParseBoundingBox(bbox, ns);
        }

        // Styles
        result.Styles = layer.Elements(ns + "Style").Select(s => ParseStyle(s, ns)).ToList();

        // 중첩된 레이어들
        result.SubLayers = layer.Elements(ns + "Layer").Select(l => ParseLayer(l, ns)).ToList();

        return result;
    }

    private static Envelope? ParseBoundingBox(XElement bbox, XNamespace ns)
    {
        try
        {
            var minX = double.Parse(bbox.Attribute("minx")?.Value ?? bbox.Element(ns + "westBoundLongitude")?.Value ?? "0");
            var minY = double.Parse(bbox.Attribute("miny")?.Value ?? bbox.Element(ns + "southBoundLatitude")?.Value ?? "0");
            var maxX = double.Parse(bbox.Attribute("maxx")?.Value ?? bbox.Element(ns + "eastBoundLongitude")?.Value ?? "0");
            var maxY = double.Parse(bbox.Attribute("maxy")?.Value ?? bbox.Element(ns + "northBoundLatitude")?.Value ?? "0");

            return new Envelope(minX, minY, maxX, maxY);
        }
        catch
        {
            return null;
        }
    }

    private static WmsStyle ParseStyle(XElement style, XNamespace ns)
    {
        return new WmsStyle
        {
            Name = style.Element(ns + "Name")?.Value ?? string.Empty,
            Title = style.Element(ns + "Title")?.Value ?? string.Empty,
            Abstract = style.Element(ns + "Abstract")?.Value,
            LegendUrl = style.Element(ns + "LegendURL")?.Element(ns + "OnlineResource")?.Attribute("href")?.Value
        };
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

#region Request Classes

/// <summary>
/// GetMap 요청 매개변수
/// </summary>
public class WmsGetMapRequest
{
    public List<string> Layers { get; set; } = new();
    public List<string> Styles { get; set; } = new();
    public string SRS { get; set; } = "EPSG:4326";
    public Envelope BoundingBox { get; set; } = new();
    public int Width { get; set; } = 256;
    public int Height { get; set; } = 256;
    public string Format { get; set; } = "image/png";
    public bool? Transparent { get; set; }
    public string? BackgroundColor { get; set; }
    public string? Time { get; set; }
    public string? Elevation { get; set; }
    public Dictionary<string, string> CustomParameters { get; set; } = new();
}

/// <summary>
/// GetFeatureInfo 요청 매개변수
/// </summary>
public class WmsGetFeatureInfoRequest
{
    public WmsGetMapRequest GetMapRequest { get; set; } = new();
    public List<string> QueryLayers { get; set; } = new();
    public string InfoFormat { get; set; } = "text/plain";
    public int X { get; set; }
    public int Y { get; set; }
    public int? FeatureCount { get; set; }
}

#endregion

#region Response Classes

/// <summary>
/// WMS Capabilities 응답
/// </summary>
public class WmsCapabilities
{
    public WmsServiceInfo? ServiceInfo { get; set; }
    public WmsCapability? Capability { get; set; }
}

/// <summary>
/// WMS 서비스 정보
/// </summary>
public class WmsServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string? OnlineResource { get; set; }
    public WmsContactInfo? ContactInformation { get; set; }
    public string? Fees { get; set; }
    public string? AccessConstraints { get; set; }
}

/// <summary>
/// WMS 연락처 정보
/// </summary>
public class WmsContactInfo
{
    public string? Person { get; set; }
    public string? Organization { get; set; }
    public string? Position { get; set; }
    public string? AddressType { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? StateOrProvince { get; set; }
    public string? PostCode { get; set; }
    public string? Country { get; set; }
    public string? VoiceTelephone { get; set; }
    public string? FacsimileTelephone { get; set; }
    public string? EmailAddress { get; set; }
}

/// <summary>
/// WMS 기능 정보
/// </summary>
public class WmsCapability
{
    public Dictionary<string, WmsRequestInfo> Requests { get; set; } = new();
    public WmsLayer? RootLayer { get; set; }
}

/// <summary>
/// WMS 요청 정보
/// </summary>
public class WmsRequestInfo
{
    public List<string> Formats { get; set; } = new();
    public string? GetUrl { get; set; }
    public string? PostUrl { get; set; }
}

/// <summary>
/// WMS 레이어 정보
/// </summary>
public class WmsLayer
{
    public string? Name { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> SRS { get; set; } = new();
    public Envelope? LatLonBoundingBox { get; set; }
    public bool Queryable { get; set; }
    public bool Opaque { get; set; }
    public bool NoSubsets { get; set; }
    public int? FixedWidth { get; set; }
    public int? FixedHeight { get; set; }
    public List<WmsStyle> Styles { get; set; } = new();
    public List<WmsLayer> SubLayers { get; set; } = new();
}

/// <summary>
/// WMS 스타일 정보
/// </summary>
public class WmsStyle
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public string? LegendUrl { get; set; }
}

#endregion