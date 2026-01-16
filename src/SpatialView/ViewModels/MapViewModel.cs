using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpatialView.Core.GisEngine;
using SpatialView.Core.Styling;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Engine.Geometry;
using System.Linq;
using EngineVectorLayer = SpatialView.Engine.Data.Layers.VectorLayer;

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
        System.Diagnostics.Debug.WriteLine("SetToolSelect 호출됨");
        ActiveTool = MapTool.Select;
        System.Diagnostics.Debug.WriteLine($"ActiveTool 설정됨: {ActiveTool}");
    }
    
    /// <summary>
    /// 지정된 좌표에서 피처 선택
    /// </summary>
    /// <param name="worldX">지도 X 좌표</param>
    /// <param name="worldY">지도 Y 좌표</param>
    /// <param name="tolerance">선택 허용 오차 (픽셀)</param>
    public void SelectFeaturesAtPoint(double worldX, double worldY, double tolerance = 20)
    {
        if (Map == null)
        {
            System.Diagnostics.Debug.WriteLine("SelectFeaturesAtPoint: Map이 null입니다.");
            return;
        }

        try
        {
            var clickCoord = new Coordinate(worldX, worldY);
            var clickScreen = MapToScreen(clickCoord, Map);
            var pixelTolerance = Math.Max(5, tolerance);

            var selectedIds = new List<uint>();
            var selectedGeometries = new List<IGeometry>();
            IVectorLayer? selectedLayer = null;
            double minScreenDistance = double.MaxValue;
            uint closestFeatureId = 0;
            IGeometry? closestGeometry = null;

            // 1) 상단 레이어부터 먼저 선택 (엔진 레이어 기준)
            foreach (var engineLayer in GetEngineLayersForSelection())
            {
                var layerMinDistance = double.MaxValue;
                uint layerClosestId = 0;

                var features = engineLayer.GetFeatures(Map.ViewExtent) ?? Enumerable.Empty<Engine.Data.IFeature>();
                foreach (var feature in features)
                {
                    if (feature.Geometry == null) continue;

                    var distancePixels = GetScreenDistance(feature.Geometry, Map, clickScreen, clickCoord);
                    if (distancePixels < layerMinDistance)
                    {
                        layerMinDistance = distancePixels;
                        layerClosestId = ToUIntId(feature.Id);
                        closestGeometry = feature.Geometry;
                    }
                }

                if (layerMinDistance <= pixelTolerance && layerClosestId != 0)
                {
                    minScreenDistance = layerMinDistance;
                    closestFeatureId = layerClosestId;
                    selectedLayer = ResolveCoreVectorLayer(engineLayer);
                    selectedIds.Add(closestFeatureId);
                    if (closestGeometry != null)
                    {
                        selectedGeometries.Add(closestGeometry);
                    }
                    break;
                }
            }

            // 2) 그래도 없으면 전체 피처에서 완화된 조건으로 재검색
            if (selectedIds.Count == 0)
            {
                var fallbackTolerance = Math.Max(pixelTolerance * 4, 80);

                foreach (var engineLayer in GetEngineLayersForSelection())
                {
                    var features = engineLayer.GetFeatures((Envelope?)null) ?? Enumerable.Empty<Engine.Data.IFeature>();
                    foreach (var feature in features)
                    {
                        if (feature.Geometry == null) continue;

                        var distancePixels = GetScreenDistance(feature.Geometry, Map, clickScreen, clickCoord);
                        if (distancePixels < minScreenDistance)
                        {
                            minScreenDistance = distancePixels;
                            closestFeatureId = ToUIntId(feature.Id);
                            selectedLayer = ResolveCoreVectorLayer(engineLayer);
                            closestGeometry = feature.Geometry;
                        }
                    }
                }

                if (minScreenDistance <= fallbackTolerance && closestFeatureId != 0 && selectedLayer != null)
                {
                    selectedIds.Add(closestFeatureId);
                    if (closestGeometry != null)
                    {
                        selectedGeometries.Add(closestGeometry);
                    }
                }
            }

            SelectedFeatureIds = selectedIds;
            SelectionTargetLayer = selectedLayer;

            UpdateHighlight(selectedLayer, selectedIds, selectedGeometries);
            FeatureSelected?.Invoke(selectedLayer, selectedIds);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"피처 선택 오류: {ex.Message}");
        }
    }

    private IEnumerable<EngineVectorLayer> GetEngineLayersForSelection()
    {
        if (Map is SpatialView.Infrastructure.GisEngine.SpatialViewMapEngine engineMap)
        {
            var layers = engineMap.EngineMap.LayerCollection;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i] is EngineVectorLayer vectorLayer && vectorLayer.Visible && vectorLayer.Selectable)
                {
                    yield return vectorLayer;
                }
            }
            yield break;
        }

        // 폴백: Core 레이어에서 엔진 레이어 추출
        for (int i = Map.Layers.Count - 1; i >= 0; i--)
        {
            if (Map.Layers[i] is IVectorLayer vectorLayer)
            {
                var engineLayer = TryGetEngineLayer(vectorLayer);
                if (engineLayer != null && engineLayer.Visible && engineLayer.Selectable)
                {
                    yield return engineLayer;
                }
            }
        }
    }

    private EngineVectorLayer? TryGetEngineLayer(IVectorLayer vectorLayer)
    {
        if (vectorLayer is SpatialView.Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
            return adapter.GetEngineLayer();

        if (vectorLayer is EngineVectorLayer direct)
            return direct;

        return null;
    }

    private IVectorLayer? ResolveCoreVectorLayer(EngineVectorLayer engineLayer)
    {
        if (Map == null) return null;

        for (int i = Map.Layers.Count - 1; i >= 0; i--)
        {
            if (Map.Layers[i] is IVectorLayer vectorLayer)
            {
                var resolved = TryGetEngineLayer(vectorLayer);
                if (resolved != null && ReferenceEquals(resolved, engineLayer))
                    return vectorLayer;
                if (Map.Layers[i].Name == engineLayer.Name)
                    return vectorLayer;
            }
        }

        var fallback = new SpatialView.Infrastructure.GisEngine.SpatialViewVectorLayerAdapter(engineLayer);
        if (engineLayer.DataSource != null && !string.IsNullOrEmpty(engineLayer.TableName))
        {
            fallback.DataSource = new SpatialView.Infrastructure.GisEngine.EngineDataSourceFeatureSourceAdapter(
                engineLayer.DataSource, engineLayer.TableName);
        }
        return fallback;
    }

    private static uint ToUIntId(object? id)
    {
        if (id is uint uid) return uid;
        if (id is int iid) return (uint)iid;
        if (id is long lid) return (uint)lid;
        return id != null ? (uint)id.GetHashCode() : 0;
    }

    /// <summary>
    /// 선택 실패 시 가장 가까운 피처를 재검색(완화된 허용오차)
    /// </summary>
    private void FallbackSelectNearest(System.Windows.Point clickScreen, Engine.Geometry.ICoordinate clickWorld,
        double pixelTolerance, double worldPerPixel,
        Envelope selectionBox, Envelope expandedSelectionBox, double worldX, double worldY,
        ref List<uint> selectedIds, ref ILayer? selectedLayer, ref double minScreenDistance, ref uint closestFeatureId)
    {
        if (Map == null) return;

        var fallbackTolerance = Math.Max(pixelTolerance * 3, 30); // 30px 이상으로 완화

        // 레이어 역순 스캔
        for (int i = Map.Layers.Count - 1; i >= 0; i--)
        {
            var layer = Map.Layers[i];
            if (layer is not IVectorLayer vectorLayer) continue;
            if (!layer.Visible) continue;

            Engine.Data.Layers.VectorLayer? engineLayer = null;
            if (vectorLayer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
            {
                engineLayer = adapter.GetEngineLayer();
            }
            else if (vectorLayer is Engine.Data.Layers.VectorLayer directLayer)
            {
                engineLayer = directLayer;
            }

            if (engineLayer == null) continue;

            IEnumerable<Engine.Data.IFeature> features =
                engineLayer.GetFeatures(expandedSelectionBox) ?? Enumerable.Empty<Engine.Data.IFeature>();
            if (!features.Any())
            {
                features = engineLayer.GetFeatures(Map.ViewExtent) ?? Enumerable.Empty<Engine.Data.IFeature>();
            }

            foreach (var feature in features)
            {
                if (feature.Geometry == null) continue;
                double distancePixels = GetScreenDistance(feature.Geometry, Map, clickScreen, clickWorld);
                if (distancePixels < minScreenDistance && distancePixels <= fallbackTolerance)
                {
                    minScreenDistance = distancePixels;
                    if (feature.Id is uint uid)
                        closestFeatureId = uid;
                    else if (feature.Id is int iid)
                        closestFeatureId = (uint)iid;
                    else if (feature.Id is long lid)
                        closestFeatureId = (uint)lid;
                    else
                        closestFeatureId = (uint)feature.Id.GetHashCode();

                    selectedLayer = layer;
                }
            }

            if (selectedLayer == layer)
            {
                selectedIds.Add(closestFeatureId);
                break;
            }
        }
    }

    /// <summary>
    /// 모든 피처(뷰포트/박스 무시)에서 최근접 검색 - 최종 안전망
    /// </summary>
    private void FallbackSelectAllFeatures(System.Windows.Point clickScreen, Engine.Geometry.ICoordinate clickWorld,
        double pixelTolerance, double worldPerPixel,
        ref List<uint> selectedIds, ref ILayer? selectedLayer, ref double minScreenDistance, ref uint closestFeatureId)
    {
        if (Map == null) return;

        for (int i = Map.Layers.Count - 1; i >= 0; i--)
        {
            var layer = Map.Layers[i];
            if (layer is not IVectorLayer vectorLayer) continue;
            if (!layer.Visible) continue;

            Engine.Data.Layers.VectorLayer? engineLayer = null;
            if (vectorLayer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
                engineLayer = adapter.GetEngineLayer();
            else if (vectorLayer is Engine.Data.Layers.VectorLayer directLayer)
                engineLayer = directLayer;

            if (engineLayer == null) continue;

            var features = engineLayer.GetFeatures((Envelope?)null) ?? Enumerable.Empty<Engine.Data.IFeature>();
            foreach (var feature in features)
            {
                if (feature.Geometry == null) continue;

                double distancePixels = GetScreenDistance(feature.Geometry, Map, clickScreen, clickWorld);
                if (distancePixels < minScreenDistance && distancePixels <= pixelTolerance * 5) // 여유 허용
                {
                    minScreenDistance = distancePixels;
                    if (feature.Id is uint uid)
                        closestFeatureId = uid;
                    else if (feature.Id is int iid)
                        closestFeatureId = (uint)iid;
                    else if (feature.Id is long lid)
                        closestFeatureId = (uint)lid;
                    else
                        closestFeatureId = (uint)feature.Id.GetHashCode();

                    selectedLayer = layer;
                }
            }

            if (selectedLayer == layer)
            {
                selectedIds.Add(closestFeatureId);
                break;
            }
        }
    }

    /// <summary>
    /// 모든 레이어/모든 피처에서 최근접 검색 (최종 폴백)
    /// </summary>
    private void FinalSelectAllLayers(System.Windows.Point clickScreen, Engine.Geometry.ICoordinate clickWorld,
        double pixelTolerance,
        ref List<uint> selectedIds, ref ILayer? selectedLayer, ref double minScreenDistance, ref uint closestFeatureId)
    {
        if (Map == null) return;

        var finalTolerance = Math.Max(pixelTolerance * 10, 100); // 여유 있게 100px 이상

        for (int i = Map.Layers.Count - 1; i >= 0; i--)
        {
            var layer = Map.Layers[i];
            if (layer is not IVectorLayer vectorLayer) continue;
            if (!layer.Visible) continue;

            Engine.Data.Layers.VectorLayer? engineLayer = null;
            if (vectorLayer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
                engineLayer = adapter.GetEngineLayer();
            else if (vectorLayer is Engine.Data.Layers.VectorLayer directLayer)
                engineLayer = directLayer;

            if (engineLayer == null) continue;

            var features = engineLayer.GetFeatures((Envelope?)null) ?? Enumerable.Empty<Engine.Data.IFeature>();
            foreach (var feature in features)
            {
                if (feature.Geometry == null) continue;

                double distancePixels = GetScreenDistance(feature.Geometry, Map, clickScreen, clickWorld);
                if (distancePixels < minScreenDistance && distancePixels <= finalTolerance)
                {
                    minScreenDistance = distancePixels;
                    if (feature.Id is uint uid)
                        closestFeatureId = uid;
                    else if (feature.Id is int iid)
                        closestFeatureId = (uint)iid;
                    else if (feature.Id is long lid)
                        closestFeatureId = (uint)lid;
                    else
                        closestFeatureId = (uint)feature.Id.GetHashCode();

                    selectedLayer = layer;
                }
            }

            if (selectedLayer == layer)
            {
                selectedIds.Add(closestFeatureId);
                break;
            }
        }
    }

    /// <summary>
    /// 지오메트리와 클릭 지점 간 최소 화면 픽셀 거리 계산
    /// </summary>
    private double GetScreenDistance(Engine.Geometry.IGeometry geometry, IMapEngine map, System.Windows.Point clickScreen, Engine.Geometry.ICoordinate clickWorld)
    {
        try
        {
            switch (geometry)
            {
                case Engine.Geometry.Point pt:
                    return Distance(clickScreen, MapToScreen(pt.Coordinate, map));
                case Engine.Geometry.MultiPoint mp:
                    return mp.Geometries
                        .OfType<Engine.Geometry.Point>()
                        .Select(p => Distance(clickScreen, MapToScreen(p.Coordinate, map)))
                        .DefaultIfEmpty(double.MaxValue)
                        .Min();
                case Engine.Geometry.LineString line:
                    return DistanceToLine(clickScreen, line.Coordinates, map);
                case Engine.Geometry.MultiLineString mls:
                    return mls.Geometries
                        .OfType<Engine.Geometry.LineString>()
                        .Select(l => DistanceToLine(clickScreen, l.Coordinates, map))
                        .DefaultIfEmpty(double.MaxValue)
                        .Min();
                case Engine.Geometry.Polygon poly:
                    // 폴리곤 내부 클릭은 거리 0으로 처리
                    if (poly.Contains(clickWorld))
                        return 0;
                    return DistanceToPolygon(clickScreen, poly, map);
                case Engine.Geometry.MultiPolygon mpoly:
                    if (mpoly.Contains(clickWorld))
                        return 0;
                    return mpoly.Geometries
                        .OfType<Engine.Geometry.Polygon>()
                        .Select(p => DistanceToPolygon(clickScreen, p, map))
                        .DefaultIfEmpty(double.MaxValue)
                        .Min();
                default:
                    return double.MaxValue;
            }
        }
        catch
        {
            return double.MaxValue;
        }
    }

    private double Distance(System.Windows.Point click, System.Windows.Point screen)
    {
        var dx = click.X - screen.X;
        var dy = click.Y - screen.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private double DistanceToLine(System.Windows.Point click, Engine.Geometry.ICoordinate[] coords, IMapEngine map)
    {
        if (coords == null || coords.Length < 2) return double.MaxValue;
        double min = double.MaxValue;
        var prev = MapToScreen(coords[0], map);
        for (int i = 1; i < coords.Length; i++)
        {
            var curr = MapToScreen(coords[i], map);
            min = Math.Min(min, DistancePointToSegment(click, prev, curr));
            prev = curr;
        }
        return min;
    }

    private double DistanceToPolygon(System.Windows.Point click, Engine.Geometry.Polygon poly, IMapEngine map)
    {
        double min = double.MaxValue;
        if (poly.ExteriorRing?.Coordinates != null)
        {
            min = Math.Min(min, DistanceToLine(click, poly.ExteriorRing.Coordinates, map));
        }
        if (poly.InteriorRings != null)
        {
            foreach (var hole in poly.InteriorRings)
            {
                if (hole?.Coordinates != null)
                    min = Math.Min(min, DistanceToLine(click, hole.Coordinates, map));
            }
        }
        return min;
    }

    private double DistancePointToSegment(System.Windows.Point p, System.Windows.Point a, System.Windows.Point b)
    {
        double ax = a.X, ay = a.Y, bx = b.X, by = b.Y;
        double vx = bx - ax, vy = by - ay;
        double wx = p.X - ax, wy = p.Y - ay;
        double c1 = vx * wx + vy * wy;
        if (c1 <= 0) return Math.Sqrt(wx * wx + wy * wy);
        double c2 = vx * vx + vy * vy;
        if (c2 <= c1) return Math.Sqrt((p.X - bx) * (p.X - bx) + (p.Y - by) * (p.Y - by));
        double t = c1 / c2;
        double projX = ax + t * vx;
        double projY = ay + t * vy;
        double dx = p.X - projX;
        double dy = p.Y - projY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private System.Windows.Point MapToScreen(Engine.Geometry.ICoordinate coord, IMapEngine map)
    {
        // MapTransform을 사용한 일관된 좌표 변환 (ScreenToMap과 동일한 변환 사용)
        return map.MapToScreen(coord);
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
    private void UpdateHighlight(IVectorLayer? sourceLayer, List<uint> featureIds, List<IGeometry>? selectedGeometries = null)
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
            if (selectedGeometries != null && selectedGeometries.Count > 0)
            {
                geometries.AddRange(selectedGeometries.Where(g => g != null));
            }
            
            // 1) Provider에서 직접 조회
            var provider = sourceLayer.Provider;
            if (geometries.Count == 0 && provider != null)
            {
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
            }
            // 2) Provider가 없거나 일부 누락된 경우: 엔진 레이어 캐시에서 보완
            if (geometries.Count < featureIds.Count)
            {
                EngineVectorLayer? engineLayer = null;
                if (sourceLayer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter adapter)
                    engineLayer = adapter.GetEngineLayer();
                else if (sourceLayer is EngineVectorLayer direct)
                    engineLayer = direct;
                
                if (engineLayer != null)
                {
                    var allFeatures = engineLayer.GetFeatures((Envelope?)null);
                    foreach (var fid in featureIds)
                    {
                        var f = allFeatures.FirstOrDefault(x => ToUIntId(x.Id) == fid);
                        if (f?.Geometry != null)
                        {
                            geometries.Add(f.Geometry);
                        }
                    }
                }
            }
            
            if (geometries.Count == 0)
            {
                RequestRefresh();
                return;
            }
            
            // 하이라이트 레이어 생성
            _highlightLayer = _layerFactory.CreateHighlightLayer("_Highlight");
            
            // 하이라이트 피처로 채우기 (메모리 캐시)
            if (_highlightLayer is Infrastructure.GisEngine.SpatialViewVectorLayerAdapter hlAdapter)
            {
                var hlEngine = hlAdapter.GetEngineLayer();
                var features = new List<Engine.Data.Feature>();
                uint idx = 1;
                foreach (var geom in geometries)
                {
                    features.Add(new Engine.Data.Feature(idx++, geom));
                }
                hlEngine.SetFeatures(features);
            }
            
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
