using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using SpatialView.Engine.Data.Layers;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// WPF 기반 지도 렌더러
/// </summary>
public class WpfMapRenderer : IDisposable
{
    private readonly MapContainer _map;
    private readonly Canvas _canvas;
    private readonly VectorRenderer _vectorRenderer;
    private DrawingVisual? _drawingVisual;
    private RenderTargetBitmap? _backBuffer;
    private bool _disposed;
    
    public WpfMapRenderer(MapContainer map, Canvas canvas)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _vectorRenderer = new VectorRenderer();
        
        // 이벤트 연결
        _map.ViewChanged += OnMapViewChanged;
        _map.LayersChanged += OnMapLayersChanged;
    }
    
    /// <summary>
    /// 지도 렌더링
    /// </summary>
    public void Render()
    {
        Log($"WpfMapRenderer.Render() 호출됨");
        if (_disposed) return;
        
        lock (_map)
        {
            Log($"WpfMapRenderer.Render(): Canvas 크기={_canvas.ActualWidth}x{_canvas.ActualHeight}");
            
            // 캔버스 크기 업데이트
            UpdateMapSize();
            
            // 백버퍼 생성
            CreateBackBuffer();
            
            // 렌더링 시작
            _drawingVisual = new DrawingVisual();
            using (var dc = _drawingVisual.RenderOpen())
            {
                // 배경 그리기
                RenderBackground(dc);
                
                // 레이어 렌더링
                RenderLayers(dc);
                
                // 디버그 정보 (개발 중)
                RenderDebugInfo(dc);
            }
            
            // 백버퍼에 렌더링
            if (_backBuffer != null)
            {
                _backBuffer.Clear();
                _backBuffer.Render(_drawingVisual);
                
                // 캔버스에 표시
                UpdateCanvas();
            }
        }
    }
    
    /// <summary>
    /// 캔버스 크기에 맞춰 맵 크기 업데이트
    /// </summary>
    private void UpdateMapSize()
    {
        var width = (int)Math.Max(1, _canvas.ActualWidth);
        var height = (int)Math.Max(1, _canvas.ActualHeight);
        
        if (_map.Size.Width != width || _map.Size.Height != height)
        {
            _map.Size = new System.Drawing.Size(width, height);
        }
    }
    
    /// <summary>
    /// 백버퍼 생성 또는 재생성
    /// </summary>
    private void CreateBackBuffer()
    {
        var width = (int)Math.Max(1, _canvas.ActualWidth);
        var height = (int)Math.Max(1, _canvas.ActualHeight);
        
        if (_backBuffer == null || 
            _backBuffer.PixelWidth != width || 
            _backBuffer.PixelHeight != height)
        {
            _backBuffer = new RenderTargetBitmap(
                width, height, 96, 96, PixelFormats.Pbgra32);
        }
    }
    
    /// <summary>
    /// 배경 렌더링
    /// </summary>
    private void RenderBackground(DrawingContext dc)
    {
        var bgColor = _map.BackgroundColor;
        var brush = new SolidColorBrush(
            Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
        
        dc.DrawRectangle(
            brush, null,
            new Rect(0, 0, _canvas.ActualWidth, _canvas.ActualHeight));
    }
    
    /// <summary>
    /// 레이어 렌더링
    /// </summary>
    private void RenderLayers(DrawingContext dc)
    {
        Log($"RenderLayers: 배경 레이어 수={_map.BackgroundLayers.Count}, 일반 레이어 수={_map.Layers.Count}");
        
        // 배경 레이어 렌더링
        foreach (var layer in _map.BackgroundLayers)
        {
            try
            {
                if (!layer.Enabled || !layer.Visible) continue;
                RenderLayer(dc, layer);
            }
            catch (Exception ex)
            {
                Log($"배경 레이어 '{layer.Name}' 렌더링 치명적 오류: {ex.Message}");
            }
        }
        
        // 일반 레이어 렌더링
        foreach (var layer in _map.Layers)
        {
            try
            {
                Log($"RenderLayers: 레이어 '{layer.Name}' 렌더링 시도 (Enabled={layer.Enabled}, Visible={layer.Visible})");
                if (!layer.Enabled || !layer.Visible) continue;
                RenderLayer(dc, layer);
                Log($"RenderLayers: 레이어 '{layer.Name}' 렌더링 완료");
            }
            catch (Exception ex)
            {
                Log($"일반 레이어 '{layer.Name}' 렌더링 치명적 오류: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 개별 레이어 렌더링
    /// </summary>
    private void RenderLayer(DrawingContext dc, ILayer layer)
    {
        try
        {
            // 가시성 확인
            if (!IsLayerVisible(layer))
                return;
                
            if (layer is VectorLayer vectorLayer)
            {
                RenderVectorLayer(dc, vectorLayer);
            }
            // TODO: 다른 레이어 타입 추가
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"레이어 '{layer.Name}' 렌더링 오류: {ex.Message}");
        }
    }
    
    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpatialView_render.log");
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    /// <summary>
    /// 벡터 레이어 렌더링
    /// </summary>
    private void RenderVectorLayer(DrawingContext dc, VectorLayer layer)
    {
        Log($"RenderVectorLayer: '{layer.Name}' 시작");
        try
        {
            // VectorLayer의 GetFeatures 사용 (캐시된 피처)
            var viewExtent = _map.ViewExtent;
            Log($"RenderVectorLayer: ViewExtent={viewExtent}");
            
            var features = layer.GetFeatures(viewExtent);
            var featureCount = features?.Count() ?? 0;
            Log($"RenderVectorLayer: '{layer.Name}' 피처 수={featureCount}");
            
            if (features != null && featureCount > 0)
            {
                // 레이어 스타일 생성
                var layerStyle = CreateLayerStyle(layer);
                
                // RenderContext 생성
                var renderContext = new RenderContext
                {
                    DrawingContext = dc,
                    ViewExtent = viewExtent,
                    ScreenSize = new Size(_canvas.ActualWidth, _canvas.ActualHeight),
                    Zoom = _map.Zoom,
                    SRID = _map.SRID,
                    Quality = RenderingQuality.Balanced,
                    AntiAliasing = true,
                    LayerStyle = layerStyle,  // 레이어 스타일 전달
                    MapToScreen = coord => {
                        var pt = _map.WorldToScreen(coord);
                        return new System.Windows.Point(pt.X, pt.Y);
                    },
                    ScreenToMap = point => _map.ScreenToWorld(new System.Drawing.Point((int)point.X, (int)point.Y))
                };
                
                Log($"RenderVectorLayer: VectorRenderer.RenderFeatures 호출");
                // VectorRenderer를 사용하여 피처 렌더링
                _vectorRenderer.RenderFeatures(features, renderContext);
                Log($"RenderVectorLayer: VectorRenderer.RenderFeatures 완료");
            }
            else
            {
                Log($"RenderVectorLayer: '{layer.Name}' 피처 없음");
            }
        }
        catch (Exception ex)
        {
            Log($"RenderVectorLayer error: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// VectorLayer에서 LayerRenderStyle 생성
    /// </summary>
    private LayerRenderStyle CreateLayerStyle(VectorLayer layer)
    {
        var style = new LayerRenderStyle
        {
            Opacity = layer.Opacity,
            StrokeWidth = 1.0,
            PointSize = 8.0,
            EnableFill = true,
            EnableStroke = true
        };
        
        // VectorLayer.Style이 있으면 사용
        if (layer.Style != null)
        {
            if (layer.Style is Styling.IPolygonStyle polyStyle)
            {
                style.FillColor = polyStyle.Fill;
                style.StrokeColor = polyStyle.Stroke;
                style.StrokeWidth = polyStyle.StrokeWidth;
                style.Opacity = polyStyle.Opacity * layer.Opacity;
                style.DashPattern = polyStyle.DashArray;
            }
            else if (layer.Style is Styling.ILineStyle lineStyle)
            {
                style.StrokeColor = lineStyle.Stroke;
                style.StrokeWidth = lineStyle.StrokeWidth;
                style.DashPattern = lineStyle.DashArray;
                style.EnableFill = false;
            }
            else if (layer.Style is Styling.IPointStyle pointStyle)
            {
                style.FillColor = pointStyle.Fill;
                style.StrokeColor = pointStyle.Stroke;
                style.StrokeWidth = pointStyle.StrokeWidth;
                style.PointSize = pointStyle.Size;
                style.SymbolType = ConvertPointShape(pointStyle.Shape);
            }
        }
        
        return style;
    }
    
    /// <summary>
    /// PointShape를 PointSymbolType으로 변환
    /// </summary>
    private PointSymbolType ConvertPointShape(Styling.PointShape shape)
    {
        return shape switch
        {
            Styling.PointShape.Circle => PointSymbolType.Circle,
            Styling.PointShape.Square => PointSymbolType.Square,
            Styling.PointShape.Triangle => PointSymbolType.Triangle,
            Styling.PointShape.Diamond => PointSymbolType.Diamond,
            Styling.PointShape.Cross => PointSymbolType.Cross,
            Styling.PointShape.X => PointSymbolType.Cross,
            _ => PointSymbolType.Circle
        };
    }
    
    
    /// <summary>
    /// 레이어 가시성 확인
    /// </summary>
    private bool IsLayerVisible(ILayer layer)
    {
        // 디버깅을 위해 가시성 확인 완화
        if (layer == null) return false;
        
        if (!layer.Enabled || !layer.Visible)
            return false;

        // 배율 필터링 잠시 비활성화 (드로잉 확인용)
        /*
        if (layer is VectorLayer vectorLayer)
        {
            var scale = _map.GetMapScale();
            return scale >= vectorLayer.MinVisible && scale <= vectorLayer.MaxVisible;
        }
        */
        return true;
    }
    
    /// <summary>
    /// 디버그 정보 렌더링
    /// </summary>
    private void RenderDebugInfo(DrawingContext dc)
    {
        #if DEBUG
        var text = new FormattedText(
            $"Center: {_map.Center.X:F2}, {_map.Center.Y:F2}\n" +
            $"Zoom: {_map.Zoom:F2}\n" +
            $"Scale: 1:{_map.GetMapScale():F0}\n" +
            $"Layers: {_map.Layers.Count}",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            12,
            Brushes.Black,
            1.0);
            
        dc.DrawText(text, new Point(10, 10));
        #endif
    }
    
    /// <summary>
    /// 캔버스 업데이트
    /// </summary>
    private void UpdateCanvas()
    {
        if (_backBuffer == null) return;
        
        // 캔버스의 배경을 비우고 이미지 컨트롤을 추가하여 표시
        _canvas.Background = null;
        
        // 기존에 추가된 이미지가 있다면 업데이트, 없으면 생성
        var image = _canvas.Children.OfType<Image>().FirstOrDefault();
        if (image == null)
        {
            image = new Image { Stretch = Stretch.None };
            _canvas.Children.Add(image);
        }
        
        image.Source = _backBuffer;
        _canvas.InvalidateVisual();
    }
    
    /// <summary>
    /// 뷰 변경 이벤트 핸들러
    /// </summary>
    private void OnMapViewChanged(object? sender, EventArgs e)
    {
        // UI 스레드에서 렌더링
        _canvas.Dispatcher.InvokeAsync(Render);
    }
    
    /// <summary>
    /// 레이어 변경 이벤트 핸들러
    /// </summary>
    private void OnMapLayersChanged(object? sender, EventArgs e)
    {
        // UI 스레드에서 렌더링
        _canvas.Dispatcher.InvokeAsync(Render);
    }
    
    #region IDisposable
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            // 이벤트 구독 해제
            _map.ViewChanged -= OnMapViewChanged;
            _map.LayersChanged -= OnMapLayersChanged;
            
            _drawingVisual = null;
            _backBuffer = null;
        }
        
        _disposed = true;
    }
    
    #endregion
}