using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpatialView.Engine.Data.Sources;

namespace SpatialView.Engine.Data.Querying;

/// <summary>
/// 쿼리 결과 캐시
/// 자주 실행되는 쿼리의 결과를 메모리에 캐싱하여 성능을 향상시킵니다
/// </summary>
public class QueryCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly Timer _cleanupTimer;
    private long _cacheHits = 0;
    private long _cacheMisses = 0;
    
    /// <summary>
    /// 최대 캐시 크기 (항목 수)
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;
    
    /// <summary>
    /// 기본 캐시 만료 시간
    /// </summary>
    public TimeSpan DefaultExpirationTime { get; set; } = TimeSpan.FromMinutes(10);
    
    /// <summary>
    /// 최대 메모리 사용량 (바이트)
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 100 * 1024 * 1024; // 100MB
    
    /// <summary>
    /// 생성자
    /// </summary>
    public QueryCache()
    {
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        
        // 5분마다 만료된 캐시 정리
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
    /// <summary>
    /// 캐시 키 생성
    /// </summary>
    /// <param name="dataSource">데이터 소스 이름</param>
    /// <param name="table">테이블 이름</param>
    /// <param name="filter">쿼리 필터</param>
    /// <returns>캐시 키</returns>
    public string GenerateCacheKey(string dataSource, string table, IQueryFilter filter)
    {
        var keyData = new
        {
            DataSource = dataSource,
            Table = table,
            Filter = SerializeFilter(filter)
        };
        
        var json = JsonSerializer.Serialize(keyData);
        return ComputeHash(json);
    }
    
    /// <summary>
    /// 캐시에서 결과 가져오기
    /// </summary>
    /// <param name="cacheKey">캐시 키</param>
    /// <returns>캐시된 결과 또는 null</returns>
    public List<IFeature>? GetCachedResults(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (DateTime.UtcNow <= entry.ExpirationTime)
            {
                entry.LastAccessTime = DateTime.UtcNow;
                entry.AccessCount++;
                Interlocked.Increment(ref _cacheHits);
                return entry.Features;
            }
            else
            {
                // 만료된 엔트리 제거
                _cache.TryRemove(cacheKey, out _);
            }
        }
        
        Interlocked.Increment(ref _cacheMisses);
        return null;
    }
    
    /// <summary>
    /// 결과를 캐시에 저장
    /// </summary>
    /// <param name="cacheKey">캐시 키</param>
    /// <param name="features">캐시할 피처들</param>
    /// <param name="expirationTime">만료 시간 (null이면 기본값 사용)</param>
    public void CacheResults(string cacheKey, List<IFeature> features, TimeSpan? expirationTime = null)
    {
        if (features == null || features.Count == 0) return;
        
        var expiration = DateTime.UtcNow.Add(expirationTime ?? DefaultExpirationTime);
        var estimatedSize = EstimateSize(features);
        
        // 메모리 사용량 체크
        var currentMemoryUsage = GetEstimatedMemoryUsage();
        if (currentMemoryUsage + estimatedSize > MaxMemoryUsage)
        {
            // 오래된 캐시 정리
            CleanupOldEntries();
        }
        
        // 캐시 크기 체크
        if (_cache.Count >= MaxCacheSize)
        {
            CleanupLeastRecentlyUsed();
        }
        
        var entry = new CacheEntry
        {
            Features = new List<IFeature>(features),
            CreationTime = DateTime.UtcNow,
            ExpirationTime = expiration,
            LastAccessTime = DateTime.UtcNow,
            AccessCount = 0,
            EstimatedSize = estimatedSize
        };
        
        _cache.TryAdd(cacheKey, entry);
    }
    
    /// <summary>
    /// 특정 키의 캐시 무효화
    /// </summary>
    /// <param name="cacheKey">캐시 키</param>
    /// <returns>제거 성공 여부</returns>
    public bool InvalidateCache(string cacheKey)
    {
        return _cache.TryRemove(cacheKey, out _);
    }
    
    /// <summary>
    /// 패턴 매칭으로 캐시 무효화
    /// </summary>
    /// <param name="dataSource">데이터 소스 이름</param>
    /// <param name="table">테이블 이름 (null이면 모든 테이블)</param>
    public void InvalidateCache(string dataSource, string? table = null)
    {
        var keysToRemove = new List<string>();
        
        foreach (var kvp in _cache)
        {
            // 간단한 패턴 매칭 (실제로는 더 정교한 방법 필요)
            var key = kvp.Key;
            if (ShouldInvalidate(key, dataSource, table))
            {
                keysToRemove.Add(key);
            }
        }
        
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }
    
    /// <summary>
    /// 전체 캐시 정리
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _cacheHits, 0);
        Interlocked.Exchange(ref _cacheMisses, 0);
    }
    
    /// <summary>
    /// 캐시 통계 가져오기
    /// </summary>
    /// <returns>캐시 통계</returns>
    public QueryCacheStatistics GetStatistics()
    {
        return new QueryCacheStatistics
        {
            CachedQueries = _cache.Count,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            CacheMemoryUsage = GetEstimatedMemoryUsage()
        };
    }
    
    #region 내부 메서드
    
    /// <summary>
    /// 쿼리 필터 직렬화
    /// </summary>
    private string SerializeFilter(IQueryFilter filter)
    {
        if (filter == null) return "null";
        
        try
        {
            var filterData = new
            {
                SpatialFilter = SerializeSpatialFilter(filter.SpatialFilter),
                AttributeFilter = SerializeAttributeFilter(filter.AttributeFilter),
                Columns = filter.Columns,
                OrderBy = filter.OrderBy,
                MaxFeatures = filter.MaxFeatures,
                Offset = filter.Offset,
                IncludeGeometry = filter.IncludeGeometry,
                Distinct = filter.Distinct,
                TargetSRID = filter.TargetSRID
            };
            
            return JsonSerializer.Serialize(filterData);
        }
        catch
        {
            return filter.GetHashCode().ToString();
        }
    }
    
    /// <summary>
    /// 공간 필터 직렬화
    /// </summary>
    private object? SerializeSpatialFilter(ISpatialFilter? spatialFilter)
    {
        if (spatialFilter == null) return null;
        
        return new
        {
            Relationship = spatialFilter.Relationship,
            Distance = spatialFilter.Distance,
            DistanceUnit = spatialFilter.DistanceUnit,
            // 지오메트리는 WKT로 직렬화 (간단화)
            Geometry = spatialFilter.FilterGeometry?.ToString() ?? ""
        };
    }
    
    /// <summary>
    /// 속성 필터 직렬화
    /// </summary>
    private object? SerializeAttributeFilter(IAttributeFilter? attributeFilter)
    {
        if (attributeFilter == null) return null;
        
        return new
        {
            WhereClause = attributeFilter.WhereClause,
            Parameters = attributeFilter.Parameters
        };
    }
    
    /// <summary>
    /// OrderBy 데이터 추출
    /// </summary>
    private object? GetOrderByData(IList<Sources.SortField>? orderBy)
    {
        if (orderBy == null || orderBy.Count == 0)
            return null;
            
        return orderBy.Select(sf => new { sf.FieldName, sf.Direction }).ToList();
    }
    
    /// <summary>
    /// 해시 계산
    /// </summary>
    private string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
    
    /// <summary>
    /// 피처 목록의 크기 추정
    /// </summary>
    private long EstimateSize(List<IFeature> features)
    {
        if (features.Count == 0) return 0;
        
        // 간단한 크기 추정: 피처당 평균 1KB
        const long averageFeatureSize = 1024;
        return features.Count * averageFeatureSize;
    }
    
    /// <summary>
    /// 총 메모리 사용량 추정
    /// </summary>
    private long GetEstimatedMemoryUsage()
    {
        return _cache.Values.Sum(entry => entry.EstimatedSize);
    }
    
    /// <summary>
    /// 만료된 엔트리 정리
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = new List<string>();
        var now = DateTime.UtcNow;
        
        foreach (var kvp in _cache)
        {
            if (now > kvp.Value.ExpirationTime)
            {
                expiredKeys.Add(kvp.Key);
            }
        }
        
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }
    
    /// <summary>
    /// 오래된 엔트리 정리
    /// </summary>
    private void CleanupOldEntries()
    {
        var entriesToRemove = _cache.Values
            .OrderBy(e => e.LastAccessTime)
            .Take(_cache.Count / 4) // 25% 제거
            .ToList();
        
        var keysToRemove = _cache
            .Where(kvp => entriesToRemove.Contains(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }
    
    /// <summary>
    /// LRU 정리
    /// </summary>
    private void CleanupLeastRecentlyUsed()
    {
        var lruEntries = _cache.Values
            .OrderBy(e => e.AccessCount)
            .ThenBy(e => e.LastAccessTime)
            .Take(MaxCacheSize / 10) // 10% 제거
            .ToList();
        
        var keysToRemove = _cache
            .Where(kvp => lruEntries.Contains(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }
    
    /// <summary>
    /// 캐시 무효화 여부 판단
    /// </summary>
    private bool ShouldInvalidate(string cacheKey, string dataSource, string? table)
    {
        // 실제로는 캐시 키에서 메타데이터를 추출하여 판단
        // 지금은 간단한 문자열 매칭
        var keyLower = cacheKey.ToLowerInvariant();
        var dataSourceLower = dataSource.ToLowerInvariant();
        
        if (table != null)
        {
            var tableLower = table.ToLowerInvariant();
            return keyLower.Contains(dataSourceLower) && keyLower.Contains(tableLower);
        }
        
        return keyLower.Contains(dataSourceLower);
    }
    
    #endregion
    
    #region IDisposable
    
    private bool _disposed = false;
    
    /// <summary>
    /// 리소스 해제
    /// </summary>
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
                _cleanupTimer?.Dispose();
                Clear();
            }
            _disposed = true;
        }
    }
    
    #endregion
    
    /// <summary>
    /// 캐시 엔트리
    /// </summary>
    private class CacheEntry
    {
        public List<IFeature> Features { get; set; } = null!;
        public DateTime CreationTime { get; set; }
        public DateTime ExpirationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public int AccessCount { get; set; }
        public long EstimatedSize { get; set; }
    }
}

/// <summary>
/// 쿼리 최적화 엔진
/// 쿼리 필터를 분석하고 최적화하여 성능을 향상시킵니다
/// </summary>
public class QueryOptimizer
{
    private long _optimizedQueries = 0;
    private double _totalOptimizationTime = 0;
    
    /// <summary>
    /// 쿼리 필터 최적화
    /// </summary>
    /// <param name="filter">원본 필터</param>
    /// <returns>최적화된 필터</returns>
    public Sources.IQueryFilter OptimizeFilter(Sources.IQueryFilter filter)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var optimizedFilter = filter.Clone();
            
            // 최적화 수행
            OptimizeSpatialFilter(optimizedFilter);
            OptimizeAttributeFilter(optimizedFilter);
            OptimizeColumnSelection(optimizedFilter);
            OptimizeSorting(optimizedFilter);
            OptimizePaging(optimizedFilter);
            
            return optimizedFilter;
        }
        finally
        {
            var optimizationTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            Interlocked.Increment(ref _optimizedQueries);
            
            lock (this)
            {
                _totalOptimizationTime += optimizationTime;
            }
        }
    }
    
    /// <summary>
    /// 최적화 통계 가져오기
    /// </summary>
    /// <returns>최적화 통계</returns>
    public QueryOptimizerStatistics GetStatistics()
    {
        lock (this)
        {
            return new QueryOptimizerStatistics
            {
                OptimizedQueries = _optimizedQueries,
                AverageOptimizationTime = _optimizedQueries > 0 ? _totalOptimizationTime / _optimizedQueries : 0,
                AveragePerformanceImprovement = 0.15 // 임시값: 평균 15% 성능 향상
            };
        }
    }
    
    #region 최적화 메서드
    
    /// <summary>
    /// 공간 필터 최적화
    /// </summary>
    private void OptimizeSpatialFilter(Sources.IQueryFilter filter)
    {
        if (filter.SpatialFilter?.FilterGeometry == null) return;
        
        // 지오메트리 단순화 (작은 면적인 경우)
        var envelope = filter.SpatialFilter.FilterGeometry.Envelope;
        if (envelope != null && envelope.Area < 0.001) // 매우 작은 영역
        {
            // 점 쿼리로 단순화 가능
            var center = envelope.Centre;
            // TODO: 점 지오메트리로 변환
        }
    }
    
    /// <summary>
    /// 속성 필터 최적화
    /// </summary>
    private void OptimizeAttributeFilter(Sources.IQueryFilter filter)
    {
        if (filter.AttributeFilter?.WhereClause == null) return;
        
        // WHERE 절 최적화
        var whereClause = filter.AttributeFilter.WhereClause;
        
        // 불필요한 공백 제거
        whereClause = System.Text.RegularExpressions.Regex.Replace(whereClause, @"\s+", " ").Trim();
        
        // 상수 폴딩 (예: 1=1 제거)
        whereClause = whereClause.Replace(" AND 1=1", "").Replace("1=1 AND ", "");
        
        filter.AttributeFilter.WhereClause = whereClause;
    }
    
    /// <summary>
    /// 컬럼 선택 최적화
    /// </summary>
    private void OptimizeColumnSelection(Sources.IQueryFilter filter)
    {
        // 중복 컬럼 제거
        if (filter.Columns?.Count > 0)
        {
            var uniqueColumns = filter.Columns.Distinct().ToList();
            filter.Columns.Clear();
            foreach (var column in uniqueColumns)
            {
                filter.Columns.Add(column);
            }
        }
    }
    
    /// <summary>
    /// 정렬 최적화
    /// </summary>
    private void OptimizeSorting(Sources.IQueryFilter filter)
    {
        if (filter.OrderBy?.Count > 0)
        {
            // 중복 정렬 필드 제거
            var uniqueSortFields = new List<SortField>();
            var seenFields = new HashSet<string>();
            
            foreach (var sortField in filter.OrderBy)
            {
                if (seenFields.Add(sortField.FieldName))
                {
                    uniqueSortFields.Add(sortField);
                }
            }
            
            filter.OrderBy.Clear();
            foreach (var sortField in uniqueSortFields)
            {
                filter.OrderBy.Add(sortField);
            }
        }
    }
    
    /// <summary>
    /// 페이징 최적화
    /// </summary>
    private void OptimizePaging(Sources.IQueryFilter filter)
    {
        // 비합리적인 페이징 설정 조정
        if (filter.MaxFeatures > 100000) // 너무 큰 제한값
        {
            filter.MaxFeatures = 10000; // 합리적인 값으로 조정
        }
        
        if (filter.Offset < 0)
        {
            filter.Offset = 0;
        }
    }
    
    #endregion
}