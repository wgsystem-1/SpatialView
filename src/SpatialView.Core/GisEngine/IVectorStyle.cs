namespace SpatialView.Core.GisEngine;

/// <summary>
/// 벡터 스타일 인터페이스
/// </summary>
public interface IVectorStyle
{
    /// <summary>
    /// 채우기 색상
    /// </summary>
    System.Drawing.Color FillColor { get; set; }
    
    /// <summary>
    /// 외곽선 색상
    /// </summary>
    System.Drawing.Color OutlineColor { get; set; }
    
    /// <summary>
    /// 외곽선 두께
    /// </summary>
    float OutlineWidth { get; set; }
    
    /// <summary>
    /// 외곽선 표시 여부
    /// </summary>
    bool EnableOutline { get; set; }
    
    /// <summary>
    /// 포인트 크기
    /// </summary>
    float PointSize { get; set; }
    
    /// <summary>
    /// 라인 두께
    /// </summary>
    float LineWidth { get; set; }
    
    /// <summary>
    /// 투명도 (0-255)
    /// </summary>
    int Opacity { get; set; }
}

/// <summary>
/// 라벨 스타일 인터페이스
/// </summary>
public interface ILabelStyle
{
    /// <summary>
    /// 폰트
    /// </summary>
    System.Drawing.Font Font { get; set; }
    
    /// <summary>
    /// 텍스트 색상
    /// </summary>
    System.Drawing.Color ForeColor { get; set; }
    
    /// <summary>
    /// 배경 색상
    /// </summary>
    System.Drawing.Color BackColor { get; set; }
    
    /// <summary>
    /// 후광 효과 색상
    /// </summary>
    System.Drawing.Color HaloColor { get; set; }
    
    /// <summary>
    /// 라벨 컬럼명
    /// </summary>
    string LabelColumn { get; set; }
    
    /// <summary>
    /// 정렬
    /// </summary>
    LabelAlignment Alignment { get; set; }
}

/// <summary>
/// 라벨 정렬
/// </summary>
public enum LabelAlignment
{
    Center,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}