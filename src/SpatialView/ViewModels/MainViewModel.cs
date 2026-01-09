using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using MessageBox = System.Windows.MessageBox;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using SpatialView.Core.GisEngine;
using SpatialView.Core.Styling;
using SpatialView.Core.Factories;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Core.Services;
using SpatialView.Core.Enums;
using System.Drawing;

namespace SpatialView.ViewModels;

/// <summary>
/// 메인 윈도우의 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IDataLoaderService _dataLoaderService;
    private readonly IProjectService _projectService;
    private readonly ColorPaletteService _colorPaletteService;
    private readonly Core.Factories.IStyleFactory _styleFactory;
    
    /// <summary>
    /// 프로젝트 로드 중 플래그 (자동 줌 비활성화용)
    /// </summary>
    private bool _isLoadingProject = false;
    
    /// <summary>
    /// 현재 열려있는 피처 팝업 창 (하나만 유지)
    /// </summary>
    private Views.FeaturePopupWindow? _featurePopupWindow;
    
    [ObservableProperty]
    private string _title = "SpatialView - 새 프로젝트";

    [ObservableProperty]
    private MapViewModel _mapViewModel;

    [ObservableProperty]
    private LayerPanelViewModel _layerPanelViewModel;
    
    [ObservableProperty]
    private AttributePanelViewModel _attributePanelViewModel;

    [ObservableProperty]
    private bool _isLayerPanelVisible = true;

    [ObservableProperty]
    private bool _isAttributePanelVisible = false;
    
    [ObservableProperty]
    private bool _isLoading = false;
    
    [ObservableProperty]
    private string _statusMessage = "준비";

    /// <summary>
    /// 현재 색상 팔레트
    /// </summary>
    public ColorPaletteService.ColorPalette CurrentColorPalette
    {
        get => _colorPaletteService.CurrentPalette;
        set
        {
            _colorPaletteService.CurrentPalette = value;
            OnPropertyChanged();
            
            // 기존 레이어들에 새 팔레트 색상 적용
            ReapplyColorsToAllLayers();
        }
    }
    
    /// <summary>
    /// 모든 레이어에 현재 팔레트 색상을 다시 적용
    /// </summary>
    public void ReapplyColorsToAllLayers()
    {
        if (LayerPanelViewModel.Layers.Count == 0) return;
        
        _colorPaletteService.ResetIndex();
        
        foreach (var layerItem in LayerPanelViewModel.Layers)
        {
            var newColor = _colorPaletteService.GetNextColor();
            
            // VectorLayer 스타일 업데이트
            if (layerItem.Layer is IVectorLayer vectorLayer)
            {
                ApplyLayerStyle(vectorLayer, newColor, layerItem.GeometryType);
            }
            
            // 색상 변경 (자동으로 UI 갱신됨)
            layerItem.LayerColor = newColor;
        }
        
        MapViewModel.RequestRefresh();
        StatusMessage = $"색상 팔레트 적용됨: {ColorPaletteService.GetPaletteName(CurrentColorPalette)}";
    }
    
    /// <summary>
    /// 사용 가능한 색상 팔레트 목록
    /// </summary>
    public IEnumerable<ColorPaletteService.ColorPalette> AvailableColorPalettes 
        => ColorPaletteService.GetAvailablePalettes();

    public MainViewModel(
        MapViewModel mapViewModel, 
        LayerPanelViewModel layerPanelViewModel,
        AttributePanelViewModel attributePanelViewModel,
        IDataLoaderService dataLoaderService,
        IProjectService projectService,
        Core.Factories.IStyleFactory styleFactory)
    {
        _mapViewModel = mapViewModel;
        _layerPanelViewModel = layerPanelViewModel;
        _attributePanelViewModel = attributePanelViewModel;
        _dataLoaderService = dataLoaderService;
        _projectService = projectService;
        _styleFactory = styleFactory;
        _colorPaletteService = new ColorPaletteService();
        
        // LayerPanel 이벤트 연결
        SetupLayerPanelEvents();
        
        // AttributePanel 이벤트 연결
        SetupAttributePanelEvents();
        
        // 레이어 변경 시 프로젝트 수정 표시
        LayerPanelViewModel.LayerChanged += () => MarkAsModified();
    }
    
    /// <summary>
    /// 프로젝트가 수정됨을 표시
    /// </summary>
    private void MarkAsModified()
    {
        if (_projectService != null)
        {
            _projectService.IsModified = true;
            UpdateTitle();
        }
    }
    
    /// <summary>
    /// 타이틀 바 업데이트
    /// </summary>
    private void UpdateTitle()
    {
        var projectName = _projectService?.CurrentProjectPath != null 
            ? Path.GetFileNameWithoutExtension(_projectService.CurrentProjectPath) 
            : "새 프로젝트";
        
        var modified = _projectService?.IsModified == true ? " *" : "";
        
        Title = $"SpatialView - {projectName}{modified}";
    }
    
    /// <summary>
    /// LayerPanel 이벤트 연결
    /// </summary>
    private void SetupLayerPanelEvents()
    {
        // 레이어 변경 시 Map 갱신
        LayerPanelViewModel.LayerChanged += () => MapViewModel.RequestRefresh();
        
        // 레이어 추가 요청
        LayerPanelViewModel.AddLayerRequested += async () => await OpenFile();
        
        // 레이어 삭제 시 Map에서도 제거
        LayerPanelViewModel.LayerRemoved += OnLayerRemoved;
        
        // Zoom to Layer 요청
        LayerPanelViewModel.ZoomToLayerRequested += OnZoomToLayerRequested;
        
        // 레이어 순서 변경 시 Map 동기화
        LayerPanelViewModel.LayerOrderChanged += SyncMapLayerOrder;
    }
    
    /// <summary>
    /// AttributePanel 이벤트 연결
    /// </summary>
    private void SetupAttributePanelEvents()
    {
        // AttributePanel에 레이어 목록 전달
        AttributePanelViewModel.SetLayers(LayerPanelViewModel.Layers);
        
        // 피처 선택 변경 시 Map에서 Highlight
        AttributePanelViewModel.FeatureSelectionChanged += OnFeatureSelectionChanged;
        
        // 피처로 줌 요청
        AttributePanelViewModel.ZoomToFeatureRequested += OnZoomToFeatureRequested;
        
        // LayerPanel 선택 변경 시 AttributePanel 동기화
        LayerPanelViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LayerPanelViewModel.SelectedLayer))
            {
                if (IsAttributePanelVisible && LayerPanelViewModel.SelectedLayer != null)
                {
                    AttributePanelViewModel.SelectedLayer = LayerPanelViewModel.SelectedLayer;
                }
            }
        };
        
        // 지도에서 피처 선택 시 속성 패널에 표시
        MapViewModel.FeatureSelected += OnMapFeatureSelected;
    }
    
    /// <summary>
    /// 지도에서 피처 선택 시 처리
    /// </summary>
    private void OnMapFeatureSelected(ILayer? layer, List<uint> featureIds)
    {
        if (layer == null || featureIds.Count == 0)
        {
            StatusMessage = "선택된 피처 없음";
            return;
        }
        
        // VectorLayer로 캐스팅
        if (layer is not IVectorLayer vectorLayer)
        {
            StatusMessage = "선택 불가능한 레이어";
            return;
        }
        
        // 첫 번째 피처의 속성을 팝업으로 표시
        ShowFeaturePopup(vectorLayer, featureIds[0]);
        
        StatusMessage = $"{featureIds.Count}개 피처 선택됨 ({layer.Name})";
    }
    
    /// <summary>
    /// 현재 선택된 레이어와 피처 ID (붙여넣기용)
    /// </summary>
    private IVectorLayer? _currentPopupLayer;
    private uint _currentPopupFeatureId;
    
    /// <summary>
    /// 피처 속성 팝업 표시 (하나의 팝업만 유지, 위치 유지)
    /// </summary>
    private void ShowFeaturePopup(IVectorLayer layer, uint featureId)
    {
        try
        {
            // 피처 데이터 가져오기
            if (layer.DataSource is null)
                return;
                
            var provider = layer.DataSource;
            
            provider.Open();
            var feature = provider.GetFeature(featureId);
            provider.Close();
            
            if (feature == null) return;
            
            // 현재 레이어/피처 저장 (붙여넣기용)
            _currentPopupLayer = layer;
            _currentPopupFeatureId = featureId;
            
            // 속성 목록 생성
            var attributes = new List<Views.AttributeItem>();
            foreach (var attributeName in feature.AttributeNames)
            {
                var value = feature.GetAttribute(attributeName);
                attributes.Add(new Views.AttributeItem(attributeName, value));
            }
            
            // 기존 팝업이 있으면 내용만 업데이트 (위치 유지)
            if (_featurePopupWindow != null && _featurePopupWindow.IsVisible)
            {
                // 기존 ViewModel 업데이트
                if (_featurePopupWindow.DataContext is Views.FeaturePopupViewModel existingVm)
                {
                    existingVm.UpdateAttributes(layer.Name, featureId, attributes);
                    
                    // 줌 요청 이벤트 재연결
                    existingVm.ZoomToFeatureRequested -= OnPopupZoomToFeatureRequested;
                    existingVm.ZoomToFeatureRequested += OnPopupZoomToFeatureRequested;
                    existingVm.PasteAttributesRequested -= OnPasteAttributesRequested;
                    existingVm.PasteAttributesRequested += OnPasteAttributesRequested;
                }
                return;
            }
            
            // ViewModel 생성
            var popupViewModel = new Views.FeaturePopupViewModel
            {
                LayerName = layer.Name,
                FeatureId = featureId
            };
            
            // 속성 추가
            foreach (var attr in attributes)
            {
                popupViewModel.Attributes.Add(attr);
            }
            
            // 줌 요청 이벤트 연결
            popupViewModel.ZoomToFeatureRequested += OnPopupZoomToFeatureRequested;
            
            // 붙여넣기 요청 이벤트 연결
            popupViewModel.PasteAttributesRequested += OnPasteAttributesRequested;
            
            // 새 팝업 창 표시
            _featurePopupWindow = new Views.FeaturePopupWindow(popupViewModel);
            
            // 팝업 닫힐 때 참조 해제
            _featurePopupWindow.Closed += (s, e) => 
            {
                _featurePopupWindow = null;
                _currentPopupLayer = null;
            };
            
            _featurePopupWindow.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"피처 팝업 표시 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 팝업에서 줌 요청 핸들러
    /// </summary>
    private void OnPopupZoomToFeatureRequested(uint fid)
    {
        if (_currentPopupLayer != null)
        {
            ZoomToFeature(_currentPopupLayer, fid);
        }
    }
    
    /// <summary>
    /// 속성 붙여넣기 요청 핸들러
    /// </summary>
    private async void OnPasteAttributesRequested(Dictionary<string, string> attributes)
    {
        if (_currentPopupLayer == null)
        {
            System.Windows.MessageBox.Show("붙여넣기 대상 레이어가 없습니다.", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            // 현재 이 기능은 DataTable에만 적용 (실제 파일 수정은 복잡함)
            // 속성 테이블이 없으면 대상 레이어로 로드 시도
            if (AttributePanelViewModel.AttributeTable == null || AttributePanelViewModel.SelectedLayer?.Layer != _currentPopupLayer)
            {
                // 레이어 패널에서 동일 레이어 찾아 선택
                var layerItem = LayerPanelViewModel.Layers.FirstOrDefault(l => ReferenceEquals(l.Layer, _currentPopupLayer));
                if (layerItem != null)
                {
                    AttributePanelViewModel.SelectedLayer = layerItem;
                    var loaded = await AttributePanelViewModel.EnsureAttributeTableAsync(layerItem);
                    if (!loaded)
                    {
                        System.Windows.MessageBox.Show(
                            "속성 테이블을 불러오지 못했습니다. 다시 시도해 주세요.",
                            "알림",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        return;
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "속성 테이블이 로드되지 않았습니다.\n" +
                        "속성 테이블 패널에서 해당 레이어를 선택한 후 다시 시도하세요.", 
                        "알림",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }
            }
            
            var table = AttributePanelViewModel.AttributeTable;
            if (table == null)
            {
                System.Windows.MessageBox.Show(
                    "속성 테이블을 불러오지 못했습니다. 다시 시도해 주세요.",
                    "알림",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }
            bool found = false;
            
            // FID로 행 찾기
            foreach (System.Data.DataRow row in table.Rows)
            {
                if (row.Table.Columns.Contains("FID"))
                {
                    var fid = Convert.ToUInt32(row["FID"]);
                    if (fid == _currentPopupFeatureId)
                    {
                        // 속성 붙여넣기
                        int pastedCount = 0;
                        int failedCount = 0;
                        
                        foreach (var kvp in attributes)
                        {
                            if (table.Columns.Contains(kvp.Key) && kvp.Key != "FID")
                            {
                                try
                                {
                                    var column = table.Columns[kvp.Key];
                                    row[kvp.Key] = Convert.ChangeType(kvp.Value, column!.DataType);
                                    pastedCount++;
                                }
                                catch
                                {
                                    failedCount++;
                                }
                            }
                        }
                        
                        found = true;
                        
                        if (pastedCount > 0)
                        {
                            System.Windows.MessageBox.Show(
                                $"{pastedCount}개 필드가 붙여넣기 되었습니다." + 
                                (failedCount > 0 ? $"\n({failedCount}개 필드는 타입 불일치로 실패)" : ""),
                                "붙여넣기 완료",
                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                            
                            StatusMessage = $"{pastedCount}개 필드 붙여넣기 완료";
                            
                            // 팝업 업데이트
                            ShowFeaturePopup(_currentPopupLayer, _currentPopupFeatureId);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(
                                "일치하는 필드가 없어 붙여넣기 되지 않았습니다.",
                                "알림",
                                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                        break;
                    }
                }
            }
            
            if (!found)
            {
                System.Windows.MessageBox.Show(
                    "속성 테이블에서 해당 피처를 찾을 수 없습니다.",
                    "오류",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"속성 붙여넣기 오류: {ex.Message}");
            System.Windows.MessageBox.Show($"붙여넣기 실패: {ex.Message}", "오류",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            StatusMessage = "속성 붙여넣기 실패";
        }
    }
    
    /// <summary>
    /// 특정 피처로 줌
    /// </summary>
    private void ZoomToFeature(IVectorLayer layer, uint featureId)
    {
        try
        {
            if (layer.DataSource is null)
                return;
                
            var provider = layer.DataSource;
            
            provider.Open();
            var geometry = provider.GetGeometryByID(featureId);
            provider.Close();
            
            if (geometry != null)
            {
                // IGeometry Envelope를 Engine.Geometry.Envelope로 변환
                var geoApiEnvelope = geometry.Envelope;
                var envelope = new Engine.Geometry.Envelope(
                    geoApiEnvelope.MinX, geoApiEnvelope.MaxX,
                    geoApiEnvelope.MinY, geoApiEnvelope.MaxY);
                
                // 피처 크기가 너무 작으면 적당히 확장
                if (envelope.Width < 10 || envelope.Height < 10)
                {
                    envelope.ExpandBy(100);
                }
                else
                {
                    envelope.ExpandBy(envelope.Width * 0.2, envelope.Height * 0.2);
                }
                
                MapViewModel.ZoomToEnvelope(envelope);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"피처 줌 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 피처 선택 변경 처리 (Map Highlight)
    /// </summary>
    private void OnFeatureSelectionChanged(IEnumerable<uint> selectedIds)
    {
        // 속성창에서 선택된 레이어/피처를 지도에 반영
        var targetLayer = AttributePanelViewModel.SelectedLayer?.Layer as ILayer;
        MapViewModel.SelectionTargetLayer = targetLayer;
        MapViewModel.SelectedFeatureIds = selectedIds?.ToList() ?? new List<uint>();

        var count = MapViewModel.SelectedFeatureIds.Count;
        StatusMessage = count > 0 ? $"{count}개 피처 선택됨" : "선택된 피처 없음";
    }
    
    /// <summary>
    /// 피처로 줌 요청 처리
    /// </summary>
    private void OnZoomToFeatureRequested(uint featureId)
    {
        if (AttributePanelViewModel.SelectedLayer?.Layer is IVectorLayer vectorLayer)
        {
            try
            {
                // IDataProvider 인터페이스로 캐스팅하여 GetGeometryByID 호출
                if (vectorLayer.DataSource is not null)
                {
                    var provider = vectorLayer.DataSource;
                    provider.Open();
                    var geometry = provider.GetGeometryByID(featureId);
                    provider.Close();
                    
                    if (geometry != null)
                    {
                        // IGeometry Envelope를 Engine.Geometry.Envelope로 변환
                        var geoApiEnvelope = geometry.Envelope;
                        var envelope = new Engine.Geometry.Envelope(
                            geoApiEnvelope.MinX, geoApiEnvelope.MaxX,
                            geoApiEnvelope.MinY, geoApiEnvelope.MaxY);
                        
                        // 피처 크기가 너무 작으면 적당히 확장
                        if (envelope.Width < 10 || envelope.Height < 10)
                        {
                            envelope.ExpandBy(100);
                        }
                        else
                        {
                            envelope.ExpandBy(envelope.Width * 0.2, envelope.Height * 0.2);
                        }
                        
                        MapViewModel.ZoomToEnvelope(envelope);
                        StatusMessage = $"피처 {featureId}로 이동";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"피처로 줌 실패: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 레이어 제거 처리
    /// </summary>
    private void OnLayerRemoved(LayerItemViewModel layerItem)
    {
        if (layerItem.Layer is ILayer sharpMapLayer)
        {
            MapViewModel.RemoveLayer(sharpMapLayer);
        }
        StatusMessage = $"'{layerItem.Name}' 레이어 제거됨";
    }
    
    /// <summary>
    /// Zoom to Layer 처리
    /// </summary>
    private void OnZoomToLayerRequested(LayerItemViewModel layerItem)
    {
        if (layerItem.Extent != null)
        {
            // layerItem.Extent는 이미 Engine.Geometry.Envelope
            MapViewModel.ZoomToEnvelope(layerItem.Extent);
            StatusMessage = $"'{layerItem.Name}' 범위로 이동";
        }
    }
    
    /// <summary>
    /// Map Layer 순서 동기화
    /// </summary>
    private void SyncMapLayerOrder()
    {
        if (MapViewModel.Map == null) return;
        
        // 배경지도 레이어를 제외한 레이어들의 순서를 동기화
        // LayerPanel의 순서 (위에서 아래)가 Map에서는 나중에 렌더링됨 (위에 표시)
        var map = MapViewModel.Map;
        
        // 배경지도 레이어 찾기 (첫 번째 TileLayer)
        var baseMapLayer = map.Layers.FirstOrDefault(l => l is Core.GisEngine.ITileLayer);
        // ILayerCollection은 IndexOf가 없으므로 수동으로 찾기
        var baseMapIndex = -1;
        if (baseMapLayer != null)
        {
            var index = 0;
            foreach (var layer in map.Layers)
            {
                if (layer == baseMapLayer)
                {
                    baseMapIndex = index;
                    break;
                }
                index++;
            }
        }
        
        // 기존 벡터 레이어들 제거 (배경지도 제외)
        var vectorLayers = map.Layers.Where(l => !(l is Core.GisEngine.ITileLayer)).ToList();
        foreach (var layer in vectorLayers)
        {
            map.Layers.Remove(layer);
        }
        
        // LayerPanel 순서대로 다시 추가 (역순으로 - 아래 레이어가 먼저)
        foreach (var layerItem in LayerPanelViewModel.Layers.Reverse())
        {
            if (layerItem.Layer is ILayer sharpMapLayer)
            {
                map.Layers.Add(sharpMapLayer);
            }
        }
        
        MapViewModel.RequestRefresh();
    }

    [RelayCommand]
    private void ToggleLayerPanel()
    {
        IsLayerPanelVisible = !IsLayerPanelVisible;
    }

    [RelayCommand]
    private void ToggleAttributePanel()
    {
        IsAttributePanelVisible = !IsAttributePanelVisible;
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "공간 데이터 파일 열기",
            Filter = _dataLoaderService.FileDialogFilter,
            Multiselect = true
        };
        
        if (dialog.ShowDialog() == true)
        {
            await LoadFilesAsync(dialog.FileNames);
        }
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        System.Diagnostics.Debug.WriteLine("OpenFolder 시작");
        
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "FileGDB 폴더 (.gdb) 선택",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };
        
        var result = dialog.ShowDialog();
        System.Diagnostics.Debug.WriteLine($"FolderBrowserDialog 결과: {result}");
        
        if (result == System.Windows.Forms.DialogResult.OK)
        {
            var folderPath = dialog.SelectedPath;
            System.Diagnostics.Debug.WriteLine($"선택된 폴더: {folderPath}");
            System.Diagnostics.Debug.WriteLine($"폴더 존재: {Directory.Exists(folderPath)}");
            System.Diagnostics.Debug.WriteLine($".gdb로 끝남: {folderPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase)}");
            
            // GDB 경로 찾기
            var gdbPath = FindGdbPath(folderPath);
            System.Diagnostics.Debug.WriteLine($"FindGdbPath 결과: {gdbPath ?? "null"}");
            
            if (!string.IsNullOrEmpty(gdbPath))
            {
                System.Diagnostics.Debug.WriteLine($"GDB 경로: {gdbPath}");
                // 드래그 앤 드롭과 동일한 방식으로 LoadFilesAsync 사용
                await LoadFilesAsync(new[] { gdbPath });
            }
            else
            {
                MessageBox.Show(
                    $"선택한 폴더가 FileGDB(.gdb)가 아닙니다.\n\n선택된 경로: {folderPath}\n\n.gdb 확장자를 가진 폴더를 선택하세요.",
                    "폴더 선택 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
    
    /// <summary>
    /// 경로에서 GDB 폴더 찾기
    /// </summary>
    private string? FindGdbPath(string path)
    {
        // 1. 선택한 경로가 .gdb로 끝나는 경우
        if (path.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
        {
            return path;
        }
        
        // 2. 상위 폴더가 .gdb인 경우 (폴더 안으로 들어간 경우)
        var parent = Directory.GetParent(path);
        while (parent != null)
        {
            if (parent.FullName.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                return parent.FullName;
            }
            parent = parent.Parent;
        }
        
        // 3. 하위에 .gdb 폴더가 있는 경우
        try
        {
            var gdbFolders = Directory.GetDirectories(path, "*.gdb", SearchOption.TopDirectoryOnly);
            if (gdbFolders.Length > 0)
            {
                return gdbFolders[0];
            }
        }
        catch { }
        
        return null;
    }
    
    /// <summary>
    /// GDB 폴더 직접 로드 (레이어 선택 다이얼로그 표시)
    /// </summary>
    private async Task LoadGdbDirectAsync(string gdbPath)
    {
        System.Diagnostics.Debug.WriteLine($"LoadGdbDirectAsync 시작: {gdbPath}");
        
        IsLoading = true;
        StatusMessage = "FileGDB 로딩 중...";
        
        try
        {
            System.Diagnostics.Debug.WriteLine("LoadGdbWithDialogAsync 호출...");
            var layers = await LoadGdbWithDialogAsync(gdbPath);
            System.Diagnostics.Debug.WriteLine($"LoadGdbWithDialogAsync 완료: {layers.Count}개 레이어");
            
            foreach (var layerInfo in layers)
            {
                System.Diagnostics.Debug.WriteLine($"AddLayerToMap: {layerInfo.Name}");
                AddLayerToMap(layerInfo);
            }
            
            StatusMessage = layers.Count > 0 
                ? $"{layers.Count}개 레이어 로드 완료" 
                : "레이어 선택 취소됨";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadGdbDirectAsync 오류: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            StatusMessage = $"오류: {ex.Message}";
            MessageBox.Show($"FileGDB 로드 오류:\n\n{ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        await SaveProjectAsync(false);
    }
    
    [RelayCommand]
    private async Task SaveProjectAs()
    {
        await SaveProjectAsync(true);
    }
    
    /// <summary>
    /// 프로젝트 저장 (다른 이름으로 저장 옵션)
    /// </summary>
    private async Task SaveProjectAsync(bool saveAs)
    {
        try
        {
            var filePath = _projectService.CurrentProjectPath;
            
            // 새 프로젝트이거나 다른 이름으로 저장인 경우
            if (string.IsNullOrEmpty(filePath) || saveAs)
            {
                var dialog = new SaveFileDialog
                {
                    Title = "프로젝트 저장",
                    Filter = "SpatialView 프로젝트|*.svproj|모든 파일|*.*",
                    DefaultExt = ".svproj",
                    FileName = _projectService.CurrentProjectPath != null 
                        ? Path.GetFileName(_projectService.CurrentProjectPath) 
                        : "project.svproj"
                };
                
                if (dialog.ShowDialog() != true)
                    return;
                
                filePath = dialog.FileName;
            }
            
            IsLoading = true;
            StatusMessage = "프로젝트 저장 중...";
            
            // 프로젝트 데이터 수집
            var project = CollectProjectData(filePath);
            
            // 저장
            await _projectService.SaveProjectAsync(filePath, project);
            
            UpdateTitle();
            StatusMessage = $"프로젝트 저장 완료: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"저장 실패: {ex.Message}";
            MessageBox.Show($"프로젝트 저장 중 오류가 발생했습니다:\n\n{ex.Message}", 
                "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// 현재 상태에서 프로젝트 데이터 수집
    /// </summary>
    private ProjectFile CollectProjectData(string projectFilePath)
    {
        var project = new ProjectFile
        {
            Name = Path.GetFileNameWithoutExtension(projectFilePath),
            MapSettings = new MapSettings
            {
                CenterX = MapViewModel.CenterX,
                CenterY = MapViewModel.CenterY,
                Zoom = MapViewModel.Map?.Zoom ?? 1000000,
                CRS = MapViewModel.CoordinateSystem,
                BaseMapEnabled = MapViewModel.IsBaseMapEnabled,
                BaseMapType = MapViewModel.SelectedBaseMap?.Id
            }
        };
        
        // 레이어 정보 수집
        int order = 0;
        foreach (var layerItem in LayerPanelViewModel.Layers)
        {
            project.Layers.Add(new LayerSettings
            {
                Id = layerItem.Id,
                Name = layerItem.Name,
                SourcePath = _projectService.ToRelativePath(layerItem.FilePath, projectFilePath),
                Visible = layerItem.IsVisible,
                Opacity = layerItem.Opacity,
                Order = order++,
                Color = ColorToHex(layerItem.LayerColor)
            });
        }
        
        return project;
    }
    
    /// <summary>
    /// Color를 Hex 문자열로 변환
    /// </summary>
    private string ColorToHex(System.Drawing.Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }
    
    /// <summary>
    /// Hex 문자열을 Color로 변환
    /// </summary>
    private System.Drawing.Color HexToColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
            return System.Drawing.Color.DodgerBlue;
        
        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch
        {
            return System.Drawing.Color.DodgerBlue;
        }
    }
    
    [RelayCommand]
    private async Task OpenProject()
    {
        // 현재 프로젝트가 수정되었으면 저장 여부 확인
        if (_projectService.IsModified)
        {
            var result = MessageBox.Show(
                "현재 프로젝트가 수정되었습니다.\n저장하시겠습니까?",
                "프로젝트 저장",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Cancel)
                return;
            
            if (result == MessageBoxResult.Yes)
            {
                await SaveProjectAsync(false);
            }
        }
        
        var dialog = new OpenFileDialog
        {
            Title = "프로젝트 열기",
            Filter = "SpatialView 프로젝트|*.svproj|모든 파일|*.*",
            DefaultExt = ".svproj"
        };
        
        if (dialog.ShowDialog() != true)
            return;
        
        await LoadProjectAsync(dialog.FileName);
    }
    
    /// <summary>
    /// 프로젝트 파일 로드
    /// </summary>
    public async Task LoadProjectAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "프로젝트 로딩 중...";
            
            // 기존 레이어 모두 제거
            ClearAllLayers();
            
            // 프로젝트 로드
            var project = await _projectService.LoadProjectAsync(filePath);
            
            // 레이어 로드 (프로젝트 로드 모드 - 자동 줌 비활성화)
            _isLoadingProject = true;
            
            var loadedCount = 0;
            var failedCount = 0;
            var failedLayers = new List<string>();
            
            // FileGDB 레이어들을 GDB 경로별로 그룹화
            var gdbLayerGroups = project.Layers
                .Where(l => l.SourcePath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                .GroupBy(l => _projectService.ToAbsolutePath(l.SourcePath, filePath))
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // 일반 파일 레이어 (Shapefile, GeoJSON 등)
            var nonGdbLayers = project.Layers
                .Where(l => !l.SourcePath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            // 1. FileGDB 레이어 로드 (GDB별로 한 번에 로드)
            foreach (var gdbGroup in gdbLayerGroups)
            {
                var gdbPath = gdbGroup.Key;
                var layerSettingsList = gdbGroup.Value.OrderBy(l => l.Order).ToList();
                
                if (!Directory.Exists(gdbPath))
                {
                    foreach (var ls in layerSettingsList)
                    {
                        failedLayers.Add($"{ls.Name}: 파일을 찾을 수 없음");
                        failedCount++;
                    }
                    continue;
                }
                
                try
                {
                    // FileGDB에서 필요한 레이어들만 이름으로 로드
                    var layerNames = layerSettingsList.Select(l => l.Name).ToList();
                    var gdbProvider = new Infrastructure.DataProviders.FileGdbDataProvider();
                    var layerInfos = await gdbProvider.LoadLayersByNamesAsync(gdbPath, layerNames);
                    
                    // 로드된 레이어에 설정 적용
                    foreach (var layerInfo in layerInfos)
                    {
                        AddLayerToMap(layerInfo);
                        
                        var addedLayer = LayerPanelViewModel.Layers.LastOrDefault();
                        var layerSettings = layerSettingsList.FirstOrDefault(l => 
                            l.Name.Equals(layerInfo.Name, StringComparison.OrdinalIgnoreCase));
                        
                        if (addedLayer != null && layerSettings != null)
                        {
                            var color = HexToColor(layerSettings.Color);
                            addedLayer.LayerColor = color;
                            
                            if (addedLayer.Layer is IVectorLayer vectorLayer)
                            {
                                ApplyLayerStyle(vectorLayer, color, addedLayer.GeometryType);
                            }
                            
                            addedLayer.IsVisible = layerSettings.Visible;
                            addedLayer.Opacity = layerSettings.Opacity;
                        }
                        
                        loadedCount++;
                    }
                    
                    // 로드 실패한 레이어 확인
                    var loadedNames = layerInfos.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var ls in layerSettingsList.Where(l => !loadedNames.Contains(l.Name)))
                    {
                        failedLayers.Add($"{ls.Name}: 레이어를 찾을 수 없음");
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    foreach (var ls in layerSettingsList)
                    {
                        failedLayers.Add($"{ls.Name}: {ex.Message}");
                        failedCount++;
                    }
                }
            }
            
            // 2. 일반 파일 레이어 로드
            foreach (var layerSettings in nonGdbLayers.OrderBy(l => l.Order))
            {
                var absolutePath = _projectService.ToAbsolutePath(layerSettings.SourcePath, filePath);
                
                if (!File.Exists(absolutePath))
                {
                    failedLayers.Add($"{layerSettings.Name}: 파일을 찾을 수 없음");
                    failedCount++;
                    continue;
                }
                
                try
                {
                    var layerInfos = await _dataLoaderService.LoadFilesAsync(new[] { absolutePath });
                    
                    foreach (var layerInfo in layerInfos)
                    {
                        AddLayerToMap(layerInfo);
                        
                        var addedLayer = LayerPanelViewModel.Layers.LastOrDefault();
                        if (addedLayer != null)
                        {
                            var color = HexToColor(layerSettings.Color);
                            addedLayer.LayerColor = color;
                            
                            if (addedLayer.Layer is IVectorLayer vectorLayer)
                            {
                                ApplyLayerStyle(vectorLayer, color, addedLayer.GeometryType);
                            }
                            
                            addedLayer.IsVisible = layerSettings.Visible;
                            addedLayer.Opacity = layerSettings.Opacity;
                        }
                        
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedLayers.Add($"{layerSettings.Name}: {ex.Message}");
                    failedCount++;
                }
            }
            
            // 프로젝트 로드 모드 해제
            _isLoadingProject = false;
            
            // 지도 설정 복원 (레이어 로드 후에 복원해야 줌/중심이 유지됨)
            RestoreMapSettings(project.MapSettings);
            
            // 지도 새로고침
            MapViewModel.RequestRefresh();
            
            UpdateTitle();
            
            if (failedCount > 0)
            {
                StatusMessage = $"프로젝트 로드: {loadedCount}개 성공, {failedCount}개 실패";
                MessageBox.Show(
                    $"일부 레이어를 로드할 수 없습니다:\n\n{string.Join("\n", failedLayers)}",
                    "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                StatusMessage = $"프로젝트 로드 완료: {loadedCount}개 레이어";
            }
        }
        catch (Exception ex)
        {
            _isLoadingProject = false;
            StatusMessage = $"프로젝트 로드 실패: {ex.Message}";
            MessageBox.Show($"프로젝트를 열 수 없습니다:\n\n{ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// 모든 레이어 제거
    /// </summary>
    private void ClearAllLayers()
    {
        foreach (var layerItem in LayerPanelViewModel.Layers.ToList())
        {
            if (layerItem.Layer is ILayer sharpMapLayer)
            {
                MapViewModel.RemoveLayer(sharpMapLayer);
            }
        }
        LayerPanelViewModel.Layers.Clear();
    }
    
    /// <summary>
    /// 지도 설정 복원
    /// </summary>
    private void RestoreMapSettings(MapSettings settings)
    {
        if (MapViewModel.Map == null) return;
        
        // 좌표계 설정
        MapViewModel.CoordinateSystem = settings.CRS;
        
        // 중심 및 줌 설정
        MapViewModel.Map.Center = new Engine.Geometry.Coordinate(settings.CenterX, settings.CenterY);
        MapViewModel.Map.Zoom = settings.Zoom;
        
        // 배경지도 설정
        MapViewModel.IsBaseMapEnabled = settings.BaseMapEnabled;
        if (!string.IsNullOrEmpty(settings.BaseMapType))
        {
            var baseMap = MapViewModel.AvailableBaseMaps.FirstOrDefault(b => b.Id == settings.BaseMapType);
            if (baseMap != null)
            {
                MapViewModel.SelectedBaseMap = baseMap;
            }
        }
        
        MapViewModel.RequestRefresh();
    }
    
    [RelayCommand]
    private async Task NewProject()
    {
        // 현재 프로젝트가 수정되었으면 저장 여부 확인
        if (_projectService.IsModified)
        {
            var result = MessageBox.Show(
                "현재 프로젝트가 수정되었습니다.\n저장하시겠습니까?",
                "프로젝트 저장",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Cancel)
                return;
            
            if (result == MessageBoxResult.Yes)
            {
                await SaveProjectAsync(false);
            }
        }
        
        // 모든 레이어 제거
        ClearAllLayers();
        
        // 새 프로젝트 생성
        _projectService.CreateNewProject();
        
        // 지도 초기화
        MapViewModel.InitializeMap();
        
        UpdateTitle();
        StatusMessage = "새 프로젝트";
    }
    
    /// <summary>
    /// 파일 목록을 로드하여 지도에 추가
    /// </summary>
    public async Task LoadFilesAsync(IEnumerable<string> filePaths)
    {
        IsLoading = true;
        StatusMessage = "파일 로딩 중...";
        
        System.Diagnostics.Debug.WriteLine($"LoadFilesAsync 시작: {string.Join(", ", filePaths)}");
        
        try
        {
            var fileList = filePaths.ToList();
            var layerInfoList = new List<LayerInfo>();
            
            // GDB 폴더와 일반 파일 분리
            var gdbPaths = fileList.Where(f => 
                Directory.Exists(f) && f.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase)).ToList();
            var regularFiles = fileList.Except(gdbPaths).ToList();
            
            // GDB 폴더 처리 - 레이어 선택 다이얼로그 표시
            foreach (var gdbPath in gdbPaths)
            {
                var gdbLayers = await LoadGdbWithDialogAsync(gdbPath);
                layerInfoList.AddRange(gdbLayers);
            }
            
            // 일반 파일 처리
            if (regularFiles.Count > 0)
            {
                var regularLayerInfos = await _dataLoaderService.LoadFilesAsync(regularFiles);
                layerInfoList.AddRange(regularLayerInfos);
            }
            
            System.Diagnostics.Debug.WriteLine($"로드된 레이어 수: {layerInfoList.Count}");
            
            foreach (var layerInfo in layerInfoList)
            {
                System.Diagnostics.Debug.WriteLine($"레이어 추가: {layerInfo.Name}, 피처 수: {layerInfo.FeatureCount}");
                AddLayerToMap(layerInfo);
            }
            
            var count = layerInfoList.Count;
            
            // 일부 성공, 일부 실패한 경우
            if (_dataLoaderService is Infrastructure.Services.DataLoaderService loaderService && loaderService.LastErrors.Count > 0)
            {
                var errorMsg = string.Join("\n", loaderService.LastErrors);
                StatusMessage = $"{count}개 로드, {loaderService.LastErrors.Count}개 실패";
                MessageBox.Show($"일부 파일을 로드할 수 없습니다:\n\n{errorMsg}", 
                    "경고", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            else
            {
                StatusMessage = count > 0 
                    ? $"{count}개 레이어 로드 완료" 
                    : "로드된 레이어 없음";
            }
                
            if (count == 0 && (_dataLoaderService is not Infrastructure.Services.DataLoaderService || 
                ((Infrastructure.Services.DataLoaderService)_dataLoaderService).LastErrors.Count == 0))
            {
                // GDB 다이얼로그에서 취소한 경우는 메시지 표시하지 않음
                if (gdbPaths.Count == 0)
                {
                    MessageBox.Show("로드된 레이어가 없습니다.\n파일이 올바른 형식인지 확인해 주세요.", 
                        "알림", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"오류: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"파일 로드 오류: {ex}");
            
            var message = ex.Message;
            if (ex.InnerException != null)
            {
                message += $"\n\n내부 오류: {ex.InnerException.Message}";
            }
            
            MessageBox.Show($"파일 로드 중 오류가 발생했습니다:\n\n{message}", 
                "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// 단일 파일 로드
    /// </summary>
    public async Task LoadFileAsync(string filePath)
    {
        await LoadFilesAsync(new[] { filePath });
    }
    
    /// <summary>
    /// FileGDB 폴더를 열고 레이어 선택 다이얼로그 표시
    /// </summary>
    private async Task<List<LayerInfo>> LoadGdbWithDialogAsync(string gdbPath)
    {
        var results = new List<LayerInfo>();
        
        try
        {
            // FileGdbDataProvider 인스턴스 생성
            var gdbProvider = new Infrastructure.DataProviders.FileGdbDataProvider();
            
            // GDB 내 레이어 목록 가져오기
            var layers = gdbProvider.GetLayersInfo(gdbPath);
            
            if (layers.Count == 0)
            {
                MessageBox.Show($"FileGDB에 레이어가 없습니다:\n{gdbPath}",
                    "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return results;
            }
            
            System.Diagnostics.Debug.WriteLine($"GDB 레이어 수: {layers.Count}");
            foreach (var layer in layers)
            {
                System.Diagnostics.Debug.WriteLine($"  - {layer.Name} ({layer.FeatureCount} features, {layer.GeometryType})");
            }
            
            // 레이어 선택 다이얼로그 표시
            var dialog = new Views.Dialogs.GdbLayerSelectDialog(gdbPath, layers);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            
            if (dialog.ShowDialog() == true && dialog.SelectedLayerIndices.Length > 0)
            {
                var totalLayers = dialog.SelectedLayerIndices.Length;
                
                // 각 레이어를 개별적으로 로드하여 진행 상황 표시
                for (int i = 0; i < totalLayers; i++)
                {
                    var layerIndex = dialog.SelectedLayerIndices[i];
                    var layerName = layers[layerIndex].Name;
                    
                    StatusMessage = $"GDB 레이어 로딩 중... ({i + 1}/{totalLayers}) - {layerName}";
                    
                    try
                    {
                        var layerInfo = await gdbProvider.LoadLayerAsync(gdbPath, layerIndex);
                        results.Add(layerInfo);
                        System.Diagnostics.Debug.WriteLine($"레이어 로드 완료: {layerName}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"레이어 '{layerName}' 로드 실패: {ex.Message}");
                    }
                    
                    // UI 업데이트를 위해 잠시 양보
                    await Task.Delay(10);
                }
                
                System.Diagnostics.Debug.WriteLine($"GDB에서 {results.Count}개 레이어 로드됨");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GDB 로드 오류: {ex.Message}");
            MessageBox.Show($"FileGDB를 열 수 없습니다:\n\n{ex.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        return results;
    }
    
    /// <summary>
    /// LayerInfo를 지도와 레이어 패널에 추가
    /// </summary>
    private void AddLayerToMap(LayerInfo layerInfo)
    {
        // 색상 가져오기
        var layerColor = _colorPaletteService.GetNextColor();
        
        // VectorLayer에 스타일 적용
        if (layerInfo.Layer is IVectorLayer vectorLayer)
        {
            ApplyLayerStyle(vectorLayer, layerColor, layerInfo.GeometryType);
        }
        
        // LayerItemViewModel 생성
        var layerItem = new LayerItemViewModel
        {
            Id = layerInfo.Id,
            Name = layerInfo.Name,
            FilePath = layerInfo.FilePath,
            GeometryType = layerInfo.GeometryType,
            FeatureCount = layerInfo.FeatureCount,
            Extent = layerInfo.Extent,
            Crs = layerInfo.CRS,
            Layer = layerInfo.Layer as SpatialView.Core.GisEngine.ILayer,
            IsVisible = true,
            Opacity = 1.0,
            LayerColor = layerColor  // 색상 저장
        };
        
        // 레이어 패널에 추가 (이벤트 연결 포함)
        LayerPanelViewModel.AddLayer(layerItem);
        
        // 지도에 레이어 추가
        if (layerInfo.Layer is ILayer sharpMapLayer)
        {
            // 빈 레이어는 UI에서만 표시 (Enabled는 유지하여 Extent 계산에 포함)
            if (layerInfo.FeatureCount == 0)
            {
                // Enabled는 true로 유지 (Extent 계산에 포함되도록)
                // 렌더링은 피처가 없으면 자동으로 스킵됨
                layerItem.IsVisible = true;  // UI에서는 보이도록
                System.Diagnostics.Debug.WriteLine($"빈 레이어 감지: {layerInfo.Name} - Extent는 유지");
            }
            
            MapViewModel.AddLayer(sharpMapLayer);
            
            // 레이어 추가 시 자동 줌 (프로젝트 로드 시에는 저장된 설정을 사용하므로 건너뜀)
            if (!_isLoadingProject)
            {
                // 첫 번째 레이어면 좌표계 설정
                if (LayerPanelViewModel.Layers.Count == 1)
                {
                    MapViewModel.UpdateCoordinateSystem(layerInfo.CRS);
                }
                
                // 유효한 Extent가 있으면 전체 범위로 줌
                if (layerInfo.Extent != null && !layerInfo.Extent.IsNull)
                {
                    // Dispatcher를 통해 렌더링 완료 후 줌 실행
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => MapViewModel.ZoomToExtentCommand.Execute(null)));
                }
            }
        }
    }
    
    /// <summary>
    /// VectorLayer에 스타일 적용
    /// </summary>
    private void ApplyLayerStyle(IVectorLayer layer, Color color, Core.Enums.GeometryType geometryType)
    {
        // DI를 통해 주입된 StyleFactory 사용
        var style = _styleFactory.CreateVectorStyle();
        
        // System.Drawing.Color를 System.Windows.Media.Color로 변환
        var mediaColor = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
        
        // 채우기 색상 (반투명)
        var fillColor = System.Windows.Media.Color.FromArgb(128, color.R, color.G, color.B);
        style.Fill = fillColor;
        
        // 외곽선 색상
        style.Outline = mediaColor;
        style.OutlineWidth = 1.5f;
        style.EnableOutline = true;
        
        // 라인 두께 (선 색상은 Outline과 공유)
        style.LineWidth = 2.0f;
        
        // 포인트 크기
        style.PointSize = 8;
        
        // 지오메트리 타입에 따라 스타일 조정
        switch (geometryType)
        {
            case GeometryType.Point:
            case GeometryType.MultiPoint:
                style.PointSize = 10;
                break;
                
            case GeometryType.Line:
            case GeometryType.LineString:
            case GeometryType.MultiLineString:
                style.LineWidth = 2.5f;
                break;
                
            case GeometryType.Polygon:
            case GeometryType.MultiPolygon:
                // 기본 설정 유지
                break;
        }
        
        layer.Style = style;
        
        System.Diagnostics.Debug.WriteLine($"레이어 스타일 적용: {layer.Name}, 색상: #{color.R:X2}{color.G:X2}{color.B:X2}");
    }
    
    /// <summary>
    /// 파일을 로드할 수 있는지 확인
    /// </summary>
    public bool CanLoadFile(string filePath)
    {
        return _dataLoaderService.CanLoad(filePath);
    }
}
