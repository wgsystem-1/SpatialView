using System.Windows;
using System.Windows.Media;

namespace SpatialView.Engine.Styling;

/// <summary>
/// 라벨 스타일 인터페이스
/// </summary>
public interface ILabelStyle : IStyle
{
    /// <summary>
    /// 라벨로 표시할 속성 필드명
    /// </summary>
    string LabelField { get; set; }
    
    /// <summary>
    /// 폰트 패밀리
    /// </summary>
    FontFamily FontFamily { get; set; }
    
    /// <summary>
    /// 폰트 크기
    /// </summary>
    double FontSize { get; set; }
    
    /// <summary>
    /// 폰트 색상
    /// </summary>
    Color FontColor { get; set; }
    
    /// <summary>
    /// 폰트 굵기
    /// </summary>
    FontWeight FontWeight { get; set; }
    
    /// <summary>
    /// 폰트 스타일 (기울임)
    /// </summary>
    FontStyle FontStyle { get; set; }
    
    /// <summary>
    /// 헤일로(외곽선) 활성화
    /// </summary>
    bool HaloEnabled { get; set; }
    
    /// <summary>
    /// 헤일로 색상
    /// </summary>
    Color HaloColor { get; set; }
    
    /// <summary>
    /// 헤일로 두께
    /// </summary>
    double HaloWidth { get; set; }
    
    /// <summary>
    /// 라벨 배치 위치
    /// </summary>
    LabelPlacement Placement { get; set; }
    
    /// <summary>
    /// X 오프셋 (픽셀)
    /// </summary>
    double OffsetX { get; set; }
    
    /// <summary>
    /// Y 오프셋 (픽셀)
    /// </summary>
    double OffsetY { get; set; }
    
    /// <summary>
    /// 라벨 회전 각도 (도)
    /// </summary>
    double Rotation { get; set; }
    
    /// <summary>
    /// 라인을 따라 라벨 배치 (LineString용)
    /// </summary>
    bool FollowLine { get; set; }
    
    /// <summary>
    /// 최대 라벨 각도 (FollowLine 사용 시)
    /// </summary>
    double MaxAngle { get; set; }
    
    /// <summary>
    /// 라벨 중복 허용
    /// </summary>
    bool AllowOverlap { get; set; }
    
    /// <summary>
    /// 우선순위 (높을수록 먼저 표시)
    /// </summary>
    int Priority { get; set; }
}

/// <summary>
/// 라벨 배치 위치
/// </summary>
public enum LabelPlacement
{
    /// <summary>
    /// 중앙
    /// </summary>
    Center,
    
    /// <summary>
    /// 상단
    /// </summary>
    Top,
    
    /// <summary>
    /// 하단
    /// </summary>
    Bottom,
    
    /// <summary>
    /// 좌측
    /// </summary>
    Left,
    
    /// <summary>
    /// 우측
    /// </summary>
    Right,
    
    /// <summary>
    /// 좌상단
    /// </summary>
    TopLeft,
    
    /// <summary>
    /// 우상단
    /// </summary>
    TopRight,
    
    /// <summary>
    /// 좌하단
    /// </summary>
    BottomLeft,
    
    /// <summary>
    /// 우하단
    /// </summary>
    BottomRight
}

/// <summary>
/// 라벨 스타일 구현
/// </summary>
public class LabelStyle : ILabelStyle
{
    public string Name { get; set; } = "Label Style";
    public bool IsVisible { get; set; } = true;
    public double MinZoom { get; set; } = 0;
    public double MaxZoom { get; set; } = double.MaxValue;
    
    public string LabelField { get; set; } = string.Empty;
    public FontFamily FontFamily { get; set; } = new FontFamily("Malgun Gothic");
    public double FontSize { get; set; } = 12;
    public Color FontColor { get; set; } = Colors.Black;
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    public FontStyle FontStyle { get; set; } = FontStyles.Normal;
    
    public bool HaloEnabled { get; set; } = true;
    public Color HaloColor { get; set; } = Colors.White;
    public double HaloWidth { get; set; } = 2;
    
    public LabelPlacement Placement { get; set; } = LabelPlacement.Center;
    public double OffsetX { get; set; } = 0;
    public double OffsetY { get; set; } = 0;
    public double Rotation { get; set; } = 0;
    
    public bool FollowLine { get; set; } = false;
    public double MaxAngle { get; set; } = 45;
    
    public bool AllowOverlap { get; set; } = false;
    public int Priority { get; set; } = 0;
    
    /// <summary>
    /// 기본 라벨 스타일 생성
    /// </summary>
    public static LabelStyle CreateDefault(string labelField)
    {
        return new LabelStyle
        {
            LabelField = labelField,
            FontFamily = new FontFamily("Malgun Gothic"),
            FontSize = 11,
            FontColor = Colors.Black,
            HaloEnabled = true,
            HaloColor = Colors.White,
            HaloWidth = 2,
            Placement = LabelPlacement.Center
        };
    }
}
