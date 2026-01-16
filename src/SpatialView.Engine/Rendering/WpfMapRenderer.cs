using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Rendering.Optimization;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// WPF 기반 지도 렌더러
/// 성능 최적화:
/// - 배칭 렌더링 (DrawingContext 호출 최소화)
/// - 캐시 시스템 (정적 레이어 재사용)
/// - 비동기 렌더링 (UI 스레드 블로킹 방지)
/// - 렌더링 쓰로틀링 (과도한 재렌더링 방지)
/// </summary>
public class WpfMapRenderer : IDisposable
{
    private readonly MapContainer _map;
    private readonly Canvas _canvas;
    private readonly VectorRenderer _vectorRenderer;
    private DrawingVisual? _drawingVisual;
    private RenderTargetBitmap? _backBuffer;
    private bool _disposed;

    // LOD 시스템
    private Optimization.LevelOfDetail? _lod;

    // 성능 최적화: 렌더링 캐시
    private readonly RenderCache _renderCache = new();

    // 성능 최적화: 렌더링 쓰로틀링
    private DateTime _lastRenderTime = DateTime.MinValue;
    private bool _renderPending = false;
    private readonly object _renderLock = new object();
    private const int MIN_RENDER_INTERVAL_MS = 16; // ~60fps

    // 성능 측정
    private readonly System.Diagnostics.Stopwatch _renderStopwatch = new();
    private double _lastRenderMs;

    /// <summary>
    /// 마지막 렌더링 소요 시간 (밀리초)
    /// </summary>
    public double LastRenderTimeMs => _lastRenderMs;
    
    public WpfMapRenderer(MapContainer map, Canvas canvas)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _vectorRenderer = new VectorRenderer();
        // 배칭 렌더링 활성화 (스타일별 Draw 호출 최소화)
        _vectorRenderer.UseBatching = true;

        // 이벤트 연결
        _map.ViewChanged += OnMapViewChanged;
        _map.LayersChanged += OnMapLayersChanged;
    }
    
    /// <summary>
    /// 지도 렌더링
    /// </summary>
    public void Render()
    {
        if (_disposed) return;
        
        // 렌더링 쓰로틀링: 너무 빈번한 렌더링 방지
        lock (_renderLock)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastRenderTime).TotalMilliseconds;
            
            if (elapsed < MIN_RENDER_INTERVAL_MS)
            {
                // 이미 대기 중인 렌더링이 없으면 예약
                if (!_renderPending)
                {
                    _renderPending = true;
                    var delay = MIN_RENDER_INTERVAL_MS - (int)elapsed;
                    _canvas.Dispatcher.InvokeAsync(async () =>
                    {
                        await Task.Delay(delay);
                        _renderPending = false;
                        RenderInternal();
                    }, System.Windows.Threading.DispatcherPriority.Render);
                }
                return;
            }
            
            _lastRenderTime = now;
        }
        
        RenderInternal();
    }
    
    /// <summary>
    /// 실제 렌더링 수행
    /// </summary>
    private void RenderInternal()
    {
        if (_disposed) return;

        _renderStopwatch.Restart();

        lock (_map)
        {
            // 캔버스 크기 업데이트
            UpdateMapSize();

            // 백버퍼 생성
            CreateBackBuffer();

            // 렌더링 시작
            _drawingVisual = new DrawingVisual();

            // RenderOptions 설정으로 성능 향상
            RenderOptions.SetEdgeMode(_drawingVisual, EdgeMode.Aliased);
            RenderOptions.SetBitmapScalingMode(_drawingVisual, BitmapScalingMode.LowQuality);

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

        _renderStopwatch.Stop();
        _lastRenderMs = _renderStopwatch.Elapsed.TotalMilliseconds;

        // 성능 로그 (100ms 이상 걸리면 경고)
        if (_lastRenderMs > 100)
        {
            System.Diagnostics.Debug.WriteLine($"[성능 경고] 렌더링 시간: {_lastRenderMs:F1}ms");
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
    
    // 캐시된 배경 브러시 (Freeze로 성능 최적화)
    private SolidColorBrush? _cachedBackgroundBrush;
    private System.Drawing.Color _cachedBackgroundColor;

    /// <summary>
    /// 배경 렌더링
    /// </summary>
    private void RenderBackground(DrawingContext dc)
    {
        var bgColor = _map.BackgroundColor;

        // 배경색이 변경되었을 때만 브러시 재생성
        if (_cachedBackgroundBrush == null || _cachedBackgroundColor != bgColor)
        {
            _cachedBackgroundBrush = new SolidColorBrush(
                Color.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B));
            _cachedBackgroundBrush.Freeze(); // Freeze로 성능 향상
            _cachedBackgroundColor = bgColor;
        }

        dc.DrawRectangle(
            _cachedBackgroundBrush, null,
            new Rect(0, 0, _canvas.ActualWidth, _canvas.ActualHeight));
    }
    
    /// <summary>
    /// 레이어 렌더링
    /// </summary>
    private void RenderLayers(DrawingContext dc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int renderedCount = 0;
        int skippedCount = 0;
        
        // 배경 레이어 렌더링
        foreach (var layer in _map.BackgroundLayers)
        {
            try
            {
                if (!layer.Enabled || !layer.Visible)
                {
                    skippedCount++;
                    continue;
                }
                RenderLayer(dc, layer);
                renderedCount++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"배경 레이어 '{layer.Name}' 렌더링 오류: {ex.Message}");
            }
        }
        
        // 일반 레이어 렌더링
        foreach (var layer in _map.Layers)
        {
            try
            {
                if (!layer.Enabled || !layer.Visible)
                {
                    skippedCount++;
                    continue;
                }
                RenderLayer(dc, layer);
                renderedCount++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"일반 레이어 '{layer.Name}' 렌더링 오류: {ex.Message}");
            }
        }
        
        sw.Stop();
        // 성능 측정 로그 (100ms 이상 걸리면 출력)
        if (sw.ElapsedMilliseconds > 100)
        {
            System.Diagnostics.Debug.WriteLine($"[성능] RenderLayers: {sw.ElapsedMilliseconds}ms, 렌더링={renderedCount}, 스킵={skippedCount}");
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

            // 캐시 시도 (뷰포트 포함 여부 + 줌 변동)
            var cached = _renderCache.GetCachedVisual(layer.Id, _map.Zoom, _map.ViewExtent);
            if (cached != null)
            {
                dc.DrawDrawing(cached.Drawing);
                return;
            }

            // 새로운 DrawingVisual에 렌더링 후 메인 DC에 그리기 + 캐시
            if (layer is VectorLayer vectorLayer)
            {
                var layerVisual = new DrawingVisual();
                using (var ldc = layerVisual.RenderOpen())
                {
                    RenderVectorLayer(ldc, vectorLayer);
                }

                dc.DrawDrawing(layerVisual.Drawing);

                // 피처 수는 캐시 통계용
                var featureCount = (int)Math.Min(int.MaxValue, vectorLayer.FeatureCount);
                _renderCache.CacheVisual(layer.Id, layerVisual, _map.Zoom, _map.ViewExtent, featureCount);
            }
            // TODO: 다른 레이어 타입 추가
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"레이어 '{layer.Name}' 렌더링 오류: {ex.Message}");
        }
    }
    
    // 성능 최적화: 로그 비활성화 (필요시 활성화)
    private static readonly bool _enableLogging = false;
    
    private static void Log(string msg)
    {
        if (!_enableLogging) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[WpfMapRenderer] {msg}");
        }
        catch { }
    }

    /// <summary>
    /// 벡터 레이어 렌더링
    /// </summary>
    private void RenderVectorLayer(DrawingContext dc, VectorLayer layer)
    {
        try
        {
            // VectorLayer의 GetFeatures 사용 (캐시된 피처)
            var viewExtent = _map.ViewExtent;
            
            // GetFeatures는 VectorLayer에서 List<IFeature>를 반환하므로 직접 캐스팅 가능
            var features = layer.GetFeatures(viewExtent);
            if (features == null) return;

            // VectorLayer.GetFeatures()는 이미 List<IFeature>를 반환 - ToList() 회피
            var featureList = features as IList<Data.IFeature>;
            if (featureList == null)
            {
                // 폴백: 다른 레이어 타입의 경우
                featureList = features as IList<Data.IFeature> ?? new List<Data.IFeature>(features);
            }

            if (featureList.Count == 0) return;
            
            // 레이어 스타일 생성 (한 번만)
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
                LayerStyle = layerStyle,
                LabelStyle = layer.LabelStyle,
                RenderLabels = layer.ShowLabels,
                MapToScreen = coord => {
                    var pt = _map.WorldToScreen(coord);
                    return new System.Windows.Point(pt.X, pt.Y);
                },
                ScreenToMap = point => _map.ScreenToWorld(point.X, point.Y)
            };
            
            // 좌표 변환 파라미터 초기화 (성능 최적화)
            renderContext.InitializeTransform();
            
            // VectorRenderer를 사용하여 피처 렌더링
            _vectorRenderer.RenderFeatures(featureList, renderContext);
            
            // 라벨 렌더링 (피처 렌더링 후)
            if (layer.ShowLabels && layer.LabelStyle != null)
            {
                _vectorRenderer.RenderLabels(featureList, renderContext, layer.LabelStyle);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RenderVectorLayer error: {ex.Message}");
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
        
        Log($"CreateLayerStyle: '{layer.Name}' Opacity={layer.Opacity}, Style={layer.Style?.GetType().Name ?? "null"}");
        
        // VectorLayer.Style이 있으면 사용
        if (layer.Style != null)
        {
            if (layer.Style is Styling.IPolygonStyle polyStyle)
            {
                style.FillColor = polyStyle.Fill;
                style.StrokeColor = polyStyle.Stroke;
                style.StrokeWidth = polyStyle.StrokeWidth;
                // 레이어의 Opacity만 사용 (스타일 Opacity는 1.0으로 유지됨)
                style.Opacity = layer.Opacity;
                style.DashPattern = polyStyle.DashArray;
                Log($"CreateLayerStyle: PolygonStyle - Fill={polyStyle.Fill}, Stroke={polyStyle.Stroke}, LayerOpacity={layer.Opacity}");
            }
            else if (layer.Style is Styling.ILineStyle lineStyle)
            {
                style.StrokeColor = lineStyle.Stroke;
                style.StrokeWidth = lineStyle.StrokeWidth;
                style.DashPattern = lineStyle.DashArray;
                style.EnableFill = false;
                style.Opacity = layer.Opacity;
                Log($"CreateLayerStyle: LineStyle - Stroke={lineStyle.Stroke}, LayerOpacity={layer.Opacity}");
            }
            else if (layer.Style is Styling.IPointStyle pointStyle)
            {
                style.FillColor = pointStyle.Fill;
                style.StrokeColor = pointStyle.Stroke;
                style.StrokeWidth = pointStyle.StrokeWidth;
                style.PointSize = pointStyle.Size;
                style.SymbolType = ConvertPointShape(pointStyle.Shape);
                style.Opacity = layer.Opacity;
                Log($"CreateLayerStyle: PointStyle - Fill={pointStyle.Fill}, LayerOpacity={layer.Opacity}");
            }
        }
        else
        {
            Log($"CreateLayerStyle: No style, using defaults with LayerOpacity={layer.Opacity}");
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
    /// 렌더 캐시 무효화 (스타일/팔레트 변경 즉시 반영용)
    /// </summary>
    public void InvalidateCache()
    {
        _renderCache.InvalidateAll();
    }
    
    /// <summary>
    /// 뷰 변경 이벤트 핸들러
    /// </summary>
    private void OnMapViewChanged(object? sender, EventArgs e)
    {
        _renderCache.InvalidateAll();
        // UI 스레드에서 렌더링
        _canvas.Dispatcher.InvokeAsync(Render);
    }
    
    /// <summary>
    /// 레이어 변경 이벤트 핸들러
    /// </summary>
    private void OnMapLayersChanged(object? sender, EventArgs e)
    {
        _renderCache.InvalidateAll();
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

            // 캐시 정리
            _renderCache.Dispose();

            _drawingVisual = null;
            _backBuffer = null;
            _cachedBackgroundBrush = null;
        }

        _disposed = true;
    }
    
    #endregion
}