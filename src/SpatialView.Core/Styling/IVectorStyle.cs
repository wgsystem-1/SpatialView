using System.Windows.Media;

namespace SpatialView.Core.Styling;

/// <summary>
/// 벡터 스타일 인터페이스
/// SharpMap VectorStyle과 호환되는 추상 인터페이스
/// </summary>
public interface IVectorStyle
{
    /// <summary>
    /// 채우기 색상
    /// </summary>
    System.Windows.Media.Color Fill { get; set; }
    
    /// <summary>
    /// 외곽선 색상
    /// </summary>
    System.Windows.Media.Color Outline { get; set; }
    
    /// <summary>
    /// 외곽선 두께
    /// </summary>
    float OutlineWidth { get; set; }
    
    /// <summary>
    /// 포인트 크기
    /// </summary>
    float PointSize { get; set; }
    
    /// <summary>
    /// 외곽선 표시 여부
    /// </summary>
    bool EnableOutline { get; set; }
    
    /// <summary>
    /// 라인 두께
    /// </summary>
    float LineWidth { get; set; }
    
    /// <summary>
    /// 투명도 (0.0 - 1.0)
    /// </summary>
    float Opacity { get; set; }
    
    /// <summary>
    /// 포인트 심볼
    /// </summary>
    IPointSymbol? PointSymbol { get; set; }
    
    /// <summary>
    /// 라인 패턴
    /// </summary>
    float[]? LineDashPattern { get; set; }
    
    /// <summary>
    /// 채움 색상 (System.Drawing.Color)
    /// </summary>
    System.Drawing.Color? FillColor { get; set; }
    
    /// <summary>
    /// 선 색상 (System.Drawing.Color)
    /// </summary>
    System.Drawing.Color? LineColor { get; set; }
    
    /// <summary>
    /// 포인트 색상 (System.Drawing.Color)
    /// </summary>
    System.Drawing.Color? PointColor { get; set; }
    
    /// <summary>
    /// 심볼 크기
    /// </summary>
    float SymbolSize { get; set; }
    
    /// <summary>
    /// 심볼 타입
    /// </summary>
    SymbolType SymbolType { get; set; }
    
    /// <summary>
    /// 선 스타일
    /// </summary>
    LineStyle LineStyle { get; set; }
    
    /// <summary>
    /// 심볼로 렌더링 (폴리곤/라인을 중심점 심볼로 변환)
    /// </summary>
    bool RenderAsSymbol { get; set; }
    
    /// <summary>
    /// 스타일 복제
    /// </summary>
    IVectorStyle Clone();
}

/// <summary>
/// 포인트 심볼 인터페이스
/// </summary>
public interface IPointSymbol
{
    /// <summary>
    /// 심볼 타입
    /// </summary>
    PointSymbolType SymbolType { get; set; }
    
    /// <summary>
    /// 크기
    /// </summary>
    float Size { get; set; }
    
    /// <summary>
    /// 회전 각도
    /// </summary>
    float Rotation { get; set; }
    
    /// <summary>
    /// 오프셋
    /// </summary>
    System.Windows.Point Offset { get; set; }
}

/// <summary>
/// 포인트 심볼 타입
/// </summary>
public enum PointSymbolType
{
    /// <summary>
    /// 원
    /// </summary>
    Circle,
    
    /// <summary>
    /// 사각형
    /// </summary>
    Square,
    
    /// <summary>
    /// 삼각형
    /// </summary>
    Triangle,
    
    /// <summary>
    /// 십자
    /// </summary>
    Cross,
    
    /// <summary>
    /// 별
    /// </summary>
    Star,
    
    /// <summary>
    /// 사용자 정의
    /// </summary>
    Custom
}

/// <summary>
/// 심볼 타입 (레이어 스타일용)
/// </summary>
public enum SymbolType
{
    Circle,
    Square,
    Triangle,
    Diamond,
    Star,
    Cross,
    X
}

/// <summary>
/// 선 스타일
/// </summary>
public enum LineStyle
{
    Solid,      // 실선
    Dot,        // 점선
    Dash,       // 파선
    DashDot,    // 일점쇄선
    DashDotDot  // 이점쇄선
}