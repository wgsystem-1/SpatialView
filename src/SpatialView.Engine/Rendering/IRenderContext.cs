using System.Drawing;
using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// 렌더링 컨텍스트 인터페이스
/// </summary>
public interface IRenderContext
{
    /// <summary>
    /// 배경색으로 화면 지우기
    /// </summary>
    void Clear(Color color);
    
    /// <summary>
    /// 현재 보이는 영역
    /// </summary>
    Envelope ViewExtent { get; }
    
    /// <summary>
    /// 화면 크기
    /// </summary>
    Size ScreenSize { get; }
    
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    double Zoom { get; }
    
    /// <summary>
    /// 좌표계 SRID
    /// </summary>
    int SRID { get; }
    
    /// <summary>
    /// 맵 좌표를 화면 좌표로 변환
    /// </summary>
    System.Drawing.Point MapToScreen(ICoordinate coordinate);
    
    /// <summary>
    /// 화면 좌표를 맵 좌표로 변환
    /// </summary>
    ICoordinate ScreenToMap(System.Drawing.Point point);
    
    /// <summary>
    /// 현재 뷰포트와 지오메트리가 교차하는지 확인
    /// </summary>
    bool IsVisible(IGeometry geometry);
}