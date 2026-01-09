using SpatialView.Engine.Styling.Rules;
using SpatialView.Engine.Data;
using System.Drawing;

namespace SpatialView.Engine.Styling.Thematic;

/// <summary>
/// 카테고리 기반 스타일 규칙
/// </summary>
public class CategoryStyleRule : IStyleRule, ICategoryStyleRule
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string? Description { get; set; } = string.Empty;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public string FieldName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public IStyle? Style { get; set; }

    public bool Matches(Data.IFeature feature, double zoom)
    {
        if (!Enabled || feature?.Attributes == null || string.IsNullOrEmpty(FieldName))
            return false;

        if (feature.Attributes.Exists(FieldName))
        {
            var featureValue = feature.Attributes[FieldName];
            return CompareValues(featureValue, Value);
        }

        return false;
    }

    public IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        return Matches(feature, zoom) ? Style : null;
    }

    public bool Matches(IFeature feature)
    {
        return Matches(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
    
    public IStyle? GetStyle(IFeature feature)
    {
        return GetStyle(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }

    private static bool CompareValues(object? value1, object? value2)
    {
        if (value1 == null && value2 == null) return true;
        if (value1 == null || value2 == null) return false;

        return value1.ToString() == value2?.ToString();
    }
}

/// <summary>
/// 범위 기반 스타일 규칙
/// </summary>
public class RangeStyleRule : IStyleRule, IRangeStyleRule
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string? Description { get; set; } = string.Empty;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public string FieldName { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public bool IncludeMinValue { get; set; } = true;
    public bool IncludeMaxValue { get; set; } = false;
    public IStyle? Style { get; set; }

    public bool Matches(Data.IFeature feature, double zoom)
    {
        if (!Enabled || feature?.Attributes == null || string.IsNullOrEmpty(FieldName))
            return false;

        if (feature.Attributes.Exists(FieldName))
        {
            var value = feature.Attributes[FieldName];
            if (double.TryParse(value?.ToString(), out var numericValue))
            {
                var minOk = IncludeMinValue ? numericValue >= MinValue : numericValue > MinValue;
                var maxOk = IncludeMaxValue ? numericValue <= MaxValue : numericValue < MaxValue;
                return minOk && maxOk;
            }
        }

        return false;
    }

    public IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        return Matches(feature, zoom) ? Style : null;
    }

    public bool Matches(IFeature feature)
    {
        return Matches(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
    
    public IStyle? GetStyle(IFeature feature)
    {
        return GetStyle(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
}

/// <summary>
/// 비례 심볼 스타일 규칙
/// </summary>
public class ProportionalSymbolRule : IStyleRule
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string? Description { get; set; } = string.Empty;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public string FieldName { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double MinSize { get; set; } = 2.0;
    public double MaxSize { get; set; } = 20.0;
    public Color BaseColor { get; set; } = Color.Blue;
    public SymbolScalingMethod ScalingMethod { get; set; } = SymbolScalingMethod.Linear;

    public bool Matches(Data.IFeature feature, double zoom)
    {
        if (!Enabled || feature?.Attributes == null || string.IsNullOrEmpty(FieldName))
            return false;

        return feature.Attributes.Exists(FieldName);
    }

    public IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        if (!Matches(feature, zoom) || feature?.Attributes == null)
            return null;

        var value = feature.Attributes[FieldName];
        if (!double.TryParse(value?.ToString(), out var numericValue))
            return null;

        // 값이 범위를 벗어나면 경계값으로 클램프
        numericValue = Math.Max(MinValue, Math.Min(MaxValue, numericValue));

        var normalizedValue = (numericValue - MinValue) / (MaxValue - MinValue);
        var size = CalculateSize(normalizedValue);

        return new VectorStyle
        {
            Fill = BaseColor,
            Stroke = Color.Black,
            StrokeWidth = 1.0,
            PointSize = size,
            Opacity = 1.0
        };
    }

    private double CalculateSize(double normalizedValue)
    {
        var scaledValue = ScalingMethod switch
        {
            SymbolScalingMethod.SquareRoot => Math.Sqrt(normalizedValue),
            SymbolScalingMethod.Logarithmic => normalizedValue > 0 ? Math.Log(1 + normalizedValue) / Math.Log(2) : 0,
            _ => normalizedValue // Linear
        };

        return MinSize + scaledValue * (MaxSize - MinSize);
    }

    public bool Matches(IFeature feature)
    {
        return Matches(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
    
    public IStyle? GetStyle(IFeature feature)
    {
        return GetStyle(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
}

/// <summary>
/// 이변량 스타일 규칙
/// </summary>
public class BivariateStyleRule : IStyleRule
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public string? Description { get; set; } = string.Empty;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    public string FieldName1 { get; set; } = string.Empty;
    public string FieldName2 { get; set; } = string.Empty;
    public double MinValue1 { get; set; }
    public double MaxValue1 { get; set; }
    public double MinValue2 { get; set; }
    public double MaxValue2 { get; set; }
    public IStyle? Style { get; set; }

    public bool Matches(Data.IFeature feature, double zoom)
    {
        if (!Enabled || feature?.Attributes == null || 
            string.IsNullOrEmpty(FieldName1) || string.IsNullOrEmpty(FieldName2))
            return false;

        if (!feature.Attributes.Exists(FieldName1) || !feature.Attributes.Exists(FieldName2))
            return false;

        var value1 = feature.Attributes[FieldName1];
        var value2 = feature.Attributes[FieldName2];

        if (!double.TryParse(value1?.ToString(), out var num1) || 
            !double.TryParse(value2?.ToString(), out var num2))
            return false;

        return num1 >= MinValue1 && num1 <= MaxValue1 && 
               num2 >= MinValue2 && num2 <= MaxValue2;
    }

    public IStyle? GetStyle(Data.IFeature feature, double zoom)
    {
        return Matches(feature, zoom) ? Style : null;
    }

    public bool Matches(IFeature feature)
    {
        return Matches(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
    
    public IStyle? GetStyle(IFeature feature)
    {
        return GetStyle(feature as Data.IFeature ?? throw new ArgumentException(), 0);
    }
}

/// <summary>
/// 조건부 스타일 규칙 인터페이스 확장
/// </summary>
public interface IConditionalStyleRule : IStyleRule
{
    IList<IStyleCondition> Conditions { get; }
}

/// <summary>
/// 카테고리 스타일 규칙 인터페이스
/// </summary>
public interface ICategoryStyleRule : IStyleRule
{
    string FieldName { get; set; }
    object? Value { get; set; }
}

/// <summary>
/// 범위 스타일 규칙 인터페이스
/// </summary>
public interface IRangeStyleRule : IStyleRule
{
    string FieldName { get; set; }
    double MinValue { get; set; }
    double MaxValue { get; set; }
    bool IncludeMinValue { get; set; }
    bool IncludeMaxValue { get; set; }
}

/// <summary>
/// 지오메트리 타입 조건 인터페이스
/// </summary>
public interface IGeometryTypeCondition : IStyleCondition
{
    IList<Type> AllowedTypes { get; }
}

/// <summary>
/// 색상 팔레트
/// </summary>
public class ColorPalette
{
    private readonly List<Color> _colors;

    public ColorPalette(IEnumerable<Color> colors)
    {
        _colors = colors?.ToList() ?? throw new ArgumentNullException(nameof(colors));
    }

    public Color GetColor(int index)
    {
        if (_colors.Count == 0) return Color.Gray;
        return _colors[index % _colors.Count];
    }

    public int Count => _colors.Count;

    /// <summary>
    /// 정성적 색상 팔레트 생성
    /// </summary>
    public static ColorPalette CreateQualitative(int count)
    {
        var colors = new List<Color>();
        
        // 기본 정성적 색상들
        var baseColors = new[]
        {
            Color.Red, Color.Blue, Color.Green, Color.Orange, Color.Purple,
            Color.Brown, Color.Pink, Color.Gray, Color.Olive, Color.Cyan
        };

        for (int i = 0; i < count; i++)
        {
            if (i < baseColors.Length)
            {
                colors.Add(baseColors[i]);
            }
            else
            {
                // HSV 색공간에서 균등하게 분포된 색상 생성
                var hue = (i * 360.0 / count) % 360;
                colors.Add(HSVToColor(hue, 0.7, 0.8));
            }
        }

        return new ColorPalette(colors);
    }

    /// <summary>
    /// 순차적 색상 팔레트 생성
    /// </summary>
    public static ColorPalette CreateSequential(Color baseColor, int count)
    {
        var colors = new List<Color>();
        
        for (int i = 0; i < count; i++)
        {
            var ratio = (double)i / (count - 1);
            var r = (int)(baseColor.R * (0.2 + 0.8 * ratio));
            var g = (int)(baseColor.G * (0.2 + 0.8 * ratio));
            var b = (int)(baseColor.B * (0.2 + 0.8 * ratio));
            
            colors.Add(Color.FromArgb(r, g, b));
        }

        return new ColorPalette(colors);
    }

    private static Color HSVToColor(double hue, double saturation, double value)
    {
        var hi = (int)Math.Floor(hue / 60) % 6;
        var f = hue / 60 - Math.Floor(hue / 60);

        var v = (int)(255 * value);
        var p = (int)(255 * value * (1 - saturation));
        var q = (int)(255 * value * (1 - f * saturation));
        var t = (int)(255 * value * (1 - (1 - f) * saturation));

        return hi switch
        {
            0 => Color.FromArgb(v, t, p),
            1 => Color.FromArgb(q, v, p),
            2 => Color.FromArgb(p, v, t),
            3 => Color.FromArgb(p, q, v),
            4 => Color.FromArgb(t, p, v),
            _ => Color.FromArgb(v, p, q)
        };
    }
}

/// <summary>
/// 색상 램프
/// </summary>
public class ColorRamp
{
    private readonly List<Color> _colors;

    public ColorRamp(IEnumerable<Color> colors)
    {
        _colors = colors?.ToList() ?? throw new ArgumentNullException(nameof(colors));
        if (_colors.Count < 2) throw new ArgumentException("Color ramp must have at least 2 colors");
    }

    public Color GetColor(double ratio)
    {
        ratio = Math.Max(0, Math.Min(1, ratio));
        
        if (ratio == 0) return _colors[0];
        if (ratio == 1) return _colors[_colors.Count - 1];

        var index = ratio * (_colors.Count - 1);
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = Math.Min(lowerIndex + 1, _colors.Count - 1);
        var localRatio = index - lowerIndex;

        var color1 = _colors[lowerIndex];
        var color2 = _colors[upperIndex];

        var r = (int)(color1.R + localRatio * (color2.R - color1.R));
        var g = (int)(color1.G + localRatio * (color2.G - color1.G));
        var b = (int)(color1.B + localRatio * (color2.B - color1.B));

        return Color.FromArgb(r, g, b);
    }

    /// <summary>
    /// 기본 색상 램프 생성
    /// </summary>
    public static ColorRamp CreateDefault(int steps)
    {
        return CreateSpectrum(steps);
    }

    /// <summary>
    /// 스펙트럼 색상 램프 생성
    /// </summary>
    public static ColorRamp CreateSpectrum(int steps)
    {
        var colors = new List<Color>();
        
        for (int i = 0; i < steps; i++)
        {
            var hue = 240 - (240.0 * i / (steps - 1)); // 파란색에서 빨간색으로
            var color = HSVToColor(hue, 1.0, 1.0);
            colors.Add(color);
        }

        return new ColorRamp(colors);
    }

    /// <summary>
    /// 두 색상 간 그라데이션 생성
    /// </summary>
    public static ColorRamp CreateGradient(Color startColor, Color endColor)
    {
        return new ColorRamp(new[] { startColor, endColor });
    }

    private static Color HSVToColor(double hue, double saturation, double value)
    {
        var hi = (int)Math.Floor(hue / 60) % 6;
        var f = hue / 60 - Math.Floor(hue / 60);

        var v = (int)(255 * value);
        var p = (int)(255 * value * (1 - saturation));
        var q = (int)(255 * value * (1 - f * saturation));
        var t = (int)(255 * value * (1 - (1 - f) * saturation));

        return hi switch
        {
            0 => Color.FromArgb(v, t, p),
            1 => Color.FromArgb(q, v, p),
            2 => Color.FromArgb(p, v, t),
            3 => Color.FromArgb(p, q, v),
            4 => Color.FromArgb(t, p, v),
            _ => Color.FromArgb(v, p, q)
        };
    }
}

/// <summary>
/// 벡터 스타일 구현
/// </summary>
public class VectorStyle : IStyle
{
    public string Name { get; set; } = "Vector Style";
    public bool IsVisible { get; set; } = true;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    
    public Color Fill { get; set; } = Color.Blue;
    public Color Stroke { get; set; } = Color.Black;
    public double StrokeWidth { get; set; } = 1.0;
    public double PointSize { get; set; } = 5.0;
    public double Opacity { get; set; } = 1.0;
    public string? Symbol { get; set; }

    public object Clone()
    {
        return new VectorStyle
        {
            Fill = Fill,
            Stroke = Stroke,
            StrokeWidth = StrokeWidth,
            PointSize = PointSize,
            Opacity = Opacity,
            Symbol = Symbol
        };
    }
}