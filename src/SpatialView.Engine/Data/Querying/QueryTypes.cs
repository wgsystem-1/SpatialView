using SpatialView.Engine.Data.Sources;

namespace SpatialView.Engine.Data.Querying;

/// <summary>
/// 조인 쿼리 정보
/// </summary>
public class JoinQuery
{
    /// <summary>
    /// 왼쪽 데이터 소스
    /// </summary>
    public string LeftDataSource { get; set; } = string.Empty;
    
    /// <summary>
    /// 왼쪽 테이블
    /// </summary>
    public string LeftTable { get; set; } = string.Empty;
    
    /// <summary>
    /// 왼쪽 필터
    /// </summary>
    public IQueryFilter? LeftFilter { get; set; }
    
    /// <summary>
    /// 오른쪽 데이터 소스
    /// </summary>
    public string RightDataSource { get; set; } = string.Empty;
    
    /// <summary>
    /// 오른쪽 테이블
    /// </summary>
    public string RightTable { get; set; } = string.Empty;
    
    /// <summary>
    /// 오른쪽 필터
    /// </summary>
    public IQueryFilter? RightFilter { get; set; }
    
    /// <summary>
    /// 조인 타입
    /// </summary>
    public JoinType JoinType { get; set; }
    
    /// <summary>
    /// 조인 조건 (속성 조인인 경우)
    /// </summary>
    public string JoinCondition { get; set; } = string.Empty;
    
    /// <summary>
    /// 공간 관계 (공간 조인인 경우)
    /// </summary>
    public SpatialRelationship SpatialRelation { get; set; }
}

/// <summary>
/// 공간 쿼리 정보
/// </summary>
public class SpatialQuery
{
    /// <summary>
    /// 소스 테이블들 (공간 분석 대상)
    /// </summary>
    public List<QueryTarget> Sources { get; set; } = new();
    
    /// <summary>
    /// 대상 테이블들 (공간 분석과 비교할 대상)
    /// </summary>
    public List<QueryTarget> Targets { get; set; } = new();
    
    /// <summary>
    /// 공간 연산
    /// </summary>
    public SpatialOperation Operation { get; set; }
    
    /// <summary>
    /// 거리 제한 (Distance 연산인 경우)
    /// </summary>
    public double? MaxDistance { get; set; }
    
    /// <summary>
    /// 거리 단위
    /// </summary>
    public DistanceUnit DistanceUnit { get; set; } = DistanceUnit.Meters;
}

/// <summary>
/// 집계 쿼리 정보
/// </summary>
public class AggregateQuery
{
    /// <summary>
    /// 데이터 소스
    /// </summary>
    public string DataSource { get; set; } = string.Empty;
    
    /// <summary>
    /// 테이블
    /// </summary>
    public string Table { get; set; } = string.Empty;
    
    /// <summary>
    /// 필터
    /// </summary>
    public IQueryFilter? Filter { get; set; }
    
    /// <summary>
    /// 집계 함수
    /// </summary>
    public AggregateFunction Function { get; set; }
    
    /// <summary>
    /// 집계 대상 필드
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// 그룹화 필드들
    /// </summary>
    public List<string> GroupByFields { get; set; } = new();
}

/// <summary>
/// 배치 쿼리 항목
/// </summary>
public class BatchQueryItem
{
    /// <summary>
    /// 쿼리 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// 데이터 소스
    /// </summary>
    public string DataSource { get; set; } = string.Empty;
    
    /// <summary>
    /// 테이블
    /// </summary>
    public string Table { get; set; } = string.Empty;
    
    /// <summary>
    /// 필터
    /// </summary>
    public IQueryFilter? Filter { get; set; }
    
    /// <summary>
    /// 우선순위
    /// </summary>
    public int Priority { get; set; } = 0;
}

/// <summary>
/// 쿼리 대상
/// </summary>
public class QueryTarget
{
    /// <summary>
    /// 데이터 소스
    /// </summary>
    public string DataSource { get; set; } = string.Empty;
    
    /// <summary>
    /// 테이블
    /// </summary>
    public string Table { get; set; } = string.Empty;
    
    /// <summary>
    /// 필터
    /// </summary>
    public IQueryFilter? Filter { get; set; }
}

/// <summary>
/// 조인 타입
/// </summary>
public enum JoinType
{
    /// <summary>
    /// 속성 조인
    /// </summary>
    AttributeJoin,
    
    /// <summary>
    /// 공간 조인
    /// </summary>
    SpatialJoin
}

/// <summary>
/// 공간 연산
/// </summary>
public enum SpatialOperation
{
    /// <summary>
    /// 교차
    /// </summary>
    Intersects,
    
    /// <summary>
    /// 포함
    /// </summary>
    Contains,
    
    /// <summary>
    /// 포함됨
    /// </summary>
    Within,
    
    /// <summary>
    /// 중첩
    /// </summary>
    Overlaps,
    
    /// <summary>
    /// 접촉
    /// </summary>
    Touches,
    
    /// <summary>
    /// 거리 계산
    /// </summary>
    Distance,
    
    /// <summary>
    /// 버퍼
    /// </summary>
    Buffer
}

/// <summary>
/// 집계 함수
/// </summary>
public enum AggregateFunction
{
    /// <summary>
    /// 개수
    /// </summary>
    Count,
    
    /// <summary>
    /// 합계
    /// </summary>
    Sum,
    
    /// <summary>
    /// 평균
    /// </summary>
    Average,
    
    /// <summary>
    /// 최솟값
    /// </summary>
    Min,
    
    /// <summary>
    /// 최댓값
    /// </summary>
    Max,
    
    /// <summary>
    /// 표준편차
    /// </summary>
    StdDev,
    
    /// <summary>
    /// 분산
    /// </summary>
    Variance
}

/// <summary>
/// 조인 결과
/// </summary>
public class JoinResult
{
    /// <summary>
    /// 왼쪽 피처
    /// </summary>
    public IFeature LeftFeature { get; set; } = null!;
    
    /// <summary>
    /// 오른쪽 피처
    /// </summary>
    public IFeature RightFeature { get; set; } = null!;
    
    /// <summary>
    /// 조인 타입
    /// </summary>
    public JoinType JoinType { get; set; }
}

/// <summary>
/// 공간 쿼리 결과
/// </summary>
public class SpatialQueryResult
{
    /// <summary>
    /// 소스 피처
    /// </summary>
    public IFeature SourceFeature { get; set; } = null!;
    
    /// <summary>
    /// 대상 피처
    /// </summary>
    public IFeature TargetFeature { get; set; } = null!;
    
    /// <summary>
    /// 공간 연산
    /// </summary>
    public SpatialOperation Operation { get; set; }
    
    /// <summary>
    /// 거리 (Distance 연산인 경우)
    /// </summary>
    public double? Distance { get; set; }
    
    /// <summary>
    /// 결과 지오메트리 (Buffer, Intersection 등)
    /// </summary>
    public Geometry.IGeometry? ResultGeometry { get; set; }
}

/// <summary>
/// 집계 결과
/// </summary>
public class AggregateResult
{
    /// <summary>
    /// 집계 함수
    /// </summary>
    public AggregateFunction Function { get; set; }
    
    /// <summary>
    /// 필드 이름
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// 집계 값
    /// </summary>
    public object? Value { get; set; }
    
    /// <summary>
    /// 처리된 레코드 수
    /// </summary>
    public int Count { get; set; }
    
    /// <summary>
    /// 그룹화된 결과들
    /// </summary>
    public Dictionary<string, object> GroupedResults { get; set; } = new();
}

/// <summary>
/// 배치 쿼리 결과
/// </summary>
public class BatchQueryResult
{
    /// <summary>
    /// 쿼리별 결과
    /// </summary>
    public Dictionary<string, List<IFeature>> Results { get; set; } = new();
    
    /// <summary>
    /// 쿼리별 오류
    /// </summary>
    public Dictionary<string, string> Errors { get; set; } = new();
    
    /// <summary>
    /// 총 쿼리 수
    /// </summary>
    public int TotalQueries { get; set; }
    
    /// <summary>
    /// 성공한 쿼리 수
    /// </summary>
    public int SuccessfulQueries { get; set; }
    
    /// <summary>
    /// 실패한 쿼리 수
    /// </summary>
    public int FailedQueries { get; set; }
    
    /// <summary>
    /// 실행 시간
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// 쿼리 엔진 통계
/// </summary>
public class QueryEngineStatistics
{
    /// <summary>
    /// 등록된 데이터 소스 수
    /// </summary>
    public int DataSourceCount { get; set; }
    
    /// <summary>
    /// 캐시 통계
    /// </summary>
    public QueryCacheStatistics? CacheStatistics { get; set; }
    
    /// <summary>
    /// 최적화 통계
    /// </summary>
    public QueryOptimizerStatistics? OptimizerStatistics { get; set; }
    
    /// <summary>
    /// 총 실행된 쿼리 수
    /// </summary>
    public long TotalQueriesExecuted { get; set; }
    
    /// <summary>
    /// 평균 쿼리 실행 시간 (밀리초)
    /// </summary>
    public double AverageQueryTime { get; set; }
    
    /// <summary>
    /// 활성 쿼리 수
    /// </summary>
    public int ActiveQueries { get; set; }
}

/// <summary>
/// 쿼리 캐시 통계
/// </summary>
public class QueryCacheStatistics
{
    /// <summary>
    /// 캐시된 쿼리 수
    /// </summary>
    public int CachedQueries { get; set; }
    
    /// <summary>
    /// 캐시 히트 수
    /// </summary>
    public long CacheHits { get; set; }
    
    /// <summary>
    /// 캐시 미스 수
    /// </summary>
    public long CacheMisses { get; set; }
    
    /// <summary>
    /// 캐시 히트 비율
    /// </summary>
    public double CacheHitRatio => CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;
    
    /// <summary>
    /// 캐시 메모리 사용량 (바이트)
    /// </summary>
    public long CacheMemoryUsage { get; set; }
}

/// <summary>
/// 쿼리 최적화 통계
/// </summary>
public class QueryOptimizerStatistics
{
    /// <summary>
    /// 최적화된 쿼리 수
    /// </summary>
    public long OptimizedQueries { get; set; }
    
    /// <summary>
    /// 평균 최적화 시간 (밀리초)
    /// </summary>
    public double AverageOptimizationTime { get; set; }
    
    /// <summary>
    /// 최적화로 인한 평균 성능 향상 비율
    /// </summary>
    public double AveragePerformanceImprovement { get; set; }
}