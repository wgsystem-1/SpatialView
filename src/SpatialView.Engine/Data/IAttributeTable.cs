namespace SpatialView.Engine.Data;

/// <summary>
/// 속성 테이블 인터페이스
/// 피처의 속성 데이터를 관리
/// </summary>
public interface IAttributeTable : IEnumerable<KeyValuePair<string, object?>>
{
    /// <summary>
    /// 속성 이름 목록
    /// </summary>
    ICollection<string> AttributeNames { get; }
    
    /// <summary>
    /// 속성값 목록
    /// </summary>
    ICollection<object?> Values { get; }
    
    /// <summary>
    /// 속성 개수
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// 속성값 얻기/설정
    /// </summary>
    object? this[string attributeName] { get; set; }
    
    /// <summary>
    /// 인덱스로 속성값 얻기/설정
    /// </summary>
    object? this[int index] { get; set; }
    
    /// <summary>
    /// 속성 이름이 존재하는지 확인
    /// </summary>
    bool Exists(string attributeName);
    
    /// <summary>
    /// 속성 추가
    /// </summary>
    void Add(string attributeName, object? value);
    
    /// <summary>
    /// 속성 제거
    /// </summary>
    bool Remove(string attributeName);
    
    /// <summary>
    /// 모든 속성 제거
    /// </summary>
    void Clear();
    
    /// <summary>
    /// 특정 타입으로 속성값 얻기
    /// </summary>
    T? GetValue<T>(string attributeName);
    
    /// <summary>
    /// 특정 타입으로 속성값 얻기 (기본값 지원)
    /// </summary>
    T GetValue<T>(string attributeName, T defaultValue);
    
    /// <summary>
    /// 모든 속성 이름 배열로 가져오기
    /// </summary>
    string[] GetNames();
}