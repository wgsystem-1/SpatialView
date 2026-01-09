using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Engine.Map;

/// <summary>
/// 맵 캔버스 인터페이스
/// </summary>
public interface IMapCanvas
{
    /// <summary>
    /// 현재 뷰 범위
    /// </summary>
    Envelope ViewExtent { get; set; }
    
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    double ZoomLevel { get; set; }
    
    /// <summary>
    /// SRID
    /// </summary>
    int SRID { get; set; }
    
    /// <summary>
    /// 레이어 컬렉션
    /// </summary>
    ILayerCollection Layers { get; }
    
    /// <summary>
    /// 맵 새로고침
    /// </summary>
    void Refresh();
    
    /// <summary>
    /// 특정 범위로 이동
    /// </summary>
    void ZoomToExtent(Envelope extent);
    
    /// <summary>
    /// 특정 좌표로 이동
    /// </summary>
    void PanTo(Coordinate center);
    
    /// <summary>
    /// 화면 좌표를 맵 좌표로 변환
    /// </summary>
    Coordinate ScreenToMap(double screenX, double screenY);
    
    /// <summary>
    /// 맵 좌표를 화면 좌표로 변환
    /// </summary>
    System.Drawing.Point MapToScreen(Coordinate mapCoordinate);
}