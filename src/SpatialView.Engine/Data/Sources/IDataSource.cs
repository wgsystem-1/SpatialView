namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// 데이터 소스 인터페이스
/// 다양한 형태의 공간 데이터에 대한 통합된 접근 인터페이스
/// </summary>
public interface IDataSource : IDisposable
{
    /// <summary>
    /// 데이터 소스 이름
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 데이터 소스 설명
    /// </summary>
    string? Description { get; set; }
    
    /// <summary>
    /// 연결 문자열
    /// </summary>
    string ConnectionString { get; }
    
    /// <summary>
    /// 데이터 소스 타입
    /// </summary>
    DataSourceType SourceType { get; }
    
    /// <summary>
    /// 좌표계 SRID
    /// </summary>
    int SRID { get; }
    
    /// <summary>
    /// 데이터 전체 영역
    /// </summary>
    Geometry.Envelope? Extent { get; }
    
    /// <summary>
    /// 연결 상태
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// 읽기 전용 여부
    /// </summary>
    bool IsReadOnly { get; }
    
    /// <summary>
    /// 사용 가능한 테이블/레이어 목록
    /// </summary>
    IEnumerable<string> GetTableNames();
    
    /// <summary>
    /// 연결 열기
    /// </summary>
    Task<bool> OpenAsync();
    
    /// <summary>
    /// 연결 열기 (동기)
    /// </summary>
    void Open();
    
    /// <summary>
    /// 연결 닫기
    /// </summary>
    void Close();
    
    /// <summary>
    /// 테이블 스키마 정보 가져오기
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <returns>스키마 정보</returns>
    Task<TableSchema?> GetSchemaAsync(string tableName);
    
    /// <summary>
    /// 피처 개수 가져오기
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="filter">필터 조건</param>
    /// <returns>피처 개수</returns>
    Task<long> GetFeatureCountAsync(string tableName, IQueryFilter? filter = null);
    
    /// <summary>
    /// 테이블의 전체 영역 가져오기
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <returns>전체 영역</returns>
    Task<Geometry.Envelope?> GetExtentAsync(string tableName);
    
    /// <summary>
    /// 피처 검색
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="filter">검색 필터</param>
    /// <returns>피처 목록</returns>
    IAsyncEnumerable<IFeature> QueryFeaturesAsync(string tableName, IQueryFilter? filter = null);
    
    /// <summary>
    /// 피처 목록 가져오기 (편의 메서드)
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="filter">검색 필터</param>
    /// <returns>피처 목록</returns>
    Task<List<IFeature>> GetFeaturesAsync(string tableName, IQueryFilter? filter = null);

    /// <summary>
    /// 영역별 피처 가져오기 (동기 메서드)
    /// </summary>
    /// <param name="envelope">검색 영역</param>
    /// <returns>피처 목록</returns>
    IEnumerable<IFeature> GetFeatures(Geometry.Envelope envelope);
    
    /// <summary>
    /// 단일 피처 가져오기
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="id">피처 ID</param>
    /// <returns>피처</returns>
    Task<IFeature?> GetFeatureAsync(string tableName, object id);
    
    /// <summary>
    /// 피처 삽입 (쓰기 가능한 경우)
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="feature">삽입할 피처</param>
    /// <returns>성공 여부</returns>
    Task<bool> InsertFeatureAsync(string tableName, IFeature feature);
    
    /// <summary>
    /// 피처 업데이트 (쓰기 가능한 경우)
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="feature">업데이트할 피처</param>
    /// <returns>성공 여부</returns>
    Task<bool> UpdateFeatureAsync(string tableName, IFeature feature);
    
    /// <summary>
    /// 피처 삭제 (쓰기 가능한 경우)
    /// </summary>
    /// <param name="tableName">테이블 이름</param>
    /// <param name="id">삭제할 피처 ID</param>
    /// <returns>성공 여부</returns>
    Task<bool> DeleteFeatureAsync(string tableName, object id);
    
    /// <summary>
    /// 연결 테스트
    /// </summary>
    /// <returns>연결 성공 여부</returns>
    Task<bool> TestConnectionAsync();
    
    /// <summary>
    /// 데이터 소스 검증
    /// </summary>
    /// <returns>검증 결과</returns>
    Task<DataSourceValidationResult> ValidateAsync();
}

/// <summary>
/// 데이터 소스 타입
/// </summary>
public enum DataSourceType
{
    /// <summary>
    /// 알 수 없음
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Shapefile
    /// </summary>
    Shapefile,
    
    /// <summary>
    /// GeoJSON
    /// </summary>
    GeoJSON,
    
    /// <summary>
    /// KML
    /// </summary>
    KML,
    
    /// <summary>
    /// PostGIS
    /// </summary>
    PostGIS,
    
    /// <summary>
    /// SQLite/SpatiaLite
    /// </summary>
    SQLite,
    
    /// <summary>
    /// SQL Server
    /// </summary>
    SqlServer,
    
    /// <summary>
    /// Oracle Spatial
    /// </summary>
    Oracle,
    
    /// <summary>
    /// WFS (Web Feature Service)
    /// </summary>
    WFS,
    
    /// <summary>
    /// WMS (Web Map Service)
    /// </summary>
    WMS,
    
    /// <summary>
    /// 메모리 내 데이터
    /// </summary>
    Memory,
    
    /// <summary>
    /// CSV
    /// </summary>
    CSV,
    
    /// <summary>
    /// GeoPackage
    /// </summary>
    GeoPackage,
    
    /// <summary>
    /// Web Service / REST API
    /// </summary>
    WebService
}

/// <summary>
/// 테이블 스키마 정보
/// </summary>
public class TableSchema
{
    /// <summary>
    /// 테이블 이름
    /// </summary>
    public string TableName { get; set; } = string.Empty;
    
    /// <summary>
    /// 지오메트리 컬럼 이름
    /// </summary>
    public string? GeometryColumn { get; set; }
    
    /// <summary>
    /// 지오메트리 타입
    /// </summary>
    public string? GeometryType { get; set; }
    
    /// <summary>
    /// 좌표계 SRID
    /// </summary>
    public int SRID { get; set; }
    
    /// <summary>
    /// 기본 키 컬럼
    /// </summary>
    public string? PrimaryKeyColumn { get; set; }
    
    /// <summary>
    /// 컬럼 정보 목록
    /// </summary>
    public List<ColumnInfo> Columns { get; } = new();
    
    /// <summary>
    /// 총 피처 수
    /// </summary>
    public long FeatureCount { get; set; }
    
    /// <summary>
    /// 테이블 전체 영역
    /// </summary>
    public Geometry.Envelope? Extent { get; set; }
}

/// <summary>
/// 컬럼 정보
/// </summary>
public class ColumnInfo
{
    /// <summary>
    /// 컬럼 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 데이터 타입
    /// </summary>
    public Type DataType { get; set; } = typeof(string);
    
    /// <summary>
    /// 데이터베이스 타입 이름
    /// </summary>
    public string? DatabaseTypeName { get; set; }
    
    /// <summary>
    /// NULL 허용 여부
    /// </summary>
    public bool AllowNull { get; set; } = true;
    
    /// <summary>
    /// NULL 허용 여부 (AllowNull과 동일, 호환성용)
    /// </summary>
    public bool IsNullable 
    { 
        get => AllowNull; 
        set => AllowNull = value; 
    }
    
    /// <summary>
    /// 최대 길이 (-1이면 제한 없음)
    /// </summary>
    public int MaxLength { get; set; } = -1;
    
    /// <summary>
    /// 기본값
    /// </summary>
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// 고유값 여부
    /// </summary>
    public bool IsUnique { get; set; }
    
    /// <summary>
    /// 인덱스 여부
    /// </summary>
    public bool IsIndexed { get; set; }
}

/// <summary>
/// 데이터 소스 검증 결과
/// </summary>
public class DataSourceValidationResult
{
    /// <summary>
    /// 검증 성공 여부
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// 오류 메시지 목록
    /// </summary>
    public List<string> Errors { get; } = new();
    
    /// <summary>
    /// 경고 메시지 목록
    /// </summary>
    public List<string> Warnings { get; } = new();
    
    /// <summary>
    /// 정보 메시지 목록
    /// </summary>
    public List<string> Information { get; } = new();
    
    /// <summary>
    /// 검증된 테이블 수
    /// </summary>
    public int ValidatedTableCount { get; set; }
    
    /// <summary>
    /// 검증 시간
    /// </summary>
    public TimeSpan ValidationTime { get; set; }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        var status = IsValid ? "Valid" : "Invalid";
        return $"{status} - Tables: {ValidatedTableCount}, Errors: {Errors.Count}, " +
               $"Warnings: {Warnings.Count}, Time: {ValidationTime.TotalMilliseconds:F0}ms";
    }
}