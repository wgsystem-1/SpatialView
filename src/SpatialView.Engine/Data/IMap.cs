using System.Collections.Generic;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Engine.Data;

/// <summary>
/// 맵 인터페이스
/// </summary>
public interface IMap
{
    /// <summary>
    /// 맵 이름
    /// </summary>
    string Name { get; set; }
    
    /// <summary>
    /// 레이어 컬렉션
    /// </summary>
    IList<ILayer> Layers { get; }
    
    /// <summary>
    /// 현재 뷰 영역
    /// </summary>
    Envelope ViewExtent { get; set; }
    
    /// <summary>
    /// 전체 맵 영역
    /// </summary>
    Envelope GetExtent();
    
    /// <summary>
    /// 특정 영역으로 확대/축소
    /// </summary>
    void ZoomToExtent(Envelope extent);
    
    /// <summary>
    /// 레이어 추가
    /// </summary>
    void AddLayer(ILayer layer);
    
    /// <summary>
    /// 레이어 제거
    /// </summary>
    bool RemoveLayer(ILayer layer);
    
    /// <summary>
    /// 이름으로 레이어 찾기
    /// </summary>
    ILayer? GetLayerByName(string name);
    
    /// <summary>
    /// 맵 새로고침
    /// </summary>
    void Refresh();
}