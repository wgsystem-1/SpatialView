using SpatialView.Engine.Styling.Rules;
using System.Drawing;

namespace SpatialView.Engine.Styling.Thematic;

/// <summary>
/// 테마틱 매핑 시스템
/// 속성 값에 따른 스타일 매핑을 처리
/// </summary>
public static class ThematicMapping
{
    /// <summary>
    /// 고유값 기반 스타일 매핑 생성
    /// </summary>
    /// <param name="fieldName">분류할 필드명</param>
    /// <param name="uniqueValues">고유값 목록</param>
    /// <param name="colorPalette">색상 팔레트</param>
    /// <returns>고유값 기반 스타일 규칙들</returns>
    public static IEnumerable<IStyleRule> CreateUniqueValueMapping(
        string fieldName, 
        IEnumerable<object> uniqueValues,
        ColorPalette? colorPalette = null)
    {
        if (string.IsNullOrEmpty(fieldName)) throw new ArgumentException("Field name cannot be empty", nameof(fieldName));
        if (uniqueValues == null) throw new ArgumentNullException(nameof(uniqueValues));

        var palette = colorPalette ?? ColorPalette.CreateQualitative(uniqueValues.Count());
        var valueList = uniqueValues.ToList();
        var rules = new List<IStyleRule>();

        for (int i = 0; i < valueList.Count; i++)
        {
            var value = valueList[i];
            var color = palette.GetColor(i);
            
            var rule = new CategoryStyleRule
            {
                Name = $"UniqueValue_{fieldName}_{value}",
                FieldName = fieldName,
                Value = value,
                Style = CreateVectorStyle(color),
                Priority = 100 - i, // 순서대로 우선순위 부여
                Enabled = true
            };

            rules.Add(rule);
        }

        return rules;
    }

    /// <summary>
    /// 등급별 분류 스타일 매핑 생성 (Graduated Symbols)
    /// </summary>
    /// <param name="fieldName">분류할 필드명</param>
    /// <param name="values">값 목록</param>
    /// <param name="classCount">클래스 개수</param>
    /// <param name="method">분류 방법</param>
    /// <param name="colorRamp">색상 램프</param>
    /// <returns>등급별 스타일 규칙들</returns>
    public static IEnumerable<IStyleRule> CreateGraduatedMapping(
        string fieldName,
        IEnumerable<double> values,
        int classCount = 5,
        ClassificationMethod method = ClassificationMethod.NaturalBreaks,
        ColorRamp? colorRamp = null)
    {
        if (string.IsNullOrEmpty(fieldName)) throw new ArgumentException("Field name cannot be empty", nameof(fieldName));
        if (values == null) throw new ArgumentNullException(nameof(values));
        if (classCount <= 0) throw new ArgumentException("Class count must be positive", nameof(classCount));

        var valueList = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
        if (valueList.Count == 0) return Enumerable.Empty<IStyleRule>();

        var breaks = CalculateClassBreaks(valueList, classCount, method);
        var ramp = colorRamp ?? ColorRamp.CreateDefault(classCount);
        var rules = new List<IStyleRule>();

        for (int i = 0; i < breaks.Length - 1; i++)
        {
            var minValue = breaks[i];
            var maxValue = breaks[i + 1];
            var color = ramp.GetColor((double)i / (classCount - 1));
            var isLastClass = i == breaks.Length - 2;

            var rule = new RangeStyleRule
            {
                Name = $"GraduatedClass_{i + 1}",
                FieldName = fieldName,
                MinValue = minValue,
                MaxValue = maxValue,
                IncludeMinValue = true,
                IncludeMaxValue = isLastClass, // 마지막 클래스만 최대값 포함
                Style = CreateVectorStyle(color),
                Priority = 100 - i,
                Enabled = true
            };

            rules.Add(rule);
        }

        return rules;
    }

    /// <summary>
    /// 비례 심볼 매핑 생성 (Proportional Symbols)
    /// </summary>
    /// <param name="fieldName">크기 기준 필드명</param>
    /// <param name="values">값 목록</param>
    /// <param name="minSize">최소 크기</param>
    /// <param name="maxSize">최대 크기</param>
    /// <param name="baseColor">기본 색상</param>
    /// <param name="scalingMethod">크기 조정 방법</param>
    /// <returns>비례 심볼 스타일 규칙들</returns>
    public static IEnumerable<IStyleRule> CreateProportionalSymbolMapping(
        string fieldName,
        IEnumerable<double> values,
        double minSize = 2.0,
        double maxSize = 20.0,
        Color? baseColor = null,
        SymbolScalingMethod scalingMethod = SymbolScalingMethod.Linear)
    {
        if (string.IsNullOrEmpty(fieldName)) throw new ArgumentException("Field name cannot be empty", nameof(fieldName));
        if (values == null) throw new ArgumentNullException(nameof(values));

        var valueList = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0).ToList();
        if (valueList.Count == 0) return Enumerable.Empty<IStyleRule>();

        var minValue = valueList.Min();
        var maxValue = valueList.Max();
        var color = baseColor ?? Color.Blue;

        // 연속적인 비례 심볼을 위한 단일 규칙
        var rule = new ProportionalSymbolRule
        {
            Name = "ProportionalSymbol",
            FieldName = fieldName,
            MinValue = minValue,
            MaxValue = maxValue,
            MinSize = minSize,
            MaxSize = maxSize,
            BaseColor = color,
            ScalingMethod = scalingMethod,
            Priority = 100,
            Enabled = true
        };

        return new[] { rule };
    }

    /// <summary>
    /// 밀도 기반 히트맵 매핑 생성
    /// </summary>
    /// <param name="fieldName">밀도 기준 필드명</param>
    /// <param name="values">값 목록</param>
    /// <param name="heatmapColors">히트맵 색상 배열</param>
    /// <returns>히트맵 스타일 규칙들</returns>
    public static IEnumerable<IStyleRule> CreateHeatmapMapping(
        string fieldName,
        IEnumerable<double> values,
        Color[]? heatmapColors = null)
    {
        if (string.IsNullOrEmpty(fieldName)) throw new ArgumentException("Field name cannot be empty", nameof(fieldName));
        if (values == null) throw new ArgumentNullException(nameof(values));

        var colors = heatmapColors ?? new[] 
        { 
            Color.Blue, Color.Cyan, Color.Green, Color.Yellow, Color.Orange, Color.Red 
        };

        var valueList = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
        if (valueList.Count == 0) return Enumerable.Empty<IStyleRule>();

        var minValue = valueList.Min();
        var maxValue = valueList.Max();
        var range = maxValue - minValue;

        if (range == 0) return Enumerable.Empty<IStyleRule>();

        var rules = new List<IStyleRule>();
        var classCount = colors.Length;

        for (int i = 0; i < classCount; i++)
        {
            var rangeMin = minValue + (range * i / classCount);
            var rangeMax = minValue + (range * (i + 1) / classCount);
            var isLastClass = i == classCount - 1;

            var rule = new RangeStyleRule
            {
                Name = $"Heatmap_{i + 1}",
                FieldName = fieldName,
                MinValue = rangeMin,
                MaxValue = rangeMax,
                IncludeMinValue = true,
                IncludeMaxValue = isLastClass,
                Style = CreateVectorStyle(colors[i]),
                Priority = 100 - i,
                Enabled = true
            };

            rules.Add(rule);
        }

        return rules;
    }

    /// <summary>
    /// 이변량 테마틱 매핑 생성 (두 변수 기반)
    /// </summary>
    /// <param name="fieldName1">첫 번째 필드명</param>
    /// <param name="fieldName2">두 번째 필드명</param>
    /// <param name="values1">첫 번째 필드 값들</param>
    /// <param name="values2">두 번째 필드 값들</param>
    /// <param name="classCount">각 차원의 클래스 개수</param>
    /// <returns>이변량 스타일 규칙들</returns>
    public static IEnumerable<IStyleRule> CreateBivariateMapping(
        string fieldName1,
        string fieldName2,
        IEnumerable<double> values1,
        IEnumerable<double> values2,
        int classCount = 3)
    {
        if (string.IsNullOrEmpty(fieldName1)) throw new ArgumentException("First field name cannot be empty", nameof(fieldName1));
        if (string.IsNullOrEmpty(fieldName2)) throw new ArgumentException("Second field name cannot be empty", nameof(fieldName2));
        if (values1 == null) throw new ArgumentNullException(nameof(values1));
        if (values2 == null) throw new ArgumentNullException(nameof(values2));

        var valueList1 = values1.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
        var valueList2 = values2.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();

        if (valueList1.Count == 0 || valueList2.Count == 0) return Enumerable.Empty<IStyleRule>();

        var breaks1 = CalculateClassBreaks(valueList1, classCount, ClassificationMethod.Quantile);
        var breaks2 = CalculateClassBreaks(valueList2, classCount, ClassificationMethod.Quantile);
        var rules = new List<IStyleRule>();

        for (int i = 0; i < classCount; i++)
        {
            for (int j = 0; j < classCount; j++)
            {
                var color = GetBivariateColor(i, j, classCount);
                
                var rule = new BivariateStyleRule
                {
                    Name = $"Bivariate_{i + 1}_{j + 1}",
                    FieldName1 = fieldName1,
                    FieldName2 = fieldName2,
                    MinValue1 = breaks1[i],
                    MaxValue1 = breaks1[i + 1],
                    MinValue2 = breaks2[j],
                    MaxValue2 = breaks2[j + 1],
                    Style = CreateVectorStyle(color),
                    Priority = 100 - (i * classCount + j),
                    Enabled = true
                };

                rules.Add(rule);
            }
        }

        return rules;
    }

    #region Private Helper Methods

    /// <summary>
    /// 클래스 구간 계산
    /// </summary>
    private static double[] CalculateClassBreaks(List<double> values, int classCount, ClassificationMethod method)
    {
        return method switch
        {
            ClassificationMethod.EqualInterval => CalculateEqualInterval(values, classCount),
            ClassificationMethod.Quantile => CalculateQuantile(values, classCount),
            ClassificationMethod.NaturalBreaks => CalculateNaturalBreaks(values, classCount),
            ClassificationMethod.StandardDeviation => CalculateStandardDeviation(values, classCount),
            _ => CalculateEqualInterval(values, classCount)
        };
    }

    /// <summary>
    /// 등간격 분류
    /// </summary>
    private static double[] CalculateEqualInterval(List<double> values, int classCount)
    {
        var min = values.Min();
        var max = values.Max();
        var interval = (max - min) / classCount;
        
        var breaks = new double[classCount + 1];
        for (int i = 0; i <= classCount; i++)
        {
            breaks[i] = min + i * interval;
        }
        
        return breaks;
    }

    /// <summary>
    /// 분위수 분류
    /// </summary>
    private static double[] CalculateQuantile(List<double> values, int classCount)
    {
        var breaks = new double[classCount + 1];
        breaks[0] = values.First();
        breaks[classCount] = values.Last();
        
        for (int i = 1; i < classCount; i++)
        {
            var position = (double)(i * values.Count) / classCount;
            var lowerIndex = (int)Math.Floor(position);
            var upperIndex = Math.Min(lowerIndex + 1, values.Count - 1);
            var fraction = position - lowerIndex;
            
            breaks[i] = values[lowerIndex] + fraction * (values[upperIndex] - values[lowerIndex]);
        }
        
        return breaks;
    }

    /// <summary>
    /// 자연 구분법 (Jenks Natural Breaks)
    /// </summary>
    private static double[] CalculateNaturalBreaks(List<double> values, int classCount)
    {
        // 간단한 구현 - 실제로는 더 복잡한 최적화 알고리즘 필요
        return CalculateQuantile(values, classCount);
    }

    /// <summary>
    /// 표준편차 분류
    /// </summary>
    private static double[] CalculateStandardDeviation(List<double> values, int classCount)
    {
        var mean = values.Average();
        var stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));
        
        var breaks = new double[classCount + 1];
        var halfClasses = classCount / 2;
        
        for (int i = 0; i <= classCount; i++)
        {
            var deviations = i - halfClasses;
            breaks[i] = mean + deviations * stdDev;
        }
        
        return breaks;
    }

    /// <summary>
    /// 벡터 스타일 생성
    /// </summary>
    private static IStyle CreateVectorStyle(Color color, double size = 1.0, double width = 1.0)
    {
        return new VectorStyle
        {
            Fill = color,
            Stroke = Color.Black,
            StrokeWidth = width,
            PointSize = size,
            Opacity = 1.0
        };
    }

    /// <summary>
    /// 이변량 색상 가져오기
    /// </summary>
    private static Color GetBivariateColor(int x, int y, int maxClass)
    {
        // 이변량 색상 매트릭스 (3x3 예시)
        var colors = new Color[,]
        {
            { Color.FromArgb(200, 200, 255), Color.FromArgb(150, 150, 255), Color.FromArgb(100, 100, 255) },
            { Color.FromArgb(255, 200, 200), Color.FromArgb(200, 150, 200), Color.FromArgb(150, 100, 200) },
            { Color.FromArgb(255, 255, 100), Color.FromArgb(200, 200, 100), Color.FromArgb(150, 150, 100) }
        };

        if (maxClass == 3 && x < 3 && y < 3)
        {
            return colors[x, y];
        }

        // 기본 색상 (클래스 수가 3이 아닌 경우)
        var intensity = 1.0 - (double)(x + y) / (2 * (maxClass - 1));
        var r = (int)(255 * (1.0 - (double)x / (maxClass - 1)));
        var g = (int)(255 * intensity);
        var b = (int)(255 * (1.0 - (double)y / (maxClass - 1)));

        return Color.FromArgb(r, g, b);
    }

    #endregion
}

/// <summary>
/// 분류 방법
/// </summary>
public enum ClassificationMethod
{
    /// <summary>등간격</summary>
    EqualInterval,
    /// <summary>분위수</summary>
    Quantile,
    /// <summary>자연 구분법</summary>
    NaturalBreaks,
    /// <summary>표준편차</summary>
    StandardDeviation
}

/// <summary>
/// 심볼 크기 조정 방법
/// </summary>
public enum SymbolScalingMethod
{
    /// <summary>선형</summary>
    Linear,
    /// <summary>제곱근</summary>
    SquareRoot,
    /// <summary>로그</summary>
    Logarithmic
}