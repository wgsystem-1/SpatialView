using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// REST API 데이터 소스
/// </summary>
public class RestApiDataSource : DataSourceBase
{
    private readonly HttpClient _httpClient;
    private readonly RestApiConfiguration _config;
    private new readonly object _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// 인증 토큰
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// 요청 타임아웃 (초)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 페이지 크기
    /// </summary>
    public int PageSize { get; set; } = 100;

    public override string Name { get; }
    public override string ConnectionString => _config.BaseUrl;
    public override DataSourceType SourceType => DataSourceType.WebService;

    public RestApiDataSource(RestApiConfiguration configuration)
    {
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Name = configuration.Name ?? "REST API";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(configuration.BaseUrl),
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
        };

        // 기본 헤더 설정
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrEmpty(configuration.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add(configuration.ApiKeyHeader ?? "X-API-Key", configuration.ApiKey);
        }
    }

    public async Task<IEnumerable<IFeature>?> GetFeaturesAsync(Envelope? extent = null, IQueryFilter? filter = null)
    {
        try
        {
            var features = new List<IFeature>();
            var pageNumber = 1;
            bool hasMoreData = true;

            while (hasMoreData)
            {
                var url = BuildUrl(_config.FeaturesEndpoint, extent, filter, pageNumber);
                var response = await SendRequestAsync(HttpMethod.Get, url);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var pageFeatures = ParseFeatures(json);
                    
                    if (pageFeatures.Any())
                    {
                        features.AddRange(pageFeatures);
                        pageNumber++;
                        
                        // 페이지네이션 확인
                        hasMoreData = _config.PaginationStyle == PaginationStyle.PageNumber && 
                                     pageFeatures.Count() == PageSize;
                    }
                    else
                    {
                        hasMoreData = false;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"REST API 요청 실패: {response.StatusCode}");
                    hasMoreData = false;
                }

                // 단일 페이지 요청인 경우 종료
                if (_config.PaginationStyle == PaginationStyle.None)
                    hasMoreData = false;
            }

            return features;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"REST API GetFeatures 실패: {ex.Message}");
            return null;
        }
    }

    public IEnumerable<IFeature>? GetFeatures(Envelope? extent = null, IQueryFilter? filter = null)
    {
        return GetFeaturesAsync(extent, filter).Result;
    }

    public async Task<IFeature?> GetFeatureAsync(object id)
    {
        try
        {
            var url = string.Format(_config.FeatureEndpoint, id);
            var response = await SendRequestAsync(HttpMethod.Get, url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return ParseSingleFeature(json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"REST API GetFeature 실패: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> AddFeatureAsync(IFeature feature)
    {
        if (string.IsNullOrEmpty(_config.CreateEndpoint))
            return false;

        try
        {
            var json = SerializeFeature(feature);
            var response = await SendRequestAsync(HttpMethod.Post, _config.CreateEndpoint, json);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"REST API AddFeature 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UpdateFeatureAsync(IFeature feature)
    {
        if (string.IsNullOrEmpty(_config.UpdateEndpoint))
            return false;

        try
        {
            var url = string.Format(_config.UpdateEndpoint, feature.Id);
            var json = SerializeFeature(feature);
            var response = await SendRequestAsync(HttpMethod.Put, url, json);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"REST API UpdateFeature 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFeatureAsync(object id)
    {
        if (string.IsNullOrEmpty(_config.DeleteEndpoint))
            return false;

        try
        {
            var url = string.Format(_config.DeleteEndpoint, id);
            var response = await SendRequestAsync(HttpMethod.Delete, url);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"REST API DeleteFeature 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 공간 쿼리 실행
    /// </summary>
    public async Task<IEnumerable<IFeature>?> ExecuteSpatialQueryAsync(
        IGeometry geometry, 
        SpatialRelation relation = SpatialRelation.Intersects)
    {
        if (string.IsNullOrEmpty(_config.SpatialQueryEndpoint))
            return null;

        try
        {
            var query = new SpatialQuery
            {
                Geometry = Geometry.IO.WktParser.Write(geometry),
                Relation = relation.ToString(),
                Format = _config.GeometryFormat
            };

            var json = JsonSerializer.Serialize(query);
            var response = await SendRequestAsync(HttpMethod.Post, _config.SpatialQueryEndpoint, json);

            if (response.IsSuccessStatusCode)
            {
                var resultJson = await response.Content.ReadAsStringAsync();
                return ParseFeatures(resultJson);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"REST API 공간 쿼리 실패: {ex.Message}");
        }

        return null;
    }

    #region Private Methods

    private string BuildUrl(string endpoint, Envelope? extent, IQueryFilter? filter, int pageNumber)
    {
        var queryParams = new List<string>();

        // 공간 필터
        if (extent != null && !string.IsNullOrEmpty(_config.BboxParameter))
        {
            queryParams.Add($"{_config.BboxParameter}={extent.MinX},{extent.MinY},{extent.MaxX},{extent.MaxY}");
        }

        // 속성 필터
        if (filter?.AttributeFilter?.WhereClause != null && !string.IsNullOrEmpty(_config.FilterParameter))
        {
            queryParams.Add($"{_config.FilterParameter}={Uri.EscapeDataString(filter.AttributeFilter.WhereClause)}");
        }

        // 페이지네이션
        switch (_config.PaginationStyle)
        {
            case PaginationStyle.PageNumber:
                queryParams.Add($"{_config.PageParameter ?? "page"}={pageNumber}");
                queryParams.Add($"{_config.PageSizeParameter ?? "pageSize"}={PageSize}");
                break;

            case PaginationStyle.Offset:
                var offset = (pageNumber - 1) * PageSize;
                queryParams.Add($"{_config.OffsetParameter ?? "offset"}={offset}");
                queryParams.Add($"{_config.LimitParameter ?? "limit"}={PageSize}");
                break;
        }

        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return endpoint + query;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpMethod method, 
        string url, 
        string? jsonContent = null)
    {
        var request = new HttpRequestMessage(method, url);

        // 인증 토큰 추가
        if (!string.IsNullOrEmpty(AuthToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
        }

        // 컨텐츠 추가
        if (jsonContent != null)
        {
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request);
    }

    private IEnumerable<IFeature> ParseFeatures(string json)
    {
        var features = new List<IFeature>();

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // GeoJSON 형식 확인
            if (root.TryGetProperty("type", out var typeElement) && 
                typeElement.GetString() == "FeatureCollection")
            {
                return ParseGeoJsonFeatures(root);
            }

            // 커스텀 형식 파싱
            JsonElement featuresElement;
            if (!string.IsNullOrEmpty(_config.FeaturesProperty))
            {
                var parts = _config.FeaturesProperty.Split('.');
                featuresElement = root;
                
                foreach (var part in parts)
                {
                    if (!featuresElement.TryGetProperty(part, out featuresElement))
                        return features;
                }
            }
            else
            {
                featuresElement = root;
            }

            if (featuresElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in featuresElement.EnumerateArray())
                {
                    var feature = ParseFeatureElement(element);
                    if (feature != null)
                        features.Add(feature);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"피처 파싱 실패: {ex.Message}");
        }

        return features;
    }

    private IEnumerable<IFeature> ParseGeoJsonFeatures(JsonElement root)
    {
        var features = new List<IFeature>();

        if (root.TryGetProperty("features", out var featuresElement) && 
            featuresElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var featureElement in featuresElement.EnumerateArray())
            {
                try
                {
                    var id = featureElement.TryGetProperty("id", out var idElement) ? 
                        idElement.ToString() : Guid.NewGuid().ToString();

                    // 지오메트리 파싱
                    IGeometry? geometry = null;
                    if (featureElement.TryGetProperty("geometry", out var geomElement))
                    {
                        var geomJson = geomElement.GetRawText();
                        geometry = Geometry.IO.GeoJsonParser.ParseGeometry(geomJson);
                    }

                    if (geometry == null)
                        continue;

                    // 속성 파싱
                    var attributes = new AttributeTable();
                    if (featureElement.TryGetProperty("properties", out var propsElement))
                    {
                        foreach (var prop in propsElement.EnumerateObject())
                        {
                            attributes[prop.Name] = ParseJsonValue(prop.Value);
                        }
                    }

                    features.Add(new Feature(id, geometry, attributes));
                }
                catch
                {
                    // 개별 피처 파싱 실패 무시
                }
            }
        }

        return features;
    }

    private IFeature? ParseSingleFeature(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseFeatureElement(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private IFeature? ParseFeatureElement(JsonElement element)
    {
        try
        {
            // ID 추출
            var id = ExtractValue(element, _config.IdProperty)?.ToString() ?? 
                     Guid.NewGuid().ToString();

            // 지오메트리 추출
            IGeometry? geometry = null;
            var geomValue = ExtractValue(element, _config.GeometryProperty);
            
            if (geomValue != null)
            {
                if (geomValue is JsonElement geomElement)
                {
                    if (_config.GeometryFormat == GeometryFormat.GeoJson)
                    {
                        var geomJson = geomElement.GetRawText();
                        geometry = Geometry.IO.GeoJsonParser.ParseGeometry(geomJson);
                    }
                    else if (_config.GeometryFormat == GeometryFormat.WKT)
                    {
                        var wkt = geomElement.GetString();
                        if (!string.IsNullOrEmpty(wkt))
                            geometry = Geometry.IO.WktParser.Parse(wkt);
                    }
                }
            }

            if (geometry == null)
                return null;

            // 속성 추출
            var attributes = new AttributeTable();
            
            if (!string.IsNullOrEmpty(_config.AttributesProperty))
            {
                var attrsValue = ExtractValue(element, _config.AttributesProperty);
                if (attrsValue is JsonElement attrsElement)
                {
                    foreach (var prop in attrsElement.EnumerateObject())
                    {
                        attributes[prop.Name] = ParseJsonValue(prop.Value);
                    }
                }
            }
            else
            {
                // 전체 객체를 속성으로 사용
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name != _config.IdProperty && 
                        prop.Name != _config.GeometryProperty)
                    {
                        attributes[prop.Name] = ParseJsonValue(prop.Value);
                    }
                }
            }

            return new Feature(id, geometry, attributes);
        }
        catch
        {
            return null;
        }
    }

    private object? ExtractValue(JsonElement element, string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out current))
                return null;
        }

        return current;
    }

    private static object? ParseJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l :
                                    element.TryGetDouble(out var d) ? d : null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray()
                                         .Select(ParseJsonValue)
                                         .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                                          .ToDictionary(p => p.Name, p => ParseJsonValue(p.Value)),
            _ => element.ToString()
        };
    }

    private string SerializeFeature(IFeature feature)
    {
        if (_config.GeometryFormat == GeometryFormat.GeoJson)
        {
            return Geometry.IO.GeoJsonParser.WriteFeature(feature);
        }

        // 커스텀 형식
        var obj = new Dictionary<string, object?>();
        
        if (!string.IsNullOrEmpty(_config.IdProperty))
            obj[_config.IdProperty] = feature.Id;

        if (!string.IsNullOrEmpty(_config.GeometryProperty))
        {
            if (_config.GeometryFormat == GeometryFormat.WKT)
                obj[_config.GeometryProperty] = Geometry.IO.WktParser.Write(feature.Geometry);
            else
                obj[_config.GeometryProperty] = Geometry.IO.GeoJsonParser.WriteGeometry(feature.Geometry);
        }

        if (!string.IsNullOrEmpty(_config.AttributesProperty))
        {
            var attrs = new Dictionary<string, object?>();
            foreach (var kvp in feature.Attributes)
            {
                attrs[kvp.Key] = kvp.Value;
            }
            obj[_config.AttributesProperty] = attrs;
        }
        else
        {
            foreach (var kvp in feature.Attributes)
            {
                obj[kvp.Key] = kvp.Value;
            }
        }

        return JsonSerializer.Serialize(obj);
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    #region Abstract Method Implementations

    public override IEnumerable<string> GetTableNames()
    {
        // REST APIs typically don't have "tables" in the traditional sense
        // Return configured endpoints as table names
        var tables = new List<string>();
        
        if (!string.IsNullOrEmpty(_config.FeaturesEndpoint))
            tables.Add("features");
            
        // Could be extended to support multiple endpoints/resources
        return tables;
    }

    public override async Task<bool> OpenAsync()
    {
        try
        {
            // Test connection by making a HEAD or OPTIONS request
            var request = new HttpRequestMessage(HttpMethod.Head, _config.FeaturesEndpoint);
            
            // Add auth if needed
            if (!string.IsNullOrEmpty(AuthToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            }
            
            var response = await _httpClient.SendAsync(request);
            
            // Accept various success codes
            IsConnected = response.IsSuccessStatusCode || 
                         response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
                         response.StatusCode == System.Net.HttpStatusCode.NoContent;
                         
            return IsConnected;
        }
        catch (Exception ex)
        {
            LogError("Failed to open REST API connection", ex);
            IsConnected = false;
            return false;
        }
    }

    public override void Close()
    {
        IsConnected = false;
        // HTTP client will be disposed in Dispose method
    }

    public override async Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        // REST APIs typically don't have a fixed schema
        // Return a basic schema based on configuration
        var schema = new TableSchema
        {
            TableName = tableName,
            GeometryColumn = _config.GeometryProperty,
            GeometryType = "Geometry", // Generic type
            SRID = 4326, // Assume WGS84 for web services
            PrimaryKeyColumn = _config.IdProperty
        };
        
        // Try to infer schema from a sample request
        try
        {
            var features = await GetFeaturesAsync(null, new QueryFilter { MaxFeatures = 1 });
            var firstFeature = features?.FirstOrDefault();
            
            if (firstFeature?.Attributes != null)
            {
                foreach (var kvp in firstFeature.Attributes)
                {
                    var dataType = kvp.Value?.GetType() ?? typeof(string);
                    
                    schema.Columns.Add(new ColumnInfo
                    {
                        Name = kvp.Key,
                        DataType = dataType,
                        AllowNull = true
                    });
                }
            }
            
            // Get total count if possible
            schema.FeatureCount = await GetFeatureCountAsync(tableName);
        }
        catch (Exception ex)
        {
            LogError("Failed to infer schema from REST API", ex);
        }
        
        return schema;
    }

    public override async Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        try
        {
            // Some APIs provide a count endpoint
            // For now, we'll fetch all features and count them
            var features = await GetFeaturesAsync(null, filter);
            return features?.Count() ?? 0;
        }
        catch (Exception ex)
        {
            LogError("Failed to get feature count", ex);
            return 0;
        }
    }

    public override async Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        try
        {
            // Calculate extent from all features
            var features = await GetFeaturesAsync(null, null);
            if (features == null || !features.Any())
                return null;
                
            double? minX = null, minY = null, maxX = null, maxY = null;
            
            foreach (var feature in features)
            {
                if (feature.Geometry != null)
                {
                    var bounds = feature.Geometry.GetBounds();
                    if (!bounds.IsNull)
                    {
                        minX = minX.HasValue ? Math.Min(minX.Value, bounds.MinX) : bounds.MinX;
                        minY = minY.HasValue ? Math.Min(minY.Value, bounds.MinY) : bounds.MinY;
                        maxX = maxX.HasValue ? Math.Max(maxX.Value, bounds.MaxX) : bounds.MaxX;
                        maxY = maxY.HasValue ? Math.Max(maxY.Value, bounds.MaxY) : bounds.MaxY;
                    }
                }
            }
            
            if (minX.HasValue && minY.HasValue && maxX.HasValue && maxY.HasValue)
            {
                return new Geometry.Envelope(minX.Value, maxX.Value, minY.Value, maxY.Value);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            LogError("Failed to get extent", ex);
            return null;
        }
    }

    public override async IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null)
    {
        var features = await GetFeaturesAsync(null, filter);
        if (features != null)
        {
            foreach (var feature in features)
            {
                yield return feature;
            }
        }
    }

    public override async Task<IFeature?> GetFeatureAsync(string tableName, object id)
    {
        return await GetFeatureAsync(id);
    }

    // Override the virtual methods from base class to adapt to table-based interface
    public override async Task<bool> InsertFeatureAsync(string tableName, IFeature feature)
    {
        return await AddFeatureAsync(feature);
    }

    public override async Task<bool> UpdateFeatureAsync(string tableName, IFeature feature)
    {
        return await UpdateFeatureAsync(feature);
    }

    public override async Task<bool> DeleteFeatureAsync(string tableName, object id)
    {
        return await DeleteFeatureAsync(id);
    }

    #endregion

    #region Helper Methods for Adapting QueryFilter to REST API

    private async Task<IEnumerable<IFeature>?> GetFeaturesWithFilterAsync(Envelope? extent, IQueryFilter? filter)
    {
        // Convert IQueryFilter to REST API parameters
        var spatialExtent = extent;
        string? whereClause = null;
        
        if (filter != null)
        {
            if (filter.SpatialFilter?.FilterGeometry != null)
            {
                spatialExtent = filter.SpatialFilter.FilterGeometry.GetBounds();
            }
            
            if (filter.AttributeFilter != null)
            {
                whereClause = filter.AttributeFilter.WhereClause;
            }
        }
        
        // Create a legacy query filter for compatibility
        var legacyFilter = new LegacyQueryFilter
        {
            WhereClause = whereClause,
            MaxFeatures = filter?.MaxFeatures ?? 0,
            Offset = filter?.Offset ?? 0
        };
        
        return await GetFeaturesAsync(spatialExtent, legacyFilter);
    }

    private class LegacyQueryFilter : IQueryFilter
    {
        public string? WhereClause { get; set; }
        public int MaxFeatures { get; set; }
        public int Offset { get; set; }
        public ISpatialFilter? SpatialFilter { get; set; }
        public IAttributeFilter? AttributeFilter { get; set; }
        public IList<SortField>? OrderBy { get; set; } = new List<SortField>();
        public IList<string>? Columns { get; set; }
        public bool IncludeGeometry { get; set; } = true;
        public bool Distinct { get; set; }
        public int TargetSRID { get; set; }
        
        public IQueryFilter Clone()
        {
            return new LegacyQueryFilter
            {
                WhereClause = WhereClause,
                MaxFeatures = MaxFeatures,
                Offset = Offset,
                SpatialFilter = SpatialFilter,
                AttributeFilter = AttributeFilter,
                OrderBy = OrderBy?.ToList(),
                Columns = Columns?.ToList(),
                IncludeGeometry = IncludeGeometry,
                Distinct = Distinct,
                TargetSRID = TargetSRID
            };
        }
    }

    #endregion
}

/// <summary>
/// REST API 구성
/// </summary>
public class RestApiConfiguration
{
    /// <summary>
    /// API 이름
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 기본 URL
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// API 키
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API 키 헤더명
    /// </summary>
    public string? ApiKeyHeader { get; set; } = "X-API-Key";

    /// <summary>
    /// 피처 목록 엔드포인트
    /// </summary>
    public string FeaturesEndpoint { get; set; } = "/features";

    /// <summary>
    /// 단일 피처 엔드포인트 ({0}는 ID)
    /// </summary>
    public string FeatureEndpoint { get; set; } = "/features/{0}";

    /// <summary>
    /// 피처 생성 엔드포인트
    /// </summary>
    public string? CreateEndpoint { get; set; } = "/features";

    /// <summary>
    /// 피처 수정 엔드포인트 ({0}는 ID)
    /// </summary>
    public string? UpdateEndpoint { get; set; } = "/features/{0}";

    /// <summary>
    /// 피처 삭제 엔드포인트 ({0}는 ID)
    /// </summary>
    public string? DeleteEndpoint { get; set; } = "/features/{0}";

    /// <summary>
    /// 공간 쿼리 엔드포인트
    /// </summary>
    public string? SpatialQueryEndpoint { get; set; } = "/features/spatial";

    /// <summary>
    /// 지오메트리 형식
    /// </summary>
    public GeometryFormat GeometryFormat { get; set; } = GeometryFormat.GeoJson;

    /// <summary>
    /// ID 속성 경로
    /// </summary>
    public string IdProperty { get; set; } = "id";

    /// <summary>
    /// 지오메트리 속성 경로
    /// </summary>
    public string GeometryProperty { get; set; } = "geometry";

    /// <summary>
    /// 속성 경로
    /// </summary>
    public string? AttributesProperty { get; set; } = "properties";

    /// <summary>
    /// 피처 배열 경로
    /// </summary>
    public string? FeaturesProperty { get; set; } = "features";

    /// <summary>
    /// BBOX 매개변수 이름
    /// </summary>
    public string? BboxParameter { get; set; } = "bbox";

    /// <summary>
    /// 필터 매개변수 이름
    /// </summary>
    public string? FilterParameter { get; set; } = "filter";

    /// <summary>
    /// 페이지네이션 스타일
    /// </summary>
    public PaginationStyle PaginationStyle { get; set; } = PaginationStyle.PageNumber;

    /// <summary>
    /// 페이지 번호 매개변수
    /// </summary>
    public string? PageParameter { get; set; } = "page";

    /// <summary>
    /// 페이지 크기 매개변수
    /// </summary>
    public string? PageSizeParameter { get; set; } = "pageSize";

    /// <summary>
    /// 오프셋 매개변수
    /// </summary>
    public string? OffsetParameter { get; set; } = "offset";

    /// <summary>
    /// 제한 매개변수
    /// </summary>
    public string? LimitParameter { get; set; } = "limit";

    /// <summary>
    /// 커스텀 헤더
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}

/// <summary>
/// 지오메트리 형식
/// </summary>
public enum GeometryFormat
{
    /// <summary>GeoJSON</summary>
    GeoJson,
    /// <summary>Well-Known Text</summary>
    WKT,
    /// <summary>Well-Known Binary (Base64)</summary>
    WKB,
    /// <summary>커스텀 JSON</summary>
    Custom
}

/// <summary>
/// 페이지네이션 스타일
/// </summary>
public enum PaginationStyle
{
    /// <summary>페이지네이션 없음</summary>
    None,
    /// <summary>페이지 번호 기반</summary>
    PageNumber,
    /// <summary>오프셋 기반</summary>
    Offset,
    /// <summary>커서 기반</summary>
    Cursor
}

/// <summary>
/// 공간 관계
/// </summary>
public enum SpatialRelation
{
    Intersects,
    Contains,
    Within,
    Crosses,
    Overlaps,
    Touches,
    Disjoint
}

/// <summary>
/// 공간 쿼리
/// </summary>
internal class SpatialQuery
{
    public string Geometry { get; set; } = "";
    public string Relation { get; set; } = "";
    public GeometryFormat Format { get; set; }
}