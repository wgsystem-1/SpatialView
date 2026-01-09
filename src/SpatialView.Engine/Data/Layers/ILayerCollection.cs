namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 레이어 컬렉션 인터페이스
/// </summary>
public interface ILayerCollection : IList<ILayer>
{
    /// <summary>
    /// 레이어 이름으로 검색
    /// </summary>
    ILayer? this[string name] { get; }
    
    /// <summary>
    /// 특정 타입의 레이어들 가져오기
    /// </summary>
    IEnumerable<T> GetLayersOfType<T>() where T : ILayer;
    
    /// <summary>
    /// 보이는 레이어들만 가져오기
    /// </summary>
    IEnumerable<ILayer> GetVisibleLayers();
    
    /// <summary>
    /// Z-순서로 정렬된 레이어들 가져오기
    /// </summary>
    IEnumerable<ILayer> GetLayersByZOrder();
    
    /// <summary>
    /// 레이어 순서 변경
    /// </summary>
    void MoveLayer(ILayer layer, int newIndex);
    
    /// <summary>
    /// 레이어 Z-인덱스 설정
    /// </summary>
    void SetZIndex(ILayer layer, int zIndex);
    
    /// <summary>
    /// 모든 레이어의 전체 영역
    /// </summary>
    Geometry.Envelope? TotalExtent { get; }
    
    /// <summary>
    /// 레이어 컬렉션 변경 이벤트
    /// </summary>
    event EventHandler<LayerCollectionChangedEventArgs> CollectionChanged;
}

/// <summary>
/// 레이어 컬렉션 변경 이벤트 인수
/// </summary>
public class LayerCollectionChangedEventArgs : EventArgs
{
    public LayerChangeType ChangeType { get; }
    public ILayer? Layer { get; }
    public int Index { get; }
    
    public LayerCollectionChangedEventArgs(LayerChangeType changeType, ILayer? layer, int index = -1)
    {
        ChangeType = changeType;
        Layer = layer;
        Index = index;
    }
}

/// <summary>
/// 레이어 변경 타입
/// </summary>
public enum LayerChangeType
{
    Added,
    Removed,
    Moved,
    ZIndexChanged
}