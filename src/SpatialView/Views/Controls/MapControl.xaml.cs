using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using SpatialView.ViewModels;
using SpatialView.Core.GisEngine;
using SpatialView.Infrastructure.GisEngine;
using Coordinate = SpatialView.Engine.Geometry.Coordinate;
using Envelope = SpatialView.Engine.Geometry.Envelope;
using Point = System.Windows.Point;

namespace SpatialView.Views.Controls;

/// <summary>
/// 지도 표시를 위한 UserControl
/// </summary>
public partial class MapControl : System.Windows.Controls.UserControl
{
    private IMapRenderer? _mapRenderer;
    private MapViewModel? _viewModel;
    private bool _isPanning = false;
    private bool _isZoomWindow = false;
    private Point _lastMousePosition;
    private Point _startMousePosition;
    private System.Windows.Shapes.Rectangle? _zoomRect;

    public MapControl()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MapViewModel viewModel)
        {
            _viewModel = viewModel;
            
            // ViewModel에서 MapRenderer 가져오기
            _mapRenderer = viewModel.MapRenderer;
            
            if (_mapRenderer is SpatialViewMapRenderer renderer)
            {
                renderer.SetCanvas(MapCanvas);
                
                if (viewModel.Map != null)
                {
                    renderer.MapEngine = viewModel.Map;
                }
            }
            
            // ViewModel 변경 시 Map 업데이트
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // RefreshRequested 이벤트 구독
            viewModel.RefreshRequested += OnRefreshRequested;
            
            // ActiveToolChanged 이벤트 구독
            viewModel.ActiveToolChanged += OnActiveToolChanged;
            
            // 초기 지도 범위 설정
            viewModel.InitializeMap();
            if (viewModel.Map != null && _mapRenderer != null)
            {
                _mapRenderer.MapEngine = viewModel.Map;
                _mapRenderer.Render();
            }

            // 마우스 이벤트 연결
            MapCanvas.MouseDown += MapCanvas_MouseDown;
            MapCanvas.MouseMove += MapCanvas_MouseMove;
            MapCanvas.MouseUp += MapCanvas_MouseUp;
            MapCanvas.MouseWheel += MapCanvas_MouseWheel;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.RefreshRequested -= OnRefreshRequested;
            _viewModel.ActiveToolChanged -= OnActiveToolChanged;
            _viewModel = null;
        }

        MapCanvas.MouseDown -= MapCanvas_MouseDown;
        MapCanvas.MouseMove -= MapCanvas_MouseMove;
        MapCanvas.MouseUp -= MapCanvas_MouseUp;
        MapCanvas.MouseWheel -= MapCanvas_MouseWheel;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewModel?.Map != null && _mapRenderer != null)
        {
            _mapRenderer.Render();
        }
    }

    private void OnActiveToolChanged(Core.GisEngine.MapTool tool)
    {
        if (_mapRenderer == null) return;
        
        _mapRenderer.ActiveTool = tool;
        
        // 커서 변경
        MapCanvas.Cursor = tool switch
        {
            Core.GisEngine.MapTool.Pan => System.Windows.Input.Cursors.Hand,
            Core.GisEngine.MapTool.ZoomIn => System.Windows.Input.Cursors.Cross,
            Core.GisEngine.MapTool.ZoomOut => System.Windows.Input.Cursors.Cross,
            Core.GisEngine.MapTool.ZoomWindow => System.Windows.Input.Cursors.Cross,
            Core.GisEngine.MapTool.Select => System.Windows.Input.Cursors.Arrow,
            _ => System.Windows.Input.Cursors.Arrow
        };
    }

    private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel == null || _viewModel.Map == null) return;
        
        MapCanvas.Focus();
        _lastMousePosition = e.GetPosition(MapCanvas);
        _startMousePosition = _lastMousePosition;
        
        if (e.ChangedButton == MouseButton.Right)
        {
            if (_viewModel.SelectedFeatureIds != null && _viewModel.SelectedFeatureIds.Count > 0)
            {
                _viewModel.RequestAttributeFocusOnSelection();
            }
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            switch (_viewModel.ActiveTool)
            {
                case Core.GisEngine.MapTool.Pan:
                    _isPanning = true;
                    MapCanvas.CaptureMouse();
                    break;

                case Core.GisEngine.MapTool.ZoomWindow:
                    _isZoomWindow = true;
                    MapCanvas.CaptureMouse();
                    _zoomRect = new System.Windows.Shapes.Rectangle
                    {
                        Stroke = System.Windows.Media.Brushes.Red,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection(new double[] { 4, 2 }),
                        Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 0, 0))
                    };
                    Canvas.SetLeft(_zoomRect, _startMousePosition.X);
                    Canvas.SetTop(_zoomRect, _startMousePosition.Y);
                    _zoomRect.Width = 0;
                    _zoomRect.Height = 0;
                    MapCanvas.Children.Add(_zoomRect);
                    break;

                case Core.GisEngine.MapTool.Select:
                    try
                    {
                        var worldPos = _viewModel.Map.ScreenToMap(new Point(_startMousePosition.X, _startMousePosition.Y));
                        _viewModel.SelectFeaturesAtPoint(worldPos.X, worldPos.Y);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"피처 선택 오류: {ex.Message}");
                    }
                    break;
            }
        }
    }

    private void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (_isPanning)
            {
                _isPanning = false;
                MapCanvas.ReleaseMouseCapture();
            }
            else if (_isZoomWindow)
            {
                _isZoomWindow = false;
                MapCanvas.ReleaseMouseCapture();

                if (_zoomRect != null && _viewModel?.Map != null)
                {
                    var rect = new Rect(
                        Canvas.GetLeft(_zoomRect), 
                        Canvas.GetTop(_zoomRect), 
                        _zoomRect.Width, 
                        _zoomRect.Height);

                    if (rect.Width > 5 && rect.Height > 5)
                    {
                        var p1 = _viewModel.Map.ScreenToMap(new Point(rect.Left, rect.Top));
                        var p2 = _viewModel.Map.ScreenToMap(new Point(rect.Right, rect.Bottom));
                        
                        var extent = new Envelope(
                            Math.Min(p1.X, p2.X), Math.Max(p1.X, p2.X),
                            Math.Min(p1.Y, p2.Y), Math.Max(p1.Y, p2.Y));
                        
                        _viewModel.ZoomToEnvelope(extent);
                    }
                    
                    MapCanvas.Children.Remove(_zoomRect);
                    _zoomRect = null;
                }
            }
        }
    }

    private void MapCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_viewModel == null || _viewModel.Map == null) return;
        
        var currentPos = e.GetPosition(MapCanvas);
        
        if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
        {
            var dx = currentPos.X - _lastMousePosition.X;
            var dy = currentPos.Y - _lastMousePosition.Y;
            
            var metersPerPixel = _viewModel.Map.Zoom / MapCanvas.ActualWidth;
            
            _viewModel.Map.Center = new Coordinate(
                _viewModel.Map.Center.X - dx * metersPerPixel,
                _viewModel.Map.Center.Y + dy * metersPerPixel);
            
            _lastMousePosition = currentPos;
            _mapRenderer?.Render();
        }
        else if (_isZoomWindow && _zoomRect != null)
        {
            var x = Math.Min(currentPos.X, _startMousePosition.X);
            var y = Math.Min(currentPos.Y, _startMousePosition.Y);
            var w = Math.Abs(currentPos.X - _startMousePosition.X);
            var h = Math.Abs(currentPos.Y - _startMousePosition.Y);

            Canvas.SetLeft(_zoomRect, x);
            Canvas.SetTop(_zoomRect, y);
            _zoomRect.Width = w;
            _zoomRect.Height = h;
        }
        
        try
        {
            var worldPos = _viewModel.Map.ScreenToMap(new Point(currentPos.X, currentPos.Y));
            _viewModel.UpdateMousePosition(worldPos.X, worldPos.Y);
        }
        catch { }
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel == null || _viewModel.Map == null) return;
        
        var factor = e.Delta > 0 ? 0.8 : 1.25;
        _viewModel.Map.Zoom *= factor;
        _mapRenderer?.Render();
    }

    private void OnRefreshRequested()
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (_mapRenderer != null && _viewModel?.Map != null)
            {
                _mapRenderer.MapEngine = _viewModel.Map;
                _mapRenderer.Render();
            }
        });
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.Map) && _viewModel?.Map != null && _mapRenderer != null)
        {
            _mapRenderer.MapEngine = _viewModel.Map;
            _mapRenderer.Refresh();
        }
    }
}
