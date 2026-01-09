using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SpatialView.ViewModels;

/// <summary>
/// 레이어 패널의 ViewModel
/// </summary>
public partial class LayerPanelViewModel : ObservableObject
{
    
    [ObservableProperty]
    private ObservableCollection<LayerItemViewModel> _layers = new();

    [ObservableProperty]
    private LayerItemViewModel? _selectedLayer;
    
    /// <summary>
    /// 다중 선택된 레이어들
    /// </summary>
    public ObservableCollection<LayerItemViewModel> SelectedLayers { get; } = new();

    public LayerPanelViewModel()
    {
        Layers.CollectionChanged += Layers_CollectionChanged;
    }
    
    /// <summary>
    /// MapViewModel 설정 (DI 후 연결)
    /// </summary>
    public void SetMapViewModel(MapViewModel mapViewModel)
    {
        // 기존에는 readonly였지만, DI 순환 참조 문제로 나중에 설정
    }

    /// <summary>
    /// 레이어 추가
    /// </summary>
    public void AddLayer(LayerItemViewModel layerItem)
    {
        // 이벤트 연결
        layerItem.LayerChanged += OnLayerChanged;
        layerItem.ZoomToLayerRequested += OnZoomToLayerRequested;
        layerItem.RemoveRequested += OnRemoveRequested;
        
        Layers.Add(layerItem);
    }
    
    /// <summary>
    /// 컬렉션 변경 시 처리
    /// </summary>
    private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasLayers));
        OnPropertyChanged(nameof(IsEmpty));
    }
    
    /// <summary>
    /// 레이어가 있는지 여부
    /// </summary>
    public bool HasLayers => Layers.Count > 0;
    
    /// <summary>
    /// 레이어가 비어있는지 여부
    /// </summary>
    public bool IsEmpty => Layers.Count == 0;

    /// <summary>
    /// 레이어 변경 이벤트 (Map 갱신용)
    /// </summary>
    public event Action? LayerChanged;
    
    /// <summary>
    /// Zoom to Layer 이벤트
    /// </summary>
    public event Action<LayerItemViewModel>? ZoomToLayerRequested;
    
    private void OnLayerChanged()
    {
        LayerChanged?.Invoke();
    }
    
    private void OnZoomToLayerRequested(LayerItemViewModel layer)
    {
        ZoomToLayerRequested?.Invoke(layer);
    }
    
    private void OnRemoveRequested(LayerItemViewModel layer)
    {
        RemoveLayerInternal(layer);
    }

    [RelayCommand]
    private void AddLayer()
    {
        // MainViewModel에서 OpenFile을 트리거해야 함
        // 여기서는 이벤트만 발생
        AddLayerRequested?.Invoke();
    }
    
    public event Action? AddLayerRequested;

    [RelayCommand]
    private void RemoveLayer()
    {
        if (SelectedLayer != null)
        {
            RemoveLayerInternal(SelectedLayer);
        }
    }
    
    /// <summary>
    /// 선택된 모든 레이어 삭제
    /// </summary>
    [RelayCommand]
    private void RemoveSelectedLayers()
    {
        if (SelectedLayers.Count == 0) return;
        
        // 복사본을 만들어서 순회 (컬렉션 수정 중 오류 방지)
        var layersToRemove = SelectedLayers.ToList();
        
        foreach (var layer in layersToRemove)
        {
            RemoveLayerInternal(layer);
        }
        
        SelectedLayers.Clear();
    }
    
    /// <summary>
    /// 선택된 레이어들의 투명도 일괄 설정
    /// </summary>
    [RelayCommand]
    private void SetSelectedLayersOpacity(double opacity)
    {
        var layers = SelectedLayers.Count > 0 ? SelectedLayers.ToList() : 
                     (SelectedLayer != null ? new List<LayerItemViewModel> { SelectedLayer } : new List<LayerItemViewModel>());
        
        foreach (var layer in layers)
        {
            layer.Opacity = opacity;
        }
        
        LayerChanged?.Invoke();
    }
    
    /// <summary>
    /// 선택된 레이어들 표시/숨김 토글
    /// </summary>
    [RelayCommand]
    private void ToggleSelectedLayersVisibility()
    {
        var layers = SelectedLayers.Count > 0 ? SelectedLayers.ToList() : 
                     (SelectedLayer != null ? new List<LayerItemViewModel> { SelectedLayer } : new List<LayerItemViewModel>());
        
        // 하나라도 보이면 모두 숨기고, 모두 숨겨져 있으면 모두 표시
        var anyVisible = layers.Any(l => l.IsVisible);
        
        foreach (var layer in layers)
        {
            layer.IsVisible = !anyVisible;
        }
        
        LayerChanged?.Invoke();
    }
    
    private void RemoveLayerInternal(LayerItemViewModel layer)
    {
        // 이벤트 연결 해제
        layer.LayerChanged -= OnLayerChanged;
        layer.ZoomToLayerRequested -= OnZoomToLayerRequested;
        layer.RemoveRequested -= OnRemoveRequested;
        
        Layers.Remove(layer);
        
        if (SelectedLayer == layer)
        {
            SelectedLayer = Layers.FirstOrDefault();
        }
        
        // Map에서 레이어 제거 이벤트
        LayerRemoved?.Invoke(layer);
    }
    
    public event Action<LayerItemViewModel>? LayerRemoved;

    [RelayCommand]
    private void MoveLayerUp()
    {
        if (SelectedLayer == null) return;
        
        var index = Layers.IndexOf(SelectedLayer);
        if (index > 0)
        {
            Layers.Move(index, index - 1);
            SyncMapLayerOrder();
        }
    }

    [RelayCommand]
    private void MoveLayerDown()
    {
        if (SelectedLayer == null) return;
        
        var index = Layers.IndexOf(SelectedLayer);
        if (index < Layers.Count - 1)
        {
            Layers.Move(index, index + 1);
            SyncMapLayerOrder();
        }
    }

    [RelayCommand]
    private void ZoomToLayer()
    {
        if (SelectedLayer != null)
        {
            ZoomToLayerRequested?.Invoke(SelectedLayer);
        }
    }
    
    /// <summary>
    /// Map Layer 순서 동기화 이벤트
    /// </summary>
    public event Action? LayerOrderChanged;
    
    private void SyncMapLayerOrder()
    {
        LayerOrderChanged?.Invoke();
    }
    
    /// <summary>
    /// 레이어 순서 변경 (Drag & Drop용)
    /// </summary>
    public void MoveLayer(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Layers.Count) return;
        if (newIndex < 0 || newIndex >= Layers.Count) return;
        if (oldIndex == newIndex) return;
        
        Layers.Move(oldIndex, newIndex);
        SyncMapLayerOrder();
    }
}
