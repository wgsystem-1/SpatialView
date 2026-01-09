namespace SpatialView.Engine.SpatialIndex;

/// <summary>
/// 공간 인덱스 인터페이스
/// </summary>
public interface ISpatialIndex<T>
{
    /// <summary>
    /// 객체를 인덱스에 추가
    /// </summary>
    /// <param name="envelope">객체의 경계 상자</param>
    /// <param name="item">저장할 객체</param>
    void Insert(Geometry.Envelope envelope, T item);
    
    /// <summary>
    /// 인덱스에서 객체 제거
    /// </summary>
    /// <param name="envelope">객체의 경계 상자</param>
    /// <param name="item">제거할 객체</param>
    /// <returns>제거 성공 여부</returns>
    bool Remove(Geometry.Envelope envelope, T item);
    
    /// <summary>
    /// 주어진 영역과 교차하는 모든 객체 검색
    /// </summary>
    /// <param name="searchEnvelope">검색 영역</param>
    /// <returns>교차하는 객체 목록</returns>
    IList<T> Query(Geometry.Envelope searchEnvelope);
    
    /// <summary>
    /// 인덱스의 모든 내용 삭제
    /// </summary>
    void Clear();
    
    /// <summary>
    /// 인덱스에 저장된 항목 수
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// 인덱스가 비어있는지 확인
    /// </summary>
    bool IsEmpty { get; }
}