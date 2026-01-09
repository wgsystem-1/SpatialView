namespace SpatialView.Engine.Styling.Rules;

/// <summary>
/// 스타일 규칙 인터페이스
/// 특정 조건에 따라 피처에 적용할 스타일을 결정합니다
/// </summary>
public interface IStyleRule
{
    /// <summary>
    /// 규칙 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 규칙 설명
    /// </summary>
    string? Description { get; set; }
    
    /// <summary>
    /// 규칙 활성화 여부
    /// </summary>
    bool Enabled { get; set; }
    
    /// <summary>
    /// 규칙 우선순위 (높은 값이 우선)
    /// </summary>
    int Priority { get; set; }
    
    /// <summary>
    /// 최소 표시 배율
    /// </summary>
    double MinZoom { get; set; }
    
    /// <summary>
    /// 최대 표시 배율
    /// </summary>
    double MaxZoom { get; set; }
    
    /// <summary>
    /// 피처가 이 규칙에 매치되는지 확인
    /// </summary>
    /// <param name="feature">확인할 피처</param>
    /// <param name="zoom">현재 줌 레벨</param>
    /// <returns>매치 여부</returns>
    bool Matches(Data.IFeature feature, double zoom);
    
    /// <summary>
    /// 매치된 피처에 적용할 스타일 반환
    /// </summary>
    /// <param name="feature">피처</param>
    /// <param name="zoom">현재 줌 레벨</param>
    /// <returns>적용할 스타일</returns>
    IStyle? GetStyle(Data.IFeature feature, double zoom);
}

/// <summary>
/// 조건부 스타일 규칙
/// 속성값이나 지오메트리 타입에 따라 다른 스타일을 적용
/// </summary>
public interface IConditionalStyleRule : IStyleRule
{
    /// <summary>
    /// 조건 목록
    /// </summary>
    IList<IStyleCondition> Conditions { get; }
    
    /// <summary>
    /// 조건 연산자 (AND, OR)
    /// </summary>
    ConditionOperator Operator { get; set; }
}

/// <summary>
/// 범위 기반 스타일 규칙
/// 속성값의 범위에 따라 다른 스타일을 적용
/// </summary>
public interface IRangeStyleRule : IStyleRule
{
    /// <summary>
    /// 분류 기준 속성 이름
    /// </summary>
    string PropertyName { get; set; }
    
    /// <summary>
    /// 범위 목록
    /// </summary>
    IList<StyleRange> Ranges { get; }
    
    /// <summary>
    /// 기본 스타일 (범위에 맞지 않는 경우)
    /// </summary>
    IStyle? DefaultStyle { get; set; }
}

/// <summary>
/// 카테고리 기반 스타일 규칙
/// 속성값의 고유값에 따라 다른 스타일을 적용
/// </summary>
public interface ICategoryStyleRule : IStyleRule
{
    /// <summary>
    /// 분류 기준 속성 이름
    /// </summary>
    string PropertyName { get; set; }
    
    /// <summary>
    /// 카테고리 매핑
    /// </summary>
    IDictionary<object, IStyle> CategoryStyles { get; }
    
    /// <summary>
    /// 기본 스타일 (매핑되지 않은 값의 경우)
    /// </summary>
    IStyle? DefaultStyle { get; set; }
}

/// <summary>
/// 조건 연산자
/// </summary>
public enum ConditionOperator
{
    /// <summary>
    /// 모든 조건이 참이어야 함
    /// </summary>
    And,
    
    /// <summary>
    /// 하나 이상의 조건이 참이면 됨
    /// </summary>
    Or
}