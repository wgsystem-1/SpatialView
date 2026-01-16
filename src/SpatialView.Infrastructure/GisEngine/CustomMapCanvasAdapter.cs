using System.Windows;
using System.Windows.Media;
using SpatialView.Core.GisEngine;
using CoreVectorStyle = SpatialView.Core.Styling.IVectorStyle;
using SpatialView.Engine.Rendering;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// CustomMapCanvas를 Core의 IMapCanvas로 감싸는 어댑터
/// SharpMapCanvas를 대체하여 Engine과 Core를 연결
/// </summary>
public class CustomMapCanvasAdapter : Core.GisEngine.IMapCanvas
{
    private readonly CustomMapCanvas _customCanvas;

    public CustomMapCanvasAdapter(Size size)
    {
        _customCanvas = new CustomMapCanvas();
        _customCanvas.Size = size;
    }

    public CustomMapCanvasAdapter(CustomMapCanvas customCanvas)
    {
        _customCanvas = customCanvas ?? throw new ArgumentNullException(nameof(customCanvas));
    }

    public Size Size
    {
        get => _customCanvas.Size;
        set => _customCanvas.Size = value;
    }

    public double Zoom
    {
        get => _customCanvas.Zoom;
        set => _customCanvas.Zoom = value;
    }

    public SpatialView.Engine.Geometry.ICoordinate Center
    {
        get => _customCanvas.Center;
        set => _customCanvas.Center = value;
    }

    public SpatialView.Engine.Geometry.Envelope ViewExtent => _customCanvas.ViewExtent;

    public int SRID
    {
        get => _customCanvas.SRID;
        set => _customCanvas.SRID = value;
    }

    public ILayerCollection Layers => new EngineLayerCollectionAdapter(_customCanvas.Layers);

    public Color BackgroundColor
    {
        get => _customCanvas.BackgroundColor;
        set => _customCanvas.BackgroundColor = value;
    }

    public Core.GisEngine.RenderingQuality RenderingQuality
    {
        get => (Core.GisEngine.RenderingQuality)_customCanvas.RenderingQuality;
        set => _customCanvas.RenderingQuality = (Engine.Rendering.RenderingQuality)value;
    }

    public bool AntiAliasing
    {
        get => _customCanvas.AntiAliasing;
        set => _customCanvas.AntiAliasing = value;
    }

    public void ZoomToExtent(SpatialView.Engine.Geometry.Envelope extent)
    {
        _customCanvas.ZoomToExtent(extent);
    }

    public void ZoomToPoint(SpatialView.Engine.Geometry.ICoordinate center, double zoom)
    {
        _customCanvas.ZoomToPoint(center, zoom);
    }

    public SpatialView.Engine.Geometry.ICoordinate ScreenToMap(Point screenPoint)
    {
        return _customCanvas.ScreenToMap(screenPoint);
    }

    public Point MapToScreen(SpatialView.Engine.Geometry.ICoordinate mapPoint)
    {
        return _customCanvas.MapToScreen(mapPoint);
    }

    public void Render()
    {
        _customCanvas.Render();
    }

    public ImageSource ExportToImage()
    {
        return _customCanvas.ExportToImage();
    }

    public void Refresh()
    {
        _customCanvas.Refresh();
    }

    public event EventHandler<Core.GisEngine.ViewportChangedEventArgs>? ViewportChanged
    {
        add => _customCanvas.ViewportChanged += ConvertViewportHandler(value);
        remove => _customCanvas.ViewportChanged -= ConvertViewportHandler(value);
    }

    public event EventHandler? RenderCompleted
    {
        add => _customCanvas.RenderCompleted += value;
        remove => _customCanvas.RenderCompleted -= value;
    }

    /// <summary>
    /// 내부 CustomMapCanvas 접근 (WPF UI에서 사용)
    /// </summary>
    public CustomMapCanvas InternalCanvas => _customCanvas;

    private EventHandler<Engine.Rendering.ViewportChangedEventArgs>? ConvertViewportHandler(EventHandler<Core.GisEngine.ViewportChangedEventArgs>? handler)
    {
        if (handler == null) return null;
        
        return (sender, e) =>
        {
            var coreEventArgs = new Core.GisEngine.ViewportChangedEventArgs(
                e.OldExtent,
                e.NewExtent,
                e.OldZoom,
                e.NewZoom
            );
            handler(sender, coreEventArgs);
        };
    }
}

/// <summary>
/// Engine의 ILayerCollection을 Core의 ILayerCollection으로 어댑터
/// </summary>
internal class EngineLayerCollectionAdapter : ILayerCollection
{
    private readonly SpatialView.Engine.Data.Layers.ILayerCollection _engineLayers;

    public EngineLayerCollectionAdapter(SpatialView.Engine.Data.Layers.ILayerCollection engineLayers)
    {
        _engineLayers = engineLayers;
        _engineLayers.CollectionChanged += OnEngineLayersChanged;
    }

    public int Count => _engineLayers.Count;

    public bool IsReadOnly => false;

    public Core.GisEngine.ILayer this[int index] => WrapLayer(_engineLayers[index]);

    public Core.GisEngine.ILayer? this[string name]
    {
        get
        {
            var engineLayer = _engineLayers[name];
            return engineLayer != null ? WrapLayer(engineLayer) : null;
        }
    }

    public void Add(Core.GisEngine.ILayer layer)
    {
        if (layer is LayerAdapter adapter)
        {
            System.Diagnostics.Debug.WriteLine($"[EngineLayerCollectionAdapter.Add] LayerAdapter: {adapter.Name}, Extent={adapter.Extent}");
            _engineLayers.Add(adapter.InternalLayer);
        }
        else if (layer is SpatialViewVectorLayerAdapter sva)
        {
            System.Diagnostics.Debug.WriteLine($"[EngineLayerCollectionAdapter.Add] SpatialViewVectorLayerAdapter: {sva.Name}, Extent={sva.Extent}, InternalLayer.Extent={sva.InternalLayer.Extent}");
            _engineLayers.Add(sva.InternalLayer);
        }
        else
        {
            throw new ArgumentException("Layer must be wrapped in LayerAdapter");
        }
        System.Diagnostics.Debug.WriteLine($"[EngineLayerCollectionAdapter.Add] 총 레이어 수: {_engineLayers.Count}");
    }

    public bool Remove(Core.GisEngine.ILayer layer)
    {
        if (layer is LayerAdapter adapter)
        {
            return _engineLayers.Remove(adapter.InternalLayer);
        }
        if (layer is SpatialViewVectorLayerAdapter sva)
        {
            return _engineLayers.Remove(sva.InternalLayer);
        }
        return false;
    }

    public void Clear()
    {
        _engineLayers.Clear();
    }

    public bool Contains(Core.GisEngine.ILayer item)
    {
        if (item is LayerAdapter adapter)
        {
            return _engineLayers.Contains(adapter.InternalLayer);
        }
        if (item is SpatialViewVectorLayerAdapter sva)
        {
            return _engineLayers.Contains(sva.InternalLayer);
        }
        return false;
    }

    public void CopyTo(Core.GisEngine.ILayer[] array, int arrayIndex)
    {
        var adapters = _engineLayers.Select(layer => new LayerAdapter(layer)).ToArray();
        adapters.CopyTo(array, arrayIndex);
    }

    public IEnumerable<T> GetLayersOfType<T>() where T : Core.GisEngine.ILayer
    {
        return this.OfType<T>();
    }

    public IEnumerable<Core.GisEngine.ILayer> GetVisibleLayers()
    {
        return this.Where(layer => layer.Visible);
    }

    public IEnumerable<Core.GisEngine.ILayer> GetLayersByZOrder()
    {
        return this.OrderBy(layer => layer.ZOrder);
    }

    public void MoveLayer(Core.GisEngine.ILayer layer, int newIndex)
    {
        if (layer is LayerAdapter adapter)
        {
            _engineLayers.MoveLayer(adapter.InternalLayer, newIndex);
        }
        else if (layer is SpatialViewVectorLayerAdapter sva)
        {
            _engineLayers.MoveLayer(sva.InternalLayer, newIndex);
        }
    }

    public void SetZIndex(Core.GisEngine.ILayer layer, int zIndex)
    {
        if (layer is LayerAdapter adapter)
        {
            _engineLayers.SetZIndex(adapter.InternalLayer, zIndex);
        }
        else if (layer is SpatialViewVectorLayerAdapter sva)
        {
            _engineLayers.SetZIndex(sva.InternalLayer, zIndex);
        }
    }

    public SpatialView.Engine.Geometry.Envelope? TotalExtent => _engineLayers.TotalExtent;

    public event EventHandler<Core.GisEngine.LayerCollectionChangedEventArgs>? CollectionChanged;

    private void OnEngineLayersChanged(object? sender, SpatialView.Engine.Data.Layers.LayerCollectionChangedEventArgs e)
    {
        var args = new Core.GisEngine.LayerCollectionChangedEventArgs(
            (Core.GisEngine.LayerChangeType)(int)e.ChangeType,
            e.Layer != null ? WrapLayer(e.Layer) : null,
            e.Index
        );
        CollectionChanged?.Invoke(this, args);
    }

    public IEnumerator<Core.GisEngine.ILayer> GetEnumerator()
    {
        return _engineLayers.Select(layer => WrapLayer(layer)).GetEnumerator();
    }

    private Core.GisEngine.ILayer WrapLayer(SpatialView.Engine.Data.Layers.ILayer engineLayer)
    {
        if (engineLayer is SpatialView.Engine.Data.Layers.VectorLayer vectorLayer)
        {
            return new VectorLayerAdapter(vectorLayer);
        }

        return new LayerAdapter(engineLayer);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Engine의 ILayer를 Core의 ILayer로 어댑터
/// </summary>
internal class LayerAdapter : Core.GisEngine.ILayer
{
    protected readonly SpatialView.Engine.Data.Layers.ILayer EngineLayer;

    public LayerAdapter(SpatialView.Engine.Data.Layers.ILayer engineLayer)
    {
        EngineLayer = engineLayer;
    }

    public string Name
    {
        get => EngineLayer.Name;
        set => EngineLayer.Name = value;
    }

    public bool Visible
    {
        get => EngineLayer.Visible;
        set => EngineLayer.Visible = value;
    }

    public double Opacity
    {
        get => EngineLayer.Opacity;
        set => EngineLayer.Opacity = value;
    }

    public int ZOrder
    {
        get => EngineLayer.ZIndex;
        set => EngineLayer.ZIndex = value;
    }

    public double MinimumZoom
    {
        get => EngineLayer.MinimumZoom;
        set => EngineLayer.MinimumZoom = value;
    }

    public double MaximumZoom
    {
        get => EngineLayer.MaximumZoom;
        set => EngineLayer.MaximumZoom = value;
    }

    public SpatialView.Engine.Geometry.Envelope? Extent => EngineLayer.Extent;

    public void Refresh()
    {
        EngineLayer.Refresh();
    }

    internal SpatialView.Engine.Data.Layers.ILayer InternalLayer => EngineLayer;
}

/// <summary>
/// Engine의 VectorLayer를 Core의 IVectorLayer로 어댑터
/// </summary>
internal class VectorLayerAdapter : LayerAdapter, Core.GisEngine.IVectorLayer
{
    private readonly SpatialView.Engine.Data.Layers.VectorLayer _vectorLayer;
    private IFeatureSource? _dataSource;
    private CoreVectorStyle? _style;

    public VectorLayerAdapter(SpatialView.Engine.Data.Layers.VectorLayer engineLayer)
        : base(engineLayer)
    {
        _vectorLayer = engineLayer;
    }

    public bool Enabled
    {
        get => _vectorLayer.Enabled;
        set => _vectorLayer.Enabled = value;
    }

    public int SRID
    {
        get => _vectorLayer.SRID;
        set => _vectorLayer.SRID = value;
    }

    public LayerType LayerType => LayerType.Vector;

    public IFeatureSource? DataSource
    {
        get
        {
            if (_dataSource == null && _vectorLayer.DataSource != null && !string.IsNullOrEmpty(_vectorLayer.TableName))
            {
                _dataSource = new EngineDataSourceFeatureSourceAdapter(_vectorLayer.DataSource, _vectorLayer.TableName);
            }

            return _dataSource;
        }
        set => _dataSource = value;
    }

    public CoreVectorStyle? Style
    {
        get => _style;
        set => _style = value;
    }

    public IDataProvider? Provider { get; set; }
}