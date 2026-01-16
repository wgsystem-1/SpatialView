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

    private IDataProvider? _provider;
    public IDataProvider? Provider
    {
        get => _provider;
        set
        {
            _provider = value;
            // 필요 시 엔진 DataSource와 연동 가능 (현재는 로깅만)
            if (_provider != null)
            {
                System.Diagnostics.Debug.WriteLine($"SpatialViewVectorLayerAdapter: Provider 설정됨 ({_provider.GetType().Name})");
            }
        }
    }

    /// <summary>엔진 레이어 (어댑터 내부용)</summary>
    internal VectorLayer InternalLayer => _engineLayer;
    
    /// <summary>
    /// 엔진 레이어 반환 (외부에서 접근 가능)
    /// </summary>
    public VectorLayer GetEngineLayer() => _engineLayer;
    
    /// <summary>
    /// Core 스타일을 Engine 스타일로 동기화
    /// </summary>
    private void SyncStyleToEngine()
    {
        if (_style == null) return;
        
        // 색상 계산
        var fillColor = _style.Fill;
        if (_style.FillColor.HasValue)
        {
            var fc = _style.FillColor.Value;
            fillColor = Color.FromArgb(fc.A, fc.R, fc.G, fc.B);
        }
        
        var strokeColor = _style.Outline;
        if (_style.LineColor.HasValue)
        {
            var lc = _style.LineColor.Value;
            strokeColor = Color.FromArgb(lc.A, lc.R, lc.G, lc.B);
        }
        
        // 포인트 전용 색상 우선
        var pointColor = fillColor;
        if (_style.PointColor.HasValue)
        {
            var pc = _style.PointColor.Value;
            pointColor = Color.FromArgb(pc.A, pc.R, pc.G, pc.B);
        }
        
        // 선 두께 계산 (LineWidth 우선, 없으면 OutlineWidth)
        var strokeWidth = _style.LineWidth > 0 ? _style.LineWidth : _style.OutlineWidth;
        var enableStroke = _style.EnableOutline;
        if (strokeWidth <= 0)
        {
            strokeWidth = 0;
            enableStroke = false;
            strokeColor = Colors.Transparent;
        }
        
        // 채움 사용 여부: 인터페이스에 별도 플래그 없음 → 기본 true
        var enableFill = true;
        
        // 심볼 사용 여부 판단
        var preferPointStyle =
            _style.RenderAsSymbol ||
            _style.SymbolSize > 0 ||
            _style.PointColor.HasValue ||
            _style.SymbolType != SymbolType.Circle; // Circle 이외면 심볼 지정으로 간주
        
        // 1) PointStyle 우선 매핑 (심볼 모드/점 전용)
        if (preferPointStyle)
        {
            var enginePointStyle = new PointStyle
            {
                Name = "Layer Style",
                Fill = enableFill ? pointColor : Colors.Transparent,
                Stroke = enableStroke ? strokeColor : Colors.Transparent,
                StrokeWidth = strokeWidth,
                Size = _style.SymbolSize > 0 ? _style.SymbolSize : 8.0,
                Shape = ConvertToEnginePointShape(_style.SymbolType),
                IsVisible = true,
                MinZoom = 0,
                MaxZoom = double.MaxValue
            };
            
            _engineLayer.Style = enginePointStyle;
            System.Diagnostics.Debug.WriteLine($"SyncStyleToEngine(Point): Layer={_engineLayer.Name}, Fill={enginePointStyle.Fill}, Stroke={enginePointStyle.Stroke}, Size={enginePointStyle.Size}, Shape={enginePointStyle.Shape}");
            return;
        }
        
        // 2) LineStyle 매핑 (채움이 꺼져 있고 외곽선만 있는 경우)
        if (!enableFill && enableStroke)
        {
            var engineLineStyle = new DefaultLineStyle
            {
                Name = "Layer Style",
                Stroke = strokeColor,
                StrokeWidth = strokeWidth,
                IsVisible = true,
                MinZoom = 0,
                MaxZoom = double.MaxValue,
                DashArray = _style.LineStyle == Core.Styling.LineStyle.Dot ? new[] { 2.0, 2.0 } :
                             _style.LineStyle == Core.Styling.LineStyle.Dash ? new[] { 6.0, 3.0 } :
                             _style.LineStyle == Core.Styling.LineStyle.DashDot ? new[] { 6.0, 3.0, 2.0, 3.0 } :
                             _style.LineStyle == Core.Styling.LineStyle.DashDotDot ? new[] { 6.0, 3.0, 2.0, 3.0, 2.0, 3.0 } :
                             null
            };
            
            _engineLayer.Style = engineLineStyle;
            System.Diagnostics.Debug.WriteLine($"SyncStyleToEngine(Line): Layer={_engineLayer.Name}, Stroke={engineLineStyle.Stroke}, StrokeWidth={engineLineStyle.StrokeWidth}");
            return;
        }
        
        // 3) PolygonStyle 기본 매핑
        var enginePolyStyle = new PolygonStyle
        {
            Name = "Layer Style",
            Fill = enableFill ? fillColor : Colors.Transparent,
            Stroke = enableStroke ? strokeColor : Colors.Transparent,
            StrokeWidth = strokeWidth,
            Opacity = 1.0,
            IsVisible = true,
            MinZoom = 0,
            MaxZoom = double.MaxValue
        };
        
        _engineLayer.Style = enginePolyStyle;
        System.Diagnostics.Debug.WriteLine($"SyncStyleToEngine(Polygon): Layer={_engineLayer.Name}, Fill={enginePolyStyle.Fill}, Stroke={enginePolyStyle.Stroke}, StrokeWidth={enginePolyStyle.StrokeWidth}");
    }
    
    private static PointShape ConvertToEnginePointShape(SymbolType symbolType)
    {
        return symbolType switch
        {
            SymbolType.Square => PointShape.Square,
            SymbolType.Triangle => PointShape.Triangle,
            SymbolType.Diamond => PointShape.Diamond,
            SymbolType.Cross => PointShape.Cross,
            SymbolType.X => PointShape.X,
            SymbolType.Star => PointShape.Circle, // 별 모양은 없음 -> Circle 대체
            _ => PointShape.Circle
        };
    }
}