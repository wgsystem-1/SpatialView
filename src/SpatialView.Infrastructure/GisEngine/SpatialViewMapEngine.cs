using System.Windows;
using System.Windows.Media;
using SpatialView.Core.GisEngine;
using SpatialView.Engine;
using SpatialView.Engine.Geometry;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// SpatialView.Engine.Map을 Core.IMapEngine으로 변환하는 어댑터
/// </summary>
public class SpatialViewMapEngine : IMapEngine
{
    private readonly MapContainer _map;
    private readonly ILayerCollection _layersAdapter;

    public SpatialViewMapEngine()
    {
        _map = new MapContainer();
        _layersAdapter = new EngineLayerCollectionAdapter(_map.LayerCollection);
        
        // 이벤트 연결
        _map.ViewChanged += (s, e) => MapChanged?.Invoke(this, EventArgs.Empty);
    }
    
    public SpatialView.Core.GisEngine.ILayerCollection Layers => _layersAdapter;
    
    public ICoordinate Center
    {
        get => _map.Center;
        set => _map.Center = value ?? new Coordinate(0, 0);
    }
    
    public double Zoom
    {
        get => _map.Zoom;
        set => _map.Zoom = value;
    }
    
    public Envelope ViewExtent => _map.ViewExtent;
    
    public int SRID
    {
        get => _map.SRID;
        set => _map.SRID = value;
    }
    
    public Size Size
    {
        get => new Size(_map.Size.Width, _map.Size.Height);
        set => _map.Size = new System.Drawing.Size((int)value.Width, (int)value.Height);
    }
    
    public System.Windows.Media.Color BackgroundColor
    {
        get
        {
            var c = _map.BackgroundColor;
            return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        set
        {
            _map.BackgroundColor = System.Drawing.Color.FromArgb(value.A, value.R, value.G, value.B);
        }
    }
    
    public double MinimumZoom
    {
        get => _map.MinimumZoom;
        set => _map.MinimumZoom = value;
    }
    
    public double MaximumZoom
    {
        get => _map.MaximumZoom;
        set => _map.MaximumZoom = value;
    }
    
    public int PixelsPerUnit => (int)(1.0 / _map.PixelSize);
    
    public void ZoomToExtent(Envelope envelope)
    {
        _map.ZoomToExtent(envelope);
    }
    
    public void ZoomToExtents()
    {
        _map.ZoomToExtents();
    }
    
    public void Refresh()
    {
        _map.Refresh();
    }
    
    public ICoordinate ScreenToMap(System.Windows.Point screenPoint)
    {
        return _map.ScreenToWorld(
            new System.Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));
    }
    
    public System.Windows.Point MapToScreen(ICoordinate mapPoint)
    {
        var screenPoint = _map.WorldToScreen(mapPoint);
        return new System.Windows.Point(screenPoint.X, screenPoint.Y);
    }
    
    public System.Drawing.Image GetMap()
    {
        // TODO: 실제 이미지 렌더링 구현
        // 현재는 빈 이미지 반환
        return new System.Drawing.Bitmap(1, 1);
    }
    
    public event EventHandler? MapChanged;
    
    /// <summary>
    /// 내부 Map 객체 접근 (Infrastructure 내부용)
    /// </summary>
    internal MapContainer InternalMap => _map;
}