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
    
    // 측정 경로 표시용
    private System.Windows.Shapes.Polyline? _measureLine;
    private List<Point> _measureScreenPoints = new();
    private List<Coordinate> _measureWorldPoints = new();
    
    // 편집 경로 표시용
    private System.Windows.Shapes.Polyline? _editLine;
    private List<Point> _editScreenPoints = new();
    private List<Coordinate> _editWorldPoints = new();
    private List<System.Windows.Shapes.Ellipse> _editVertexMarkers = new();

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
                // Map.Size 초기화 (좌표 변환에 필수)
                var width = Math.Max(1, MapCanvas.ActualWidth);
                var height = Math.Max(1, MapCanvas.ActualHeight);
                viewModel.Map.Size = new System.Windows.Size(width, height);

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
            // Map.Size를 즉시 업데이트하여 좌표 변환이 정확하게 동작하도록 함
            var width = Math.Max(1, MapCanvas.ActualWidth);
            var height = Math.Max(1, MapCanvas.ActualHeight);
            _viewModel.Map.Size = new System.Windows.Size(width, height);

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
            Core.GisEngine.MapTool.MeasureDistance => System.Windows.Input.Cursors.Cross,
            Core.GisEngine.MapTool.MeasureArea => System.Windows.Input.Cursors.Cross,
            Core.GisEngine.MapTool.Edit => System.Windows.Input.Cursors.Cross,
            Core.GisEngine.MapTool.Draw => System.Windows.Input.Cursors.Cross,
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
            System.Diagnostics.Debug.WriteLine($"MapCanvas 좌클릭: ActiveTool={_viewModel.ActiveTool}");
            
            // 더블클릭 처리 (편집 모드에서 완료)
            if (e.ClickCount == 2)
            {
                if (_viewModel.ActiveTool == Core.GisEngine.MapTool.Edit || 
                    _viewModel.ActiveTool == Core.GisEngine.MapTool.Draw)
                {
                    var mainWindow = Window.GetWindow(this) as MainWindow;
                    mainWindow?.CompleteEdit();
                    e.Handled = true;
                    return;
                }
            }
            
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
                    System.Diagnostics.Debug.WriteLine($"Select 도구 클릭: 화면좌표=({_startMousePosition.X:F2}, {_startMousePosition.Y:F2})");
                    System.Diagnostics.Debug.WriteLine($"  Canvas.ActualSize=({MapCanvas.ActualWidth:F0}x{MapCanvas.ActualHeight:F0}), Map.Size=({_viewModel.Map.Size.Width:F0}x{_viewModel.Map.Size.Height:F0})");
                    try
                    {
                        // Map.Size가 캔버스 크기와 일치하는지 확인 및 동기화
                        if (Math.Abs(_viewModel.Map.Size.Width - MapCanvas.ActualWidth) > 1 ||
                            Math.Abs(_viewModel.Map.Size.Height - MapCanvas.ActualHeight) > 1)
                        {
                            System.Diagnostics.Debug.WriteLine($"  [주의] Map.Size와 Canvas 크기 불일치! 동기화 중...");
                            _viewModel.Map.Size = new System.Windows.Size(MapCanvas.ActualWidth, MapCanvas.ActualHeight);
                        }

                        var worldPos = _viewModel.Map.ScreenToMap(new Point(_startMousePosition.X, _startMousePosition.Y));
                        System.Diagnostics.Debug.WriteLine($"Select 도구: 지도좌표=({worldPos.X:F2}, {worldPos.Y:F2})");
                        _viewModel.SelectFeaturesAtPoint(worldPos.X, worldPos.Y);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"피처 선택 오류: {ex.Message}");
                    }
                    break;
                    
                case Core.GisEngine.MapTool.MeasureDistance:
                case Core.GisEngine.MapTool.MeasureArea:
                    try
                    {
                        var measurePos = _viewModel.Map.ScreenToMap(new Point(_startMousePosition.X, _startMousePosition.Y));
                        
                        // 측정 경로에 포인트 추가
                        AddMeasurePoint(_startMousePosition, new Coordinate(measurePos.X, measurePos.Y));
                        
                        // MainWindow의 측정 다이얼로그에 포인트 추가
                        var mainWindow = Window.GetWindow(this) as MainWindow;
                        mainWindow?.AddMeasurementPoint(measurePos.X, measurePos.Y);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"측정 포인트 추가 오류: {ex.Message}");
                    }
                    break;
                    
                case Core.GisEngine.MapTool.Edit:
                case Core.GisEngine.MapTool.Draw:
                    try
                    {
                        var editPos = _viewModel.Map.ScreenToMap(new Point(_startMousePosition.X, _startMousePosition.Y));
                        
                        // 편집 경로에 포인트 추가 (화면 표시용)
                        AddEditPoint(_startMousePosition, new Coordinate(editPos.X, editPos.Y));
                        
                        // MainWindow의 편집 다이얼로그에 포인트 추가
                        var editWindow = Window.GetWindow(this) as MainWindow;
                        editWindow?.AddEditPoint(editPos.X, editPos.Y);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"편집 포인트 추가 오류: {ex.Message}");
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
                // 스타일/팔레트 변경 시 캐시 무효화
                (_mapRenderer as SpatialView.Infrastructure.GisEngine.SpatialViewMapRenderer)?.InvalidateCache();
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
    
    #region 측정 경로 표시
    
    /// <summary>
    /// 측정 포인트 추가
    /// </summary>
    private void AddMeasurePoint(Point screenPoint, Coordinate worldPoint)
    {
        _measureScreenPoints.Add(screenPoint);
        _measureWorldPoints.Add(worldPoint);
        UpdateMeasureLine();
    }
    
    /// <summary>
    /// 측정 경로 라인 업데이트
    /// </summary>
    private void UpdateMeasureLine()
    {
        if (_measureScreenPoints.Count < 1) return;
        
        // 기존 라인 제거
        if (_measureLine != null)
        {
            MapCanvas.Children.Remove(_measureLine);
        }
        
        // 새 라인 생성
        _measureLine = new System.Windows.Shapes.Polyline
        {
            Stroke = System.Windows.Media.Brushes.Red,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 5, 3 })
        };
        
        foreach (var pt in _measureScreenPoints)
        {
            _measureLine.Points.Add(pt);
        }
        
        // 면적 측정인 경우 폴리곤 닫기
        if (_viewModel?.ActiveTool == Core.GisEngine.MapTool.MeasureArea && _measureScreenPoints.Count > 2)
        {
            _measureLine.Points.Add(_measureScreenPoints[0]);
        }
        
        MapCanvas.Children.Add(_measureLine);
        
        // 포인트 마커 추가
        UpdateMeasureMarkers();
    }
    
    /// <summary>
    /// 측정 포인트 마커 업데이트
    /// </summary>
    private void UpdateMeasureMarkers()
    {
        // 기존 마커 제거 (태그로 식별)
        var markersToRemove = MapCanvas.Children.OfType<System.Windows.Shapes.Ellipse>()
            .Where(e => e.Tag?.ToString() == "MeasureMarker")
            .ToList();
        foreach (var marker in markersToRemove)
        {
            MapCanvas.Children.Remove(marker);
        }
        
        // 새 마커 추가
        for (int i = 0; i < _measureScreenPoints.Count; i++)
        {
            var pt = _measureScreenPoints[i];
            var marker = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = i == 0 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red,
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2,
                Tag = "MeasureMarker"
            };
            Canvas.SetLeft(marker, pt.X - 5);
            Canvas.SetTop(marker, pt.Y - 5);
            MapCanvas.Children.Add(marker);
        }
    }
    
    /// <summary>
    /// 측정 경로 초기화
    /// </summary>
    public void ClearMeasurePath()
    {
        _measureScreenPoints.Clear();
        _measureWorldPoints.Clear();
        
        if (_measureLine != null)
        {
            MapCanvas.Children.Remove(_measureLine);
            _measureLine = null;
        }
        
        // 마커 제거
        var markersToRemove = MapCanvas.Children.OfType<System.Windows.Shapes.Ellipse>()
            .Where(e => e.Tag?.ToString() == "MeasureMarker")
            .ToList();
        foreach (var marker in markersToRemove)
        {
            MapCanvas.Children.Remove(marker);
        }
    }
    
    /// <summary>
    /// 지도 이동/줌 시 측정 경로 다시 그리기
    /// </summary>
    private void RefreshMeasurePath()
    {
        if (_measureWorldPoints.Count == 0 || _viewModel?.Map == null) return;
        
        // 월드 좌표를 화면 좌표로 다시 변환
        _measureScreenPoints.Clear();
        foreach (var worldPt in _measureWorldPoints)
        {
            var screenPt = _viewModel.Map.MapToScreen(worldPt);
            _measureScreenPoints.Add(new Point(screenPt.X, screenPt.Y));
        }
        
        UpdateMeasureLine();
    }
    
    #endregion
    
    #region 편집 경로 표시
    
    /// <summary>
    /// 편집 포인트 추가
    /// </summary>
    private void AddEditPoint(Point screenPoint, Coordinate worldPoint)
    {
        _editScreenPoints.Add(screenPoint);
        _editWorldPoints.Add(worldPoint);
        UpdateEditLine();
    }
    
    /// <summary>
    /// 편집 경로 라인 업데이트
    /// </summary>
    private void UpdateEditLine()
    {
        if (_editScreenPoints.Count < 1) return;
        
        // 기존 라인 제거
        if (_editLine != null)
        {
            MapCanvas.Children.Remove(_editLine);
        }
        
        // 새 라인 생성
        _editLine = new System.Windows.Shapes.Polyline
        {
            Stroke = System.Windows.Media.Brushes.Blue,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection(new double[] { 5, 3 })
        };
        
        foreach (var pt in _editScreenPoints)
        {
            _editLine.Points.Add(pt);
        }
        
        MapCanvas.Children.Add(_editLine);
        
        // 포인트 마커 추가
        UpdateEditMarkers();
    }
    
    /// <summary>
    /// 편집 포인트 마커 업데이트
    /// </summary>
    private void UpdateEditMarkers()
    {
        // 기존 마커 제거
        foreach (var marker in _editVertexMarkers)
        {
            MapCanvas.Children.Remove(marker);
        }
        _editVertexMarkers.Clear();
        
        // 새 마커 추가
        for (int i = 0; i < _editScreenPoints.Count; i++)
        {
            var pt = _editScreenPoints[i];
            var marker = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = i == 0 ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Blue,
                Stroke = System.Windows.Media.Brushes.White,
                StrokeThickness = 2
            };
            Canvas.SetLeft(marker, pt.X - 5);
            Canvas.SetTop(marker, pt.Y - 5);
            MapCanvas.Children.Add(marker);
            _editVertexMarkers.Add(marker);
        }
    }
    
    /// <summary>
    /// 편집 경로 초기화
    /// </summary>
    public void ClearEditPath()
    {
        _editScreenPoints.Clear();
        _editWorldPoints.Clear();
        
        if (_editLine != null)
        {
            MapCanvas.Children.Remove(_editLine);
            _editLine = null;
        }
        
        // 마커 제거
        foreach (var marker in _editVertexMarkers)
        {
            MapCanvas.Children.Remove(marker);
        }
        _editVertexMarkers.Clear();
    }
    
    /// <summary>
    /// 지도 이동/줌 시 편집 경로 다시 그리기
    /// </summary>
    private void RefreshEditPath()
    {
        if (_editWorldPoints.Count == 0 || _viewModel?.Map == null) return;
        
        // 월드 좌표를 화면 좌표로 다시 변환
        _editScreenPoints.Clear();
        foreach (var worldPt in _editWorldPoints)
        {
            var screenPt = _viewModel.Map.MapToScreen(worldPt);
            _editScreenPoints.Add(new Point(screenPt.X, screenPt.Y));
        }
        
        UpdateEditLine();
    }
    
    #endregion
}
