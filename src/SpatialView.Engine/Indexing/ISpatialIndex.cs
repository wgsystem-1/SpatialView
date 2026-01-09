namespace SpatialView.Engine.Indexing;

/// <summary>
/// 공간 인덱스 인터페이스
/// 지리공간 객체의 빠른 검색을 위한 인덱스
/// </summary>
/// <typeparam name="T">인덱싱할 객체 타입</typeparam>
public interface ISpatialIndex<T>
{
    /// <summary>
    /// 인덱스에 객체 추가
    /// </summary>
    /// <param name="envelope">객체의 경계 영역</param>
    /// <param name="item">추가할 객체</param>
    void Insert(Geometry.Envelope envelope, T item);
    
    /// <summary>
    /// 인덱스에서 객체 제거
    /// </summary>
    /// <param name="envelope">객체의 경계 영역</param>
    /// <param name="item">제거할 객체</param>
    /// <returns>제거 성공 여부</returns>
    bool Remove(Geometry.Envelope envelope, T item);
    
    /// <summary>
    /// 특정 영역과 교차하는 객체들 검색
    /// </summary>
    /// <param name="envelope">검색 영역</param>
    /// <returns>교차하는 객체들</returns>
    IEnumerable<T> Query(Geometry.Envelope envelope);
    
    /// <summary>
    /// 점과 교차하는 객체들 검색
    /// </summary>
    /// <param name="coordinate">검색 점</param>
    /// <returns>교차하는 객체들</returns>
    IEnumerable<T> Query(Geometry.ICoordinate coordinate);
    
    /// <summary>
    /// 지오메트리와 교차하는 객체들 검색
    /// </summary>
    /// <param name="geometry">검색 지오메트리</param>
    /// <returns>교차하는 객체들</returns>
    IEnumerable<T> Query(Geometry.IGeometry geometry);
    
    /// <summary>
    /// 가장 가까운 객체 찾기
    /// </summary>
    /// <param name="coordinate">기준 점</param>
    /// <param name="maxDistance">최대 거리 (null이면 제한 없음)</param>
    /// <returns>가장 가까운 객체</returns>
    T? FindNearest(Geometry.ICoordinate coordinate, double? maxDistance = null);
    
    /// <summary>
    /// 가장 가까운 K개 객체 찾기
    /// </summary>
    /// <param name="coordinate">기준 점</param>
    /// <param name="k">찾을 객체 수</param>
    /// <param name="maxDistance">최대 거리 (null이면 제한 없음)</param>
    /// <returns>가까운 객체들</returns>
    IEnumerable<T> FindKNearest(Geometry.ICoordinate coordinate, int k, double? maxDistance = null);
    
    /// <summary>
    /// 인덱스의 모든 객체 가져오기
    /// </summary>
    /// <returns>모든 객체들</returns>
    IEnumerable<T> GetAll();
    
    /// <summary>
    /// 인덱스 크기 (객체 수)
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// 인덱스 경계 영역
    /// </summary>
    Geometry.Envelope? Bounds { get; }
    
    /// <summary>
    /// 인덱스 초기화
    /// </summary>
    void Clear();
    
    /// <summary>
    /// 인덱스 최적화
    /// </summary>
    void Optimize();
    
    /// <summary>
    /// 인덱스 통계 정보
    /// </summary>
    SpatialIndexStatistics Statistics { get; }
}

/// <summary>
/// 공간 인덱스 통계 정보
/// </summary>
public class SpatialIndexStatistics
{
    /// <summary>
    /// 인덱스된 객체 수
    /// </summary>
    public int ItemCount { get; set; }
    
    /// <summary>
    /// 인덱스 노드 수
    /// </summary>
    public int NodeCount { get; set; }
    
    /// <summary>
    /// 인덱스 깊이
    /// </summary>
    public int Depth { get; set; }
    
    /// <summary>
    /// 메모리 사용량 (바이트, 추정치)
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }
    
    /// <summary>
    /// 평균 검색 시간 (밀리초)
    /// </summary>
    public double AverageQueryTime { get; set; }
    
    /// <summary>
    /// 총 검색 횟수
    /// </summary>
    public long QueryCount { get; set; }
    
    /// <summary>
    /// 인덱스 효율성 (0.0 ~ 1.0)
    /// </summary>
    public double Efficiency => NodeCount > 0 ? (double)ItemCount / NodeCount : 0.0;
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Items: {ItemCount}, Nodes: {NodeCount}, Depth: {Depth}, " +
               $"Memory: {EstimatedMemoryUsage / 1024:N0} KB, " +
               $"Avg Query: {AverageQueryTime:F2} ms, Efficiency: {Efficiency:P1}";
    }
}