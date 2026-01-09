using System.Net.Http;
using System.Xml.Linq;
using System.Text;
using System.Globalization;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Engine.Web;

/// <summary>
/// WFS (Web Feature Service) 클라이언트
/// </summary>
public class WfsClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WfsClientOptions _options;
    private WfsCapabilities? _capabilities;
    
    public WfsCapabilities? Capabilities => _capabilities;
    
    public WfsClient(string serviceUrl, WfsClientOptions? options = null)
    {
        _options = options ?? new WfsClientOptions();
        _httpClient = new HttpClient { BaseAddress = new Uri(serviceUrl) };
        
        if (_options.Timeout.HasValue)
            _httpClient.Timeout = _options.Timeout.Value;
    }
    
    /// <summary>
    /// GetCapabilities 요청
    /// </summary>
    public async Task<WfsCapabilities> GetCapabilitiesAsync()
    {
        var url = BuildUrl(new Dictionary<string, string>
        {
            ["SERVICE"] = "WFS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetCapabilities"
        });
        
        var response = await _httpClient.GetStringAsync(url);
        _capabilities = ParseCapabilities(response);
        return _capabilities;
    }
    
    /// <summary>
    /// GetFeature 요청 - 피처 가져오기
    /// </summary>
    public async Task<FeatureCollection?> GetFeaturesAsync(GetFeatureRequest request)
    {
        var parameters = new Dictionary<string, string>
        {
            ["SERVICE"] = "WFS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetFeature",
            ["TYPENAME"] = string.Join(",", request.TypeNames),
            ["OUTPUTFORMAT"] = request.OutputFormat ?? "application/gml+xml; version=3.2"
        };
        
        if (request.MaxFeatures.HasValue)
            parameters["MAXFEATURES"] = request.MaxFeatures.Value.ToString();
        
        if (!string.IsNullOrEmpty(request.Filter))
            parameters["FILTER"] = request.Filter;
        
        if (request.BoundingBox != null)
            parameters["BBOX"] = FormatBBox(request.BoundingBox);
        
        if (!string.IsNullOrEmpty(request.SrsName))
            parameters["SRSNAME"] = request.SrsName;
        
        var url = BuildUrl(parameters);
        
        try
        {
            var response = await _httpClient.GetStringAsync(url);
            return ParseFeatures(response);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// DescribeFeatureType 요청 - 피처 타입 스키마 조회
    /// </summary>
    public async Task<FeatureTypeSchema?> DescribeFeatureTypeAsync(string typeName)
    {
        var parameters = new Dictionary<string, string>
        {
            ["SERVICE"] = "WFS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "DescribeFeatureType",
            ["TYPENAME"] = typeName
        };
        
        var url = BuildUrl(parameters);
        
        try
        {
            var response = await _httpClient.GetStringAsync(url);
            return ParseFeatureTypeSchema(response);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Transaction 요청 - 피처 추가/수정/삭제
    /// </summary>
    public async Task<TransactionResponse?> TransactionAsync(WfsTransaction transaction)
    {
        var xml = BuildTransactionXml(transaction);
        
        var content = new StringContent(xml, Encoding.UTF8, "text/xml");
        var response = await _httpClient.PostAsync("", content);
        
        if (response.IsSuccessStatusCode)
        {
            var responseXml = await response.Content.ReadAsStringAsync();
            return ParseTransactionResponse(responseXml);
        }
        
        return null;
    }
    
    /// <summary>
    /// GetPropertyValue 요청 - 특정 속성 값 조회
    /// </summary>
    public async Task<List<object>?> GetPropertyValueAsync(string typeName, 
        string valueReference, string? filter = null)
    {
        if (_options.Version != "2.0.0")
            throw new NotSupportedException("GetPropertyValue requires WFS 2.0.0");
        
        var parameters = new Dictionary<string, string>
        {
            ["SERVICE"] = "WFS",
            ["VERSION"] = _options.Version,
            ["REQUEST"] = "GetPropertyValue",
            ["TYPENAME"] = typeName,
            ["VALUEREFERENCE"] = valueReference
        };
        
        if (!string.IsNullOrEmpty(filter))
            parameters["FILTER"] = filter;
        
        var url = BuildUrl(parameters);
        
        try
        {
            var response = await _httpClient.GetStringAsync(url);
            return ParsePropertyValues(response);
        }
        catch
        {
            return null;
        }
    }
    
    private WfsCapabilities ParseCapabilities(string xml)
    {
        var doc = XDocument.Parse(xml);
        var capabilities = new WfsCapabilities();
        
        // 서비스 정보
        var serviceId = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "ServiceIdentification");
        if (serviceId != null)
        {
            capabilities.ServiceTitle = serviceId.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Title")?.Value;
            capabilities.ServiceAbstract = serviceId.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Abstract")?.Value;
        }
        
        // 피처 타입 목록
        var featureTypeList = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "FeatureTypeList");
        if (featureTypeList != null)
        {
            capabilities.FeatureTypes = ParseFeatureTypes(featureTypeList);
        }
        
        // 지원 작업
        var operations = doc.Descendants()
            .Where(e => e.Name.LocalName == "Operation")
            .Select(e => e.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
        capabilities.SupportedOperations = operations!;
        
        return capabilities;
    }
    
    private List<WfsFeatureType> ParseFeatureTypes(XElement featureTypeList)
    {
        var featureTypes = new List<WfsFeatureType>();
        
        foreach (var ft in featureTypeList.Elements()
            .Where(e => e.Name.LocalName == "FeatureType"))
        {
            var featureType = new WfsFeatureType
            {
                Name = ft.Elements().FirstOrDefault(e => e.Name.LocalName == "Name")?.Value,
                Title = ft.Elements().FirstOrDefault(e => e.Name.LocalName == "Title")?.Value,
                Abstract = ft.Elements().FirstOrDefault(e => e.Name.LocalName == "Abstract")?.Value
            };
            
            // WGS84 BoundingBox
            var bbox = ft.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "WGS84BoundingBox");
            if (bbox != null)
            {
                var lowerCorner = bbox.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "LowerCorner")?.Value;
                var upperCorner = bbox.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "UpperCorner")?.Value;
                
                if (!string.IsNullOrEmpty(lowerCorner) && !string.IsNullOrEmpty(upperCorner))
                {
                    var lower = lowerCorner.Split(' ');
                    var upper = upperCorner.Split(' ');
                    
                    featureType.BoundingBox = new Envelope(
                        double.Parse(lower[0], CultureInfo.InvariantCulture),
                        double.Parse(lower[1], CultureInfo.InvariantCulture),
                        double.Parse(upper[0], CultureInfo.InvariantCulture),
                        double.Parse(upper[1], CultureInfo.InvariantCulture)
                    );
                }
            }
            
            if (!string.IsNullOrEmpty(featureType.Name))
                featureTypes.Add(featureType);
        }
        
        return featureTypes;
    }
    
    private FeatureCollection ParseFeatures(string xml)
    {
        var doc = XDocument.Parse(xml);
        var features = new List<Feature>();
        
        // 네임스페이스 처리
        var gmlNs = doc.Root?.GetNamespaceOfPrefix("gml") ?? 
                    XNamespace.Get("http://www.opengis.net/gml/3.2");
        
        // 모든 member 요소 찾기
        var members = doc.Descendants()
            .Where(e => e.Name.LocalName == "member" || 
                       e.Name.LocalName == "featureMember");
        
        foreach (var member in members)
        {
            var featureElement = member.Elements().FirstOrDefault();
            if (featureElement != null)
            {
                var feature = ParseFeature(featureElement, gmlNs);
                if (feature != null)
                    features.Add(feature);
            }
        }
        
        return new FeatureCollection(features);
    }
    
    private Feature? ParseFeature(XElement featureElement, XNamespace gmlNs)
    {
        // ID 추출
        var id = featureElement.Attribute(gmlNs + "id")?.Value ?? 
                Guid.NewGuid().ToString();
        
        // 속성 추출
        var attributes = new AttributeTable();
        IGeometry? geometry = null;
        
        foreach (var element in featureElement.Elements())
        {
            var localName = element.Name.LocalName;
            
            // 지오메트리 요소 확인
            if (IsGeometryElement(localName))
            {
                geometry = ParseGeometry(element, gmlNs);
            }
            else
            {
                // 일반 속성
                attributes[localName] = element.Value;
            }
        }
        
        if (geometry != null)
        {
            return new Feature(id, geometry, attributes);
        }
        
        return null;
    }
    
    private bool IsGeometryElement(string localName)
    {
        var geometryTypes = new[] 
        { 
            "Point", "LineString", "Polygon", "MultiPoint", 
            "MultiLineString", "MultiPolygon", "GeometryCollection",
            "geometry", "the_geom", "geom", "shape"
        };
        
        return geometryTypes.Any(type => 
            localName.Equals(type, StringComparison.OrdinalIgnoreCase));
    }
    
    private IGeometry? ParseGeometry(XElement element, XNamespace gmlNs)
    {
        // 실제 지오메트리 요소 찾기
        var geomElement = element.Elements()
            .FirstOrDefault(e => e.Name.Namespace == gmlNs) ?? element;
        
        var typeName = geomElement.Name.LocalName;
        
        return typeName switch
        {
            "Point" => ParseGmlPoint(geomElement, gmlNs),
            "LineString" => ParseGmlLineString(geomElement, gmlNs),
            "Polygon" => ParseGmlPolygon(geomElement, gmlNs),
            _ => null
        };
    }
    
    private Point? ParseGmlPoint(XElement pointElement, XNamespace gmlNs)
    {
        var pos = pointElement.Element(gmlNs + "pos")?.Value;
        if (string.IsNullOrEmpty(pos)) return null;
        
        var coords = pos.Split(' ');
        if (coords.Length >= 2)
        {
            return new Point(
                double.Parse(coords[0], CultureInfo.InvariantCulture),
                double.Parse(coords[1], CultureInfo.InvariantCulture)
            );
        }
        
        return null;
    }
    
    private LineString? ParseGmlLineString(XElement lineElement, XNamespace gmlNs)
    {
        var posList = lineElement.Element(gmlNs + "posList")?.Value;
        if (string.IsNullOrEmpty(posList)) return null;
        
        var coords = ParsePosList(posList);
        return coords.Count >= 2 ? new LineString(coords.ToArray()) : null;
    }
    
    private Polygon? ParseGmlPolygon(XElement polygonElement, XNamespace gmlNs)
    {
        var exterior = polygonElement.Element(gmlNs + "exterior")
            ?.Element(gmlNs + "LinearRing");
        
        if (exterior == null) return null;
        
        var posList = exterior.Element(gmlNs + "posList")?.Value;
        if (string.IsNullOrEmpty(posList)) return null;
        
        var exteriorCoords = ParsePosList(posList);
        if (exteriorCoords.Count < 4) return null;
        
        var exteriorRing = new LinearRing(exteriorCoords.ToArray());
        
        // 내부 링 처리
        var holes = new List<LinearRing>();
        var interiors = polygonElement.Elements(gmlNs + "interior");
        
        foreach (var interior in interiors)
        {
            var holeRing = interior.Element(gmlNs + "LinearRing");
            if (holeRing != null)
            {
                var holePosList = holeRing.Element(gmlNs + "posList")?.Value;
                if (!string.IsNullOrEmpty(holePosList))
                {
                    var holeCoords = ParsePosList(holePosList);
                    if (holeCoords.Count >= 4)
                    {
                        holes.Add(new LinearRing(holeCoords.ToArray()));
                    }
                }
            }
        }
        
        return new Polygon(exteriorRing, holes.ToArray());
    }
    
    private List<Coordinate> ParsePosList(string posList)
    {
        var values = posList.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var coords = new List<Coordinate>();
        
        for (int i = 0; i < values.Length - 1; i += 2)
        {
            coords.Add(new Coordinate(
                double.Parse(values[i], CultureInfo.InvariantCulture),
                double.Parse(values[i + 1], CultureInfo.InvariantCulture)
            ));
        }
        
        return coords;
    }
    
    private FeatureTypeSchema ParseFeatureTypeSchema(string xml)
    {
        var doc = XDocument.Parse(xml);
        var schema = new FeatureTypeSchema();
        
        // 스키마 정보 파싱 (간단한 구현)
        var complexType = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "complexType");
        
        if (complexType != null)
        {
            schema.TypeName = complexType.Attribute("name")?.Value;
            
            var sequence = complexType.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "sequence");
            
            if (sequence != null)
            {
                foreach (var element in sequence.Elements()
                    .Where(e => e.Name.LocalName == "element"))
                {
                    var property = new PropertyDefinition
                    {
                        Name = element.Attribute("name")?.Value,
                        Type = element.Attribute("type")?.Value,
                        MinOccurs = int.Parse(element.Attribute("minOccurs")?.Value ?? "0"),
                        MaxOccurs = element.Attribute("maxOccurs")?.Value ?? "1"
                    };
                    
                    if (!string.IsNullOrEmpty(property.Name))
                        schema.Properties.Add(property);
                }
            }
        }
        
        return schema;
    }
    
    private string BuildTransactionXml(WfsTransaction transaction)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<wfs:Transaction service=\"WFS\" version=\"{_options.Version}\"");
        sb.AppendLine("  xmlns:wfs=\"http://www.opengis.net/wfs/2.0\"");
        sb.AppendLine("  xmlns:gml=\"http://www.opengis.net/gml/3.2\">");
        
        // Insert 작업
        foreach (var insert in transaction.Inserts)
        {
            sb.AppendLine("  <wfs:Insert>");
            // 피처 XML 추가
            sb.AppendLine("  </wfs:Insert>");
        }
        
        // Update 작업
        foreach (var update in transaction.Updates)
        {
            sb.AppendLine("  <wfs:Update typeName=\"" + update.TypeName + "\">");
            // 속성 업데이트 XML
            sb.AppendLine("  </wfs:Update>");
        }
        
        // Delete 작업
        foreach (var delete in transaction.Deletes)
        {
            sb.AppendLine("  <wfs:Delete typeName=\"" + delete.TypeName + "\">");
            sb.AppendLine("    <fes:Filter>");
            sb.AppendLine("      <fes:ResourceId rid=\"" + delete.FeatureId + "\"/>");
            sb.AppendLine("    </fes:Filter>");
            sb.AppendLine("  </wfs:Delete>");
        }
        
        sb.AppendLine("</wfs:Transaction>");
        return sb.ToString();
    }
    
    private TransactionResponse ParseTransactionResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var response = new TransactionResponse();
        
        var transactionResponse = doc.Root;
        if (transactionResponse != null)
        {
            var summary = transactionResponse.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "TransactionSummary");
            
            if (summary != null)
            {
                response.TotalInserted = int.Parse(
                    summary.Elements().FirstOrDefault(e => 
                        e.Name.LocalName == "totalInserted")?.Value ?? "0");
                response.TotalUpdated = int.Parse(
                    summary.Elements().FirstOrDefault(e => 
                        e.Name.LocalName == "totalUpdated")?.Value ?? "0");
                response.TotalDeleted = int.Parse(
                    summary.Elements().FirstOrDefault(e => 
                        e.Name.LocalName == "totalDeleted")?.Value ?? "0");
            }
            
            response.Success = true;
        }
        
        return response;
    }
    
    private List<object> ParsePropertyValues(string xml)
    {
        var doc = XDocument.Parse(xml);
        var values = new List<object>();
        
        var valueCollection = doc.Root;
        if (valueCollection != null)
        {
            foreach (var member in valueCollection.Elements()
                .Where(e => e.Name.LocalName == "member"))
            {
                values.Add(member.Value);
            }
        }
        
        return values;
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
        var culture = CultureInfo.InvariantCulture;
        return $"{bbox.MinX.ToString(culture)},{bbox.MinY.ToString(culture)}," +
               $"{bbox.MaxX.ToString(culture)},{bbox.MaxY.ToString(culture)}";
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// WFS 클라이언트 옵션
/// </summary>
public class WfsClientOptions
{
    public string Version { get; set; } = "2.0.0";
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(60);
}

/// <summary>
/// WFS Capabilities
/// </summary>
public class WfsCapabilities
{
    public string? ServiceTitle { get; set; }
    public string? ServiceAbstract { get; set; }
    public List<WfsFeatureType> FeatureTypes { get; set; } = new();
    public List<string> SupportedOperations { get; set; } = new();
}

/// <summary>
/// WFS 피처 타입
/// </summary>
public class WfsFeatureType
{
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Abstract { get; set; }
    public Envelope? BoundingBox { get; set; }
}

/// <summary>
/// GetFeature 요청
/// </summary>
public class GetFeatureRequest
{
    public IEnumerable<string> TypeNames { get; set; } = new List<string>();
    public string? OutputFormat { get; set; }
    public int? MaxFeatures { get; set; }
    public string? Filter { get; set; }
    public Envelope? BoundingBox { get; set; }
    public string? SrsName { get; set; }
}

/// <summary>
/// 피처 타입 스키마
/// </summary>
public class FeatureTypeSchema
{
    public string? TypeName { get; set; }
    public List<PropertyDefinition> Properties { get; set; } = new();
}

/// <summary>
/// 속성 정의
/// </summary>
public class PropertyDefinition
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public int MinOccurs { get; set; }
    public string? MaxOccurs { get; set; }
}

/// <summary>
/// WFS Transaction
/// </summary>
public class WfsTransaction
{
    public List<InsertOperation> Inserts { get; set; } = new();
    public List<UpdateOperation> Updates { get; set; } = new();
    public List<DeleteOperation> Deletes { get; set; } = new();
}

/// <summary>
/// Insert 작업
/// </summary>
public class InsertOperation
{
    public Feature Feature { get; set; } = null!;
}

/// <summary>
/// Update 작업
/// </summary>
public class UpdateOperation
{
    public string TypeName { get; set; } = null!;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? Filter { get; set; }
}

/// <summary>
/// Delete 작업
/// </summary>
public class DeleteOperation
{
    public string TypeName { get; set; } = null!;
    public string FeatureId { get; set; } = null!;
}

/// <summary>
/// Transaction 응답
/// </summary>
public class TransactionResponse
{
    public bool Success { get; set; }
    public int TotalInserted { get; set; }
    public int TotalUpdated { get; set; }
    public int TotalDeleted { get; set; }
    public string? Message { get; set; }
}