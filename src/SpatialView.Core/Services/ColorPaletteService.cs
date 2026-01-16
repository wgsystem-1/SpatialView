using System.Drawing;

namespace SpatialView.Core.Services;

/// <summary>
/// 레이어 색상 팔레트 서비스
/// </summary>
public class ColorPaletteService
{
    private int _currentIndex = 0;
    private ColorPalette _currentPalette = ColorPalette.Vivid;
    
    /// <summary>
    /// 사용 가능한 색상 팔레트
    /// </summary>
    public enum ColorPalette
    {
        Vivid,      // 선명한 색상
        Pastel,     // 파스텔 톤
        Earth,      // 자연/대지 색상
        Ocean,      // 바다/파랑 계열
        Warm,       // 따뜻한 색상
        Cool,       // 차가운 색상
        Rainbow,    // 무지개
        Grayscale,  // 회색조
        Negative    // 네거티브 (반전)
    }
    
    /// <summary>
    /// 선명한 색상 팔레트
    /// </summary>
    private static readonly Color[] VividColors = new[]
    {
        Color.FromArgb(255, 31, 119, 180),   // Blue
        Color.FromArgb(255, 255, 127, 14),   // Orange
        Color.FromArgb(255, 44, 160, 44),    // Green
        Color.FromArgb(255, 214, 39, 40),    // Red
        Color.FromArgb(255, 148, 103, 189),  // Purple
        Color.FromArgb(255, 140, 86, 75),    // Brown
        Color.FromArgb(255, 227, 119, 194),  // Pink
        Color.FromArgb(255, 127, 127, 127),  // Gray
        Color.FromArgb(255, 188, 189, 34),   // Olive
        Color.FromArgb(255, 23, 190, 207),   // Cyan
    };
    
    /// <summary>
    /// 파스텔 색상 팔레트
    /// </summary>
    private static readonly Color[] PastelColors = new[]
    {
        Color.FromArgb(255, 174, 199, 232),  // Light Blue
        Color.FromArgb(255, 255, 187, 120),  // Light Orange
        Color.FromArgb(255, 152, 223, 138),  // Light Green
        Color.FromArgb(255, 255, 152, 150),  // Light Red
        Color.FromArgb(255, 197, 176, 213),  // Light Purple
        Color.FromArgb(255, 196, 156, 148),  // Light Brown
        Color.FromArgb(255, 247, 182, 210),  // Light Pink
        Color.FromArgb(255, 199, 199, 199),  // Light Gray
        Color.FromArgb(255, 219, 219, 141),  // Light Olive
        Color.FromArgb(255, 158, 218, 229),  // Light Cyan
    };
    
    /// <summary>
    /// 대지/자연 색상 팔레트
    /// </summary>
    private static readonly Color[] EarthColors = new[]
    {
        Color.FromArgb(255, 139, 90, 43),    // Brown
        Color.FromArgb(255, 85, 107, 47),    // Olive Green
        Color.FromArgb(255, 160, 82, 45),    // Sienna
        Color.FromArgb(255, 107, 142, 35),   // Yellow Green
        Color.FromArgb(255, 184, 134, 11),   // Dark Goldenrod
        Color.FromArgb(255, 128, 128, 0),    // Olive
        Color.FromArgb(255, 189, 183, 107),  // Dark Khaki
        Color.FromArgb(255, 143, 188, 143),  // Dark Sea Green
        Color.FromArgb(255, 205, 133, 63),   // Peru
        Color.FromArgb(255, 46, 139, 87),    // Sea Green
    };
    
    /// <summary>
    /// 바다 색상 팔레트
    /// </summary>
    private static readonly Color[] OceanColors = new[]
    {
        Color.FromArgb(255, 0, 119, 182),    // Blue
        Color.FromArgb(255, 0, 150, 199),    // Light Blue
        Color.FromArgb(255, 0, 180, 216),    // Sky Blue
        Color.FromArgb(255, 72, 202, 228),   // Turquoise
        Color.FromArgb(255, 144, 224, 239),  // Light Turquoise
        Color.FromArgb(255, 3, 4, 94),       // Navy
        Color.FromArgb(255, 2, 62, 138),     // Dark Blue
        Color.FromArgb(255, 0, 119, 182),    // Ocean Blue
        Color.FromArgb(255, 0, 150, 136),    // Teal
        Color.FromArgb(255, 0, 77, 64),      // Dark Teal
    };
    
    /// <summary>
    /// 따뜻한 색상 팔레트
    /// </summary>
    private static readonly Color[] WarmColors = new[]
    {
        Color.FromArgb(255, 255, 87, 51),    // Red Orange
        Color.FromArgb(255, 255, 195, 0),    // Yellow
        Color.FromArgb(255, 255, 128, 0),    // Orange
        Color.FromArgb(255, 255, 0, 0),      // Red
        Color.FromArgb(255, 255, 69, 0),     // Orange Red
        Color.FromArgb(255, 255, 165, 0),    // Bright Orange
        Color.FromArgb(255, 255, 215, 0),    // Gold
        Color.FromArgb(255, 218, 165, 32),   // Goldenrod
        Color.FromArgb(255, 255, 99, 71),    // Tomato
        Color.FromArgb(255, 255, 140, 0),    // Dark Orange
    };
    
    /// <summary>
    /// 차가운 색상 팔레트
    /// </summary>
    private static readonly Color[] CoolColors = new[]
    {
        Color.FromArgb(255, 0, 191, 255),    // Deep Sky Blue
        Color.FromArgb(255, 70, 130, 180),   // Steel Blue
        Color.FromArgb(255, 100, 149, 237),  // Cornflower Blue
        Color.FromArgb(255, 138, 43, 226),   // Blue Violet
        Color.FromArgb(255, 75, 0, 130),     // Indigo
        Color.FromArgb(255, 106, 90, 205),   // Slate Blue
        Color.FromArgb(255, 123, 104, 238),  // Medium Slate Blue
        Color.FromArgb(255, 147, 112, 219),  // Medium Purple
        Color.FromArgb(255, 72, 61, 139),    // Dark Slate Blue
        Color.FromArgb(255, 65, 105, 225),   // Royal Blue
    };
    
    /// <summary>
    /// 무지개 색상 팔레트
    /// </summary>
    private static readonly Color[] RainbowColors = new[]
    {
        Color.FromArgb(255, 255, 0, 0),      // Red
        Color.FromArgb(255, 255, 127, 0),    // Orange
        Color.FromArgb(255, 255, 255, 0),    // Yellow
        Color.FromArgb(255, 0, 255, 0),      // Green
        Color.FromArgb(255, 0, 0, 255),      // Blue
        Color.FromArgb(255, 75, 0, 130),     // Indigo
        Color.FromArgb(255, 148, 0, 211),    // Violet
        Color.FromArgb(255, 255, 20, 147),   // Pink
        Color.FromArgb(255, 0, 255, 255),    // Cyan
        Color.FromArgb(255, 127, 255, 0),    // Chartreuse
    };
    
    /// <summary>
    /// 회색조 팔레트
    /// </summary>
    private static readonly Color[] GrayscaleColors = new[]
    {
        Color.FromArgb(255, 50, 50, 50),
        Color.FromArgb(255, 80, 80, 80),
        Color.FromArgb(255, 110, 110, 110),
        Color.FromArgb(255, 140, 140, 140),
        Color.FromArgb(255, 170, 170, 170),
        Color.FromArgb(255, 200, 200, 200),
        Color.FromArgb(255, 70, 70, 70),
        Color.FromArgb(255, 100, 100, 100),
        Color.FromArgb(255, 130, 130, 130),
        Color.FromArgb(255, 160, 160, 160),
    };
    
    /// <summary>
    /// 네거티브 (반전) 팔레트 - 어두운 배경에서 밝은 색상
    /// </summary>
    private static readonly Color[] NegativeColors = new[]
    {
        Color.FromArgb(255, 0, 255, 255),      // Cyan (Red 반전)
        Color.FromArgb(255, 255, 0, 255),      // Magenta (Green 반전)
        Color.FromArgb(255, 255, 255, 0),      // Yellow (Blue 반전)
        Color.FromArgb(255, 0, 255, 0),        // Green
        Color.FromArgb(255, 255, 128, 255),    // Light Magenta
        Color.FromArgb(255, 128, 255, 255),    // Light Cyan
        Color.FromArgb(255, 255, 255, 128),    // Light Yellow
        Color.FromArgb(255, 255, 200, 100),    // Light Orange
        Color.FromArgb(255, 200, 255, 200),    // Light Green
        Color.FromArgb(255, 200, 200, 255),    // Light Blue
    };
    
    /// <summary>
    /// 현재 팔레트 설정
    /// </summary>
    public ColorPalette CurrentPalette
    {
        get => _currentPalette;
        set
        {
            _currentPalette = value;
            _currentIndex = 0; // 팔레트 변경 시 인덱스 초기화
        }
    }
    
    /// <summary>
    /// 다음 색상 가져오기 (순환)
    /// </summary>
    public Color GetNextColor()
    {
        var colors = GetPaletteColors(_currentPalette);
        var color = colors[_currentIndex % colors.Length];
        _currentIndex++;
        return color;
    }
    
    /// <summary>
    /// 랜덤 색상 가져오기
    /// </summary>
    public Color GetRandomColor()
    {
        var colors = GetPaletteColors(_currentPalette);
        var random = new Random();
        return colors[random.Next(colors.Length)];
    }
    
    /// <summary>
    /// 인덱스 초기화
    /// </summary>
    public void ResetIndex()
    {
        _currentIndex = 0;
    }
    
    /// <summary>
    /// 팔레트별 색상 배열 반환
    /// </summary>
    public Color[] GetPaletteColors(ColorPalette palette)
    {
        return palette switch
        {
            ColorPalette.Vivid => VividColors,
            ColorPalette.Pastel => PastelColors,
            ColorPalette.Earth => EarthColors,
            ColorPalette.Ocean => OceanColors,
            ColorPalette.Warm => WarmColors,
            ColorPalette.Cool => CoolColors,
            ColorPalette.Rainbow => RainbowColors,
            ColorPalette.Grayscale => GrayscaleColors,
            ColorPalette.Negative => NegativeColors,
            _ => VividColors
        };
    }
    
    /// <summary>
    /// 사용 가능한 모든 팔레트 목록
    /// </summary>
    public static IEnumerable<ColorPalette> GetAvailablePalettes()
    {
        return Enum.GetValues<ColorPalette>();
    }
    
    /// <summary>
    /// 팔레트 이름 반환
    /// </summary>
    public static string GetPaletteName(ColorPalette palette)
    {
        return palette switch
        {
            ColorPalette.Vivid => "선명한 색상",
            ColorPalette.Pastel => "파스텔",
            ColorPalette.Earth => "대지/자연",
            ColorPalette.Ocean => "바다/파랑",
            ColorPalette.Warm => "따뜻한 색상",
            ColorPalette.Cool => "차가운 색상",
            ColorPalette.Rainbow => "무지개",
            ColorPalette.Grayscale => "회색조",
            ColorPalette.Negative => "네거티브",
            _ => palette.ToString()
        };
    }
}

