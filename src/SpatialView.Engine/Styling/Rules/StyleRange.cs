namespace SpatialView.Engine.Styling.Rules;

/// <summary>
/// 스타일 범위 정의
/// 특정 값 범위에 해당하는 피처에 적용할 스타일
/// </summary>
public class StyleRange
{
    /// <summary>
    /// 범위 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 최소값 (포함)
    /// </summary>
    public double MinValue { get; set; } = double.MinValue;
    
    /// <summary>
    /// 최대값 (포함)
    /// </summary>
    public double MaxValue { get; set; } = double.MaxValue;
    
    /// <summary>
    /// 최소값 포함 여부
    /// </summary>
    public bool IncludeMin { get; set; } = true;
    
    /// <summary>
    /// 최대값 포함 여부
    /// </summary>
    public bool IncludeMax { get; set; } = true;
    
    /// <summary>
    /// 이 범위에 적용할 스타일
    /// </summary>
    public IStyle? Style { get; set; }
    
    /// <summary>
    /// 레이블 (범례나 표시용)
    /// </summary>
    public string? Label { get; set; }
    
    /// <summary>
    /// 범위 설명
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 값이 이 범위에 포함되는지 확인
    /// </summary>
    /// <param name="value">확인할 값</param>
    /// <returns>포함 여부</returns>
    public bool Contains(double value)
    {
        var minCheck = IncludeMin ? value >= MinValue : value > MinValue;
        var maxCheck = IncludeMax ? value <= MaxValue : value < MaxValue;
        
        return minCheck && maxCheck;
    }
    
    /// <summary>
    /// 문자열을 숫자로 변환하여 범위 확인
    /// </summary>
    /// <param name="stringValue">문자열 값</param>
    /// <returns>포함 여부</returns>
    public bool Contains(string? stringValue)
    {
        if (string.IsNullOrWhiteSpace(stringValue))
            return false;
            
        if (double.TryParse(stringValue, out var numericValue))
        {
            return Contains(numericValue);
        }
        
        return false;
    }
    
    /// <summary>
    /// 객체 값을 변환하여 범위 확인
    /// </summary>
    /// <param name="value">확인할 값</param>
    /// <returns>포함 여부</returns>
    public bool Contains(object? value)
    {
        if (value == null) return false;
        
        try
        {
            var numericValue = Convert.ToDouble(value);
            return Contains(numericValue);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 범위의 중간값 계산
    /// </summary>
    public double MidValue => (MinValue + MaxValue) / 2;
    
    /// <summary>
    /// 범위의 크기
    /// </summary>
    public double Range => MaxValue - MinValue;
    
    /// <inheritdoc/>
    public override string ToString()
    {
        var minBracket = IncludeMin ? "[" : "(";
        var maxBracket = IncludeMax ? "]" : ")";
        
        return $"{Name}: {minBracket}{MinValue}, {MaxValue}{maxBracket}";
    }
    
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is StyleRange other)
        {
            return MinValue == other.MinValue && 
                   MaxValue == other.MaxValue &&
                   IncludeMin == other.IncludeMin &&
                   IncludeMax == other.IncludeMax;
        }
        
        return false;
    }
    
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(MinValue, MaxValue, IncludeMin, IncludeMax);
    }
}

/// <summary>
/// 스타일 범위 생성 헬퍼 클래스
/// </summary>
public static class StyleRangeBuilder
{
    /// <summary>
    /// 등간격 범위 생성
    /// </summary>
    /// <param name="minValue">최소값</param>
    /// <param name="maxValue">최대값</param>
    /// <param name="count">구간 수</param>
    /// <param name="styleGenerator">각 구간의 스타일 생성 함수</param>
    /// <returns>생성된 범위 목록</returns>
    public static IList<StyleRange> CreateEqualIntervals(
        double minValue, 
        double maxValue, 
        int count,
        Func<int, IStyle> styleGenerator)
    {
        if (count <= 0) throw new ArgumentException("Count must be greater than 0", nameof(count));
        if (maxValue <= minValue) throw new ArgumentException("MaxValue must be greater than MinValue");
        
        var ranges = new List<StyleRange>();
        var interval = (maxValue - minValue) / count;
        
        for (int i = 0; i < count; i++)
        {
            var rangeMin = minValue + i * interval;
            var rangeMax = i == count - 1 ? maxValue : minValue + (i + 1) * interval;
            
            ranges.Add(new StyleRange
            {
                Name = $"Range {i + 1}",
                MinValue = rangeMin,
                MaxValue = rangeMax,
                IncludeMin = true,
                IncludeMax = i == count - 1, // 마지막 범위만 최대값 포함
                Style = styleGenerator(i),
                Label = $"{rangeMin:F1} - {rangeMax:F1}"
            });
        }
        
        return ranges;
    }
    
    /// <summary>
    /// 분위수 기반 범위 생성
    /// </summary>
    /// <param name="values">값 목록</param>
    /// <param name="quantileCount">분위수 개수</param>
    /// <param name="styleGenerator">각 구간의 스타일 생성 함수</param>
    /// <returns>생성된 범위 목록</returns>
    public static IList<StyleRange> CreateQuantiles(
        IEnumerable<double> values, 
        int quantileCount,
        Func<int, IStyle> styleGenerator)
    {
        if (quantileCount <= 0) throw new ArgumentException("QuantileCount must be greater than 0", nameof(quantileCount));
        
        var sortedValues = values.OrderBy(v => v).ToArray();
        if (sortedValues.Length == 0) return new List<StyleRange>();
        
        var ranges = new List<StyleRange>();
        
        for (int i = 0; i < quantileCount; i++)
        {
            var minIndex = i * sortedValues.Length / quantileCount;
            var maxIndex = (i + 1) * sortedValues.Length / quantileCount - 1;
            
            if (i == quantileCount - 1) maxIndex = sortedValues.Length - 1;
            
            var rangeMin = sortedValues[minIndex];
            var rangeMax = sortedValues[maxIndex];
            
            ranges.Add(new StyleRange
            {
                Name = $"Quantile {i + 1}",
                MinValue = rangeMin,
                MaxValue = rangeMax,
                IncludeMin = true,
                IncludeMax = i == quantileCount - 1,
                Style = styleGenerator(i),
                Label = $"Quantile {i + 1} ({rangeMin:F1} - {rangeMax:F1})"
            });
        }
        
        return ranges;
    }
    
    /// <summary>
    /// 자연스러운 구간 나누기 (Jenks Natural Breaks)
    /// </summary>
    /// <param name="values">값 목록</param>
    /// <param name="classCount">구간 수</param>
    /// <param name="styleGenerator">각 구간의 스타일 생성 함수</param>
    /// <returns>생성된 범위 목록</returns>
    public static IList<StyleRange> CreateNaturalBreaks(
        IEnumerable<double> values, 
        int classCount,
        Func<int, IStyle> styleGenerator)
    {
        var sortedValues = values.OrderBy(v => v).ToArray();
        if (sortedValues.Length == 0 || classCount <= 0) return new List<StyleRange>();
        
        if (classCount >= sortedValues.Length)
        {
            // 구간 수가 값 개수보다 많으면 등간격으로 처리
            return CreateEqualIntervals(sortedValues[0], sortedValues[^1], sortedValues.Length, styleGenerator);
        }
        
        // 간단한 Jenks 알고리즘 구현
        var breaks = new List<double> { sortedValues[0] };
        
        for (int i = 1; i < classCount; i++)
        {
            var breakIndex = i * sortedValues.Length / classCount;
            if (breakIndex >= sortedValues.Length) breakIndex = sortedValues.Length - 1;
            breaks.Add(sortedValues[breakIndex]);
        }
        
        if (breaks[^1] != sortedValues[^1])
        {
            breaks[^1] = sortedValues[^1];
        }
        
        var ranges = new List<StyleRange>();
        for (int i = 0; i < breaks.Count - 1; i++)
        {
            ranges.Add(new StyleRange
            {
                Name = $"Class {i + 1}",
                MinValue = breaks[i],
                MaxValue = breaks[i + 1],
                IncludeMin = true,
                IncludeMax = i == breaks.Count - 2,
                Style = styleGenerator(i),
                Label = $"Class {i + 1} ({breaks[i]:F1} - {breaks[i + 1]:F1})"
            });
        }
        
        return ranges;
    }
}