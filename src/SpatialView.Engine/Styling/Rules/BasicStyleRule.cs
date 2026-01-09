using System.Text.RegularExpressions;

namespace SpatialView.Engine.Styling.Rules;

/// <summary>
/// 기본 스타일 규칙
/// 단순한 조건과 스타일을 적용
/// </summary>
public class BasicStyleRule : IStyleRule
{
    /// <inheritdoc/>
    public string Name { get; set; } = "Basic Rule";
    
    /// <inheritdoc/>
    public string? Description { get; set; }
    
    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;
    
    /// <inheritdoc/>
    public int Priority { get; set; } = 0;
    
    /// <inheritdoc/>
    public double MinZoom { get; set; } = 0;
    
    /// <inheritdoc/>
    public double MaxZoom { get; set; } = double.MaxValue;
    
    /// <summary>
    /// 적용할 스타일
    /// </summary>
    public IStyle? Style { get; set; }
    
    /// <inheritdoc/>
    public virtual bool Matches(Data.IFeature feature, double zoom)
    {
        if (!Enabled) return false;
        
        // 줌 레벨 체크
        if (zoom < MinZoom || zoom > MaxZoom) return false;
        
        return true; // 기본 규칙은 모든 피처에 매치
    }
    
    /// <inheritdoc/>
    public virtual IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        return Style;
    }
}

/// <summary>
/// 조건부 스타일 규칙 구현
/// </summary>
public class ConditionalStyleRule : BasicStyleRule, IConditionalStyleRule
{
    /// <inheritdoc/>
    public IList<IStyleCondition> Conditions { get; } = new List<IStyleCondition>();
    
    /// <inheritdoc/>
    public ConditionOperator Operator { get; set; } = ConditionOperator.And;
    
    /// <inheritdoc/>
    public override bool Matches(Data.IFeature feature, double zoom)
    {
        if (!base.Matches(feature, zoom)) return false;
        
        if (Conditions.Count == 0) return true;
        
        switch (Operator)
        {
            case ConditionOperator.And:
                return Conditions.All(c => c.Evaluate(feature, zoom));
                
            case ConditionOperator.Or:
                return Conditions.Any(c => c.Evaluate(feature, zoom));
                
            default:
                return false;
        }
    }
}

/// <summary>
/// 범위 기반 스타일 규칙 구현
/// </summary>
public class RangeStyleRule : BasicStyleRule, IRangeStyleRule
{
    /// <inheritdoc/>
    public string PropertyName { get; set; } = string.Empty;
    
    /// <inheritdoc/>
    public IList<StyleRange> Ranges { get; } = new List<StyleRange>();
    
    /// <inheritdoc/>
    public IStyle? DefaultStyle { get; set; }
    
    /// <inheritdoc/>
    public override bool Matches(Data.IFeature feature, double zoom)
    {
        if (!base.Matches(feature, zoom)) return false;
        
        if (string.IsNullOrWhiteSpace(PropertyName)) return false;
        
        // 속성값 가져오기
        var propertyValue = feature.GetAttribute(PropertyName);
        if (propertyValue == null) return DefaultStyle != null;
        
        // 범위에 매치되는지 확인
        return Ranges.Any(range => range.Contains(propertyValue)) || DefaultStyle != null;
    }
    
    /// <inheritdoc/>
    public override IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        if (string.IsNullOrWhiteSpace(PropertyName)) return DefaultStyle ?? Style;
        
        var propertyValue = feature.GetAttribute(PropertyName);
        if (propertyValue == null) return DefaultStyle ?? Style;
        
        // 첫 번째로 매치되는 범위의 스타일 반환
        var matchingRange = Ranges.FirstOrDefault(range => range.Contains(propertyValue));
        return matchingRange?.Style ?? DefaultStyle ?? Style;
    }
}

/// <summary>
/// 카테고리 기반 스타일 규칙 구현
/// </summary>
public class CategoryStyleRule : BasicStyleRule, ICategoryStyleRule
{
    /// <inheritdoc/>
    public string PropertyName { get; set; } = string.Empty;
    
    /// <inheritdoc/>
    public IDictionary<object, IStyle> CategoryStyles { get; } = new Dictionary<object, IStyle>();
    
    /// <inheritdoc/>
    public IStyle? DefaultStyle { get; set; }
    
    /// <inheritdoc/>
    public override bool Matches(Data.IFeature feature, double zoom)
    {
        if (!base.Matches(feature, zoom)) return false;
        
        if (string.IsNullOrWhiteSpace(PropertyName)) return false;
        
        var propertyValue = feature.GetAttribute(PropertyName);
        return CategoryStyles.ContainsKey(propertyValue ?? string.Empty) || DefaultStyle != null;
    }
    
    /// <inheritdoc/>
    public override IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        if (string.IsNullOrWhiteSpace(PropertyName)) return DefaultStyle ?? Style;
        
        var propertyValue = feature.GetAttribute(PropertyName) ?? string.Empty;
        
        if (CategoryStyles.TryGetValue(propertyValue, out var categoryStyle))
        {
            return categoryStyle;
        }
        
        return DefaultStyle ?? Style;
    }
}

/// <summary>
/// 속성 값 조건 구현
/// </summary>
public class PropertyCondition : IPropertyCondition
{
    /// <inheritdoc/>
    public string Name { get; set; } = "Property Condition";
    
    /// <inheritdoc/>
    public string PropertyName { get; set; } = string.Empty;
    
    /// <inheritdoc/>
    public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equal;
    
    /// <inheritdoc/>
    public object Value { get; set; } = string.Empty;
    
    /// <inheritdoc/>
    public bool Evaluate(Data.IFeature feature, double zoom)
    {
        if (string.IsNullOrWhiteSpace(PropertyName)) return false;
        
        var propertyValue = feature.GetAttribute(PropertyName);
        
        return Operator switch
        {
            ComparisonOperator.Equal => AreEqual(propertyValue, Value),
            ComparisonOperator.NotEqual => !AreEqual(propertyValue, Value),
            ComparisonOperator.GreaterThan => CompareNumeric(propertyValue, Value) > 0,
            ComparisonOperator.GreaterThanOrEqual => CompareNumeric(propertyValue, Value) >= 0,
            ComparisonOperator.LessThan => CompareNumeric(propertyValue, Value) < 0,
            ComparisonOperator.LessThanOrEqual => CompareNumeric(propertyValue, Value) <= 0,
            ComparisonOperator.Contains => ContainsString(propertyValue, Value),
            ComparisonOperator.StartsWith => StartsWithString(propertyValue, Value),
            ComparisonOperator.EndsWith => EndsWithString(propertyValue, Value),
            ComparisonOperator.Regex => MatchesRegex(propertyValue, Value),
            ComparisonOperator.In => IsInCollection(propertyValue, Value),
            ComparisonOperator.NotIn => !IsInCollection(propertyValue, Value),
            _ => false
        };
    }
    
    private static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;
        
        return left.ToString() == right.ToString();
    }
    
    private static int CompareNumeric(object? left, object? right)
    {
        try
        {
            var leftNum = Convert.ToDouble(left);
            var rightNum = Convert.ToDouble(right);
            return leftNum.CompareTo(rightNum);
        }
        catch
        {
            return string.Compare(left?.ToString(), right?.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
    
    private static bool ContainsString(object? source, object? value)
    {
        var sourceStr = source?.ToString();
        var valueStr = value?.ToString();
        
        if (sourceStr == null || valueStr == null) return false;
        
        return sourceStr.Contains(valueStr, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool StartsWithString(object? source, object? value)
    {
        var sourceStr = source?.ToString();
        var valueStr = value?.ToString();
        
        if (sourceStr == null || valueStr == null) return false;
        
        return sourceStr.StartsWith(valueStr, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool EndsWithString(object? source, object? value)
    {
        var sourceStr = source?.ToString();
        var valueStr = value?.ToString();
        
        if (sourceStr == null || valueStr == null) return false;
        
        return sourceStr.EndsWith(valueStr, StringComparison.OrdinalIgnoreCase);
    }
    
    private static bool MatchesRegex(object? source, object? pattern)
    {
        var sourceStr = source?.ToString();
        var patternStr = pattern?.ToString();
        
        if (sourceStr == null || patternStr == null) return false;
        
        try
        {
            return Regex.IsMatch(sourceStr, patternStr, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsInCollection(object? value, object? collection)
    {
        if (collection is System.Collections.IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (AreEqual(value, item)) return true;
            }
        }
        
        return false;
    }
}

/// <summary>
/// 지오메트리 타입 조건 구현
/// </summary>
public class GeometryTypeCondition : IGeometryTypeCondition
{
    /// <inheritdoc/>
    public string Name { get; set; } = "Geometry Type Condition";
    
    /// <inheritdoc/>
    public ISet<Type> AllowedTypes { get; } = new HashSet<Type>();
    
    /// <inheritdoc/>
    public bool Evaluate(Data.IFeature feature, double zoom)
    {
        if (feature?.Geometry == null) return false;
        
        var geometryType = feature.Geometry.GetType();
        return AllowedTypes.Contains(geometryType) ||
               AllowedTypes.Any(t => t.IsAssignableFrom(geometryType));
    }
}

/// <summary>
/// 줌 레벨 조건 구현
/// </summary>
public class ZoomCondition : IZoomCondition
{
    /// <inheritdoc/>
    public string Name { get; set; } = "Zoom Condition";
    
    /// <inheritdoc/>
    public double MinZoom { get; set; } = 0;
    
    /// <inheritdoc/>
    public double MaxZoom { get; set; } = double.MaxValue;
    
    /// <inheritdoc/>
    public bool Evaluate(Data.IFeature feature, double zoom)
    {
        return zoom >= MinZoom && zoom <= MaxZoom;
    }
}