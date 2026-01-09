namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// 쿼리 필터 인터페이스
/// 데이터 검색 시 적용할 조건들을 정의
/// </summary>
public interface IQueryFilter
{
    /// <summary>
    /// 공간 필터 (지오메트리 조건)
    /// </summary>
    ISpatialFilter? SpatialFilter { get; set; }
    
    /// <summary>
    /// 속성 필터 (WHERE 조건)
    /// </summary>
    IAttributeFilter? AttributeFilter { get; set; }
    
    /// <summary>
    /// 반환할 컬럼 목록 (null이면 모든 컬럼)
    /// </summary>
    IList<string>? Columns { get; set; }
    
    /// <summary>
    /// 정렬 조건
    /// </summary>
    IList<SortField>? OrderBy { get; set; }
    
    /// <summary>
    /// 반환할 최대 피처 수 (0이면 제한 없음)
    /// </summary>
    int MaxFeatures { get; set; }
    
    /// <summary>
    /// 건너뛸 피처 수 (페이징용)
    /// </summary>
    int Offset { get; set; }
    
    /// <summary>
    /// 지오메트리 포함 여부
    /// </summary>
    bool IncludeGeometry { get; set; }
    
    /// <summary>
    /// 고유값만 반환 여부
    /// </summary>
    bool Distinct { get; set; }
    
    /// <summary>
    /// 좌표계 변환 대상 SRID (0이면 변환 안 함)
    /// </summary>
    int TargetSRID { get; set; }
    
    /// <summary>
    /// 필터 복제
    /// </summary>
    IQueryFilter Clone();
}

/// <summary>
/// 공간 필터 인터페이스
/// </summary>
public interface ISpatialFilter
{
    /// <summary>
    /// 필터 지오메트리
    /// </summary>
    Geometry.IGeometry FilterGeometry { get; set; }
    
    /// <summary>
    /// 공간 관계 연산자
    /// </summary>
    SpatialRelationship Relationship { get; set; }
    
    /// <summary>
    /// 거리 (Distance 관계일 때 사용)
    /// </summary>
    double Distance { get; set; }
    
    /// <summary>
    /// 거리 단위
    /// </summary>
    DistanceUnit DistanceUnit { get; set; }
}

/// <summary>
/// 속성 필터 인터페이스
/// </summary>
public interface IAttributeFilter
{
    /// <summary>
    /// WHERE 절 조건
    /// </summary>
    string WhereClause { get; set; }
    
    /// <summary>
    /// 매개변수 목록
    /// </summary>
    IDictionary<string, object> Parameters { get; set; }
}

/// <summary>
/// 공간 관계 열거형
/// </summary>
public enum SpatialRelationship
{
    /// <summary>
    /// 교차 (기본값)
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
    /// 분리
    /// </summary>
    Disjoint,
    
    /// <summary>
    /// 특정 거리 내
    /// </summary>
    Distance,
    
    /// <summary>
    /// 완전히 같음
    /// </summary>
    Equals
}

/// <summary>
/// 거리 단위 열거형
/// </summary>
public enum DistanceUnit
{
    /// <summary>
    /// 미터 (기본값)
    /// </summary>
    Meters,
    
    /// <summary>
    /// 킬로미터
    /// </summary>
    Kilometers,
    
    /// <summary>
    /// 피트
    /// </summary>
    Feet,
    
    /// <summary>
    /// 마일
    /// </summary>
    Miles,
    
    /// <summary>
    /// 도 (각도)
    /// </summary>
    Degrees,
    
    /// <summary>
    /// 맵 단위 (좌표계 기본 단위)
    /// </summary>
    MapUnits
}

/// <summary>
/// 정렬 필드
/// </summary>
public class SortField
{
    /// <summary>
    /// 필드 이름
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// 정렬 방향
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
    
    /// <summary>
    /// 생성자
    /// </summary>
    public SortField() { }
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="fieldName">필드 이름</param>
    /// <param name="direction">정렬 방향</param>
    public SortField(string fieldName, SortDirection direction = SortDirection.Ascending)
    {
        FieldName = fieldName;
        Direction = direction;
    }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        var directionStr = Direction == SortDirection.Ascending ? "ASC" : "DESC";
        return $"{FieldName} {directionStr}";
    }
}

/// <summary>
/// 정렬 방향
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// 오름차순
    /// </summary>
    Ascending,
    
    /// <summary>
    /// 내림차순
    /// </summary>
    Descending
}

/// <summary>
/// 기본 쿼리 필터 구현
/// </summary>
public class QueryFilter : IQueryFilter
{
    /// <inheritdoc/>
    public ISpatialFilter? SpatialFilter { get; set; }
    
    /// <inheritdoc/>
    public IAttributeFilter? AttributeFilter { get; set; }
    
    /// <inheritdoc/>
    public IList<string>? Columns { get; set; }
    
    /// <inheritdoc/>
    public IList<SortField>? OrderBy { get; set; }
    
    /// <inheritdoc/>
    public int MaxFeatures { get; set; } = 0;
    
    /// <inheritdoc/>
    public int Offset { get; set; } = 0;
    
    /// <inheritdoc/>
    public bool IncludeGeometry { get; set; } = true;
    
    /// <inheritdoc/>
    public bool Distinct { get; set; } = false;
    
    /// <inheritdoc/>
    public int TargetSRID { get; set; } = 0;
    
    /// <inheritdoc/>
    public virtual IQueryFilter Clone()
    {
        return new QueryFilter
        {
            SpatialFilter = SpatialFilter,
            AttributeFilter = AttributeFilter,
            Columns = Columns?.ToList(),
            OrderBy = OrderBy?.ToList(),
            MaxFeatures = MaxFeatures,
            Offset = Offset,
            IncludeGeometry = IncludeGeometry,
            Distinct = Distinct,
            TargetSRID = TargetSRID
        };
    }
}

/// <summary>
/// 공간 필터 구현
/// </summary>
public class SpatialFilter : ISpatialFilter
{
    /// <inheritdoc/>
    public Geometry.IGeometry FilterGeometry { get; set; } = null!;
    
    /// <inheritdoc/>
    public SpatialRelationship Relationship { get; set; } = SpatialRelationship.Intersects;
    
    /// <inheritdoc/>
    public double Distance { get; set; } = 0;
    
    /// <inheritdoc/>
    public DistanceUnit DistanceUnit { get; set; } = DistanceUnit.Meters;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="geometry">필터 지오메트리</param>
    /// <param name="relationship">공간 관계</param>
    public SpatialFilter(Geometry.IGeometry geometry, SpatialRelationship relationship = SpatialRelationship.Intersects)
    {
        FilterGeometry = geometry;
        Relationship = relationship;
    }
    
    /// <summary>
    /// 거리 기반 공간 필터 생성자
    /// </summary>
    /// <param name="geometry">필터 지오메트리</param>
    /// <param name="distance">거리</param>
    /// <param name="unit">거리 단위</param>
    public SpatialFilter(Geometry.IGeometry geometry, double distance, DistanceUnit unit = DistanceUnit.Meters)
    {
        FilterGeometry = geometry;
        Relationship = SpatialRelationship.Distance;
        Distance = distance;
        DistanceUnit = unit;
    }
}

/// <summary>
/// 속성 필터 구현
/// </summary>
public class AttributeFilter : IAttributeFilter
{
    /// <inheritdoc/>
    public string WhereClause { get; set; } = string.Empty;
    
    /// <inheritdoc/>
    public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    
    /// <summary>
    /// 생성자
    /// </summary>
    public AttributeFilter() { }
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="whereClause">WHERE 절</param>
    public AttributeFilter(string whereClause)
    {
        WhereClause = whereClause;
    }
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="whereClause">WHERE 절</param>
    /// <param name="parameters">매개변수</param>
    public AttributeFilter(string whereClause, IDictionary<string, object> parameters)
    {
        WhereClause = whereClause;
        Parameters = parameters;
    }
}

/// <summary>
/// 쿼리 빌더
/// 편리한 필터 구성을 위한 빌더 패턴
/// </summary>
public class QueryBuilder
{
    private readonly QueryFilter _filter = new();
    
    /// <summary>
    /// 공간 필터 추가
    /// </summary>
    /// <param name="geometry">필터 지오메트리</param>
    /// <param name="relationship">공간 관계</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Where(Geometry.IGeometry geometry, SpatialRelationship relationship = SpatialRelationship.Intersects)
    {
        _filter.SpatialFilter = new SpatialFilter(geometry, relationship);
        return this;
    }
    
    /// <summary>
    /// 거리 기반 공간 필터 추가
    /// </summary>
    /// <param name="geometry">필터 지오메트리</param>
    /// <param name="distance">거리</param>
    /// <param name="unit">거리 단위</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Within(Geometry.IGeometry geometry, double distance, DistanceUnit unit = DistanceUnit.Meters)
    {
        _filter.SpatialFilter = new SpatialFilter(geometry, distance, unit);
        return this;
    }
    
    /// <summary>
    /// 속성 필터 추가
    /// </summary>
    /// <param name="whereClause">WHERE 절</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Where(string whereClause)
    {
        _filter.AttributeFilter = new AttributeFilter(whereClause);
        return this;
    }
    
    /// <summary>
    /// 컬럼 선택
    /// </summary>
    /// <param name="columns">선택할 컬럼들</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Select(params string[] columns)
    {
        _filter.Columns = columns.ToList();
        return this;
    }
    
    /// <summary>
    /// 정렬 추가
    /// </summary>
    /// <param name="fieldName">정렬 필드</param>
    /// <param name="direction">정렬 방향</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder OrderBy(string fieldName, SortDirection direction = SortDirection.Ascending)
    {
        _filter.OrderBy ??= new List<SortField>();
        _filter.OrderBy.Add(new SortField(fieldName, direction));
        return this;
    }
    
    /// <summary>
    /// 최대 피처 수 설정
    /// </summary>
    /// <param name="maxFeatures">최대 피처 수</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Take(int maxFeatures)
    {
        _filter.MaxFeatures = maxFeatures;
        return this;
    }
    
    /// <summary>
    /// 오프셋 설정
    /// </summary>
    /// <param name="offset">건너뛸 피처 수</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Skip(int offset)
    {
        _filter.Offset = offset;
        return this;
    }
    
    /// <summary>
    /// 지오메트리 제외
    /// </summary>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder WithoutGeometry()
    {
        _filter.IncludeGeometry = false;
        return this;
    }
    
    /// <summary>
    /// 고유값만 반환
    /// </summary>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Distinct()
    {
        _filter.Distinct = true;
        return this;
    }
    
    /// <summary>
    /// 좌표계 변환
    /// </summary>
    /// <param name="srid">대상 SRID</param>
    /// <returns>쿼리 빌더</returns>
    public QueryBuilder Transform(int srid)
    {
        _filter.TargetSRID = srid;
        return this;
    }
    
    /// <summary>
    /// 구성된 필터 반환
    /// </summary>
    /// <returns>쿼리 필터</returns>
    public IQueryFilter Build()
    {
        return _filter.Clone();
    }
    
    /// <summary>
    /// 암시적 변환
    /// </summary>
    /// <param name="builder">쿼리 빌더</param>
    /// <returns>쿼리 필터</returns>
    public static implicit operator QueryFilter(QueryBuilder builder)
    {
        return (QueryFilter)builder.Build();
    }
}