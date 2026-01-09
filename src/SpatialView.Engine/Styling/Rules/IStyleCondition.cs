namespace SpatialView.Engine.Styling.Rules;

/// <summary>
/// 스타일 조건 인터페이스
/// 피처의 속성이나 지오메트리를 기준으로 조건을 확인합니다
/// </summary>
public interface IStyleCondition
{
    /// <summary>
    /// 조건 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 피처가 이 조건에 만족하는지 확인
    /// </summary>
    /// <param name="feature">확인할 피처</param>
    /// <param name="zoom">현재 줌 레벨</param>
    /// <returns>조건 만족 여부</returns>
    bool Evaluate(Data.IFeature feature, double zoom);
}

/// <summary>
/// 속성 값 조건
/// 피처의 특정 속성값을 기준으로 조건을 확인
/// </summary>
public interface IPropertyCondition : IStyleCondition
{
    /// <summary>
    /// 속성 이름
    /// </summary>
    string PropertyName { get; set; }
    
    /// <summary>
    /// 비교 연산자
    /// </summary>
    ComparisonOperator Operator { get; set; }
    
    /// <summary>
    /// 비교 값
    /// </summary>
    object Value { get; set; }
}

/// <summary>
/// 지오메트리 타입 조건
/// 피처의 지오메트리 타입을 확인
/// </summary>
public interface IGeometryTypeCondition : IStyleCondition
{
    /// <summary>
    /// 허용되는 지오메트리 타입들
    /// </summary>
    ISet<Type> AllowedTypes { get; }
}

/// <summary>
/// 공간 조건
/// 피처의 지오메트리가 특정 공간 관계를 만족하는지 확인
/// </summary>
public interface ISpatialCondition : IStyleCondition
{
    /// <summary>
    /// 참조 지오메트리
    /// </summary>
    Geometry.IGeometry ReferenceGeometry { get; set; }
    
    /// <summary>
    /// 공간 관계 연산자
    /// </summary>
    SpatialOperator SpatialOperator { get; set; }
}

/// <summary>
/// 줌 레벨 조건
/// 현재 줌 레벨이 특정 범위에 있는지 확인
/// </summary>
public interface IZoomCondition : IStyleCondition
{
    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    double MinZoom { get; set; }
    
    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    double MaxZoom { get; set; }
}

/// <summary>
/// 비교 연산자
/// </summary>
public enum ComparisonOperator
{
    /// <summary>
    /// 같음
    /// </summary>
    Equal,
    
    /// <summary>
    /// 다름
    /// </summary>
    NotEqual,
    
    /// <summary>
    /// 보다 큼
    /// </summary>
    GreaterThan,
    
    /// <summary>
    /// 보다 큼 또는 같음
    /// </summary>
    GreaterThanOrEqual,
    
    /// <summary>
    /// 보다 작음
    /// </summary>
    LessThan,
    
    /// <summary>
    /// 보다 작음 또는 같음
    /// </summary>
    LessThanOrEqual,
    
    /// <summary>
    /// 포함 (문자열)
    /// </summary>
    Contains,
    
    /// <summary>
    /// 시작 (문자열)
    /// </summary>
    StartsWith,
    
    /// <summary>
    /// 끝 (문자열)
    /// </summary>
    EndsWith,
    
    /// <summary>
    /// 정규식 매치
    /// </summary>
    Regex,
    
    /// <summary>
    /// 목록에 포함
    /// </summary>
    In,
    
    /// <summary>
    /// 목록에 포함되지 않음
    /// </summary>
    NotIn
}

/// <summary>
/// 공간 연산자
/// </summary>
public enum SpatialOperator
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
    /// 분리
    /// </summary>
    Disjoint
}