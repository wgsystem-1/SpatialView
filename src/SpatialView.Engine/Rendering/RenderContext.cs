using System.Windows;
using System.Windows.Media;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// 렌더링 컨텍스트
/// 렌더링에 필요한 정보를 담는 정보 객체
/// </summary>
public class RenderContext
{
    /// <summary>
    /// WPF DrawingContext
    /// </summary>
    public DrawingContext DrawingContext { get; set; } = null!;
    
    /// <summary>
    /// 현재 보이는 영역
    /// </summary>
    public Geometry.Envelope ViewExtent { get; set; } = new Geometry.Envelope();
    
    /// <summary>
    /// 화면 크기
    /// </summary>
    public Size ScreenSize { get; set; }
    
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    public double Zoom { get; set; }
    
    /// <summary>
    /// 좌표계 SRID
    /// </summary>
    public int SRID { get; set; }
    
    /// <summary>
    /// 렌더링 품질
    /// </summary>
    public RenderingQuality Quality { get; set; }
    
    /// <summary>
    /// 안티앨리어싱 사용 여부
    /// </summary>
    public bool AntiAliasing { get; set; }
    
    /// <summary>
    /// 맵 좌표를 화면 좌표로 변환하는 함수
    /// </summary>
    public Func<Geometry.ICoordinate, Point> MapToScreen { get; set; } = null!;
    
    /// <summary>
    /// 화면 좌표를 맵 좌표로 변환하는 함수
    /// </summary>
    public Func<Point, Geometry.ICoordinate> ScreenToMap { get; set; } = null!;
    
    /// <summary>
    /// 레이어 스타일 (채움색, 선색, 투명도 등)
    /// </summary>
    public LayerRenderStyle? LayerStyle { get; set; }
    
    /// <summary>
    /// 현재 뷰포트와 지오메트리가 교차하는지 확인
    /// </summary>
    public bool IsVisible(Geometry.IGeometry geometry)
    {
        if (geometry?.Envelope == null) return false;
        return ViewExtent.Intersects(geometry.Envelope);
    }
    
    /// <summary>
    /// 좌표 배열을 화면 점 배열로 변환
    /// </summary>
    public Point[] ConvertToScreenPoints(Geometry.ICoordinate[] coordinates)
    {
        return coordinates.Select(MapToScreen).ToArray();
    }
    
    /// <summary>
    /// 해상도 계산 (맵 단위/픽셀)
    /// </summary>
    public double Resolution => 1.0 / Zoom;
    
    /// <summary>
    /// 화면 단위로 크기 계산
    /// </summary>
    public double MapDistanceToScreen(double mapDistance)
    {
        return mapDistance / Resolution;
    }
    
    /// <summary>
    /// 맵 단위로 크기 계산
    /// </summary>
    public double ScreenDistanceToMap(double screenDistance)
    {
        return screenDistance * Resolution;
    }
}

/// <summary>
/// 레이어 렌더링 스타일
/// </summary>
public class LayerRenderStyle
{
    /// <summary>
    /// 채움 색상
    /// </summary>
    public Color FillColor { get; set; } = Colors.Blue;
    
    /// <summary>
    /// 외곽선 색상
    /// </summary>
    public Color StrokeColor { get; set; } = Colors.Black;
    
    /// <summary>
    /// 외곽선 두께
    /// </summary>
    public double StrokeWidth { get; set; } = 1.0;
    
    /// <summary>
    /// 투명도 (0.0 ~ 1.0)
    /// </summary>
    public double Opacity { get; set; } = 1.0;
    
    /// <summary>
    /// 포인트 크기
    /// </summary>
    public double PointSize { get; set; } = 8.0;
    
    /// <summary>
    /// 채움 활성화
    /// </summary>
    public bool EnableFill { get; set; } = true;
    
    /// <summary>
    /// 외곽선 활성화
    /// </summary>
    public bool EnableStroke { get; set; } = true;
    
    /// <summary>
    /// 라인 대시 패턴
    /// </summary>
    public double[]? DashPattern { get; set; }
    
    /// <summary>
    /// 포인트 심볼 타입
    /// </summary>
    public PointSymbolType SymbolType { get; set; } = PointSymbolType.Circle;
}

/// <summary>
/// 포인트 심볼 타입
/// </summary>
public enum PointSymbolType
{
    Circle,
    Square,
    Triangle,
    Diamond,
    Cross,
    Star
}