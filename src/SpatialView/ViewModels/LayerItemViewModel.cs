using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialView.Core.Enums;
using SpatialView.Core.GisEngine;
using SpatialView.Engine.Geometry;

namespace SpatialView.ViewModels;

/// <summary>
/// 개별 레이어 항목의 ViewModel
/// </summary>
public partial class LayerItemViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id;
    
    [ObservableProperty]
    private string _name = string.Empty;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    [ObservableProperty]
    private double _opacity = 1.0;
    
    [ObservableProperty]
    private Core.Enums.GeometryType _geometryType = Core.Enums.GeometryType.Unknown;
    
    [ObservableProperty]
    private int _featureCount;
    
    /// <summary>
    /// 빈 레이어 여부
    /// </summary>
    public bool IsEmpty => FeatureCount == 0;
    
    /// <summary>
    /// FeatureCount 변경 시 관련 속성 갱신
    /// </summary>
    partial void OnFeatureCountChanged(int value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(GeometryIcon));
        OnPropertyChanged(nameof(GeometryIconBrush));
    }
    
    [ObservableProperty]
    private string _filePath = string.Empty;
    
    [ObservableProperty]
    private string _crs = "EPSG:4326";
    
    [ObservableProperty]
    private bool _isExpanded = false;
    
    [ObservableProperty]
    private bool _isEditing = false;
    
    /// <summary>
    /// 범위 (Envelope)
    /// </summary>
    public SpatialView.Engine.Geometry.Envelope? Extent { get; set; }
    
    /// <summary>
    /// SpatialView Layer 객체 참조
    /// </summary>
    public ILayer? Layer { get; set; }
    
    /// <summary>
    /// 레이어 색상 (System.Drawing.Color)
    /// </summary>
    private System.Drawing.Color _layerColor = System.Drawing.Color.DodgerBlue;
    public System.Drawing.Color LayerColor
    {
        get => _layerColor;
        set
        {
            if (_layerColor != value)
            {
                _layerColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GeometryIconBrush));
            }
        }
    }
    
    /// <summary>
    /// 지오메트리 타입에 따른 아이콘
    /// 빈 레이어인 경우 경고 아이콘 표시
    /// </summary>
    public string GeometryIcon
    {
        get
        {
            // 빈 레이어는 경고 아이콘
            if (IsEmpty)
                return "AlertCircleOutline";
            
            return GeometryType switch
            {
                Core.Enums.GeometryType.Point => "MapMarker",
                Core.Enums.GeometryType.MultiPoint => "MapMarkerMultiple",
                Core.Enums.GeometryType.Line or Core.Enums.GeometryType.LineString => "VectorLine",
                Core.Enums.GeometryType.MultiLineString => "VectorPolyline",
                Core.Enums.GeometryType.Polygon => "VectorPolygon",
                Core.Enums.GeometryType.MultiPolygon => "VectorSquare",
                Core.Enums.GeometryType.GeometryCollection => "VectorCombine",
                _ => "LayersOutline"
            };
        }
    }
    
    /// <summary>
    /// 아이콘 색상 (빈 레이어는 경고색, 그 외는 레이어 색상)
    /// </summary>
    public System.Windows.Media.Brush GeometryIconBrush
    {
        get
        {
            if (IsEmpty)
                return System.Windows.Media.Brushes.Orange;
            
            // System.Drawing.Color → System.Windows.Media.Color 변환
            var wpfColor = System.Windows.Media.Color.FromRgb(LayerColor.R, LayerColor.G, LayerColor.B);
            return new System.Windows.Media.SolidColorBrush(wpfColor);
        }
    }
    
    /// <summary>
    /// 투명도 퍼센트 (UI 표시용)
    /// </summary>
    public int OpacityPercent => (int)(Opacity * 100);
    
    /// <summary>
    /// 레이어 변경 시 Map 갱신을 위한 이벤트
    /// </summary>
    public event Action? LayerChanged;
    
    /// <summary>
    /// Zoom to Layer 요청 이벤트
    /// </summary>
    public event Action<LayerItemViewModel>? ZoomToLayerRequested;
    
    /// <summary>
    /// 삭제 요청 이벤트
    /// </summary>
    public event Action<LayerItemViewModel>? RemoveRequested;
    
    /// <summary>
    /// IsVisible 변경 시 레이어 표시/숨김 처리
    /// </summary>
    partial void OnIsVisibleChanged(bool value)
    {
        if (Layer != null)
        {
            Layer.Visible = value;
            LayerChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// 투명도 변경 시 레이어 스타일 업데이트
    /// </summary>
    partial void OnOpacityChanged(double value)
    {
        ApplyOpacity();
        OnPropertyChanged(nameof(OpacityPercent));
        LayerChanged?.Invoke();
    }
    
    /// <summary>
    /// 투명도를 레이어에 적용
    /// </summary>
    private void ApplyOpacity()
    {
        if (Layer != null)
        {
            try
            {
                // ILayer.Opacity 설정 (VectorLayer.Opacity에 전달됨)
                Layer.Opacity = Opacity;
                
                // IVectorStyle에도 Opacity 설정
                if (Layer is Core.GisEngine.IVectorLayer vectorLayer && vectorLayer.Style != null)
                {
                    vectorLayer.Style.Opacity = (float)Opacity;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"투명도 적용 오류: {ex.Message}");
            }
        }
    }
    
    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }
    
    [RelayCommand]
    private void StartEditing()
    {
        IsEditing = true;
    }
    
    [RelayCommand]
    private void EndEditing()
    {
        IsEditing = false;
    }
    
    [RelayCommand]
    private void ZoomToLayer()
    {
        ZoomToLayerRequested?.Invoke(this);
    }
    
    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this);
    }
}
