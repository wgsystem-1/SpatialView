using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Rendering.Optimization;

/// <summary>
/// 렌더링 캐시 시스템
/// 줌/팬 시 정적 레이어의 재렌더링을 방지
/// </summary>
public class RenderCache : IDisposable
{
    private readonly Dictionary<string, CacheEntry> _layerCache = new();
    private readonly object _lockObject = new();

    // 캐시 설정
    private const int MAX_CACHE_ENTRIES = 20;
    private const double ZOOM_TOLERANCE = 0.01; // 1% 줌 변경까지는 캐시 사용

    private bool _disposed;

    /// <summary>
    /// 레이어의 캐시된 렌더링 결과 가져오기
    /// </summary>
    /// <param name="layerId">레이어 식별자</param>
    /// <param name="currentZoom">현재 줌 레벨</param>
    /// <param name="currentExtent">현재 뷰 범위</param>
    /// <returns>캐시된 렌더링 결과 또는 null</returns>
    public DrawingVisual? GetCachedVisual(string layerId, double currentZoom, Envelope currentExtent)
    {
        lock (_lockObject)
        {
            if (!_layerCache.TryGetValue(layerId, out var entry))
                return null;

            // 줌 레벨 체크
            if (Math.Abs(entry.Zoom - currentZoom) / currentZoom > ZOOM_TOLERANCE)
                return null;

            // 뷰 범위 체크 (캐시된 범위가 현재 범위를 포함해야 함)
            if (!entry.Extent.Contains(currentExtent))
                return null;

            entry.LastAccess = DateTime.UtcNow;
            return entry.Visual;
        }
    }

    /// <summary>
    /// 레이어 렌더링 결과 캐시
    /// </summary>
    public void CacheVisual(string layerId, DrawingVisual visual, double zoom, Envelope extent, int featureCount)
    {
        lock (_lockObject)
        {
            // 캐시 크기 제한
            if (_layerCache.Count >= MAX_CACHE_ENTRIES)
            {
                EvictOldestEntry();
            }

            _layerCache[layerId] = new CacheEntry
            {
                Visual = visual,
                Zoom = zoom,
                Extent = extent,
                FeatureCount = featureCount,
                CreatedAt = DateTime.UtcNow,
                LastAccess = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 특정 레이어 캐시 무효화
    /// </summary>
    public void InvalidateLayer(string layerId)
    {
        lock (_lockObject)
        {
            _layerCache.Remove(layerId);
        }
    }

    /// <summary>
    /// 모든 캐시 무효화
    /// </summary>
    public void InvalidateAll()
    {
        lock (_lockObject)
        {
            _layerCache.Clear();
        }
    }

    /// <summary>
    /// 줌 변경에 따른 캐시 무효화
    /// </summary>
    public void InvalidateOnZoomChange(double oldZoom, double newZoom)
    {
        var zoomRatio = Math.Abs(newZoom - oldZoom) / oldZoom;
        if (zoomRatio > ZOOM_TOLERANCE)
        {
            InvalidateAll();
        }
    }

    /// <summary>
    /// 캐시 통계
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_lockObject)
        {
            return new CacheStatistics
            {
                EntryCount = _layerCache.Count,
                TotalFeatures = _layerCache.Values.Sum(e => e.FeatureCount)
            };
        }
    }

    private void EvictOldestEntry()
    {
        var oldest = _layerCache
            .OrderBy(kvp => kvp.Value.LastAccess)
            .FirstOrDefault();

        if (oldest.Key != null)
        {
            _layerCache.Remove(oldest.Key);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lockObject)
        {
            _layerCache.Clear();
        }
    }

    private class CacheEntry
    {
        public DrawingVisual Visual { get; set; } = null!;
        public double Zoom { get; set; }
        public Envelope Extent { get; set; } = null!;
        public int FeatureCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccess { get; set; }
    }
}

/// <summary>
/// 캐시 통계 정보
/// </summary>
public class CacheStatistics
{
    public int EntryCount { get; set; }
    public int TotalFeatures { get; set; }
}

/// <summary>
/// 타일 기반 렌더링 캐시 (대용량 데이터용)
/// </summary>
public class TileRenderCache : IDisposable
{
    private readonly Dictionary<TileKey, RenderTargetBitmap> _tileCache = new();
    private readonly object _lockObject = new();

    // 타일 설정
    public const int TILE_SIZE = 256;
    private const int MAX_TILES = 100;

    private int _currentZoomLevel = -1;
    private bool _disposed;

    /// <summary>
    /// 타일 가져오기 또는 렌더링
    /// </summary>
    public RenderTargetBitmap? GetTile(int tileX, int tileY, int zoomLevel)
    {
        var key = new TileKey(tileX, tileY, zoomLevel);

        lock (_lockObject)
        {
            // 줌 레벨이 변경되면 캐시 무효화
            if (_currentZoomLevel != zoomLevel)
            {
                _tileCache.Clear();
                _currentZoomLevel = zoomLevel;
            }

            _tileCache.TryGetValue(key, out var tile);
            return tile;
        }
    }

    /// <summary>
    /// 타일 캐시
    /// </summary>
    public void CacheTile(int tileX, int tileY, int zoomLevel, RenderTargetBitmap tile)
    {
        var key = new TileKey(tileX, tileY, zoomLevel);

        lock (_lockObject)
        {
            if (_tileCache.Count >= MAX_TILES)
            {
                // LRU 방식으로 오래된 타일 제거
                var toRemove = _tileCache.Keys.Take(10).ToList();
                foreach (var k in toRemove)
                {
                    _tileCache.Remove(k);
                }
            }

            _tileCache[key] = tile;
        }
    }

    /// <summary>
    /// 필요한 타일 목록 계산
    /// </summary>
    public static List<(int TileX, int TileY)> GetVisibleTiles(Envelope viewExtent, double zoom, int tileSize = TILE_SIZE)
    {
        var tiles = new List<(int, int)>();

        // 뷰 범위를 타일 좌표로 변환
        var resolution = zoom; // meters per pixel
        var worldWidth = viewExtent.Width / resolution;
        var worldHeight = viewExtent.Height / resolution;

        var startTileX = (int)Math.Floor(viewExtent.MinX / (tileSize * resolution));
        var startTileY = (int)Math.Floor(viewExtent.MinY / (tileSize * resolution));
        var endTileX = (int)Math.Ceiling(viewExtent.MaxX / (tileSize * resolution));
        var endTileY = (int)Math.Ceiling(viewExtent.MaxY / (tileSize * resolution));

        for (int x = startTileX; x <= endTileX; x++)
        {
            for (int y = startTileY; y <= endTileY; y++)
            {
                tiles.Add((x, y));
            }
        }

        return tiles;
    }

    /// <summary>
    /// 캐시 무효화
    /// </summary>
    public void InvalidateAll()
    {
        lock (_lockObject)
        {
            _tileCache.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lockObject)
        {
            _tileCache.Clear();
        }
    }

    private readonly struct TileKey : IEquatable<TileKey>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Zoom;

        public TileKey(int x, int y, int zoom)
        {
            X = x;
            Y = y;
            Zoom = zoom;
        }

        public bool Equals(TileKey other) => X == other.X && Y == other.Y && Zoom == other.Zoom;
        public override bool Equals(object? obj) => obj is TileKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Zoom);
    }
}
