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
