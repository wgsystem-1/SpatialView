using SpatialView.Engine.Data.Sources;

namespace SpatialView.Engine.Data.Querying;

/// <summary>
/// 고급 쿼리 엔진
/// 다중 데이터 소스에 대한 통합 쿼리 기능을 제공
/// </summary>
public class QueryEngine
{
    private readonly Dictionary<string, IDataSource> _dataSources;
    private readonly QueryCache _cache;
    private readonly QueryOptimizer _optimizer;
    
    /// <summary>
    /// 캐싱 활성화 여부
    /// </summary>
    public bool EnableCaching { get; set; } = true;
    
    /// <summary>
    /// 쿼리 최적화 활성화 여부
    /// </summary>
    public bool EnableOptimization { get; set; } = true;
    
    /// <summary>
    /// 최대 동시 실행 쿼리 수
    /// </summary>
    public int MaxConcurrentQueries { get; set; } = 10;
    
    /// <summary>
    /// 쿼리 타임아웃 (초)
    /// </summary>
    public int QueryTimeoutSeconds { get; set; } = 60;
    
    /// <summary>
    /// 생성자
    /// </summary>
    public QueryEngine()
    {
        _dataSources = new Dictionary<string, IDataSource>();
        _cache = new QueryCache();
        _optimizer = new QueryOptimizer();
    }
    
    /// <summary>
    /// 데이터 소스 추가
    /// </summary>
    /// <param name="name">데이터 소스 이름</param>
    /// <param name="dataSource">데이터 소스</param>
    public void AddDataSource(string name, IDataSource dataSource)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Data source name cannot be null or empty", nameof(name));
        
        if (dataSource == null)
            throw new ArgumentNullException(nameof(dataSource));
        
        _dataSources[name] = dataSource;
    }
    
    /// <summary>
    /// 데이터 소스 제거
    /// </summary>
    /// <param name="name">데이터 소스 이름</param>
    /// <returns>제거 성공 여부</returns>
    public bool RemoveDataSource(string name)
    {
        return _dataSources.Remove(name);
    }
    
    /// <summary>
    /// 데이터 소스 가져오기
    /// </summary>
    /// <param name="name">데이터 소스 이름</param>
    /// <returns>데이터 소스</returns>
    public IDataSource? GetDataSource(string name)
    {
        return _dataSources.TryGetValue(name, out var dataSource) ? dataSource : null;
    }
    
    /// <summary>
    /// 모든 데이터 소스 이름 가져오기
    /// </summary>
    /// <returns>데이터 소스 이름 목록</returns>
    public IEnumerable<string> GetDataSourceNames()
    {
        return _dataSources.Keys.ToList();
    }
    
    /// <summary>
    /// 단일 테이블 쿼리
    /// </summary>
    /// <param name="dataSourceName">데이터 소스 이름</param>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="filter">쿼리 필터</param>
    /// <returns>피처 목록</returns>
    public async IAsyncEnumerable<IFeature> QueryAsync(string dataSourceName, string tableName, IQueryFilter? filter = null)
    {
        var dataSource = GetDataSource(dataSourceName);
        if (dataSource == null)
        {
            throw new ArgumentException($"Data source '{dataSourceName}' not found", nameof(dataSourceName));
        }
        
        // 캐시 확인
        if (EnableCaching && filter != null)
        {
            var cacheKey = _cache.GenerateCacheKey(dataSourceName, tableName, filter);
            var cachedResults = _cache.GetCachedResults(cacheKey);
            if (cachedResults != null)
            {
                foreach (var feature in cachedResults)
                {
                    yield return feature;
                }
                yield break;
            }
        }
        
        // 필터 변환
        Sources.IQueryFilter? sourcesFilter = null;
        if (filter != null)
        {
            sourcesFilter = ConvertToSourcesFilter(filter);
        }
        
        // 쿼리 최적화
        if (EnableOptimization && sourcesFilter != null)
        {
            sourcesFilter = _optimizer.OptimizeFilter(sourcesFilter);
        }
        
        // 데이터 소스에서 쿼리 실행
        var results = new List<IFeature>();
        
        await foreach (var feature in dataSource.QueryFeaturesAsync(tableName, sourcesFilter))
        {
            results.Add(feature);
            yield return feature;
        }
        
        // 결과 캐싱
        if (EnableCaching && filter != null && results.Count <= 1000) // 큰 결과는 캐싱하지 않음
        {
            var cacheKey = _cache.GenerateCacheKey(dataSourceName, tableName, filter);
            _cache.CacheResults(cacheKey, results, TimeSpan.FromMinutes(5));
        }
    }
    
    /// <summary>
    /// 다중 테이블 조인 쿼리
    /// </summary>
    /// <param name="joinQuery">조인 쿼리 정보</param>
    /// <returns>조인 결과</returns>
    public async IAsyncEnumerable<JoinResult> QueryJoinAsync(JoinQuery joinQuery)
    {
        if (joinQuery == null)
            throw new ArgumentNullException(nameof(joinQuery));
        
        // 왼쪽 테이블 쿼리
        var leftFeatures = new List<IFeature>();
        await foreach (var feature in QueryAsync(joinQuery.LeftDataSource, joinQuery.LeftTable, joinQuery.LeftFilter))
        {
            leftFeatures.Add(feature);
        }
        
        // 오른쪽 테이블 쿼리
        var rightFeatures = new List<IFeature>();
        await foreach (var feature in QueryAsync(joinQuery.RightDataSource, joinQuery.RightTable, joinQuery.RightFilter))
        {
            rightFeatures.Add(feature);
        }
        
        // 조인 수행
        await foreach (var result in PerformJoinAsync(leftFeatures, rightFeatures, joinQuery))
        {
            yield return result;
        }
    }
    
    /// <summary>
    /// 공간 쿼리 (근접, 교차 등)
    /// </summary>
    /// <param name="spatialQuery">공간 쿼리 정보</param>
    /// <returns>공간 쿼리 결과</returns>
    public async IAsyncEnumerable<SpatialQueryResult> QuerySpatialAsync(SpatialQuery spatialQuery)
    {
        if (spatialQuery == null)
            throw new ArgumentNullException(nameof(spatialQuery));
        
        var targetFeatures = new List<IFeature>();
        
        // 대상 피처들 수집
        foreach (var target in spatialQuery.Targets)
        {
            await foreach (var feature in QueryAsync(target.DataSource, target.Table, target.Filter))
            {
                targetFeatures.Add(feature);
            }
        }
        
        // 공간 분석 수행
        foreach (var sourceTarget in spatialQuery.Sources)
        {
            await foreach (var sourceFeature in QueryAsync(sourceTarget.DataSource, sourceTarget.Table, sourceTarget.Filter))
            {
                var spatialResults = PerformSpatialAnalysis(sourceFeature, targetFeatures, spatialQuery.Operation);
                
                foreach (var result in spatialResults)
                {
                    yield return result;
                }
            }
        }
    }
    
    /// <summary>
    /// 집계 쿼리 (COUNT, SUM, AVG 등)
    /// </summary>
    /// <param name="aggregateQuery">집계 쿼리 정보</param>
    /// <returns>집계 결과</returns>
    public async Task<AggregateResult> QueryAggregateAsync(AggregateQuery aggregateQuery)
    {
        if (aggregateQuery == null)
            throw new ArgumentNullException(nameof(aggregateQuery));
        
        var features = new List<IFeature>();
        
        // 피처 수집
        await foreach (var feature in QueryAsync(aggregateQuery.DataSource, aggregateQuery.Table, aggregateQuery.Filter))
        {
            features.Add(feature);
        }
        
        // 집계 계산
        return CalculateAggregate(features, aggregateQuery);
    }
    
    /// <summary>
    /// 배치 쿼리 (여러 쿼리를 동시에 실행)
    /// </summary>
    /// <param name="queries">쿼리 목록</param>
    /// <returns>배치 쿼리 결과</returns>
    public async Task<BatchQueryResult> QueryBatchAsync(IEnumerable<BatchQueryItem> queries)
    {
        if (queries == null)
            throw new ArgumentNullException(nameof(queries));
        
        var queryList = queries.ToList();
        var results = new Dictionary<string, List<IFeature>>();
        var errors = new Dictionary<string, string>();
        
        // 병렬 실행을 위한 세마포어
        using var semaphore = new SemaphoreSlim(MaxConcurrentQueries, MaxConcurrentQueries);
        
        var tasks = queryList.Select(async query =>
        {
            await semaphore.WaitAsync();
            try
            {
                var queryResults = new List<IFeature>();
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(QueryTimeoutSeconds));
                
                await foreach (var feature in QueryAsync(query.DataSource, query.Table, query.Filter).WithCancellation(cts.Token))
                {
                    queryResults.Add(feature);
                }
                
                lock (results)
                {
                    results[query.Id] = queryResults;
                }
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors[query.Id] = ex.Message;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        
        return new BatchQueryResult
        {
            Results = results,
            Errors = errors,
            TotalQueries = queryList.Count,
            SuccessfulQueries = results.Count,
            FailedQueries = errors.Count
        };
    }
    
    /// <summary>
    /// 쿼리 통계 정보 가져오기
    /// </summary>
    /// <returns>통계 정보</returns>
    public QueryEngineStatistics GetStatistics()
    {
        return new QueryEngineStatistics
        {
            DataSourceCount = _dataSources.Count,
            CacheStatistics = _cache.GetStatistics(),
            OptimizerStatistics = _optimizer.GetStatistics()
        };
    }
    
    /// <summary>
    /// 캐시 정리
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    #region 내부 메서드
    
    /// <summary>
    /// 조인 수행
    /// </summary>
    private async IAsyncEnumerable<JoinResult> PerformJoinAsync(
        IEnumerable<IFeature> leftFeatures,
        IEnumerable<IFeature> rightFeatures,
        JoinQuery joinQuery)
    {
        foreach (var leftFeature in leftFeatures)
        {
            foreach (var rightFeature in rightFeatures)
            {
                bool isMatch = false;
                
                switch (joinQuery.JoinType)
                {
                    case JoinType.AttributeJoin:
                        isMatch = EvaluateAttributeJoin(leftFeature, rightFeature, joinQuery.JoinCondition);
                        break;
                        
                    case JoinType.SpatialJoin:
                        isMatch = EvaluateSpatialJoin(leftFeature, rightFeature, joinQuery.SpatialRelation);
                        break;
                }
                
                if (isMatch)
                {
                    yield return new JoinResult
                    {
                        LeftFeature = leftFeature,
                        RightFeature = rightFeature,
                        JoinType = joinQuery.JoinType
                    };
                }
            }
            
            await Task.Yield(); // 비동기 양보
        }
    }
    
    /// <summary>
    /// 속성 조인 평가
    /// </summary>
    private bool EvaluateAttributeJoin(IFeature leftFeature, IFeature rightFeature, string joinCondition)
    {
        // 간단한 조인 조건 평가 (실제로는 더 복잡한 파서 필요)
        var parts = joinCondition.Split('=');
        if (parts.Length == 2)
        {
            var leftValue = leftFeature.GetAttribute(parts[0].Trim());
            var rightValue = rightFeature.GetAttribute(parts[1].Trim());
            
            return Equals(leftValue, rightValue);
        }
        
        return false;
    }
    
    /// <summary>
    /// 공간 조인 평가
    /// </summary>
    private bool EvaluateSpatialJoin(IFeature leftFeature, IFeature rightFeature, SpatialRelationship spatialRelation)
    {
        if (leftFeature.Geometry == null || rightFeature.Geometry == null)
            return false;
        
        return spatialRelation switch
        {
            SpatialRelationship.Intersects => leftFeature.Geometry.Intersects(rightFeature.Geometry),
            SpatialRelationship.Contains => leftFeature.Geometry.Contains(rightFeature.Geometry),
            SpatialRelationship.Within => rightFeature.Geometry.Contains(leftFeature.Geometry),
            _ => false
        };
    }
    
    /// <summary>
    /// 공간 분석 수행
    /// </summary>
    private IEnumerable<SpatialQueryResult> PerformSpatialAnalysis(
        IFeature sourceFeature,
        IEnumerable<IFeature> targetFeatures,
        SpatialOperation operation)
    {
        if (sourceFeature.Geometry == null)
            yield break;
        
        foreach (var targetFeature in targetFeatures)
        {
            if (targetFeature.Geometry == null)
                continue;
            
            bool matches = false;
            double? distance = null;
            
            switch (operation)
            {
                case SpatialOperation.Intersects:
                    matches = sourceFeature.Geometry.Intersects(targetFeature.Geometry);
                    break;
                    
                case SpatialOperation.Contains:
                    matches = sourceFeature.Geometry.Contains(targetFeature.Geometry);
                    break;
                    
                case SpatialOperation.Within:
                    matches = targetFeature.Geometry.Contains(sourceFeature.Geometry);
                    break;
                    
                case SpatialOperation.Distance:
                    distance = sourceFeature.Geometry.Distance(targetFeature.Geometry);
                    matches = true;
                    break;
            }
            
            if (matches)
            {
                yield return new SpatialQueryResult
                {
                    SourceFeature = sourceFeature,
                    TargetFeature = targetFeature,
                    Operation = operation,
                    Distance = distance
                };
            }
        }
    }
    
    /// <summary>
    /// 집계 계산
    /// </summary>
    private AggregateResult CalculateAggregate(IEnumerable<IFeature> features, AggregateQuery aggregateQuery)
    {
        var featureList = features.ToList();
        var result = new AggregateResult
        {
            Function = aggregateQuery.Function,
            FieldName = aggregateQuery.FieldName,
            Count = featureList.Count
        };
        
        if (featureList.Count == 0)
        {
            return result;
        }
        
        switch (aggregateQuery.Function)
        {
            case AggregateFunction.Count:
                result.Value = featureList.Count;
                break;
                
            case AggregateFunction.Sum:
            case AggregateFunction.Average:
            case AggregateFunction.Min:
            case AggregateFunction.Max:
                var numericValues = featureList
                    .Select(f => f.GetAttribute(aggregateQuery.FieldName))
                    .Where(v => v != null)
                    .Select(v => Convert.ToDouble(v))
                    .ToList();
                
                if (numericValues.Count > 0)
                {
                    result.Value = aggregateQuery.Function switch
                    {
                        AggregateFunction.Sum => numericValues.Sum(),
                        AggregateFunction.Average => numericValues.Average(),
                        AggregateFunction.Min => numericValues.Min(),
                        AggregateFunction.Max => numericValues.Max(),
                        _ => 0
                    };
                }
                break;
        }
        
        return result;
    }
    
    #endregion
    
    /// <summary>
    /// Data.IQueryFilter를 Sources.IQueryFilter로 변환
    /// </summary>
    private Sources.IQueryFilter ConvertToSourcesFilter(IQueryFilter filter)
    {
        var sourcesFilter = new Sources.QueryFilter();
        
        // 기본 속성 복사
        sourcesFilter.MaxFeatures = filter.MaxFeatures;
        sourcesFilter.Offset = filter.Offset ?? 0;
        sourcesFilter.IncludeGeometry = filter.IncludeGeometry;
        sourcesFilter.Distinct = filter.Distinct;
        sourcesFilter.TargetSRID = filter.TargetSRID;
        
        // 컬럼 복사
        if (filter.Columns != null)
        {
            sourcesFilter.Columns = new List<string>(filter.Columns);
        }
        
        return sourcesFilter;
    }
}