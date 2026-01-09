using System.Collections.Concurrent;

namespace SpatialView.Engine.Data.Cache;

/// <summary>
/// 메모리 기반 타일 캐시
/// LRU (Least Recently Used) 정책을 사용하여 메모리 사용량 제한
/// </summary>
public class MemoryTileCache : ITileCache
{
    private readonly ConcurrentDictionary<string, CacheItem> _cache;
    private readonly ConcurrentDictionary<string, DateTime> _accessTimes;
    private readonly int _maxSize;
    private readonly long _maxMemoryBytes;
    private long _currentMemoryBytes;
    private readonly object _cleanupLock = new();

    public MemoryTileCache(int maxSize = 1000, long maxMemoryMB = 100)
    {
        _cache = new ConcurrentDictionary<string, CacheItem>();
        _accessTimes = new ConcurrentDictionary<string, DateTime>();
        _maxSize = maxSize;
        _maxMemoryBytes = maxMemoryMB * 1024 * 1024; // MB to bytes
        _currentMemoryBytes = 0;
    }

    public byte[]? GetTile(string key)
    {
        if (_cache.TryGetValue(key, out var item))
        {
            // 액세스 시간 업데이트
            _accessTimes[key] = DateTime.UtcNow;
            return item.Data;
        }
        
        return null;
    }

    public void SetTile(string key, byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        var item = new CacheItem
        {
            Data = data,
            Size = data.Length,
            CreatedAt = DateTime.UtcNow
        };

        // 새 아이템 추가
        if (_cache.TryAdd(key, item))
        {
            _accessTimes[key] = DateTime.UtcNow;
            Interlocked.Add(ref _currentMemoryBytes, data.Length);

            // 캐시 크기 또는 메모리 한계 초과 시 정리
            if (_cache.Count > _maxSize || _currentMemoryBytes > _maxMemoryBytes)
            {
                Task.Run(CleanupCache);
            }
        }
    }

    public void RemoveTile(string key)
    {
        if (_cache.TryRemove(key, out var item))
        {
            _accessTimes.TryRemove(key, out _);
            Interlocked.Add(ref _currentMemoryBytes, -item.Size);
        }
    }

    public void Clear()
    {
        _cache.Clear();
        _accessTimes.Clear();
        Interlocked.Exchange(ref _currentMemoryBytes, 0);
    }

    public bool ContainsKey(string key)
    {
        return _cache.ContainsKey(key);
    }

    public long GetCacheSize()
    {
        return _currentMemoryBytes;
    }

    public int GetCacheCount()
    {
        return _cache.Count;
    }

    /// <summary>
    /// LRU 정책으로 캐시 정리
    /// </summary>
    private void CleanupCache()
    {
        lock (_cleanupLock)
        {
            // 메모리나 개수가 한계를 초과했는지 다시 확인
            if (_cache.Count <= _maxSize && _currentMemoryBytes <= _maxMemoryBytes)
                return;

            try
            {
                // 액세스 시간 기준으로 정렬 (오래된 것부터)
                var sortedKeys = _accessTimes
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // 캐시 크기가 목표 크기의 80%가 될 때까지 제거
                var targetSize = (int)(_maxSize * 0.8);
                var targetMemory = (long)(_maxMemoryBytes * 0.8);
                
                foreach (var key in sortedKeys)
                {
                    if (_cache.Count <= targetSize && _currentMemoryBytes <= targetMemory)
                        break;

                    RemoveTile(key);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"Cache cleanup completed. Size: {_cache.Count}/{_maxSize}, " +
                    $"Memory: {_currentMemoryBytes / 1024 / 1024}MB/{_maxMemoryBytes / 1024 / 1024}MB");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache cleanup error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 캐시 통계 정보 가져오기
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalItems = _cache.Count,
            TotalMemoryBytes = _currentMemoryBytes,
            MaxItems = _maxSize,
            MaxMemoryBytes = _maxMemoryBytes,
            MemoryUsagePercentage = (double)_currentMemoryBytes / _maxMemoryBytes * 100,
            ItemUsagePercentage = (double)_cache.Count / _maxSize * 100
        };
    }

    /// <summary>
    /// 만료된 캐시 항목 제거
    /// </summary>
    /// <param name="maxAge">최대 보관 시간</param>
    public void RemoveExpiredItems(TimeSpan maxAge)
    {
        var cutoffTime = DateTime.UtcNow - maxAge;
        var expiredKeys = new List<string>();

        foreach (var kvp in _cache)
        {
            if (kvp.Value.CreatedAt < cutoffTime)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            RemoveTile(key);
        }

        if (expiredKeys.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Removed {expiredKeys.Count} expired cache items");
        }
    }

    public void Dispose()
    {
        Clear();
    }
}

/// <summary>
/// 캐시 아이템
/// </summary>
internal class CacheItem
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Size { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 캐시 통계 정보
/// </summary>
public class CacheStatistics
{
    public int TotalItems { get; set; }
    public long TotalMemoryBytes { get; set; }
    public int MaxItems { get; set; }
    public long MaxMemoryBytes { get; set; }
    public double MemoryUsagePercentage { get; set; }
    public double ItemUsagePercentage { get; set; }
}

/// <summary>
/// 타일 캐시 인터페이스
/// </summary>
public interface ITileCache : IDisposable
{
    byte[]? GetTile(string key);
    void SetTile(string key, byte[] data);
    void RemoveTile(string key);
    void Clear();
    bool ContainsKey(string key);
    long GetCacheSize();
    int GetCacheCount();
}