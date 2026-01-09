namespace SpatialView.Core.GisEngine;

/// <summary>
/// 레이어 인터페이스
/// Core 레이어에서 Engine 레이어에 대한 추상화
/// </summary>
public interface ILayer
{
    /// <summary>
    /// 레이어 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 레이어 표시 여부
    /// </summary>
    bool Visible { get; set; }
    
    /// <summary>
    /// 레이어 투명도 (0.0 ~ 1.0)
    /// </summary>
    double Opacity { get; set; }
    
    /// <summary>
    /// 레이어 Z-순서
    /// </summary>
    int ZOrder { get; set; }
    
    /// <summary>
    /// 레이어의 최소 표시 배율
    /// </summary>
    double MinimumZoom { get; set; }
    
    /// <summary>
    /// 레이어의 최대 표시 배율
    /// </summary>
    double MaximumZoom { get; set; }
    
    /// <summary>
    /// 레이어의 전체 영역
    /// </summary>
    Engine.Geometry.Envelope? Extent { get; }
    
    /// <summary>
    /// 레이어 새로고침
    /// </summary>
    void Refresh();
}