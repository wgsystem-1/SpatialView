using System.Collections;

namespace SpatialView.Engine.Data.Layers;

/// <summary>
/// 레이어 컬렉션 구현
/// </summary>
public class LayerCollection : ILayerCollection
{
    private readonly List<ILayer> _layers = new List<ILayer>();
    
    /// <inheritdoc/>
    public event EventHandler<LayerCollectionChangedEventArgs>? CollectionChanged;
    
    /// <summary>
    /// 레이어 추가 이벤트
    /// </summary>
    public event EventHandler<ILayer>? LayerAdded;
    
    /// <summary>
    /// 레이어 제거 이벤트
    /// </summary>
    public event EventHandler<ILayer>? LayerRemoved;
    
    /// <inheritdoc/>
    public ILayer this[int index]
    {
        get => _layers[index];
        set
        {
            var oldLayer = _layers[index];
            _layers[index] = value;
            OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Removed, oldLayer, index));
            OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Added, value, index));
        }
    }
    
    /// <inheritdoc/>
    public ILayer? this[string name]
    {
        get => _layers.FirstOrDefault(l => l.Name == name);
    }
    
    /// <inheritdoc/>
    public int Count => _layers.Count;
    
    /// <inheritdoc/>
    public bool IsReadOnly => false;
    
    /// <inheritdoc/>
    public Geometry.Envelope? TotalExtent
    {
        get
        {
            if (_layers.Count == 0) return null;
            
            var totalExtent = new Geometry.Envelope();
            foreach (var layer in _layers.Where(l => l.Visible))
            {
                if (layer.Extent != null)
                {
                    totalExtent.ExpandToInclude(layer.Extent);
                }
            }
            
            return totalExtent.IsNull ? null : totalExtent;
        }
    }
    
    /// <inheritdoc/>
    public void Add(ILayer item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        _layers.Add(item);
        OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Added, item, _layers.Count - 1));
        LayerAdded?.Invoke(this, item);
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        var removedLayers = _layers.ToList();
        _layers.Clear();
        
        foreach (var layer in removedLayers)
        {
            OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Removed, layer));
            LayerRemoved?.Invoke(this, layer);
        }
    }
    
    /// <inheritdoc/>
    public bool Contains(ILayer item)
    {
        return _layers.Contains(item);
    }
    
    /// <inheritdoc/>
    public void CopyTo(ILayer[] array, int arrayIndex)
    {
        _layers.CopyTo(array, arrayIndex);
    }
    
    /// <inheritdoc/>
    public IEnumerator<ILayer> GetEnumerator()
    {
        return _layers.GetEnumerator();
    }
    
    /// <inheritdoc/>
    public int IndexOf(ILayer item)
    {
        return _layers.IndexOf(item);
    }
    
    /// <inheritdoc/>
    public void Insert(int index, ILayer item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        _layers.Insert(index, item);
        OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Added, item, index));
        LayerAdded?.Invoke(this, item);
    }
    
    /// <inheritdoc/>
    public bool Remove(ILayer item)
    {
        var index = _layers.IndexOf(item);
        if (index >= 0)
        {
            _layers.RemoveAt(index);
            OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Removed, item, index));
            LayerRemoved?.Invoke(this, item);
            return true;
        }
        return false;
    }
    
    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        var item = _layers[index];
        _layers.RemoveAt(index);
        OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Removed, item, index));
        LayerRemoved?.Invoke(this, item);
    }
    
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    /// <inheritdoc/>
    public IEnumerable<T> GetLayersOfType<T>() where T : ILayer
    {
        return _layers.OfType<T>();
    }
    
    /// <inheritdoc/>
    public IEnumerable<ILayer> GetVisibleLayers()
    {
        return _layers.Where(l => l.Visible);
    }
    
    /// <inheritdoc/>
    public IEnumerable<ILayer> GetLayersByZOrder()
    {
        return _layers.OrderBy(l => l.ZIndex);
    }
    
    /// <inheritdoc/>
    public void MoveLayer(ILayer layer, int newIndex)
    {
        if (layer == null) throw new ArgumentNullException(nameof(layer));
        
        var currentIndex = _layers.IndexOf(layer);
        if (currentIndex == -1) throw new ArgumentException("Layer not found in collection");
        if (newIndex < 0 || newIndex >= _layers.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        
        if (currentIndex != newIndex)
        {
            _layers.RemoveAt(currentIndex);
            _layers.Insert(newIndex, layer);
            OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.Moved, layer, newIndex));
        }
    }
    
    /// <inheritdoc/>
    public void SetZIndex(ILayer layer, int zIndex)
    {
        if (layer == null) throw new ArgumentNullException(nameof(layer));
        
        if (_layers.Contains(layer))
        {
            layer.ZIndex = zIndex;
            OnCollectionChanged(new LayerCollectionChangedEventArgs(LayerChangeType.ZIndexChanged, layer));
        }
    }
    
    /// <summary>
    /// 이벤트 발생
    /// </summary>
    protected virtual void OnCollectionChanged(LayerCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }
}