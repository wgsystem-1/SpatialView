using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace SpatialView.Engine.Rendering;

/// <summary>
/// WPF 기반 사용자 정의 지도 캔버스
/// GPU 가속과 고성능 렌더링을 위한 사용자 정의 컨트롤
/// </summary>
public class CustomMapCanvas : Canvas, IMapCanvas
{
    private Geometry.ICoordinate _center;
    private double _zoom = 1.0;
    private int _srid = 4326;
    private Color _backgroundColor = Colors.White;
    private RenderingQuality _renderingQuality = RenderingQuality.Balanced;
    private bool _antiAliasing = true;
    
    private readonly Data.Layers.ILayerCollection _layers;
    private readonly IVectorRenderer _vectorRenderer;
    private readonly Tiles.ITileRenderer _tileRenderer;
    private readonly CoordinateSystems.Transformation.CoordinateTransformationFactory _transformationFactory;
    
    // 렌더링 캐시
    private WriteableBitmap? _renderBuffer;
    private bool _needsRedraw = true;
    
    /// <summary>
    /// 생성자
    /// </summary>
    public CustomMapCanvas()
    {
        _center = new Geometry.Coordinate(0, 0);
        _layers = new Data.Layers.LayerCollection();
        _vectorRenderer = new VectorRenderer();
        _tileRenderer = new Tiles.TileRenderer();
        _transformationFactory = new CoordinateSystems.Transformation.CoordinateTransformationFactory();
        
        // 이벤트 등록
        _layers.CollectionChanged += OnLayersChanged;
        SizeChanged += OnSizeChanged;
        
        // 마우스 이벤트
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseWheel += OnMouseWheel;
        
        // 렌더링 최적화
        SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
        ClipToBounds = true;
    }
    
    #region IMapCanvas 구현
    
    /// <inheritdoc/>
    public new Size Size
    {
        get => new Size(ActualWidth, ActualHeight);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }
    
    /// <inheritdoc/>
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Math.Abs(_zoom - value) > double.Epsilon)
            {
                var oldZoom = _zoom;
                var oldExtent = ViewExtent;
                
                _zoom = Math.Max(0.001, value);
                _needsRedraw = true;
                
                OnViewportChanged(new ViewportChangedEventArgs(oldExtent, ViewExtent, oldZoom, _zoom));
                InvalidateVisual();
            }
        }
    }
    
    /// <inheritdoc/>
    public Geometry.ICoordinate Center
    {
        get => _center;
        set
        {
            if (value != null && !_center.Equals(value))
            {
                var oldExtent = ViewExtent;
                _center = value.Copy();
                _needsRedraw = true;
                
                OnViewportChanged(new ViewportChangedEventArgs(oldExtent, ViewExtent, _zoom, _zoom));
                InvalidateVisual();
            }
        }
    }
    
    /// <inheritdoc/>
    public Geometry.Envelope ViewExtent
    {
        get
        {
            if (ActualWidth == 0 || ActualHeight == 0)
                return new Geometry.Envelope();
                
            var resolution = GetResolution();
            var halfWidth = (ActualWidth * resolution) / 2;
            var halfHeight = (ActualHeight * resolution) / 2;
            
            return new Geometry.Envelope(
                _center.X - halfWidth, _center.X + halfWidth,
                _center.Y - halfHeight, _center.Y + halfHeight);
        }
    }
    
    /// <inheritdoc/>
    public int SRID
    {
        get => _srid;
        set
        {
            if (_srid != value)
            {
                _srid = value;
                _needsRedraw = true;
                InvalidateVisual();
            }
        }
    }
    
    /// <inheritdoc/>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                Background = new SolidColorBrush(value);
                _needsRedraw = true;
                InvalidateVisual();
            }
        }
    }
    
    /// <inheritdoc/>
    public Data.Layers.ILayerCollection Layers => _layers;
    
    /// <inheritdoc/>
    public RenderingQuality RenderingQuality
    {
        get => _renderingQuality;
        set
        {
            if (_renderingQuality != value)
            {
                _renderingQuality = value;
                UpdateRenderingHints();
                _needsRedraw = true;
                InvalidateVisual();
            }
        }
    }
    
    /// <inheritdoc/>
    public bool AntiAliasing
    {
        get => _antiAliasing;
        set
        {
            if (_antiAliasing != value)
            {
                _antiAliasing = value;
                UpdateRenderingHints();
                _needsRedraw = true;
                InvalidateVisual();
            }
        }
    }
    
    #endregion
    
    #region 좌표 변환
    
    /// <inheritdoc/>
    public Geometry.ICoordinate ScreenToMap(Point screenPoint)
    {
        var resolution = GetResolution();
        var centerX = ActualWidth / 2;
        var centerY = ActualHeight / 2;
        
        var mapX = _center.X + (screenPoint.X - centerX) * resolution;
        var mapY = _center.Y - (screenPoint.Y - centerY) * resolution; // Y축 반전
        
        return new Geometry.Coordinate(mapX, mapY);
    }
    
    /// <inheritdoc/>
    public Point MapToScreen(Geometry.ICoordinate mapPoint)
    {
        var resolution = GetResolution();
        var centerX = ActualWidth / 2;
        var centerY = ActualHeight / 2;
        
        var screenX = centerX + (mapPoint.X - _center.X) / resolution;
        var screenY = centerY - (mapPoint.Y - _center.Y) / resolution; // Y축 반전
        
        return new Point(screenX, screenY);
    }
    
    /// <summary>
    /// 현재 해상도 계산 (맵 단위 / 픽셀)
    /// </summary>
    private double GetResolution()
    {
        // 기본적인 해상도 계산
        // 좌표계에 따라 조정 필요
        return 1.0 / _zoom;
    }
    
    #endregion
    
    #region 줌 및 내비게이션
    
    /// <inheritdoc/>
    public void ZoomToExtent(Geometry.Envelope extent)
    {
        if (extent == null || extent.IsNull || ActualWidth == 0 || ActualHeight == 0)
            return;
            
        // 중심점 설정
        Center = extent.Centre;
        
        // 줌 레벨 계산
        var scaleX = ActualWidth / extent.Width;
        var scaleY = ActualHeight / extent.Height;
        var scale = Math.Min(scaleX, scaleY) * 0.9; // 10% 여백
        
        Zoom = scale;
    }
    
    /// <inheritdoc/>
    public void ZoomToPoint(Geometry.ICoordinate center, double zoom)
    {
        Center = center;
        Zoom = zoom;
    }
    
    #endregion
    
    #region 렌더링
    
    /// <inheritdoc/>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        
        if (_needsRedraw || _renderBuffer == null)
        {
            RenderMap(drawingContext);
            _needsRedraw = false;
        }
        else if (_renderBuffer != null)
        {
            // 캐시된 렌더 결과 사용
            drawingContext.DrawImage(_renderBuffer, new Rect(0, 0, ActualWidth, ActualHeight));
        }
        
        OnRenderCompleted();
    }
    
    /// <summary>
    /// 지도 렌더링 수행
    /// </summary>
    private void RenderMap(DrawingContext drawingContext)
    {
        // 배경 그리기
        drawingContext.DrawRectangle(
            new SolidColorBrush(_backgroundColor), 
            null, 
            new Rect(0, 0, ActualWidth, ActualHeight));
        
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;
            
        // 렌더링 컨텍스트 생성
        var renderContext = new RenderContext
        {
            DrawingContext = drawingContext,
            ViewExtent = ViewExtent,
            ScreenSize = new Size(ActualWidth, ActualHeight),
            Zoom = _zoom,
            SRID = _srid,
            Quality = _renderingQuality,
            AntiAliasing = _antiAliasing,
            MapToScreen = MapToScreen,
            ScreenToMap = ScreenToMap
        };
        
        // Z-순서대로 레이어 렌더링
        var visibleLayers = _layers.GetLayersByZOrder()
            .Where(layer => layer.Visible && IsLayerInScale(layer))
            .ToList();
            
        foreach (var layer in visibleLayers)
        {
            try
            {
                RenderLayer(layer, renderContext);
            }
            catch (Exception ex)
            {
                // 레이어 렌더링 오류 처리
                System.Diagnostics.Debug.WriteLine($"Layer rendering error: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 레이어 렌더링
    /// </summary>
    private void RenderLayer(Data.Layers.ILayer layer, RenderContext context)
    {
        // 레이어와 뷰포트 교차 테스트
        if (layer.Extent != null && !context.ViewExtent.Intersects(layer.Extent))
            return;

        switch (layer)
        {
            case Data.Layers.ITileLayer tileLayer:
                RenderTileLayer(tileLayer, context);
                break;
                
            default:
                // 벡터 레이어 렌더링
                var features = layer.GetFeatures(context.ViewExtent).ToList();
                if (features.Any())
                {
                    _vectorRenderer.RenderFeatures(features, context);
                }
                break;
        }
    }
    
    /// <summary>
    /// 타일 레이어 렌더링
    /// </summary>
    private void RenderTileLayer(Data.Layers.ITileLayer tileLayer, RenderContext context)
    {
        // 현재 줌 레벨에서 적절한 타일 줌 레벨 계산
        var tileZoom = CalculateTileZoomLevel(context.Zoom, tileLayer);
        
        // 줌 레벨이 레이어 범위를 벗어나면 렌더링하지 않음
        if (tileZoom < tileLayer.MinZoomLevel || tileZoom > tileLayer.MaxZoomLevel)
            return;

        try
        {
            // 현재 뷰 영역의 타일들 가져오기
            var tiles = tileLayer.GetTiles(context.ViewExtent, tileZoom).ToList();
            
            if (tiles.Any())
            {
                // 타일 렌더링
                _tileRenderer.RenderTiles(tiles, context);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tile layer rendering error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 맵 줌 레벨에서 타일 줌 레벨 계산
    /// </summary>
    private static int CalculateTileZoomLevel(double mapZoom, Data.Layers.ITileLayer tileLayer)
    {
        // 기본적인 변환 공식 (좀 더 정교한 변환이 필요할 수 있음)
        var tileZoom = (int)Math.Round(Math.Log(mapZoom, 2));
        
        // 레이어의 줌 범위로 제한
        return Math.Max(tileLayer.MinZoomLevel, Math.Min(tileLayer.MaxZoomLevel, tileZoom));
    }
    
    /// <summary>
    /// 레이어가 현재 배율 범위에 있는지 확인
    /// </summary>
    private bool IsLayerInScale(Data.Layers.ILayer layer)
    {
        return _zoom >= layer.MinimumZoom && _zoom <= layer.MaximumZoom;
    }
    
    /// <inheritdoc/>
    public void Render()
    {
        InvalidateVisual();
    }
    
    /// <inheritdoc/>
    public ImageSource ExportToImage()
    {
        var renderBitmap = new RenderTargetBitmap(
            (int)ActualWidth, (int)ActualHeight, 
            96, 96, PixelFormats.Pbgra32);
            
        renderBitmap.Render(this);
        return renderBitmap;
    }
    
    /// <inheritdoc/>
    public void Refresh()
    {
        _needsRedraw = true;
        foreach (var layer in _layers)
        {
            layer.Refresh();
        }
        InvalidateVisual();
    }
    
    #endregion
    
    #region 렌더링 설정
    
    /// <summary>
    /// 렌더링 힌트 업데이트
    /// </summary>
    private void UpdateRenderingHints()
    {
        switch (_renderingQuality)
        {
            case RenderingQuality.Fast:
                SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.LowQuality);
                SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
                break;
                
            case RenderingQuality.Balanced:
                SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.Linear);
                SetValue(RenderOptions.EdgeModeProperty, _antiAliasing ? EdgeMode.Unspecified : EdgeMode.Aliased);
                break;
                
            case RenderingQuality.HighQuality:
                SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Unspecified);
                break;
        }
    }
    
    #endregion
    
    #region 이벤트 처리
    
    /// <inheritdoc/>
    public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
    
    /// <inheritdoc/>
    public event EventHandler? RenderCompleted;
    
    /// <summary>
    /// 뷰포트 변경 이벤트 발생
    /// </summary>
    protected virtual void OnViewportChanged(ViewportChangedEventArgs e)
    {
        ViewportChanged?.Invoke(this, e);
    }
    
    /// <summary>
    /// 렌더링 완료 이벤트 발생
    /// </summary>
    protected virtual void OnRenderCompleted()
    {
        RenderCompleted?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnLayersChanged(object? sender, Data.Layers.LayerCollectionChangedEventArgs e)
    {
        _needsRedraw = true;
        InvalidateVisual();
    }
    
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _needsRedraw = true;
        _renderBuffer = null; // 캐시 무효화
        InvalidateVisual();
    }
    
    #endregion
    
    #region 마우스 상호작용 (기본 패닝/줌)
    
    private bool _isDragging;
    private Point _lastMousePosition;
    
    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }
    
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;
            
            var resolution = GetResolution();
            var newCenterX = _center.X - deltaX * resolution;
            var newCenterY = _center.Y + deltaY * resolution; // Y축 반전
            
            Center = new Geometry.Coordinate(newCenterX, newCenterY);
            _lastMousePosition = currentPosition;
        }
        e.Handled = true;
    }
    
    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }
        e.Handled = true;
    }
    
    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePosition = e.GetPosition(this);
        var mapPosition = ScreenToMap(mousePosition);
        
        // 줌 배율 조정
        var zoomFactor = e.Delta > 0 ? 1.2 : 1.0 / 1.2;
        var newZoom = _zoom * zoomFactor;
        
        // 마우스 위치를 중심으로 줌
        Zoom = newZoom;
        
        // 마우스 위치가 그대로 유지되도록 중심점 조정
        var newMouseMapPosition = ScreenToMap(mousePosition);
        var offsetX = mapPosition.X - newMouseMapPosition.X;
        var offsetY = mapPosition.Y - newMouseMapPosition.Y;
        
        Center = new Geometry.Coordinate(_center.X + offsetX, _center.Y + offsetY);
        
        e.Handled = true;
    }
    
    #endregion
}