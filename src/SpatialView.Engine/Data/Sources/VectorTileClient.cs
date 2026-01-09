using SpatialView.Engine.Geometry;
using System.IO.Compression;
using System.Net.Http;

namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// Vector Tiles (MVT) 클라이언트
/// Mapbox Vector Tiles 형식 지원
/// </summary>
public class VectorTileClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _accessToken;
    private bool _disposed = false;

    /// <summary>
    /// 요청 타임아웃 (밀리초)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 최대 동시 요청 수
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 6;

    /// <summary>
    /// 사용자 정의 헤더
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; } = new();

    public VectorTileClient(string baseUrl, string? accessToken = null)
    {
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _accessToken = accessToken ?? string.Empty;

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(TimeoutMs)
        };

        // 기본 헤더 설정
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpatialView GIS Engine MVT Client");
        
        if (!string.IsNullOrEmpty(_accessToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
        }
    }

    /// <summary>
    /// 단일 벡터 타일 요청
    /// </summary>
    /// <param name="z">줌 레벨</param>
    /// <param name="x">타일 X 좌표</param>
    /// <param name="y">타일 Y 좌표</param>
    /// <returns>벡터 타일 데이터</returns>
    public async Task<VectorTile?> GetVectorTileAsync(int z, int x, int y)
    {
        try
        {
            var url = BuildTileUrl(z, x, y);
            
            foreach (var header in CustomHeaders)
            {
                _httpClient.DefaultRequestHeaders.Remove(header.Key);
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsByteArrayAsync();
            
            // MVT 데이터는 보통 gzip으로 압축됨
            var decompressedData = await DecompressIfNeeded(data, response.Content.Headers.ContentEncoding);
            
            return await ParseVectorTile(decompressedData, z, x, y);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetVectorTile failed for {z}/{x}/{y}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 여러 벡터 타일 동시 요청
    /// </summary>
    /// <param name="tileRequests">타일 요청 목록</param>
    /// <returns>타일 데이터 딕셔너리</returns>
    public async Task<Dictionary<string, VectorTile>> GetMultipleVectorTilesAsync(
        IEnumerable<TileCoordinate> tileRequests)
    {
        var results = new Dictionary<string, VectorTile>();
        var semaphore = new SemaphoreSlim(MaxConcurrentRequests, MaxConcurrentRequests);
        
        var tasks = tileRequests.Select(async request =>
        {
            await semaphore.WaitAsync();
            try
            {
                var tile = await GetVectorTileAsync(request.Z, request.X, request.Y);
                if (tile != null)
                {
                    var key = $"{request.Z}/{request.X}/{request.Y}";
                    lock (results)
                    {
                        results[key] = tile;
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

    /// <summary>
    /// 타일 범위에서 벡터 타일들 가져오기
    /// </summary>
    /// <param name="zoom">줌 레벨</param>
    /// <param name="envelope">지리적 범위</param>
    /// <returns>타일 데이터 목록</returns>
    public async Task<List<VectorTile>> GetVectorTilesInEnvelopeAsync(int zoom, Envelope envelope)
    {
        var tileCoords = CalculateTileCoordinates(zoom, envelope);
        var tilesDict = await GetMultipleVectorTilesAsync(tileCoords);
        return tilesDict.Values.ToList();
    }

    /// <summary>
    /// 벡터 타일을 피처로 변환
    /// </summary>
    /// <param name="tiles">벡터 타일들</param>
    /// <param name="layerName">추출할 레이어명 (null이면 모든 레이어)</param>
    /// <returns>변환된 피처들</returns>
    public IEnumerable<IFeature> ConvertTilesToFeatures(
        IEnumerable<VectorTile> tiles, 
        string? layerName = null)
    {
        foreach (var tile in tiles)
        {
            foreach (var layer in tile.Layers.Values)
            {
                if (layerName != null && layer.Name != layerName)
                    continue;

                foreach (var feature in layer.Features)
                {
                    // 타일 좌표를 지리 좌표로 변환
                    var geoGeometry = ConvertTileGeometryToGeo(feature.Geometry, tile);
                    if (geoGeometry != null)
                    {
                        yield return new Feature(feature.Id?.ToString() ?? Guid.NewGuid().ToString(), geoGeometry, CreateAttributeTable(feature.Properties));
                    }
                }
            }
        }
    }

    #region Private Methods

    private string BuildTileUrl(int z, int x, int y)
    {
        var url = _baseUrl.Replace("{z}", z.ToString())
                         .Replace("{x}", x.ToString())
                         .Replace("{y}", y.ToString());

        // 액세스 토큰이 URL에 포함되어야 하는 경우
        if (!string.IsNullOrEmpty(_accessToken) && !_httpClient.DefaultRequestHeaders.Contains("Authorization"))
        {
            var separator = url.Contains('?') ? "&" : "?";
            url += $"{separator}access_token={_accessToken}";
        }

        return url;
    }

    private static async Task<byte[]> DecompressIfNeeded(byte[] data, IEnumerable<string>? contentEncodings)
    {
        if (contentEncodings?.Contains("gzip") == true)
        {
            using var compressedStream = new MemoryStream(data);
            using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            
            await gzipStream.CopyToAsync(decompressedStream);
            return decompressedStream.ToArray();
        }

        return data;
    }

    private static async Task<VectorTile?> ParseVectorTile(byte[] data, int z, int x, int y)
    {
        try
        {
            // 간단한 MVT 파서 구현
            // 실제로는 Protocol Buffers를 사용해야 하지만, 여기서는 기본 구조만 제공
            return await Task.Run(() =>
            {
                var tile = new VectorTile
                {
                    Z = z,
                    X = x,
                    Y = y,
                    Extent = CalculateTileExtent(z, x, y)
                };

                // MVT 파싱 로직 (Protocol Buffers 파싱 필요)
                // 여기서는 기본 구조만 제공
                tile.Layers = ParseMvtLayers(data);

                return tile;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Parse vector tile failed: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, VectorTileLayer> ParseMvtLayers(byte[] data)
    {
        // 실제 MVT 파싱은 Protocol Buffers 라이브러리가 필요
        // 여기서는 기본 구조만 제공
        var layers = new Dictionary<string, VectorTileLayer>();

        try
        {
            // Protocol Buffers 파싱 로직이 여기 들어가야 함
            // Google.Protobuf 라이브러리 사용 권장
            
            // 임시 더미 레이어
            var dummyLayer = new VectorTileLayer
            {
                Name = "default",
                Version = 2,
                Extent = 4096,
                Features = new List<VectorTileFeature>()
            };
            
            layers["default"] = dummyLayer;
        }
        catch
        {
            // 파싱 실패 시 빈 딕셔너리 반환
        }

        return layers;
    }

    private static Envelope CalculateTileExtent(int z, int x, int y)
    {
        var n = Math.Pow(2.0, z);
        var lonMin = x / n * 360.0 - 180.0;
        var latMax = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n))) * 180.0 / Math.PI;
        var lonMax = (x + 1) / n * 360.0 - 180.0;
        var latMin = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (y + 1) / n))) * 180.0 / Math.PI;

        return new Envelope(lonMin, latMin, lonMax, latMax);
    }

    private static List<TileCoordinate> CalculateTileCoordinates(int zoom, Envelope envelope)
    {
        var tileCoords = new List<TileCoordinate>();
        
        // 지리적 범위를 타일 좌표로 변환
        var n = Math.Pow(2.0, zoom);
        
        var minTileX = (int)Math.Floor((envelope.MinX + 180.0) / 360.0 * n);
        var maxTileX = (int)Math.Floor((envelope.MaxX + 180.0) / 360.0 * n);
        
        var minTileY = (int)Math.Floor((1.0 - Math.Log(Math.Tan(envelope.MaxY * Math.PI / 180.0) + 
            1.0 / Math.Cos(envelope.MaxY * Math.PI / 180.0)) / Math.PI) / 2.0 * n);
        var maxTileY = (int)Math.Floor((1.0 - Math.Log(Math.Tan(envelope.MinY * Math.PI / 180.0) + 
            1.0 / Math.Cos(envelope.MinY * Math.PI / 180.0)) / Math.PI) / 2.0 * n);

        for (int x = minTileX; x <= maxTileX; x++)
        {
            for (int y = minTileY; y <= maxTileY; y++)
            {
                if (x >= 0 && x < n && y >= 0 && y < n)
                {
                    tileCoords.Add(new TileCoordinate { Z = zoom, X = x, Y = y });
                }
            }
        }

        return tileCoords;
    }

    private IGeometry? ConvertTileGeometryToGeo(VectorTileGeometry tileGeometry, VectorTile tile)
    {
        try
        {
            // 타일 좌표를 지리 좌표로 변환
            var extent = tile.Extent;
            var tileEnvelope = extent;
            
            return tileGeometry.Type switch
            {
                VectorTileGeometryType.Point => ConvertPointToGeo(tileGeometry, tileEnvelope),
                VectorTileGeometryType.LineString => ConvertLineStringToGeo(tileGeometry, tileEnvelope),
                VectorTileGeometryType.Polygon => ConvertPolygonToGeo(tileGeometry, tileEnvelope),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static Point? ConvertPointToGeo(VectorTileGeometry tileGeometry, Envelope tileEnvelope)
    {
        if (tileGeometry.Coordinates.Count == 0) return null;
        
        var coord = tileGeometry.Coordinates[0];
        var geoCoord = TileCoordToGeoCoord(coord, tileEnvelope, 4096);
        return new Point(geoCoord);
    }

    private static LineString? ConvertLineStringToGeo(VectorTileGeometry tileGeometry, Envelope tileEnvelope)
    {
        if (tileGeometry.Coordinates.Count < 2) return null;
        
        var geoCoords = tileGeometry.Coordinates
            .Select(c => TileCoordToGeoCoord(c, tileEnvelope, 4096))
            .ToList();
        
        return new LineString(geoCoords);
    }

    private static Polygon? ConvertPolygonToGeo(VectorTileGeometry tileGeometry, Envelope tileEnvelope)
    {
        if (tileGeometry.CoordinateRings.Count == 0) return null;
        
        var exteriorRing = new LinearRing(tileGeometry.CoordinateRings[0]
            .Select(c => TileCoordToGeoCoord(c, tileEnvelope, 4096)).ToArray());
        
        var interiorRings = tileGeometry.CoordinateRings.Skip(1)
            .Where(ring => ring.Count >= 4)
            .Select(ring => new LinearRing(ring.Select(c => TileCoordToGeoCoord(c, tileEnvelope, 4096)).ToArray()))
            .ToList();
        
        return new Polygon(exteriorRing, interiorRings.ToArray());
    }

    private static ICoordinate TileCoordToGeoCoord(ICoordinate tileCoord, Envelope tileEnvelope, int tileExtent)
    {
        var geoX = tileEnvelope.MinX + (tileCoord.X / tileExtent) * tileEnvelope.Width;
        var geoY = tileEnvelope.MaxY - (tileCoord.Y / tileExtent) * tileEnvelope.Height;
        
        return new Coordinate(geoX, geoY);
    }

    private static AttributeTable CreateAttributeTable(Dictionary<string, object> properties)
    {
        var attributes = new AttributeTable();
        foreach (var prop in properties)
        {
            attributes[prop.Key] = prop.Value;
        }
        return attributes;
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

#region Supporting Classes

/// <summary>
/// 타일 좌표
/// </summary>
public struct TileCoordinate
{
    public int Z { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    public override string ToString() => $"{Z}/{X}/{Y}";
}

/// <summary>
/// 벡터 타일
/// </summary>
public class VectorTile
{
    public int Z { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Envelope Extent { get; set; } = new();
    public Dictionary<string, VectorTileLayer> Layers { get; set; } = new();
}

/// <summary>
/// 벡터 타일 레이어
/// </summary>
public class VectorTileLayer
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; } = 2;
    public int Extent { get; set; } = 4096;
    public List<string> Keys { get; set; } = new();
    public List<object> Values { get; set; } = new();
    public List<VectorTileFeature> Features { get; set; } = new();
}

/// <summary>
/// 벡터 타일 피처
/// </summary>
public class VectorTileFeature
{
    public ulong? Id { get; set; }
    public VectorTileGeometry Geometry { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public VectorTileGeometryType Type { get; set; }
}

/// <summary>
/// 벡터 타일 지오메트리
/// </summary>
public class VectorTileGeometry
{
    public VectorTileGeometryType Type { get; set; }
    public List<ICoordinate> Coordinates { get; set; } = new();
    public List<List<ICoordinate>> CoordinateRings { get; set; } = new(); // 폴리곤용
}

/// <summary>
/// 벡터 타일 지오메트리 타입
/// </summary>
public enum VectorTileGeometryType
{
    Unknown = 0,
    Point = 1,
    LineString = 2,
    Polygon = 3
}

/// <summary>
/// 벡터 타일 서비스 설정
/// </summary>
public class VectorTileServiceConfig
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public int MinZoom { get; set; } = 0;
    public int MaxZoom { get; set; } = 18;
    public List<string> Layers { get; set; } = new();
    public string Attribution { get; set; } = string.Empty;
    public string Format { get; set; } = "pbf"; // Protocol Buffer format
}

/// <summary>
/// 벡터 타일 소스
/// </summary>
public class VectorTileSource : DataSourceBase
{
    private readonly VectorTileClient _client;
    private readonly VectorTileServiceConfig _config;

    public VectorTileSource(VectorTileServiceConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _client = new VectorTileClient(config.Url, config.AccessToken);
    }

    public override string Name => _config.Name;
    public override string ConnectionString => _config.Url;
    public override DataSourceType SourceType => DataSourceType.WebService;

    public async Task<IEnumerable<IFeature>?> GetFeaturesAsync(Envelope? extent = null, IQueryFilter? filter = null)
    {
        if (extent == null) return null;

        try
        {
            // 적절한 줌 레벨 결정
            var zoom = DetermineZoomLevel(extent);
            
            // 벡터 타일들 가져오기
            var tiles = await _client.GetVectorTilesInEnvelopeAsync(zoom, extent);
            
            // 피처로 변환
            var features = _client.ConvertTilesToFeatures(tiles);
            
            // 필터 적용
            if (filter?.AttributeFilter?.WhereClause != null)
            {
                features = features.Where(f => ApplyFilter(f, filter));
            }

            return features;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetFeatures failed: {ex.Message}");
            return null;
        }
    }

    public IEnumerable<IFeature>? GetFeatures(Envelope? extent = null, IQueryFilter? filter = null)
    {
        return GetFeaturesAsync(extent, filter).Result;
    }

    private int DetermineZoomLevel(Envelope extent)
    {
        // 간단한 줌 레벨 결정 로직
        var resolution = Math.Min(extent.Width, extent.Height);
        
        var zoom = resolution switch
        {
            > 10 => Math.Max(_config.MinZoom, 5),
            > 1 => Math.Max(_config.MinZoom, 8),
            > 0.1 => Math.Max(_config.MinZoom, 10),
            > 0.01 => Math.Max(_config.MinZoom, 12),
            _ => Math.Min(_config.MaxZoom, 14)
        };

        return Math.Max(_config.MinZoom, Math.Min(_config.MaxZoom, zoom));
    }

    private static bool ApplyFilter(IFeature feature, IQueryFilter filter)
    {
        // 간단한 필터 적용 로직
        // 실제로는 더 복잡한 SQL 파싱이 필요
        return true;
    }

    #region Abstract Method Implementations

    public override IEnumerable<string> GetTableNames()
    {
        return new[] { "vector_tiles" };
    }

    public override async Task<bool> OpenAsync()
    {
        // Vector tiles don't need explicit connection opening
        IsConnected = true;
        return true;
    }

    public override void Close()
    {
        IsConnected = false;
    }

    public override async Task<TableSchema?> GetSchemaAsync(string tableName)
    {
        var schema = new TableSchema
        {
            TableName = tableName,
            GeometryType = "Mixed",
            SRID = 3857 // Web Mercator for vector tiles
        };
        
        // 기본 컬럼들 추가
        schema.Columns.Add(new ColumnInfo 
        { 
            Name = "id", 
            DataType = typeof(string),
            DatabaseTypeName = "varchar"
        });
        
        return schema;
    }

    public override async Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null)
    {
        // Vector tiles don't provide accurate counts without fetching all tiles
        return -1; // Unknown
    }

    public override async Task<Geometry.Envelope?> GetExtentAsync(string tableName)
    {
        // Return global extent for vector tiles
        return new Geometry.Envelope(-180, 180, -85.051, 85.051);
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
        // Vector tiles don't support direct feature lookup by ID
        return null;
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();
        }
        base.Dispose(disposing);
    }
}

#endregion