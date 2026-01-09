using System.Collections.Specialized;
using System.Runtime.Caching;
using SpatialView.Engine.Data;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Memory;

/// <summary>
/// 피처 캐싱 시스템
/// LRU(Least Recently Used) 정책 기반 메모리 캐싱
/// </summary>
public class FeatureCache : IDisposable
{
    private readonly MemoryCache _cache;
    private readonly CacheItemPolicy _defaultPolicy;
    private readonly long _memoryCacheLimit;
    private long _currentMemoryUsage;
    private readonly object _lock = new();
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="name">캐시 이름</param>
    /// <param name="memoryCacheLimitMB">메모리 제한 (MB)</param>
    /// <param name="slidingExpiration">슬라이딩 만료 시간</param>
    public FeatureCache(string name = "FeatureCache", 
        long memoryCacheLimitMB = 100, 
        TimeSpan? slidingExpiration = null)
    {
        _memoryCacheLimit = memoryCacheLimitMB * 1024 * 1024; // MB to bytes
        
        var config = new NameValueCollection
        {
            { "cacheMemoryLimitMegabytes", memoryCacheLimitMB.ToString() },
            { "physicalMemoryLimitPercentage", "50" },
            { "pollingInterval", "00:01:00" }
        };
        
        _cache = new MemoryCache(name, config);
        
        _defaultPolicy = new CacheItemPolicy
        {
            SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(10),
            RemovedCallback = OnCacheItemRemoved
        };
    }
    
    /// <summary>
    /// 피처 추가
    /// </summary>
    public bool Add(string key, IFeature feature, CacheItemPolicy? policy = null)
    {
        if (string.IsNullOrEmpty(key) || feature == null)
            return false;
        
        var estimatedSize = EstimateFeatureSize(feature);
        
        lock (_lock)
        {
            // 메모리 제한 확인
            if (_currentMemoryUsage + estimatedSize > _memoryCacheLimit)
            {
                // 공간 확보를 위해 오래된 항목 제거
                TrimCache(estimatedSize);
            }
            
            var cacheItem = new CachedFeature(feature, estimatedSize);
            var result = _cache.Add(key, cacheItem, policy ?? _defaultPolicy);
            
            if (result)
            {
                _currentMemoryUsage += estimatedSize;
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 피처 가져오기
    /// </summary>
    public IFeature? Get(string key)
    {
        var cachedItem = _cache.Get(key) as CachedFeature;
        return cachedItem?.Feature;
    }
    
    /// <summary>
    /// 영역 기반 피처 가져오기
    /// </summary>
    public IEnumerable<IFeature> GetByEnvelope(Envelope envelope, string layerPrefix = "")
    {
        var features = new List<IFeature>();
        
        foreach (var item in _cache)
        {
            if (!string.IsNullOrEmpty(layerPrefix) && !item.Key.StartsWith(layerPrefix))
                continue;
            
            if (item.Value is CachedFeature cachedFeature)
            {
                var feature = cachedFeature.Feature;
                if (feature.BoundingBox?.Intersects(envelope) == true)
                {
                    features.Add(feature);
                }
            }
        }
        
        return features;
    }
    
    /// <summary>
    /// 캐시에서 피처 제거
    /// </summary>
    public bool Remove(string key)
    {
        lock (_lock)
        {
            var cachedItem = _cache.Get(key) as CachedFeature;
            if (cachedItem != null)
            {
                _currentMemoryUsage -= cachedItem.EstimatedSize;
                return _cache.Remove(key) != null;
            }
            return false;
        }
    }
    
    /// <summary>
    /// 레이어의 모든 피처 제거
    /// </summary>
    public void RemoveLayer(string layerName)
    {
        var keysToRemove = _cache
            .Where(kvp => kvp.Key.StartsWith($"{layerName}_"))
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            Remove(key);
        }
    }
    
    /// <summary>
    /// 캐시 비우기
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Trim(100); // 100% 제거
            _currentMemoryUsage = 0;
        }
    }
    
    /// <summary>
    /// 캐시 통계
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            ItemCount = _cache.GetCount(),
            MemoryUsageBytes = _currentMemoryUsage,
            MemoryLimitBytes = _memoryCacheLimit,
            MemoryUsagePercent = (_currentMemoryUsage * 100.0) / _memoryCacheLimit
        };
    }
    
    /// <summary>
    /// 피처 크기 추정
    /// </summary>
    private long EstimateFeatureSize(IFeature feature)
    {
        long size = 64; // 기본 오버헤드
        
        // 지오메트리 크기 추정
        if (feature.Geometry != null)
        {
            size += feature.Geometry.NumPoints * 24; // Coordinate당 약 24 bytes (X, Y, Z)
        }
        
        // 속성 크기 추정
        size += feature.Attributes.Count * 64; // 속성당 평균 64 bytes
        
        return size;
    }
    
    /// <summary>
    /// 캐시 공간 확보
    /// </summary>
    private void TrimCache(long requiredSpace)
    {
        var percentToTrim = Math.Min(50, (requiredSpace * 100.0) / _memoryCacheLimit + 10);
        _cache.Trim((int)percentToTrim);
    }
    
    /// <summary>
    /// 캐시 항목 제거 콜백
    /// </summary>
    private void OnCacheItemRemoved(CacheEntryRemovedArguments args)
    {
        if (args.CacheItem.Value is CachedFeature cachedFeature)
        {
            lock (_lock)
            {
                _currentMemoryUsage -= cachedFeature.EstimatedSize;
            }
        }
    }
    
    public void Dispose()
    {
        _cache?.Dispose();
    }
    
    /// <summary>
    /// 캐시된 피처 래퍼
    /// </summary>
    private class CachedFeature
    {
        public IFeature Feature { get; }
        public long EstimatedSize { get; }
        public DateTime CachedTime { get; }
        
        public CachedFeature(IFeature feature, long estimatedSize)
        {
            Feature = feature;
            EstimatedSize = estimatedSize;
            CachedTime = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// 캐시 통계
/// </summary>
public class CacheStatistics
{
    public long ItemCount { get; set; }
    public long MemoryUsageBytes { get; set; }
    public long MemoryLimitBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    
    public string FormatMemoryUsage()
    {
        return $"{MemoryUsageBytes / (1024.0 * 1024.0):F2} MB / {MemoryLimitBytes / (1024.0 * 1024.0):F2} MB ({MemoryUsagePercent:F1}%)";
    }
}