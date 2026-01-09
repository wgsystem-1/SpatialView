using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpatialView.Engine.Rendering.Tiles;

/// <summary>
/// WPF 기반 타일 렌더러 구현
/// 타일 캐싱과 비동기 로딩을 지원합니다
/// </summary>
public class TileRenderer : ITileRenderer
{
    private readonly ConcurrentDictionary<string, WeakReference<ImageSource>> _imageCache;
    private readonly object _cacheLock = new();
    private long _totalCacheSize = 0;
    
    /// <inheritdoc/>
    public bool EnableCaching { get; set; } = true;
    
    /// <inheritdoc/>
    public int MaxCacheSizeMB { get; set; } = 100; // 100MB 기본값
    
    /// <summary>
    /// 생성자
    /// </summary>
    public TileRenderer()
    {
        _imageCache = new ConcurrentDictionary<string, WeakReference<ImageSource>>();
        
        // 주기적으로 캐시 정리 (약한 참조 정리)
        var timer = new System.Timers.Timer(30000); // 30초마다
        timer.Elapsed += (_, _) => CleanupWeakReferences();
        timer.Start();
    }
    
    /// <inheritdoc/>
    public void RenderTiles(IEnumerable<ITile> tiles, RenderContext context)
    {
        if (tiles == null || context?.DrawingContext == null) return;

        // 타일을 거리 순으로 정렬 (가까운 것부터 렌더링)
        var sortedTiles = tiles
            .Where(tile => tile != null && IsTileInView(tile, context))
            .OrderBy(tile => GetTileDistanceFromCenter(tile, context))
            .ToList();

        foreach (var tile in sortedTiles)
        {
            try
            {
                RenderTile(tile, context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tile rendering error: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public void RenderTile(ITile tile, RenderContext context)
    {
        if (tile?.Image == null || context?.DrawingContext == null) return;

        // 타일 액세스 시간 업데이트
        tile.LastAccessed = DateTime.Now;

        // 캐시에서 이미지 확인
        ImageSource? image = null;
        
        if (EnableCaching && _imageCache.TryGetValue(tile.Id, out var weakRef))
        {
            if (weakRef.TryGetTarget(out var cachedImage))
            {
                image = cachedImage;
            }
            else
            {
                // 약한 참조가 해제됨, 캐시에서 제거
                _imageCache.TryRemove(tile.Id, out _);
            }
        }

        // 캐시에 없으면 타일 이미지 사용
        image ??= tile.Image;
        
        if (image == null) return;

        // 타일 경계를 화면 좌표로 변환
        var topLeft = context.MapToScreen(new Geometry.Coordinate(tile.Bounds.MinX, tile.Bounds.MaxY));
        var bottomRight = context.MapToScreen(new Geometry.Coordinate(tile.Bounds.MaxX, tile.Bounds.MinY));
        
        var rect = new Rect(topLeft, bottomRight);
        
        // 화면 범위와 교차하는지 확인
        var screenRect = new Rect(0, 0, context.ScreenSize.Width, context.ScreenSize.Height);
        if (!rect.IntersectsWith(screenRect)) return;

        // 타일 이미지 그리기
        context.DrawingContext.DrawImage(image, rect);

        // 캐시에 추가 (약한 참조로)
        if (EnableCaching && !_imageCache.ContainsKey(tile.Id))
        {
            AddToCache(tile.Id, image);
        }
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _imageCache.Clear();
            _totalCacheSize = 0;
        }
        
        // 가비지 컬렉션 강제 실행
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    #region 헬퍼 메서드

    /// <summary>
    /// 타일이 현재 뷰에 보이는지 확인
    /// </summary>
    private static bool IsTileInView(ITile tile, RenderContext context)
    {
        if (tile.Bounds == null) return false;
        return context.ViewExtent.Intersects(tile.Bounds);
    }

    /// <summary>
    /// 뷰 중심에서 타일까지의 거리 계산
    /// </summary>
    private static double GetTileDistanceFromCenter(ITile tile, RenderContext context)
    {
        var tileCenter = tile.Bounds.Centre;
        var viewCenter = context.ViewExtent.Centre;
        
        var dx = tileCenter.X - viewCenter.X;
        var dy = tileCenter.Y - viewCenter.Y;
        
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 이미지를 캐시에 추가
    /// </summary>
    private void AddToCache(string tileId, ImageSource image)
    {
        if (!EnableCaching) return;

        lock (_cacheLock)
        {
            // 캐시 크기 확인 및 정리
            var imageSize = EstimateImageSize(image);
            var maxCacheBytes = MaxCacheSizeMB * 1024 * 1024;
            
            if (_totalCacheSize + imageSize > maxCacheBytes)
            {
                CleanupCache();
            }

            // 캐시에 추가
            if (_totalCacheSize + imageSize <= maxCacheBytes)
            {
                _imageCache.TryAdd(tileId, new WeakReference<ImageSource>(image));
                _totalCacheSize += imageSize;
            }
        }
    }

    /// <summary>
    /// 이미지 크기 추정
    /// </summary>
    private static long EstimateImageSize(ImageSource image)
    {
        if (image is BitmapSource bitmap)
        {
            // 비트맵 크기 = 너비 × 높이 × 픽셀당 바이트 수
            var bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
            return (long)bitmap.PixelWidth * bitmap.PixelHeight * bytesPerPixel;
        }
        
        // 기본값
        return 256 * 256 * 4; // 256×256 RGBA
    }

    /// <summary>
    /// 캐시 정리 (LRU 방식)
    /// </summary>
    private void CleanupCache()
    {
        lock (_cacheLock)
        {
            var toRemove = new List<string>();
            
            foreach (var kvp in _imageCache)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            // 약한 참조가 해제된 항목들 제거
            foreach (var key in toRemove)
            {
                _imageCache.TryRemove(key, out _);
            }

            // 캐시 크기 재계산
            RecalculateCacheSize();

            // 여전히 캐시가 너무 크면 강제로 일부 항목 제거
            var maxCacheBytes = MaxCacheSizeMB * 1024 * 1024;
            if (_totalCacheSize > maxCacheBytes)
            {
                var currentItems = _imageCache.ToArray();
                var itemsToRemove = currentItems.Length / 4; // 25% 제거
                
                for (int i = 0; i < itemsToRemove && _imageCache.Count > 0; i++)
                {
                    var randomItem = currentItems[Random.Shared.Next(currentItems.Length)];
                    _imageCache.TryRemove(randomItem.Key, out _);
                }
                
                RecalculateCacheSize();
            }
        }
    }

    /// <summary>
    /// 약한 참조 정리
    /// </summary>
    private void CleanupWeakReferences()
    {
        lock (_cacheLock)
        {
            var toRemove = new List<string>();
            
            foreach (var kvp in _imageCache)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                _imageCache.TryRemove(key, out _);
            }

            if (toRemove.Count > 0)
            {
                RecalculateCacheSize();
            }
        }
    }

    /// <summary>
    /// 캐시 크기 재계산
    /// </summary>
    private void RecalculateCacheSize()
    {
        _totalCacheSize = 0;
        
        foreach (var kvp in _imageCache)
        {
            if (kvp.Value.TryGetTarget(out var image))
            {
                _totalCacheSize += EstimateImageSize(image);
            }
        }
    }

    #endregion

    #region IDisposable 구현

    private bool _disposed = false;

    /// <summary>
    /// 리소스 정리
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 리소스 정리 구현
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                ClearCache();
            }
            _disposed = true;
        }
    }

    #endregion
}