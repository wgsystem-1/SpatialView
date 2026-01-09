namespace SpatialView.Engine.Data;

/// <summary>
/// 쿼리 필터 인터페이스
/// 데이터 소스에서 피처를 필터링하기 위한 조건
/// </summary>
public interface IQueryFilter
{
    /// <summary>
    /// SQL WHERE 절 (호환성을 위해 유지)
    /// </summary>
    string? WhereClause { get; set; }
    
    /// <summary>
    /// 공간 필터 (지오메트리 조건)
    /// </summary>
    Sources.ISpatialFilter? SpatialFilter { get; set; }
    
    /// <summary>
    /// 속성 필터 (WHERE 조건)
    /// </summary>
    Sources.IAttributeFilter? AttributeFilter { get; set; }
    
    /// <summary>
    /// 반환할 컬럼 목록
    /// </summary>
    string[]? Columns { get; set; }
    
    /// <summary>
    /// 정렬 순서 (ORDER BY 절)
    /// </summary>
    string? OrderBy { get; set; }
    
    /// <summary>
    /// 반환할 최대 레코드 수 (MaxFeatures와 동일)
    /// </summary>
    int? MaxRecords { get; set; }
    
    /// <summary>
    /// 반환할 최대 피처 수
    /// </summary>
    int MaxFeatures { get; set; }
    
    /// <summary>
    /// 건너뛸 레코드 수 (OFFSET)
    /// </summary>
    int? Offset { get; set; }
    
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
/// 기본 쿼리 필터 구현
/// </summary>
public class QueryFilter : IQueryFilter
{
    /// <inheritdoc/>
    public string? WhereClause { get; set; }
    
    /// <inheritdoc/>
    public Sources.ISpatialFilter? SpatialFilter { get; set; }
    
    /// <inheritdoc/>
    public Sources.IAttributeFilter? AttributeFilter { get; set; }
    
    /// <inheritdoc/>
    public string[]? Columns { get; set; }
    
    /// <inheritdoc/>
    public string? OrderBy { get; set; }
    
    /// <inheritdoc/>
    public int? MaxRecords { get; set; }
    
    /// <inheritdoc/>
    public int MaxFeatures { get; set; } = 0;
    
    /// <inheritdoc/>
    public int? Offset { get; set; }
    
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
            WhereClause = WhereClause,
            SpatialFilter = SpatialFilter,
            AttributeFilter = AttributeFilter,
            Columns = Columns?.ToArray(),
            OrderBy = OrderBy,
            MaxRecords = MaxRecords,
            MaxFeatures = MaxFeatures,
            Offset = Offset,
            IncludeGeometry = IncludeGeometry,
            Distinct = Distinct,
            TargetSRID = TargetSRID
        };
    }
}