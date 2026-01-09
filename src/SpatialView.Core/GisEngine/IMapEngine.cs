using System.Windows;
using SpatialView.Engine.Geometry;

namespace SpatialView.Core.GisEngine;

/// <summary>
/// 지도 엔진 추상 인터페이스
/// SharpMap 등 구체적인 구현으로부터 독립적인 지도 기능 정의
/// </summary>
public interface IMapEngine
{
    /// <summary>
    /// 레이어 컬렉션
    /// </summary>
    ILayerCollection Layers { get; }
    
    /// <summary>
    /// 지도 중심 좌표
    /// </summary>
    ICoordinate Center { get; set; }
    
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    double Zoom { get; set; }
    
    /// <summary>
    /// 현재 보이는 지도 영역
    /// </summary>
    Envelope ViewExtent { get; }
    
    /// <summary>
    /// 좌표계 SRID
    /// </summary>
    int SRID { get; set; }
    
    /// <summary>
    /// 지도 크기 (픽셀)
    /// </summary>
    System.Windows.Size Size { get; set; }
    
    /// <summary>
    /// 배경색
    /// </summary>
    System.Windows.Media.Color BackgroundColor { get; set; }
    
    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    double MinimumZoom { get; set; }
    
    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    double MaximumZoom { get; set; }
    
    /// <summary>
    /// 픽셀 단위 확대/축소 비율
    /// </summary>
    int PixelsPerUnit { get; }
    
    /// <summary>
    /// 특정 영역으로 줌
    /// </summary>
    void ZoomToExtent(Envelope extent);
    
    /// <summary>
    /// 모든 레이어가 보이도록 줌
    /// </summary>
    void ZoomToExtents();
    
    /// <summary>
    /// 지도 새로고침
    /// </summary>
    void Refresh();
    
    /// <summary>
    /// 화면 좌표를 지도 좌표로 변환
    /// </summary>
    ICoordinate ScreenToMap(System.Windows.Point screenPoint);
    
    /// <summary>
    /// 지도 좌표를 화면 좌표로 변환
    /// </summary>
    System.Windows.Point MapToScreen(ICoordinate mapPoint);
    
    /// <summary>
    /// 지도 렌더링을 이미지로 가져오기
    /// </summary>
    System.Drawing.Image GetMap();
    
    /// <summary>
    /// 지도 변경 이벤트
    /// </summary>
    event EventHandler? MapChanged;
}