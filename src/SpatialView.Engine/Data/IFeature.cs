namespace SpatialView.Engine.Data;

/// <summary>
/// 공간 피처 인터페이스
/// 지오메트리와 속성 데이터를 포함하는 공간 객체
/// </summary>
public interface IFeature
{
    /// <summary>
    /// 피처 고유 ID
    /// </summary>
    object Id { get; set; }
    
    /// <summary>
    /// 피처의 지오메트리
    /// </summary>
    Geometry.IGeometry? Geometry { get; set; }
    
    /// <summary>
    /// 피처의 속성 데이터
    /// </summary>
    IAttributeTable Attributes { get; }
    
    /// <summary>
    /// 피처가 유효한지 확인
    /// </summary>
    bool IsValid { get; }
    
    /// <summary>
    /// 피처의 경계 영역
    /// </summary>
    Geometry.Envelope? BoundingBox { get; }
    
    /// <summary>
    /// 속성값 가져오기
    /// </summary>
    object? GetAttribute(string name);
    
    /// <summary>
    /// 피처의 스타일
    /// </summary>
    Styling.IStyle? Style { get; set; }
}