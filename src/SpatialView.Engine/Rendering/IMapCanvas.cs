using System.Windows;
using System.Windows.Media;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// 지도 캔버스 인터페이스
/// 지도 렌더링과 상호작용의 핵심 인터페이스
/// </summary>
public interface IMapCanvas
{
    /// <summary>
    /// 캔버스 크기 (픽셀)
    /// </summary>
    Size Size { get; set; }
    
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    double Zoom { get; set; }
    
    /// <summary>
    /// 지도 중심점 좌표
    /// </summary>
    Geometry.ICoordinate Center { get; set; }
    
    /// <summary>
    /// 현재 보이는 영역 (지리좌표)
    /// </summary>
    Geometry.Envelope ViewExtent { get; }
    
    /// <summary>
    /// 지도의 좌표계 SRID
    /// </summary>
    int SRID { get; set; }
    
    /// <summary>
    /// 배경색
    /// </summary>
    Color BackgroundColor { get; set; }
    
    /// <summary>
    /// 레이어 컬렉션
    /// </summary>
    Data.Layers.ILayerCollection Layers { get; }
    
    /// <summary>
    /// 렌더링 품질
    /// </summary>
    RenderingQuality RenderingQuality { get; set; }
    
    /// <summary>
    /// 안티앨리어싱 사용 여부
    /// </summary>
    bool AntiAliasing { get; set; }
    
    /// <summary>
    /// 특정 영역으로 줌
    /// </summary>
    void ZoomToExtent(Geometry.Envelope extent);
    
    /// <summary>
    /// 특정 점과 줌 레벨로 이동
    /// </summary>
    void ZoomToPoint(Geometry.ICoordinate center, double zoom);
    
    /// <summary>
    /// 화면 좌표를 지리 좌표로 변환
    /// </summary>
    Geometry.ICoordinate ScreenToMap(Point screenPoint);
    
    /// <summary>
    /// 지리 좌표를 화면 좌표로 변환
    /// </summary>
    Point MapToScreen(Geometry.ICoordinate mapPoint);
    
    /// <summary>
    /// 지도 렌더링
    /// </summary>
    void Render();
    
    /// <summary>
    /// 렌더링 결과를 이미지로 내보내기
    /// </summary>
    ImageSource ExportToImage();
    
    /// <summary>
    /// 지도 새로고침
    /// </summary>
    void Refresh();
    
    /// <summary>
    /// 뷰포트 변경 이벤트
    /// </summary>
    event EventHandler<ViewportChangedEventArgs> ViewportChanged;
    
    /// <summary>
    /// 렌더링 완료 이벤트
    /// </summary>
    event EventHandler RenderCompleted;
}

/// <summary>
/// 렌더링 품질 설정
/// </summary>
public enum RenderingQuality
{
    /// <summary>
    /// 빠른 렌더링 (품질 낮음)
    /// </summary>
    Fast,
    
    /// <summary>
    /// 균형 잡힌 렌더링
    /// </summary>
    Balanced,
    
    /// <summary>
    /// 고품질 렌더링 (속도 느림)
    /// </summary>
    HighQuality
}

/// <summary>
/// 뷰포트 변경 이벤트 인수
/// </summary>
public class ViewportChangedEventArgs : EventArgs
{
    public Geometry.Envelope OldExtent { get; }
    public Geometry.Envelope NewExtent { get; }
    public double OldZoom { get; }
    public double NewZoom { get; }
    
    public ViewportChangedEventArgs(Geometry.Envelope oldExtent, Geometry.Envelope newExtent, 
                                   double oldZoom, double newZoom)
    {
        OldExtent = oldExtent;
        NewExtent = newExtent;
        OldZoom = oldZoom;
        NewZoom = newZoom;
    }
}