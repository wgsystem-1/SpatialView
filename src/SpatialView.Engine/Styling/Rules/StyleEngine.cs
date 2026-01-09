namespace SpatialView.Engine.Styling.Rules;

/// <summary>
/// 스타일 엔진
/// 피처에 적용할 스타일을 규칙에 따라 결정합니다
/// </summary>
public class StyleEngine
{
    private readonly List<IStyleRule> _rules = new();
    private readonly object _lockObject = new();
    
    /// <summary>
    /// 기본 스타일 (규칙에 매치되지 않는 경우)
    /// </summary>
    public IStyle? DefaultStyle { get; set; }
    
    /// <summary>
    /// 규칙 목록 (읽기 전용)
    /// </summary>
    public IReadOnlyList<IStyleRule> Rules
    {
        get
        {
            lock (_lockObject)
            {
                return _rules.ToList();
            }
        }
    }
    
    /// <summary>
    /// 스타일 규칙 추가
    /// </summary>
    /// <param name="rule">추가할 규칙</param>
    public void AddRule(IStyleRule rule)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        
        lock (_lockObject)
        {
            _rules.Add(rule);
            SortRulesByPriority();
        }
    }
    
    /// <summary>
    /// 여러 스타일 규칙 추가
    /// </summary>
    /// <param name="rules">추가할 규칙들</param>
    public void AddRules(IEnumerable<IStyleRule> rules)
    {
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        
        lock (_lockObject)
        {
            _rules.AddRange(rules);
            SortRulesByPriority();
        }
    }
    
    /// <summary>
    /// 스타일 규칙 제거
    /// </summary>
    /// <param name="rule">제거할 규칙</param>
    /// <returns>제거 성공 여부</returns>
    public bool RemoveRule(IStyleRule rule)
    {
        if (rule == null) return false;
        
        lock (_lockObject)
        {
            return _rules.Remove(rule);
        }
    }
    
    /// <summary>
    /// 이름으로 스타일 규칙 제거
    /// </summary>
    /// <param name="ruleName">제거할 규칙 이름</param>
    /// <returns>제거 성공 여부</returns>
    public bool RemoveRule(string ruleName)
    {
        if (string.IsNullOrWhiteSpace(ruleName)) return false;
        
        lock (_lockObject)
        {
            var rule = _rules.FirstOrDefault(r => r.Name == ruleName);
            return rule != null && _rules.Remove(rule);
        }
    }
    
    /// <summary>
    /// 모든 스타일 규칙 제거
    /// </summary>
    public void ClearRules()
    {
        lock (_lockObject)
        {
            _rules.Clear();
        }
    }
    
    /// <summary>
    /// 이름으로 스타일 규칙 찾기
    /// </summary>
    /// <param name="ruleName">찾을 규칙 이름</param>
    /// <returns>찾은 규칙 또는 null</returns>
    public IStyleRule? GetRule(string ruleName)
    {
        if (string.IsNullOrWhiteSpace(ruleName)) return null;
        
        lock (_lockObject)
        {
            return _rules.FirstOrDefault(r => r.Name == ruleName);
        }
    }
    
    /// <summary>
    /// 피처에 적용할 스타일 결정
    /// </summary>
    /// <param name="feature">스타일을 결정할 피처</param>
    /// <param name="zoom">현재 줌 레벨</param>
    /// <returns>적용할 스타일</returns>
    public IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        if (feature == null) return DefaultStyle;
        
        lock (_lockObject)
        {
            // 우선순위가 높은 규칙부터 확인
            foreach (var rule in _rules.Where(r => r.Enabled))
            {
                try
                {
                    if (rule.Matches(feature, zoom))
                    {
                        var style = rule.GetStyle(feature, zoom);
                        if (style != null) return style;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Style rule error ({rule.Name}): {ex.Message}");
                }
            }
        }
        
        return DefaultStyle;
    }
    
    /// <summary>
    /// 피처 목록에 스타일 적용
    /// </summary>
    /// <param name="features">피처 목록</param>
    /// <param name="zoom">현재 줌 레벨</param>
    /// <returns>스타일이 적용된 피처 목록</returns>
    public IEnumerable<Data.IFeature> ApplyStyles(IEnumerable<Data.IFeature> features, double zoom)
    {
        if (features == null) yield break;
        
        foreach (var feature in features)
        {
            if (feature != null)
            {
                feature.Style = GetStyle(feature, zoom);
                yield return feature;
            }
        }
    }
    
    /// <summary>
    /// 특정 지오메트리 타입의 규칙들 가져오기
    /// </summary>
    /// <param name="geometryType">지오메트리 타입</param>
    /// <returns>해당 타입에 적용되는 규칙들</returns>
    public IEnumerable<IStyleRule> GetRulesForGeometryType(Type geometryType)
    {
        if (geometryType == null) yield break;
        
        lock (_lockObject)
        {
            foreach (var rule in _rules.Where(r => r.Enabled))
            {
                // ConditionalStyleRule인 경우 지오메트리 타입 조건 확인
                if (rule is IConditionalStyleRule conditionalRule)
                {
                    var hasGeometryCondition = conditionalRule.Conditions
                        .OfType<IGeometryTypeCondition>()
                        .Any(gc => gc.AllowedTypes.Contains(geometryType) ||
                                  gc.AllowedTypes.Any(t => t.IsAssignableFrom(geometryType)));
                    
                    if (hasGeometryCondition)
                    {
                        yield return rule;
                    }
                }
                else
                {
                    // 기본 규칙은 모든 타입에 적용
                    yield return rule;
                }
            }
        }
    }
    
    /// <summary>
    /// 활성화된 규칙 수
    /// </summary>
    public int ActiveRuleCount
    {
        get
        {
            lock (_lockObject)
            {
                return _rules.Count(r => r.Enabled);
            }
        }
    }
    
    /// <summary>
    /// 전체 규칙 수
    /// </summary>
    public int TotalRuleCount
    {
        get
        {
            lock (_lockObject)
            {
                return _rules.Count;
            }
        }
    }
    
    /// <summary>
    /// 우선순위에 따라 규칙 정렬
    /// </summary>
    private void SortRulesByPriority()
    {
        _rules.Sort((r1, r2) => r2.Priority.CompareTo(r1.Priority));
    }
    
    /// <summary>
    /// 스타일 엔진 복제
    /// </summary>
    /// <returns>복제된 스타일 엔진</returns>
    public StyleEngine Clone()
    {
        var clonedEngine = new StyleEngine
        {
            DefaultStyle = DefaultStyle
        };
        
        lock (_lockObject)
        {
            clonedEngine.AddRules(_rules);
        }
        
        return clonedEngine;
    }
    
    /// <summary>
    /// 규칙 통계 정보
    /// </summary>
    /// <returns>규칙 통계</returns>
    public StyleEngineStatistics GetStatistics()
    {
        lock (_lockObject)
        {
            return new StyleEngineStatistics
            {
                TotalRules = _rules.Count,
                ActiveRules = _rules.Count(r => r.Enabled),
                ConditionalRules = _rules.OfType<IConditionalStyleRule>().Count(),
                RangeRules = _rules.OfType<IRangeStyleRule>().Count(),
                CategoryRules = _rules.OfType<ICategoryStyleRule>().Count(),
                BasicRules = _rules.Count(r => r.GetType() == typeof(BasicStyleRule))
            };
        }
    }
}

/// <summary>
/// 스타일 엔진 통계 정보
/// </summary>
public class StyleEngineStatistics
{
    /// <summary>
    /// 전체 규칙 수
    /// </summary>
    public int TotalRules { get; set; }
    
    /// <summary>
    /// 활성화된 규칙 수
    /// </summary>
    public int ActiveRules { get; set; }
    
    /// <summary>
    /// 조건부 규칙 수
    /// </summary>
    public int ConditionalRules { get; set; }
    
    /// <summary>
    /// 범위 기반 규칙 수
    /// </summary>
    public int RangeRules { get; set; }
    
    /// <summary>
    /// 카테고리 기반 규칙 수
    /// </summary>
    public int CategoryRules { get; set; }
    
    /// <summary>
    /// 기본 규칙 수
    /// </summary>
    public int BasicRules { get; set; }
    
    /// <inheritdoc/>
    public override string ToString()
    {
        return $"Total: {TotalRules}, Active: {ActiveRules}, " +
               $"Conditional: {ConditionalRules}, Range: {RangeRules}, " +
               $"Category: {CategoryRules}, Basic: {BasicRules}";
    }
}