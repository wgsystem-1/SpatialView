using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SpatialView.ViewModels;
using System.Linq;
using System.Threading.Tasks;

// WPF 타입 명시적 사용 (Windows Forms과 충돌 방지)
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using DataObject = System.Windows.DataObject;

namespace SpatialView;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Map -> Attribute 포커스 요청 구독
        _viewModel.MapViewModel.FocusAttributeTableRequested += OnFocusAttributeTableRequested;
        
        // Drag & Drop 이벤트 등록
        AllowDrop = true;
        DragEnter += MainWindow_DragEnter;
        DragOver += MainWindow_DragOver;
        DragLeave += MainWindow_DragLeave;
        Drop += MainWindow_Drop;
    }

    private async void OnFocusAttributeTableRequested(Core.GisEngine.ILayer? layer, uint fid)
    {
        try
        {
            // 속성 패널 표시
            _viewModel.IsAttributePanelVisible = true;

            // 레이어 찾기
            var layerItem = _viewModel.LayerPanelViewModel.Layers
                .FirstOrDefault(l => ReferenceEquals(l.Layer, layer))
                ?? _viewModel.LayerPanelViewModel.SelectedLayer;

            if (layerItem == null)
                return;

            // 레이어 선택 및 테이블 로드
            _viewModel.AttributePanelViewModel.SelectedLayer = layerItem;
            await _viewModel.AttributePanelViewModel.EnsureAttributeTableAsync(layerItem);

            // 테이블에서 FID 포커스
            AttributePanelControl?.SelectRowByFid(fid);
        }
        catch
        {
            // 예외는 무시 (UI 편의 기능)
        }
    }
    
    /// <summary>
    /// DragEnter 이벤트 처리 - 유효한 파일인지 확인
    /// </summary>
    private void MainWindow_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Any(f => _viewModel.CanLoadFile(f)))
            {
                e.Effects = DragDropEffects.Copy;
                ShowDropOverlay(true);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        
        e.Handled = true;
    }
    
    /// <summary>
    /// DragOver 이벤트 처리
    /// </summary>
    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Any(f => _viewModel.CanLoadFile(f)))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        
        e.Handled = true;
    }
    
    /// <summary>
    /// DragLeave 이벤트 처리
    /// </summary>
    private void MainWindow_DragLeave(object sender, DragEventArgs e)
    {
        ShowDropOverlay(false);
    }
    
    /// <summary>
    /// Drop 이벤트 처리 - 파일 로드
    /// </summary>
    private async void MainWindow_Drop(object sender, DragEventArgs e)
    {
        ShowDropOverlay(false);
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null)
            {
                // 로드 가능한 파일만 필터링
                var loadableFiles = files.Where(f => _viewModel.CanLoadFile(f)).ToArray();
                
                if (loadableFiles.Length > 0)
                {
                    await _viewModel.LoadFilesAsync(loadableFiles);
                }
                else
                {
                    MessageBox.Show(
                        "지원하지 않는 파일 형식입니다.\n\n지원 형식:\n- Shapefile (.shp)\n- GeoJSON (.geojson, .json)\n- FileGDB (.gdb 폴더)",
                        "파일 열기 오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }
        
        e.Handled = true;
    }
    
    /// <summary>
    /// Drop Overlay 표시/숨김
    /// </summary>
    private void ShowDropOverlay(bool show)
    {
        if (FindName("DropOverlay") is Border dropOverlay)
        {
            dropOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    
    /// <summary>
    /// 레이어 메뉴 버튼 클릭 - Context Menu 표시
    /// </summary>
    private void LayerMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }
    
    /// <summary>
    /// 레이어 리스트 선택 변경 - 다중 선택 동기화
    /// </summary>
    private void LayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox)
        {
            _viewModel.LayerPanelViewModel.SelectedLayers.Clear();
            
            foreach (var item in listBox.SelectedItems)
            {
                if (item is LayerItemViewModel layer)
                {
                    _viewModel.LayerPanelViewModel.SelectedLayers.Add(layer);
                }
            }
        }
    }
    
    /// <summary>
    /// 레이어 리스트 키 입력 - Delete 키로 삭제
    /// </summary>
    private void LayerListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete)
        {
            if (_viewModel.LayerPanelViewModel.SelectedLayers.Count > 0)
            {
                var count = _viewModel.LayerPanelViewModel.SelectedLayers.Count;
                var result = MessageBox.Show(
                    $"{count}개의 레이어를 삭제하시겠습니까?",
                    "레이어 삭제",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.LayerPanelViewModel.RemoveSelectedLayersCommand.Execute(null);
                }
            }
            e.Handled = true;
        }
    }
    
    #region Context Menu 이벤트 핸들러
    
    private void SetOpacity100_Click(object sender, RoutedEventArgs e) => SetSelectedLayersOpacity(1.0);
    private void SetOpacity75_Click(object sender, RoutedEventArgs e) => SetSelectedLayersOpacity(0.75);
    private void SetOpacity50_Click(object sender, RoutedEventArgs e) => SetSelectedLayersOpacity(0.5);
    private void SetOpacity25_Click(object sender, RoutedEventArgs e) => SetSelectedLayersOpacity(0.25);
    private void SetOpacity10_Click(object sender, RoutedEventArgs e) => SetSelectedLayersOpacity(0.1);
    
    /// <summary>
    /// 선택된 레이어로 줌
    /// </summary>
    private void ZoomToSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LayerPanelViewModel.SelectedLayer != null)
        {
            _viewModel.LayerPanelViewModel.ZoomToLayerCommand.Execute(null);
        }
    }
    
    /// <summary>
    /// 선택된 레이어 삭제
    /// </summary>
    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var count = _viewModel.LayerPanelViewModel.SelectedLayers.Count;
        if (count == 0 && _viewModel.LayerPanelViewModel.SelectedLayer != null)
            count = 1;
            
        if (count > 0)
        {
            var result = MessageBox.Show(
                $"{count}개의 레이어를 삭제하시겠습니까?",
                "레이어 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.LayerPanelViewModel.RemoveSelectedLayersCommand.Execute(null);
            }
        }
    }
    
    /// <summary>
    /// 선택된 레이어들의 투명도 일괄 설정
    /// </summary>
    private void SetSelectedLayersOpacity(double opacity)
    {
        _viewModel.LayerPanelViewModel.SetSelectedLayersOpacityCommand.Execute(opacity);
        
        var count = _viewModel.LayerPanelViewModel.SelectedLayers.Count;
        if (count == 0 && _viewModel.LayerPanelViewModel.SelectedLayer != null)
            count = 1;
            
        _viewModel.StatusMessage = $"{count}개 레이어 투명도 {(int)(opacity * 100)}%로 설정";
    }
    
    /// <summary>
    /// 선택된 레이어들 표시/숨김 토글
    /// </summary>
    private void ToggleVisibility_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LayerPanelViewModel.ToggleSelectedLayersVisibilityCommand.Execute(null);
    }
    
    private void OpenLayerProperties_Click(object sender, RoutedEventArgs e)
    {
        // 우클릭 컨텍스트 메뉴에서 속성창 열기
        var layer = _viewModel.LayerPanelViewModel.SelectedLayer;
        if (layer == null) return;

        _viewModel.IsAttributePanelVisible = true;
        _viewModel.AttributePanelViewModel.SelectedLayer = layer;
        _viewModel.StatusMessage = $"'{layer.Name}' 속성 열기";
    }
    
    /// <summary>
    /// 라벨 설정 다이얼로그 열기
    /// </summary>
    private void LabelSettings_Click(object sender, RoutedEventArgs e)
    {
        var layer = _viewModel.LayerPanelViewModel.SelectedLayer;
        if (layer == null)
        {
            // 컨텍스트 메뉴에서 호출된 경우 해당 레이어 찾기
            if (sender is System.Windows.Controls.MenuItem menuItem)
            {
                var contextMenu = menuItem.Parent as System.Windows.Controls.ContextMenu;
                if (contextMenu?.PlacementTarget is System.Windows.Controls.Button button)
                {
                    layer = button.DataContext as LayerItemViewModel;
                }
            }
        }
        
        if (layer == null)
        {
            MessageBox.Show("레이어를 선택해주세요.", "라벨 설정", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new Views.Dialogs.LabelSettingsDialog(layer)
        {
            Owner = this
        };
        
        if (dialog.ShowDialog() == true)
        {
            // 맵 새로고침
            _viewModel.MapViewModel.RequestRefresh();
            _viewModel.StatusMessage = $"'{layer.Name}' 라벨 설정 적용됨";
        }
    }
    
    /// <summary>
    /// 레이어 스타일 설정 다이얼로그 열기
    /// </summary>
    private void LayerStyleSettings_Click(object sender, RoutedEventArgs e)
    {
        var layer = _viewModel.LayerPanelViewModel.SelectedLayer;
        if (layer == null)
        {
            // 컨텍스트 메뉴에서 호출된 경우 해당 레이어 찾기
            if (sender is System.Windows.Controls.MenuItem menuItem)
            {
                var contextMenu = menuItem.Parent as System.Windows.Controls.ContextMenu;
                if (contextMenu?.PlacementTarget is System.Windows.Controls.Button button)
                {
                    layer = button.DataContext as LayerItemViewModel;
                }
            }
        }
        
        if (layer == null)
        {
            MessageBox.Show("레이어를 선택해주세요.", "스타일 설정", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new Views.Dialogs.LayerStyleDialog(layer)
        {
            Owner = this
        };
        
        if (dialog.ShowDialog() == true)
        {
            // 맵 새로고침
            _viewModel.MapViewModel.RequestRefresh();
            _viewModel.StatusMessage = $"'{layer.Name}' 스타일 설정 적용됨";
        }
    }

    #endregion
    
    #region 색상 팔레트 이벤트 핸들러
    
    private void SetPaletteVivid_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Vivid;
        _viewModel.StatusMessage = "색상 팔레트: 선명한 색상";
    }
    
    private void SetPalettePastel_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Pastel;
        _viewModel.StatusMessage = "색상 팔레트: 파스텔";
    }
    
    private void SetPaletteEarth_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Earth;
        _viewModel.StatusMessage = "색상 팔레트: 대지/자연";
    }
    
    private void SetPaletteOcean_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Ocean;
        _viewModel.StatusMessage = "색상 팔레트: 바다/파랑";
    }
    
    private void SetPaletteWarm_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Warm;
        _viewModel.StatusMessage = "색상 팔레트: 따뜻한 색상";
    }
    
    private void SetPaletteCool_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Cool;
        _viewModel.StatusMessage = "색상 팔레트: 차가운 색상";
    }
    
    private void SetPaletteRainbow_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Rainbow;
        _viewModel.StatusMessage = "색상 팔레트: 무지개";
    }
    
    private void SetPaletteGrayscale_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Grayscale;
        _viewModel.StatusMessage = "색상 팔레트: 회색조";
    }
    
    private void SetPaletteNegative_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentColorPalette = SpatialView.Core.Services.ColorPaletteService.ColorPalette.Negative;
        _viewModel.StatusMessage = "색상 팔레트: 네거티브";
    }
    
    #endregion
    
    #region 지도 도구 핸들러
    
    /// <summary>
    /// 줌 윈도우 도구 클릭
    /// </summary>
    private void ZoomWindowTool_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("ZoomWindowTool_Click 호출됨");
        _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.ZoomWindow;
        _viewModel.StatusMessage = "선택 영역 확대 - 드래그하여 영역을 선택하세요";
        UpdateToolButtonStyles();
    }
    
    /// <summary>
    /// 피처 선택 도구 클릭
    /// </summary>
    private void SelectTool_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"SelectTool_Click 호출됨, 이전 ActiveTool={_viewModel.MapViewModel.ActiveTool}");
        _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Select;
        System.Diagnostics.Debug.WriteLine($"SelectTool_Click 완료, 현재 ActiveTool={_viewModel.MapViewModel.ActiveTool}");
        _viewModel.StatusMessage = "피처 선택 - 지도에서 피처를 클릭하세요";
        UpdateToolButtonStyles();
    }
    
    /// <summary>
    /// 이동 도구 클릭
    /// </summary>
    private void PanTool_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("PanTool_Click 호출됨");
        _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Pan;
        _viewModel.StatusMessage = "이동 모드";
        UpdateToolButtonStyles();
    }
    
    /// <summary>
    /// 도구 버튼 스타일 업데이트 (활성화 상태 표시)
    /// </summary>
    private void UpdateToolButtonStyles()
    {
        var activeTool = _viewModel.MapViewModel.ActiveTool;
        var activeColor = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(60, 33, 150, 243)); // 반투명 파란색
        var normalColor = System.Windows.Media.Brushes.Transparent;
        
        ZoomWindowToolButton.Background = activeTool == Core.GisEngine.MapTool.ZoomWindow ? activeColor : normalColor;
        SelectToolButton.Background = activeTool == Core.GisEngine.MapTool.Select ? activeColor : normalColor;
        PanToolButton.Background = activeTool == Core.GisEngine.MapTool.Pan ? activeColor : normalColor;
        
        System.Diagnostics.Debug.WriteLine($"UpdateToolButtonStyles: ActiveTool={activeTool}");
    }
    
    #endregion
    
    #region 새 도구 핸들러
    
    private Views.Dialogs.MeasurementDialog? _measurementDialog;
    
    /// <summary>
    /// 측정 도구 열기
    /// </summary>
    private void OpenMeasurementTool_Click(object sender, RoutedEventArgs e)
    {
        // 이미 열려있으면 포커스
        if (_measurementDialog != null && _measurementDialog.IsVisible)
        {
            _measurementDialog.Activate();
            return;
        }
        
        var srid = _viewModel.MapViewModel.Map?.SRID ?? 4326;
        _measurementDialog = new Views.Dialogs.MeasurementDialog(srid);
        _measurementDialog.Owner = this;
        
        // 측정 이벤트 연결
        _measurementDialog.MeasurementStarted += (isDistance) =>
        {
            _viewModel.StatusMessage = isDistance ? "거리 측정 모드 - 지도에서 클릭하세요" : "면적 측정 모드 - 지도에서 클릭하세요";
            // 측정 도구 활성화
            _viewModel.MapViewModel.ActiveTool = isDistance 
                ? Core.GisEngine.MapTool.MeasureDistance 
                : Core.GisEngine.MapTool.MeasureArea;
        };
        
        _measurementDialog.MeasurementEnded += () =>
        {
            _viewModel.StatusMessage = "측정 완료";
            _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Pan;
            _measurementDialog = null;
        };
        
        _measurementDialog.Closed += (s, args) =>
        {
            _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Pan;
            _measurementDialog = null;
            
            // 측정 경로 초기화
            var mapControl = FindVisualChild<Views.Controls.MapControl>(this);
            mapControl?.ClearMeasurePath();
        };
        
        _measurementDialog.Show();
    }
    
    /// <summary>
    /// 측정 다이얼로그에 포인트 추가 (MapControl에서 호출)
    /// </summary>
    public void AddMeasurementPoint(double x, double y)
    {
        _measurementDialog?.AddPoint(x, y);
    }
    
    /// <summary>
    /// 좌표계 변환 도구 열기
    /// </summary>
    private void OpenCoordinateTransform_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.Dialogs.CoordinateTransformDialog(_viewModel);
        dialog.Owner = this;
        dialog.ShowDialog();
    }
    
    /// <summary>
    /// 내보내기 도구 열기
    /// </summary>
    private void OpenExportDialog_Click(object sender, RoutedEventArgs e)
    {
        // MapControl에서 Canvas 가져오기
        var mapControl = FindVisualChild<Views.Controls.MapControl>(this);
        if (mapControl == null)
        {
            MessageBox.Show("지도 컨트롤을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        var canvas = FindVisualChild<System.Windows.Controls.Canvas>(mapControl);
        if (canvas == null)
        {
            MessageBox.Show("지도 캔버스를 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        var dialog = new Views.Dialogs.ExportDialog(canvas);
        dialog.Owner = this;
        dialog.ShowDialog();
    }
    
    /// <summary>
    /// 고급 필터 도구 열기
    /// </summary>
    private void OpenAdvancedFilter_Click(object sender, RoutedEventArgs e)
    {
        var attributeTable = _viewModel.AttributePanelViewModel.AttributeTable;
        if (attributeTable == null)
        {
            MessageBox.Show("속성 테이블을 먼저 로드하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new Views.Dialogs.AdvancedFilterDialog(attributeTable);
        dialog.Owner = this;
        
        dialog.FilterApplied += (filter) =>
        {
            _viewModel.StatusMessage = string.IsNullOrEmpty(filter) 
                ? "필터 해제됨" 
                : $"필터 적용됨: {dialog.FilteredCount}개 피처";
        };
        
        dialog.Show();
    }
    
    private Views.Dialogs.EditToolsDialog? _editToolsDialog;
    
    /// <summary>
    /// 편집 도구 열기
    /// </summary>
    private void OpenEditTools_Click(object sender, RoutedEventArgs e)
    {
        // 이미 열려있으면 포커스
        if (_editToolsDialog != null && _editToolsDialog.IsVisible)
        {
            _editToolsDialog.Activate();
            return;
        }
        
        var layers = _viewModel.LayerPanelViewModel.Layers.ToList();
        if (layers.Count == 0)
        {
            MessageBox.Show("편집할 레이어가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        _editToolsDialog = new Views.Dialogs.EditToolsDialog(layers);
        _editToolsDialog.Owner = this;
        
        _editToolsDialog.EditModeChanged += (mode, geometryType) =>
        {
            _viewModel.StatusMessage = mode switch
            {
                Engine.Editing.EditMode.Create => $"{geometryType} 생성 모드 - 지도에서 클릭하세요",
                Engine.Editing.EditMode.Modify => "수정 모드 - 피처를 클릭하세요",
                Engine.Editing.EditMode.Delete => "삭제 모드 - 삭제할 피처를 클릭하세요",
                _ => "편집 모드 해제"
            };
            
            // 편집 모드에 따라 MapTool 변경
            if (mode != Engine.Editing.EditMode.None)
            {
                _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Edit;
            }
            else
            {
                _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Pan;
            }
        };
        
        _editToolsDialog.EditCompleted += () =>
        {
            // 편집 경로 초기화
            MapControlInstance?.ClearEditPath();
            _viewModel.MapViewModel.RequestRefresh();
        };
        
        _editToolsDialog.Closed += (s, args) =>
        {
            // 편집 경로 초기화
            MapControlInstance?.ClearEditPath();
            _viewModel.MapViewModel.ActiveTool = Core.GisEngine.MapTool.Pan;
            _editToolsDialog = null;
        };
        
        _editToolsDialog.Show();
    }
    
    /// <summary>
    /// 편집 다이얼로그에 포인트 추가 (MapControl에서 호출)
    /// </summary>
    public void AddEditPoint(double x, double y)
    {
        _editToolsDialog?.AddPoint(x, y);
    }
    
    /// <summary>
    /// 편집 완료 (MapControl에서 호출 - 더블클릭)
    /// </summary>
    public void CompleteEdit()
    {
        _editToolsDialog?.CompleteCreate();
        // 편집 경로 초기화
        MapControlInstance?.ClearEditPath();
    }
    
    /// <summary>
    /// MapControl 인스턴스 가져오기
    /// </summary>
    private Views.Controls.MapControl? MapControlInstance => 
        FindVisualChild<Views.Controls.MapControl>(this);
    
    /// <summary>
    /// VisualTree에서 자식 요소 찾기
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            
            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
    
    #endregion
    
    #region 레이어 드래그 앤 드롭
    
    private Point _dragStartPoint;
    private bool _isDragging = false;
    
    /// <summary>
    /// 마우스 버튼 누름 - 드래그 시작점 기록
    /// </summary>
    private void LayerListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }
    
    /// <summary>
    /// 마우스 이동 - 드래그 시작 판단
    /// </summary>
    private void LayerListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isDragging = false;
            return;
        }
        
        Point currentPosition = e.GetPosition(null);
        Vector diff = _dragStartPoint - currentPosition;
        
        // 최소 드래그 거리 체크
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            if (_isDragging) return;
            
            var listBox = sender as ListBox;
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            
            if (listBox == null || listBoxItem == null) return;
            
            var item = listBox.ItemContainerGenerator.ItemFromContainer(listBoxItem) as LayerItemViewModel;
            if (item == null) return;
            
            _isDragging = true;
            
            // 드래그 데이터 설정
            var dragData = new DataObject("LayerItem", item);
            DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Move);
            
            _isDragging = false;
        }
    }
    
    /// <summary>
    /// 드래그 오버 - 드롭 위치 표시
    /// </summary>
    private void LayerListBox_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("LayerItem"))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }
    
    /// <summary>
    /// 드롭 - 레이어 순서 변경
    /// </summary>
    private void LayerListBox_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("LayerItem")) return;
        
        var droppedData = e.Data.GetData("LayerItem") as LayerItemViewModel;
        if (droppedData == null) return;
        
        var listBox = sender as ListBox;
        if (listBox == null) return;
        
        // 드롭 위치의 아이템 찾기
        var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        LayerItemViewModel? targetData = null;
        
        if (targetItem != null)
        {
            targetData = listBox.ItemContainerGenerator.ItemFromContainer(targetItem) as LayerItemViewModel;
        }
        
        // 인덱스 계산
        int oldIndex = _viewModel.LayerPanelViewModel.Layers.IndexOf(droppedData);
        int newIndex = targetData != null 
            ? _viewModel.LayerPanelViewModel.Layers.IndexOf(targetData) 
            : _viewModel.LayerPanelViewModel.Layers.Count - 1;
        
        if (oldIndex != newIndex && oldIndex >= 0 && newIndex >= 0)
        {
            _viewModel.LayerPanelViewModel.MoveLayer(oldIndex, newIndex);
            _viewModel.StatusMessage = $"'{droppedData.Name}' 레이어 순서 변경됨";
        }
        
        e.Handled = true;
    }
    
    /// <summary>
    /// VisualTree에서 부모 요소 찾기
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
                return ancestor;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
    
    #endregion
}
