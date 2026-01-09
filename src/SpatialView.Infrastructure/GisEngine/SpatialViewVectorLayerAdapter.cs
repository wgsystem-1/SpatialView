using SpatialView.Core.Enums;
using SpatialView.Core.GisEngine;
using SpatialView.Core.Styling;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Geometry;
using SpatialView.Engine.Styling;
using System.Windows.Media;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// SpatialView.Engine.VectorLayer를 Core.IVectorLayer로 어댑터
/// </summary>
public class SpatialViewVectorLayerAdapter : IVectorLayer
{
    private readonly VectorLayer _engineLayer;
    private Core.Styling.IVectorStyle? _style;

    public SpatialViewVectorLayerAdapter(VectorLayer engineLayer)
    {
        _engineLayer = engineLayer ?? throw new ArgumentNullException(nameof(engineLayer));
        _style = new SpatialViewVectorStyle(); // 기본 스타일
        SyncStyleToEngine();
    }

    // ILayer 속성들
    public string Name
    {
        get => _engineLayer.Name;
        set => _engineLayer.Name = value;
    }

    public bool Visible
    {
        get => _engineLayer.Visible;
        set => _engineLayer.Visible = value;
    }

    public double Opacity
    {
        get => _engineLayer.Opacity;
        set
        {
            _engineLayer.Opacity = value;
            // 스타일에도 투명도 반영
            if (_style != null)
            {
                _style.Opacity = (float)value;
            }
            // Engine 스타일에도 동기화
            SyncStyleToEngine();
        }
    }

    public int ZOrder
    {
        get => _engineLayer.ZIndex;
        set => _engineLayer.ZIndex = value;
    }

    public double MinimumZoom
    {
        get => _engineLayer.MinimumZoom;
        set => _engineLayer.MinimumZoom = value;
    }

    public double MaximumZoom
    {
        get => _engineLayer.MaximumZoom;
        set => _engineLayer.MaximumZoom = value;
    }

    public Envelope? Extent => _engineLayer.Extent;

    public void Refresh()
    {
        _engineLayer.Refresh();
    }

    // IMapLayer 속성들
    public bool Enabled
    {
        get => _engineLayer.Enabled;
        set => _engineLayer.Enabled = value;
    }

    public int SRID
    {
        get => _engineLayer.SRID;
        set => _engineLayer.SRID = value;
    }

    public LayerType LayerType => LayerType.Vector;

    // IVectorLayer 속성들
    public IFeatureSource? DataSource { get; set; }

    public Core.Styling.IVectorStyle? Style
    {
        get => _style;
        set
        {
            _style = value;
            SyncStyleToEngine();
        }
    }

    public IDataProvider? Provider { get; set; }

    /// <summary>엔진 레이어 (어댑터 내부용)</summary>
    internal VectorLayer InternalLayer => _engineLayer;
    
    /// <summary>
    /// Core 스타일을 Engine 스타일로 동기화
    /// </summary>
    private void SyncStyleToEngine()
    {
        if (_style == null) return;
        
        // IVectorStyle을 Engine의 IPolygonStyle로 변환 (가장 일반적인 케이스)
        var engineStyle = new PolygonStyle
        {
            Name = "Layer Style",
            Fill = _style.Fill,
            Stroke = _style.Outline,
            StrokeWidth = _style.OutlineWidth,
            Opacity = _style.Opacity,
            IsVisible = true,
            MinZoom = 0,
            MaxZoom = double.MaxValue
        };
        
        _engineLayer.Style = engineStyle;
    }
}