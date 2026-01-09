namespace SpatialView.Core.GisEngine;

/// <summary>
/// 맵 도구 종류
/// </summary>
public enum MapTool
{
    /// <summary>
    /// 없음
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 이동 도구
    /// </summary>
    Pan = 1,
    
    /// <summary>
    /// 확대 도구
    /// </summary>
    ZoomIn = 2,
    
    /// <summary>
    /// 축소 도구
    /// </summary>
    ZoomOut = 3,
    
    /// <summary>
    /// 영역 확대
    /// </summary>
    ZoomWindow = 4,
    
    /// <summary>
    /// 선택 도구
    /// </summary>
    Select = 5,
    
    /// <summary>
    /// 정보 조회
    /// </summary>
    Info = 6,
    
    /// <summary>
    /// 거리 측정
    /// </summary>
    MeasureDistance = 7,
    
    /// <summary>
    /// 면적 측정
    /// </summary>
    MeasureArea = 8,
    
    /// <summary>
    /// 그리기 도구
    /// </summary>
    Draw = 9,
    
    /// <summary>
    /// 편집 도구
    /// </summary>
    Edit = 10
}

/// <summary>
/// 마우스 버튼
/// </summary>
public enum MapMouseButton
{
    /// <summary>
    /// 없음
    /// </summary>
    None = 0,
    
    /// <summary>
    /// 왼쪽 버튼
    /// </summary>
    Left = 1,
    
    /// <summary>
    /// 오른쪽 버튼
    /// </summary>
    Right = 2,
    
    /// <summary>
    /// 중간 버튼
    /// </summary>
    Middle = 3,
    
    /// <summary>
    /// X1 버튼
    /// </summary>
    XButton1 = 4,
    
    /// <summary>
    /// X2 버튼
    /// </summary>
    XButton2 = 5
}

/// <summary>
/// 맵 마우스 이벤트 인자
/// </summary>
public class MapMouseEventArgs : EventArgs
{
    /// <summary>
    /// X 좌표 (화면)
    /// </summary>
    public int X { get; set; }
    
    /// <summary>
    /// Y 좌표 (화면)
    /// </summary>
    public int Y { get; set; }
    
    /// <summary>
    /// 마우스 버튼
    /// </summary>
    public MapMouseButton Button { get; set; }
    
    /// <summary>
    /// 휠 델타값
    /// </summary>
    public int Delta { get; set; }
    
    /// <summary>
    /// Shift 키 눌림 여부
    /// </summary>
    public bool Shift { get; set; }
    
    /// <summary>
    /// Ctrl 키 눌림 여부
    /// </summary>
    public bool Control { get; set; }
    
    /// <summary>
    /// Alt 키 눌림 여부
    /// </summary>
    public bool Alt { get; set; }
}