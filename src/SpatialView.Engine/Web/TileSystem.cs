using System.Drawing;
using System.Drawing.Imaging;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Rendering;
using SpatialView.Engine.Styling;
using GeomPoint = SpatialView.Engine.Geometry.Point;

namespace SpatialView.Engine.Web;

/// <summary>
/// 타일 시스템 관리자
/// </summary>
public class TileSystem
{
    private readonly TileSystemOptions _options;
    private readonly Dictionary<string, TileCache> _tileCaches;
    private readonly SemaphoreSlim _renderSemaphore;
    
    public TileSystemOptions Options => _options;
    
    public TileSystem(TileSystemOptions? options = null)
    {
        _options = options ?? new TileSystemOptions();
        _tileCaches = new Dictionary<string, TileCache>();
        _renderSemaphore = new SemaphoreSlim(_options.MaxConcurrentRenders);
    }
    
    /// <summary>
    /// 벡터 타일 생성
    /// </summary>
    public async Task<VectorTile?> GenerateVectorTileAsync(ILayer layer, int x, int y, int z)
    {
        var tileEnvelope = TileToEnvelope(x, y, z);
        var features = layer.GetFeatures(tileEnvelope).ToList();
        
        if (features.Count == 0)
            return null;
        
        var vectorTile = new VectorTile(x, y, z)
        {
            LayerName = layer.Name
        };
        
        // 피처를 타일 좌표계로 변환
        foreach (var feature in features)
        {
            if (feature.Geometry == null) continue;
            
            var tileGeometry = TransformToTileCoordinates(feature.Geometry, tileEnvelope);
            var tileFeature = new TileFeature
            {
                Id = feature.Id,
                Geometry = tileGeometry,
                Properties = feature.Attributes
            };
            
            vectorTile.Features.Add(tileFeature);
        }
        
        return vectorTile;
    }
    
    /// <summary>
    /// 래스터 타일 생성
    /// </summary>
    public async Task<Image?> GenerateRasterTileAsync(IMap map, int x, int y, int z)
    {
        var tileEnvelope = TileToEnvelope(x, y, z);
        
        await _renderSemaphore.WaitAsync();
        try
        {
            using var bitmap = new Bitmap(_options.TileSize, _options.TileSize);
            using var graphics = Graphics.FromImage(bitmap);
            
            // 배경 투명 처리
            graphics.Clear(Color.Transparent);
            
            // TODO: Renderer 클래스가 구현되면 활성화
            // var renderer = new Renderer(graphics, bitmap.Width, bitmap.Height);
            
            // 타일 영역으로 뷰 설정
            map.ZoomToExtent(tileEnvelope);
            
            // TODO: Renderer 클래스가 구현되면 활성화
            /*
            // 레이어 렌더링
            foreach (var layer in map.Layers.Where(l => l.Visible))
            {
                renderer.RenderLayer(layer, tileEnvelope);
            }
            */
            
            // 복사본 반환
            return new Bitmap(bitmap);
        }
        finally
        {
            _renderSemaphore.Release();
        }
    }
    
    /// <summary>
    /// 타일 캐시 가져오기 또는 생성
    /// </summary>
    public async Task<Image?> GetOrCreateTileAsync(string layerId, int x, int y, int z, 
        Func<Task<Image?>> tileGenerator)
    {
        var cache = GetOrCreateCache(layerId);
        var tileKey = $"{z}_{x}_{y}";
        
        // 캐시에서 찾기
        var cachedTile = cache.Get(tileKey);
        if (cachedTile != null)
            return cachedTile;
        
        // 새로 생성
        var tile = await tileGenerator();
        if (tile != null)
        {
            cache.Add(tileKey, tile);
        }
        
        return tile;
    }
    
    /// <summary>
    /// 타일 피라미드 생성
    /// </summary>
    public async Task GenerateTilePyramidAsync(IMap map, int minZoom, int maxZoom, 
        IProgress<TileGenerationProgress>? progress = null)
    {
        var totalTiles = CalculateTotalTiles(map.GetExtent(), minZoom, maxZoom);
        var processedTiles = 0;
        
        for (int z = minZoom; z <= maxZoom; z++)
        {
            var tiles = GetTilesForEnvelope(map.GetExtent(), z);
            
            foreach (var (x, y) in tiles)
            {
                var tile = await GenerateRasterTileAsync(map, x, y, z);
                
                if (tile != null && _options.SaveTilesToDisk)
                {
                    await SaveTileAsync(tile, x, y, z);
                }
                
                processedTiles++;
                
                // 진행 상황 보고
                progress?.Report(new TileGenerationProgress
                {
                    CurrentZoom = z,
                    CurrentX = x,
                    CurrentY = y,
                    ProcessedTiles = processedTiles,
                    TotalTiles = totalTiles,
                    PercentComplete = (double)processedTiles / totalTiles * 100
                });
            }
        }
    }
    
    /// <summary>
    /// MBTiles 형식으로 내보내기
    /// </summary>
    public async Task ExportToMBTilesAsync(string filePath, string layerId, 
        int minZoom, int maxZoom, Envelope bounds)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={filePath}");
        await connection.OpenAsync();
        
        // MBTiles 스키마 생성
        await CreateMBTilesSchema(connection);
        
        // 메타데이터 삽입
        await InsertMBTilesMetadata(connection, layerId, minZoom, maxZoom, bounds);
        
        var cache = GetOrCreateCache(layerId);
        
        for (int z = minZoom; z <= maxZoom; z++)
        {
            var tiles = GetTilesForEnvelope(bounds, z);
            
            foreach (var (x, y) in tiles)
            {
                var tileKey = $"{z}_{x}_{y}";
                var tile = cache.Get(tileKey);
                
                if (tile != null)
                {
                    await InsertTileToMBTiles(connection, x, y, z, tile);
                }
            }
        }
    }
    
    private TileCache GetOrCreateCache(string layerId)
    {
        if (!_tileCaches.TryGetValue(layerId, out var cache))
        {
            cache = new TileCache(_options.MaxCacheSizePerLayer);
            _tileCaches[layerId] = cache;
        }
        return cache;
    }
    
    private IGeometry TransformToTileCoordinates(IGeometry geometry, Envelope tileEnvelope)
    {
        var scaleX = _options.TileSize / tileEnvelope.Width;
        var scaleY = _options.TileSize / tileEnvelope.Height;
        
        return geometry.GeometryType switch
        {
            GeometryType.Point => TransformPoint((GeomPoint)geometry, tileEnvelope, scaleX, scaleY),
            GeometryType.LineString => TransformLineString((LineString)geometry, tileEnvelope, scaleX, scaleY),
            GeometryType.Polygon => TransformPolygon((Polygon)geometry, tileEnvelope, scaleX, scaleY),
            _ => geometry
        };
    }
    
    private GeomPoint TransformPoint(GeomPoint point, Envelope tileEnvelope, double scaleX, double scaleY)
    {
        var x = (point.X - tileEnvelope.MinX) * scaleX;
        var y = _options.TileSize - (point.Y - tileEnvelope.MinY) * scaleY; // Y축 반전
        return new GeomPoint(x, y);
    }
    
    private LineString TransformLineString(LineString lineString, Envelope tileEnvelope, 
        double scaleX, double scaleY)
    {
        var coords = lineString.Coordinates.Select(c =>
        {
            var x = (c.X - tileEnvelope.MinX) * scaleX;
            var y = _options.TileSize - (c.Y - tileEnvelope.MinY) * scaleY;
            return new Coordinate(x, y);
        }).ToArray();
        
        return new LineString(coords);
    }
    
    private Polygon TransformPolygon(Polygon polygon, Envelope tileEnvelope, 
        double scaleX, double scaleY)
    {
        var exteriorCoords = polygon.ExteriorRing.Coordinates.Select(c =>
        {
            var x = (c.X - tileEnvelope.MinX) * scaleX;
            var y = _options.TileSize - (c.Y - tileEnvelope.MinY) * scaleY;
            return new Coordinate(x, y);
        }).ToArray();
        
        var exterior = new LinearRing(exteriorCoords);
        
        var holes = polygon.InteriorRings.Select(hole =>
        {
            var holeCoords = hole.Coordinates.Select(c =>
            {
                var x = (c.X - tileEnvelope.MinX) * scaleX;
                var y = _options.TileSize - (c.Y - tileEnvelope.MinY) * scaleY;
                return new Coordinate(x, y);
            }).ToArray();
            
            return new LinearRing(holeCoords);
        }).ToArray();
        
        return new Polygon(exterior, holes);
    }
    
    private async Task SaveTileAsync(Image tile, int x, int y, int z)
    {
        var directory = Path.Combine(_options.TileCacheDirectory, z.ToString(), x.ToString());
        Directory.CreateDirectory(directory);
        
        var filePath = Path.Combine(directory, $"{y}.png");
        tile.Save(filePath, ImageFormat.Png);
    }
    
    private int CalculateTotalTiles(Envelope bounds, int minZoom, int maxZoom)
    {
        var total = 0;
        
        for (int z = minZoom; z <= maxZoom; z++)
        {
            var tiles = GetTilesForEnvelope(bounds, z);
            total += tiles.Count;
        }
        
        return total;
    }
    
    private async Task CreateMBTilesSchema(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var commands = new[]
        {
            @"CREATE TABLE IF NOT EXISTS metadata (
                name TEXT,
                value TEXT,
                PRIMARY KEY (name)
            )",
            @"CREATE TABLE IF NOT EXISTS tiles (
                zoom_level INTEGER,
                tile_column INTEGER,
                tile_row INTEGER,
                tile_data BLOB,
                PRIMARY KEY (zoom_level, tile_column, tile_row)
            )"
        };
        
        foreach (var cmd in commands)
        {
            using var command = connection.CreateCommand();
            command.CommandText = cmd;
            await command.ExecuteNonQueryAsync();
        }
    }
    
    private async Task InsertMBTilesMetadata(Microsoft.Data.Sqlite.SqliteConnection connection,
        string name, int minZoom, int maxZoom, Envelope bounds)
    {
        var metadata = new Dictionary<string, string>
        {
            ["name"] = name,
            ["type"] = "overlay",
            ["version"] = "1.0.0",
            ["description"] = $"Generated by SpatialView.Engine",
            ["format"] = "png",
            ["bounds"] = $"{bounds.MinX},{bounds.MinY},{bounds.MaxX},{bounds.MaxY}",
            ["minzoom"] = minZoom.ToString(),
            ["maxzoom"] = maxZoom.ToString()
        };
        
        foreach (var kvp in metadata)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO metadata (name, value) VALUES (@name, @value)";
            command.Parameters.AddWithValue("@name", kvp.Key);
            command.Parameters.AddWithValue("@value", kvp.Value);
            await command.ExecuteNonQueryAsync();
        }
    }
    
    private async Task InsertTileToMBTiles(Microsoft.Data.Sqlite.SqliteConnection connection,
        int x, int y, int z, Image tile)
    {
        // TMS 스타일로 Y 좌표 변환
        var tmsY = (int)Math.Pow(2, z) - 1 - y;
        
        using var ms = new MemoryStream();
        tile.Save(ms, ImageFormat.Png);
        var tileData = ms.ToArray();
        
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT OR REPLACE INTO tiles 
            (zoom_level, tile_column, tile_row, tile_data) 
            VALUES (@z, @x, @y, @data)";
        command.Parameters.AddWithValue("@z", z);
        command.Parameters.AddWithValue("@x", x);
        command.Parameters.AddWithValue("@y", tmsY);
        command.Parameters.AddWithValue("@data", tileData);
        
        await command.ExecuteNonQueryAsync();
    }
    
    public static Envelope TileToEnvelope(int x, int y, int z)
    {
        return WebMapClient.TileToEnvelope(x, y, z);
    }
    
    public static (int x, int y) CoordinateToTile(double lon, double lat, int z)
    {
        return WebMapClient.CoordinateToTile(lon, lat, z);
    }
    
    public static List<(int x, int y)> GetTilesForEnvelope(Envelope envelope, int z)
    {
        return WebMapClient.GetTilesForEnvelope(envelope, z);
    }
}

/// <summary>
/// 타일 시스템 옵션
/// </summary>
public class TileSystemOptions
{
    /// <summary>
    /// 타일 크기 (픽셀)
    /// </summary>
    public int TileSize { get; set; } = 256;
    
    /// <summary>
    /// 최대 동시 렌더링 수
    /// </summary>
    public int MaxConcurrentRenders { get; set; } = 4;
    
    /// <summary>
    /// 레이어당 최대 캐시 크기
    /// </summary>
    public int MaxCacheSizePerLayer { get; set; } = 1000;
    
    /// <summary>
    /// 타일을 디스크에 저장
    /// </summary>
    public bool SaveTilesToDisk { get; set; } = false;
    
    /// <summary>
    /// 타일 캐시 디렉토리
    /// </summary>
    public string TileCacheDirectory { get; set; } = "tiles";
}

/// <summary>
/// 벡터 타일
/// </summary>
public class VectorTile
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public string? LayerName { get; set; }
    public List<TileFeature> Features { get; }
    
    public VectorTile(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
        Features = new List<TileFeature>();
    }
}

/// <summary>
/// 타일 피처
/// </summary>
public class TileFeature
{
    public object Id { get; set; } = null!;
    public IGeometry? Geometry { get; set; }
    public IAttributeTable? Properties { get; set; }
}

/// <summary>
/// 타일 캐시
/// </summary>
public class TileCache
{
    private readonly Dictionary<string, Image> _cache;
    private readonly Queue<string> _accessOrder;
    private readonly int _maxSize;
    private readonly object _lock = new();
    
    public TileCache(int maxSize)
    {
        _maxSize = maxSize;
        _cache = new Dictionary<string, Image>();
        _accessOrder = new Queue<string>();
    }
    
    public void Add(string key, Image tile)
    {
        lock (_lock)
        {
            if (_cache.ContainsKey(key))
                return;
            
            if (_cache.Count >= _maxSize && _accessOrder.Count > 0)
            {
                var oldestKey = _accessOrder.Dequeue();
                if (_cache.TryGetValue(oldestKey, out var oldTile))
                {
                    oldTile.Dispose();
                    _cache.Remove(oldestKey);
                }
            }
            
            _cache[key] = tile;
            _accessOrder.Enqueue(key);
        }
    }
    
    public Image? Get(string key)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(key, out var tile) ? tile : null;
        }
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var tile in _cache.Values)
            {
                tile.Dispose();
            }
            _cache.Clear();
            _accessOrder.Clear();
        }
    }
}

/// <summary>
/// 타일 생성 진행 상황
/// </summary>
public class TileGenerationProgress
{
    public int CurrentZoom { get; set; }
    public int CurrentX { get; set; }
    public int CurrentY { get; set; }
    public int ProcessedTiles { get; set; }
    public int TotalTiles { get; set; }
    public double PercentComplete { get; set; }
}