using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Data.Sources;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Styling;
using System.Net.Http;
using System.Windows.Media.Imaging;

namespace SpatialView.Engine.Web;

/// <summary>
/// 타일 기반 배경지도 레이어
/// </summary>
public class TileLayer : ILayer, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, BitmapImage> _tileCache = new();
    private readonly object _cacheLock = new();
    private const int MaxCacheSize = 500;
    private bool _disposed = false;
    
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Tile Layer";
    public string Description { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool IsVisible { get => Visible; set => Visible = value; }
    public bool Enabled { get => Visible; set => Visible = value; }
    public double Opacity { get; set; } = 1.0;
    public int ZIndex { get; set; } = -1000; // 배경지도는 항상 맨 아래
    public int SRID { get; set; } = 3857; // Web Mercator
    public Envelope? Extent { get; set; }
    public double MinimumZoom { get; set; } = 0;
    public double MaximumZoom { get; set; } = 20;
    public double MinVisible { get => MinimumZoom; set => MinimumZoom = value; }
    public double MaxVisible { get => MaximumZoom; set => MaximumZoom = value; }
    public double MinScale { get; set; } = 0;
    public double MaxScale { get; set; } = double.MaxValue;
    public bool Selectable { get; set; } = false;
    public bool IsSelectable { get => Selectable; set => Selectable = value; }
    public bool Editable { get; set; } = false;
    public bool IsEditable { get => Editable; set => Editable = value; }
    public long FeatureCount => 0;
    public IStyle? Style { get; set; }
    public IDataSource DataSource { get => null!; set { /* 타일 레이어는 DataSource 설정 불가 */ } }
    
    /// <summary>
    /// 타일 URL 템플릿 (예: https://tile.openstreetmap.org/{z}/{x}/{y}.png)
    /// </summary>
    public string UrlTemplate { get; set; } = string.Empty;
    
    /// <summary>
    /// 타일 크기 (기본 256)
    /// </summary>
    public int TileSize { get; set; } = 256;
    
    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    public int MinZoom { get; set; } = 0;
    
    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    public int MaxZoom { get; set; } = 19;
    
    /// <summary>
    /// 타일 소스 유형
    /// </summary>
    public TileSourceType SourceType { get; set; } = TileSourceType.OpenStreetMap;

    public TileLayer(string name = "Tile Layer")
    {
        Name = name;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpatialView/1.0");
        
        // 전체 세계 범위 (Web Mercator)
        Extent = new Envelope(-20037508.34, 20037508.34, -20037508.34, 20037508.34);
    }
    
    /// <summary>
    /// 타일 URL 생성
    /// </summary>
    public string GetTileUrl(int x, int y, int z)
    {
        if (!string.IsNullOrEmpty(UrlTemplate))
        {
            return UrlTemplate
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString())
                .Replace("{z}", z.ToString());
        }
        
        // 기본 타일 소스 URL
        return SourceType switch
        {
            TileSourceType.OpenStreetMap => $"https://tile.openstreetmap.org/{z}/{x}/{y}.png",
            TileSourceType.OpenCycleMap => $"https://tile.thunderforest.com/cycle/{z}/{x}/{y}.png",
            TileSourceType.OpenTopoMap => $"https://tile.opentopomap.org/{z}/{x}/{y}.png",
            TileSourceType.CartoLight => $"https://cartodb-basemaps-a.global.ssl.fastly.net/light_all/{z}/{x}/{y}.png",
            TileSourceType.CartoDark => $"https://cartodb-basemaps-a.global.ssl.fastly.net/dark_all/{z}/{x}/{y}.png",
            TileSourceType.StamenTerrain => $"https://stamen-tiles.a.ssl.fastly.net/terrain/{z}/{x}/{y}.jpg",
            TileSourceType.StamenWatercolor => $"https://stamen-tiles.a.ssl.fastly.net/watercolor/{z}/{x}/{y}.jpg",
            _ => $"https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        };
    }
    
    /// <summary>
    /// 타일 이미지 가져오기 (캐시 사용)
    /// </summary>
    public async Task<BitmapImage?> GetTileAsync(int x, int y, int z)
    {
        var key = $"{z}/{x}/{y}";
        
        lock (_cacheLock)
        {
            if (_tileCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }
        
        try
        {
            var url = GetTileUrl(x, y, z);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync();
                
                var bitmap = new BitmapImage();
                using (var stream = new System.IO.MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                
                lock (_cacheLock)
                {
                    // 캐시 크기 제한
                    if (_tileCache.Count >= MaxCacheSize)
                    {
                        // 가장 오래된 항목 제거 (간단한 구현)
                        var firstKey = _tileCache.Keys.First();
                        _tileCache.Remove(firstKey);
                    }
                    
                    _tileCache[key] = bitmap;
                }
                
                return bitmap;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"타일 로드 실패 ({key}): {ex.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// 동기 타일 가져오기 (캐시에서만)
    /// </summary>
    public BitmapImage? GetTileCached(int x, int y, int z)
    {
        var key = $"{z}/{x}/{y}";
        
        lock (_cacheLock)
        {
            if (_tileCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 뷰 범위에 필요한 타일 목록 계산
    /// </summary>
    public List<TileInfo> GetTilesInView(Envelope viewExtent, int zoomLevel)
    {
        var tiles = new List<TileInfo>();
        
        // 줌 레벨 제한
        zoomLevel = Math.Clamp(zoomLevel, MinZoom, MaxZoom);
        
        // 뷰 범위를 타일 좌표로 변환
        var (minTileX, minTileY) = WorldToTile(viewExtent.MinX, viewExtent.MaxY, zoomLevel);
        var (maxTileX, maxTileY) = WorldToTile(viewExtent.MaxX, viewExtent.MinY, zoomLevel);
        
        // 타일 범위 제한
        var maxTile = (1 << zoomLevel) - 1;
        minTileX = Math.Clamp(minTileX, 0, maxTile);
        maxTileX = Math.Clamp(maxTileX, 0, maxTile);
        minTileY = Math.Clamp(minTileY, 0, maxTile);
        maxTileY = Math.Clamp(maxTileY, 0, maxTile);
        
        for (int x = minTileX; x <= maxTileX; x++)
        {
            for (int y = minTileY; y <= maxTileY; y++)
            {
                var tileBounds = TileToWorld(x, y, zoomLevel);
                tiles.Add(new TileInfo
                {
                    X = x,
                    Y = y,
                    Z = zoomLevel,
                    Bounds = tileBounds
                });
            }
        }
        
        return tiles;
    }
    
    /// <summary>
    /// 세계 좌표를 타일 좌표로 변환 (Web Mercator)
    /// </summary>
    public static (int tileX, int tileY) WorldToTile(double worldX, double worldY, int zoom)
    {
        // Web Mercator 좌표를 경위도로 변환
        var lon = worldX / 20037508.34 * 180;
        var lat = Math.Atan(Math.Exp(worldY / 20037508.34 * Math.PI)) * 360 / Math.PI - 90;
        
        // 경위도를 타일 좌표로 변환
        var n = 1 << zoom;
        var tileX = (int)((lon + 180) / 360 * n);
        var latRad = lat * Math.PI / 180;
        var tileY = (int)((1 - Math.Log(Math.Tan(latRad) + 1 / Math.Cos(latRad)) / Math.PI) / 2 * n);
        
        return (tileX, tileY);
    }
    
    /// <summary>
    /// 타일 좌표를 세계 좌표 범위로 변환 (Web Mercator)
    /// </summary>
    public static Envelope TileToWorld(int tileX, int tileY, int zoom)
    {
        var n = 1 << zoom;
        
        // 타일 좌표를 경위도로 변환
        var lon1 = tileX / (double)n * 360 - 180;
        var lon2 = (tileX + 1) / (double)n * 360 - 180;
        
        var lat1Rad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * tileY / (double)n)));
        var lat2Rad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (tileY + 1) / (double)n)));
        var lat1 = lat1Rad * 180 / Math.PI;
        var lat2 = lat2Rad * 180 / Math.PI;
        
        // 경위도를 Web Mercator로 변환
        var x1 = lon1 * 20037508.34 / 180;
        var x2 = lon2 * 20037508.34 / 180;
        var y1 = Math.Log(Math.Tan((90 + lat1) * Math.PI / 360)) / Math.PI * 20037508.34;
        var y2 = Math.Log(Math.Tan((90 + lat2) * Math.PI / 360)) / Math.PI * 20037508.34;
        
        return new Envelope(x1, x2, Math.Min(y1, y2), Math.Max(y1, y2));
    }
    
    /// <summary>
    /// 줌 레벨 계산 (지도 줌에서)
    /// </summary>
    public static int CalculateZoomLevel(double mapZoom, int screenWidth)
    {
        // mapZoom = 지도 너비 (미터 단위)
        // 전체 세계 너비 = 40075016.68 미터
        var worldWidth = 40075016.68;
        var metersPerPixel = mapZoom / screenWidth;
        var zoom = Math.Log2(worldWidth / (metersPerPixel * 256));
        
        return Math.Clamp((int)Math.Round(zoom), 0, 19);
    }
    
    /// <summary>
    /// 캐시 비우기
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _tileCache.Clear();
        }
    }
    
    public void Refresh()
    {
        // 타일 레이어는 자동으로 갱신됨
    }
    
    public Envelope? GetExtent() => Extent;
    
    public IEnumerable<IFeature> GetFeatures(Envelope? extent) => Enumerable.Empty<IFeature>();
    
    public IEnumerable<IFeature> GetFeatures(IGeometry geometry) => Enumerable.Empty<IFeature>();
    
    public void AddFeature(IFeature feature) { /* 타일 레이어는 피처 추가 불가 */ }
    
    public void DeleteFeature(IFeature feature) { /* 타일 레이어는 피처 삭제 불가 */ }
    
    public void UpdateFeature(IFeature feature) { /* 타일 레이어는 피처 수정 불가 */ }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
                ClearCache();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// 타일 정보
/// </summary>
public class TileInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public Envelope? Bounds { get; set; }
}

/// <summary>
/// 타일 소스 유형
/// </summary>
public enum TileSourceType
{
    OpenStreetMap,
    OpenCycleMap,
    OpenTopoMap,
    CartoLight,
    CartoDark,
    StamenTerrain,
    StamenWatercolor,
    Custom
}
