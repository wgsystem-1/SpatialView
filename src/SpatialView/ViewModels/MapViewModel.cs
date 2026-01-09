using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialView.Core.GisEngine;
using SpatialView.Core.Styling;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Engine.Geometry;

namespace SpatialView.ViewModels;

/// <summary>
/// 지도 도구 유형
/// </summary>
// MapTool enum is now in Core.GisEngine

/// <summary>
/// 지도 뷰의 ViewModel
/// SpatialView.Engine 객체를 관리하고 지도 조작 기능을 제공
/// </summary>
public partial class MapViewModel : ObservableObject
{
    private readonly IBaseMapService _baseMapService;
    private readonly Core.Factories.IMapFactory _mapFactory;
    private readonly Core.Factories.ILayerFactory _layerFactory;
    private readonly Core.Factories.IStyleFactory _styleFactory;
    private Core.GisEngine.ILayer? _currentBaseMapLayer;
    private IMapRenderer? _mapRenderer;
    
    [ObservableProperty]
    private IMapEngine? _map;

    [ObservableProperty]
    private double _mouseX;

    [ObservableProperty]
    private double _mouseY;

    [ObservableProperty]
    private double _currentScale = 1.0;

    [ObservableProperty]
    private string _coordinateSystem = "EPSG:3857";

    [ObservableProperty]
    private double _centerX = 126.9780;

    [ObservableProperty]
    private double _centerY = 37.5665;

    [ObservableProperty]
    private double _zoomLevel = 10;

    [ObservableProperty]
    private bool _isLoading = false;
    
    [ObservableProperty]
    private List<BaseMapInfo> _availableBaseMaps = new();
    
    [ObservableProperty]
    private BaseMapInfo? _selectedBaseMap;
    
    [ObservableProperty]
    private bool _isBaseMapEnabled = true;
    
    [ObservableProperty]
    private Core.GisEngine.MapTool _activeTool = Core.GisEngine.MapTool.Pan;
    
    /// <summary>
    /// 선택된 피처 ID 목록
    /// </summary>
    [ObservableProperty]
    private List<uint> _selectedFeatureIds = new();
    
    /// <summary>
    /// 선택된 레이어 (피처 선택 대상)
    /// </summary>
    [ObservableProperty]
    private Core.GisEngine.ILayer? _selectionTargetLayer;
    
    /// <summary>
    /// 외부(속성창 등)에서 선택 피처가 바뀌면 하이라이트 동기화
    /// </summary>
    partial void OnSelectedFeatureIdsChanged(List<uint> value)
    {
        var layer = SelectionTargetLayer as IVectorLayer;

        if (value == null || value.Count == 0 || layer == null)
        {
            UpdateHighlight(null, new List<uint>());
            return;
        }

        UpdateHighlight(layer, value);
    }

    /// <summary>
    /// 외부에서 선택 대상 레이어가 바뀌면 하이라이트 동기화
    /// </summary>
    partial void OnSelectionTargetLayerChanged(ILayer? value)
    {
        var ids = SelectedFeatureIds ?? new List<uint>();

        if (value is IVectorLayer vectorLayer && ids.Count > 0)
        {
            UpdateHighlight(vectorLayer, ids);
            return;
        }

        // 선택 레이어가 해제되면 하이라이트도 제거
        if (value == null)
        {
            UpdateHighlight(null, new List<uint>());
        }
    }

    /// <summary>
    /// 하이라이트 레이어 (선택된 피처 표시용)
    /// </summary>
    private IVectorLayer? _highlightLayer;

    /// <summary>
    /// 축척 표시 문자열
    /// </summary>
    public string ScaleText => $"1:{CurrentScale:N0}";
    
    /// <summary>
    /// 활성 도구 변경 이벤트
    /// </summary>
    public event Action<MapTool>? ActiveToolChanged;
    
    /// <summary>
    /// 피처 선택 이벤트
    /// </summary>
    public event Action<ILayer?, List<uint>>? FeatureSelected;

    /// <summary>
    /// 속성 테이블 포커스 요청 이벤트 (레이어, FID)
    /// </summary>
    public event Action<ILayer?, uint>? FocusAttributeTableRequested;

    public MapViewModel(IBaseMapService baseMapService, 
        Core.Factories.IMapFactory mapFactory,
        Core.Factories.ILayerFactory layerFactory,
        Core.Factories.IStyleFactory styleFactory)
    {
        _baseMapService = baseMapService;
        _mapFactory = mapFactory;
        _layerFactory = layerFactory;
        _styleFactory = styleFactory;
        AvailableBaseMaps = _baseMapService.GetAvailableBaseMaps();
        
        // MapRenderer 생성
        _mapRenderer = _mapFactory.CreateMapRenderer();
        
        // 기본값: OSM (배경지도 바로 표시)
        SelectedBaseMap = AvailableBaseMaps.FirstOrDefault(b => b.Type != BaseMapType.None)
                          ?? AvailableBaseMaps.FirstOrDefault();
        
        // 배경지도를 비활성화
        IsBaseMapEnabled = false;
    }

    /// <summary>
    /// 지도 초기화 - 빈 지도 생성 (좌표계는 첫 번째 레이어 로드 시 자동 설정)
    /// </summary>
    public void InitializeMap()
    {
        // DI를 통해 생성된 Factory 사용
        Map = _mapFactory.CreateMapEngine();
        
        // 초기 범위는 설정하지 않음 - 첫 번째 레이어 로드 시 자동 설정됨
        // 좌표계도 첫 번째 레이어 기준으로 자동 설정됨
        CoordinateSystem = ""; // 데이터 로드 전까지 비어있음
        
        System.Diagnostics.Debug.WriteLine($"지도 초기화 완료. 데이터 로드 대기 중...");
        
        // 초기 중심점 업데이트
        UpdateCenterFromMap();
        UpdateScale();
    }

    /// <summary>
    /// 배경지도 적용
    /// </summary>
    private void ApplyBaseMap(BaseMapInfo baseMapInfo)
    {
        if (Map == null) return;
        
        try
        {
            IsLoading = true;
            
            // 기존 배경지도 레이어 제거
            if (_currentBaseMapLayer != null)
            {
                Map?.Layers.Remove(_currentBaseMapLayer);
                _currentBaseMapLayer = null;
            }
            
            // 새 배경지도 레이어 생성
            var layer = _baseMapService.CreateLayer(baseMapInfo) as Core.GisEngine.ILayer;
            
            if (layer != null && IsBaseMapEnabled)
            {
                _currentBaseMapLayer = layer;
                
                // 배경지도는 항상 맨 아래에 추가
                Map?.Layers.Add(layer); // Will be at the bottom

                // 배경지도 좌표계(주로 WebMercator 3857)에 맞춰 지도 초기 시점 조정
                // 다른 벡터 레이어가 없는 초기 상태라도 바로 지도가 보이도록 범위를 맞춘다.
                if (layer is ITileLayer tileLayer)
                {
                    if (Map != null)
                    {
                        Map.SRID = tileLayer.SRID;
                        if (tileLayer.Extent != null)
                            Map.ZoomToExtent(tileLayer.Extent);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"배경지도 적용됨: {baseMapInfo.Name}");
            }
            
            OnPropertyChanged(nameof(Map));
            RequestRefresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"배경지도 적용 실패: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    /// <summary>
    /// 배경지도 선택 변경 시 처리
    /// </summary>
    partial void OnSelectedBaseMapChanged(BaseMapInfo? value)
    {
        if (value != null && Map != null)
        {
            ApplyBaseMap(value);
        }
    }
    
    /// <summary>
    /// 배경지도 활성화/비활성화 토글
    /// </summary>
    partial void OnIsBaseMapEnabledChanged(bool value)
    {
        if (_currentBaseMapLayer != null)
        {
            // Layer enabling through visibility
            _currentBaseMapLayer.Visible = value;
            OnPropertyChanged(nameof(Map));
            RequestRefresh();
        }
        else if (value)
        {
            // 배경지도가 없으면 새로 적용 (None이면 OSM으로 대체)
            if (SelectedBaseMap == null || SelectedBaseMap.Type == BaseMapType.None)
            {
                SelectedBaseMap = AvailableBaseMaps.FirstOrDefault(b => b.Type != BaseMapType.None)
                                  ?? AvailableBaseMaps.FirstOrDefault();
            }

            if (SelectedBaseMap != null)
                ApplyBaseMap(SelectedBaseMap);
        }
    }
    
    [RelayCommand]
    private void ChangeBaseMap(BaseMapInfo? baseMap)
    {
        if (baseMap != null)
        {
            SelectedBaseMap = baseMap;
        }
    }
    
    [RelayCommand]
    private void ToggleBaseMap()
    {
        IsBaseMapEnabled = !IsBaseMapEnabled;
        
        // 켤 때 기본값이 None이면 OSM으로 설정
        if (IsBaseMapEnabled && (SelectedBaseMap == null || SelectedBaseMap.Type == BaseMapType.None))
        {
            SelectedBaseMap = AvailableBaseMaps.FirstOrDefault(b => b.Type != BaseMapType.None)
                              ?? AvailableBaseMaps.FirstOrDefault();
        }

        if (IsBaseMapEnabled && SelectedBaseMap != null)
        {
            ApplyBaseMap(SelectedBaseMap);
        }
    }

    /// <summary>
    /// 지도에 레이어 추가
    /// </summary>
    public void AddLayer(ILayer layer)
    {
        if (Map == null) return;
        
        // 배경지도 위에 추가
        Map.Layers.Add(layer);
        
        System.Diagnostics.Debug.WriteLine($"레이어 추가됨: {layer.Name}, 전체 레이어 수: {Map.Layers.Count}");
        
        OnPropertyChanged(nameof(Map));
        RequestRefresh();
    }
    
    /// <summary>
    /// 좌표계 업데이트 (첫 레이어 기준)
    /// </summary>
    public void UpdateCoordinateSystem(string crs)
    {
        if (!string.IsNullOrEmpty(crs))
        {
            CoordinateSystem = crs;
            System.Diagnostics.Debug.WriteLine($"좌표계 업데이트: {crs}");
        }
    }

    /// <summary>
    /// 지도에서 레이어 제거
    /// </summary>
    public void RemoveLayer(ILayer layer)
    {
        Map?.Layers.Remove(layer);
        OnPropertyChanged(nameof(Map));
    }

    [RelayCommand]
    private void ZoomIn()
    {
        if (Map == null) return;
        
        Map.Zoom *= 0.5; // 확대 (Zoom 값이 작을수록 더 가까이)
        UpdateScale();
        OnPropertyChanged(nameof(Map));
        RequestRefresh();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        if (Map == null) return;
        
        Map.Zoom *= 2; // 축소
        UpdateScale();
        OnPropertyChanged(nameof(Map));
        RequestRefresh();
    }

    [RelayCommand]
    private void ZoomToExtent()
    {
        if (Map == null) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine("전체보기(ZoomToExtent) 실행");
            Map.ZoomToExtents();
            
            UpdateCenterFromMap();
            UpdateScale();
            OnPropertyChanged(nameof(Map));
            RequestRefresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ZoomToExtent 오류: {ex.Message}");
            // 오류 발생 시 기본 범위로
            var defaultEnvelope = new Envelope(124, 132, 33, 43);
            Map.ZoomToExtent(defaultEnvelope);
            UpdateCenterFromMap();
            UpdateScale();
            RequestRefresh();
        }
    }

    /// <summary>
    /// 특정 범위로 줌
    /// </summary>
    public void ZoomToEnvelope(Envelope envelope)
    {
        if (Map == null) return;
        
        Map.ZoomToExtent(envelope);
        UpdateCenterFromMap();
        UpdateScale();
        OnPropertyChanged(nameof(Map));
        RequestRefresh();
    }

    /// <summary>
    /// 마우스 위치 업데이트 (MapControl에서 호출)
    /// </summary>
    public void UpdateMousePosition(double x, double y)
    {
        MouseX = x;
        MouseY = y;
    }

    /// <summary>
    /// 지도 줌 변경 시 호출 (MapControl에서 호출)
    /// </summary>
    public void OnMapZoomChanged(double zoom)
    {
        UpdateScale();
    }

    /// <summary>
    /// 지도 중심 변경 시 호출 (MapControl에서 호출)
    /// </summary>
    public void OnMapCenterChanged(double x, double y)
    {
        CenterX = x;
        CenterY = y;
    }

    /// <summary>
    /// Map 객체에서 중심점 업데이트
    /// </summary>
    private void UpdateCenterFromMap()
    {
        if (Map?.Center != null)
        {
            CenterX = Map.Center.X;
            CenterY = Map.Center.Y;
        }
    }

    /// <summary>
    /// 축척 계산 및 업데이트
    /// </summary>
    private void UpdateScale()
    {
        if (Map == null) return;
        
        // Web Mercator 기준 축척 계산
        // Map.Zoom = 지도 너비(미터 단위)
        var metersPerPixel = Map.Zoom / Map.Size.Width;
        
        // 일반적인 모니터 DPI (96 dpi = 0.0254m/pixel 기준)
        // 1 inch = 0.0254m, 96 pixels/inch
        var dpi = 96;
        var inchesPerMeter = 39.3701;
        CurrentScale = metersPerPixel * dpi * inchesPerMeter;
        
        OnPropertyChanged(nameof(ScaleText));
    }

    /// <summary>
    /// 지도 새로고침 요청 (View에서 처리)
    /// </summary>
    public event Action? RefreshRequested;
    
    public void RequestRefresh()
    {
        OnPropertyChanged(nameof(Map));
        RefreshRequested?.Invoke();
    }
    
    /// <summary>
    /// ActiveTool 변경 시 이벤트 발생
    /// </summary>
    partial void OnActiveToolChanged(MapTool value)
    {
        ActiveToolChanged?.Invoke(value);
        System.Diagnostics.Debug.WriteLine($"활성 도구 변경: {value}");
    }
    
    [RelayCommand]
    private void SetToolPan()
    {
        ActiveTool = MapTool.Pan;
    }
    
    [RelayCommand]
    private void SetToolZoomIn()
    {
        ActiveTool = MapTool.ZoomIn;
    }
    
    [RelayCommand]
    private void SetToolZoomOut()
    {
        ActiveTool = MapTool.ZoomOut;
    }
    
    [RelayCommand]
    private void SetToolZoomWindow()
    {
        ActiveTool = MapTool.ZoomWindow;
    }
    
    [RelayCommand]
    private void SetToolSelect()
    {
        ActiveTool = MapTool.Select;
    }
    
    /// <summary>
    /// 지정된 좌표에서 피처 선택
    /// </summary>
    /// <param name="worldX">지도 X 좌표</param>
    /// <param name="worldY">지도 Y 좌표</param>
    /// <param name="tolerance">선택 허용 오차 (픽셀)</param>
    public void SelectFeaturesAtPoint(double worldX, double worldY, double tolerance = 5)
    {
        if (Map == null) return;
        
        try
        {
            // 허용 오차를 지도 좌표로 변환
            var pixelTolerance = tolerance;
            // PixelSize 계산: Zoom / Width
            var pixelSize = Map.Zoom / Map.Size.Width;
            var worldTolerance = pixelSize * pixelTolerance;
            
            // 클릭 포인트 생성
            var clickPoint = new Engine.Geometry.Point(worldX, worldY);
            
            // 선택 영역 생성 (점 주변 박스) - 후보 필터링용
            var selectionBox = new Envelope(
                worldX - worldTolerance * 2,
                worldX + worldTolerance * 2,
                worldY - worldTolerance * 2,
                worldY + worldTolerance * 2);
            
            var selectedIds = new List<uint>();
            ILayer? selectedLayer = null;
            double minDistance = double.MaxValue;
            uint closestFeatureId = 0;
            
            // 레이어를 역순으로 순회 (맨 위 레이어부터)
            for (int i = Map.Layers.Count - 1; i >= 0; i--)
            {
                var layer = Map.Layers[i];
                
                // VectorLayer만 선택 가능
                if (layer is not IVectorLayer vectorLayer) continue;
                if (!layer.Visible) continue;
                
                try
                {
                    // IProvider로 캐스팅
                    var provider = vectorLayer.Provider;
                    if (provider != null)
                    {
                        provider.Open();
                        
                        // 1단계: Bounding Box로 후보 피처 조회
                        var candidateOids = provider.GetObjectIDsInView(selectionBox);
                        
                        if (candidateOids != null && candidateOids.Count > 0)
                        {
                            // 2단계: 실제 지오메트리와의 거리 계산
                            foreach (var oid in candidateOids)
                            {
                                try
                                {
                                    var geometry = provider.GetGeometryByID(oid);
                                    if (geometry == null) continue;
                                    
                                    // 지오메트리와 클릭 포인트 간의 거리 계산
                                    double distance = geometry.Distance(clickPoint);
                                    
                                    // 허용 오차 내에 있고, 가장 가까운 피처 선택
                                    if (distance <= worldTolerance && distance < minDistance)
                                    {
                                        minDistance = distance;
                                        closestFeatureId = oid;
                                        selectedLayer = layer;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"지오메트리 거리 계산 오류: {ex.Message}");
                                }
                            }
                        }
                        
                        provider.Close();
                        
                        // 이 레이어에서 피처를 찾았으면 중단
                        if (selectedLayer == vectorLayer)
                        {
                            selectedIds.Add(closestFeatureId);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"레이어 '{layer.Name}' 선택 오류: {ex.Message}");
                }
            }
            
            // 선택 결과 저장
            SelectedFeatureIds = selectedIds;
            SelectionTargetLayer = selectedLayer;
            
            // 하이라이트 업데이트
            UpdateHighlight(selectedLayer as IVectorLayer, selectedIds);
            
            // 이벤트 발생
            FeatureSelected?.Invoke(selectedLayer, selectedIds);
            
            System.Diagnostics.Debug.WriteLine($"피처 선택: {selectedIds.Count}개 (레이어: {selectedLayer?.Name ?? "없음"}, 거리: {minDistance:F2})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"피처 선택 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 현재 선택을 속성 테이블에 포커싱하도록 요청
    /// </summary>
    public void RequestAttributeFocusOnSelection()
    {
        if (SelectedFeatureIds != null && SelectedFeatureIds.Count > 0)
        {
            var fid = SelectedFeatureIds[0];
            FocusAttributeTableRequested?.Invoke(SelectionTargetLayer, fid);
        }
    }
    
    /// <summary>
    /// 선택된 피처 하이라이트 업데이트
    /// </summary>
    private void UpdateHighlight(IVectorLayer? sourceLayer, List<uint> featureIds)
    {
        if (Map == null) return;
        
        // 기존 하이라이트 레이어 제거
        if (_highlightLayer != null)
        {
            Map.Layers.Remove(_highlightLayer);
            _highlightLayer = null;
        }
        
        if (sourceLayer == null || featureIds.Count == 0)
        {
            RequestRefresh();
            return;
        }
        
        try
        {
            // 선택된 피처의 지오메트리 수집
            var geometries = new List<IGeometry>();
            
            // Get provider from layer
            var provider = sourceLayer.Provider;
            if (provider == null)
            {
                RequestRefresh();
                return;
            }
            
            provider.Open();
            foreach (var fid in featureIds)
            {
                var geom = provider.GetGeometryByID(fid);
                if (geom != null)
                {
                    geometries.Add(geom);
                }
            }
            provider.Close();
            
            if (geometries.Count == 0)
            {
                RequestRefresh();
                return;
            }
            
            // 하이라이트 레이어 생성
            _highlightLayer = _layerFactory.CreateHighlightLayer("_Highlight");
            
            // TODO: 하이라이트 레이어에 지오메트리 추가
            // 현재는 하이라이트 레이어 생성만 수행
            
            // 맨 위에 추가
            Map.Layers.Add(_highlightLayer);
            
            RequestRefresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"하이라이트 업데이트 오류: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 선택 해제
    /// </summary>
    public void ClearSelection()
    {
        SelectedFeatureIds = new List<uint>();
        SelectionTargetLayer = null;
        UpdateHighlight(null, new List<uint>());
        FeatureSelected?.Invoke(null, new List<uint>());
    }
    
    /// <summary>
    /// MapRenderer 접근자 (MapControl에서 사용)
    /// </summary>
    public IMapRenderer? MapRenderer
    {
        get => _mapRenderer;
        set => _mapRenderer = value;
    }
}
