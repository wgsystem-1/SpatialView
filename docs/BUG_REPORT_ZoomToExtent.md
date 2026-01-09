# 버그 리포트: 데이터 로딩 후 전체 영역 확대(ZoomToExtent) 오류

## 1. 문제 요약

데이터 로딩 후 전체 영역으로 확대(ZoomToExtent)가 정상적으로 작동하지 않음.

---

## 2. 근본 원인

### 핵심 문제: 레이어 어댑터 구조와 조건 검사 로직의 불일치

```
[SpatialViewMapEngine]
    └─► [EngineLayerCollectionAdapter] ─► MapViewModel.AddLayer()에서 사용
    └─► [MapContainer._layers] ─► Map.GetExtents()에서 조회
```

---

## 3. 발견된 구체적 오류들

### 오류 1: 조건 검사에서 레이어 제외 (`Map.cs:250`)

```csharp
// 파일: src/SpatialView.Engine/Map.cs, 라인 250
if (!layer.Enabled || !layer.Visible) continue;
```

**문제**: `Enabled`와 `Visible`이 동일한 속성을 가리킴 (`VectorLayer.cs:31`)

```csharp
// 파일: src/SpatialView.Engine/Data/Layers/VectorLayer.cs, 라인 31
public bool Enabled { get => Visible; set => Visible = value; }
```

### 오류 2: 빈 레이어 비활성화 로직 (`MainViewModel.cs:1336-1344`)

```csharp
// 파일: src/SpatialView/ViewModels/MainViewModel.cs, 라인 1336-1344
if (layerInfo.FeatureCount == 0)
{
    if (sharpMapLayer is IMapLayer mapLayer)
    {
        mapLayer.Enabled = false;  // ⚠️ Visible도 false가 됨
    }
    layerItem.IsVisible = false;
}
```

**문제**: 피처가 0개인 레이어는 `Enabled = false`가 되어 `GetExtents()`에서 완전히 제외됨. 하지만 Extent는 유효할 수 있음 (Shapefile 헤더에서 읽은 범위).

### 오류 3: 첫 번째 레이어만 자동 줌 (`MainViewModel.cs:1351-1361`)

```csharp
// 파일: src/SpatialView/ViewModels/MainViewModel.cs, 라인 1351-1361
if (LayerPanelViewModel.Layers.Count == 1 && !_isLoadingProject)
{
    MapViewModel.UpdateCoordinateSystem(layerInfo.CRS);

    if (layerInfo.Extent != null && layerInfo.FeatureCount > 0)
    {
        MapViewModel.ZoomToEnvelope(layerInfo.Extent);
    }
}
```

**문제**:
- `FeatureCount > 0` 조건으로 빈 레이어는 줌되지 않음
- 두 번째 이후 레이어는 자동 줌 대상이 아님
- 여러 레이어 로드 시 전체 범위로 줌되지 않음

### 오류 4: ZoomToExtents() 내부 로직 (`Map.cs:243-284`)

```csharp
// 파일: src/SpatialView.Engine/Map.cs, 라인 243-284
public Envelope? GetExtents()
{
    Envelope? totalEnvelope = null;

    foreach (var layer in _layers)
    {
        if (!layer.Enabled || !layer.Visible) continue;  // ⚠️ 여기서 필터링

        var layerEnvelope = GetLayerEnvelope(layer);
        // ...
    }
    return totalEnvelope;
}
```

---

## 4. 데이터 흐름 분석

```
1. ShapefileDataProvider.LoadInternal()
   └─ VectorLayer 생성 + Extent 설정 (정상)
   └─ SpatialViewVectorLayerAdapter 생성

2. MainViewModel.AddLayerToMap()
   └─ 빈 레이어면 mapLayer.Enabled = false (문제)
   └─ MapViewModel.AddLayer() 호출
   └─ 첫 번째 레이어 && FeatureCount > 0 일 때만 ZoomToEnvelope (문제)

3. MapViewModel.AddLayer()
   └─ Map.Layers.Add(layer)
   └─ EngineLayerCollectionAdapter.Add()
   └─ _engineLayers.Add(sva.InternalLayer)  // VectorLayer 추가

4. ZoomToExtent 버튼 클릭 시
   └─ MapViewModel.ZoomToExtent()
   └─ Map.ZoomToExtents()
   └─ Map.GetExtents() - Enabled=false 레이어 스킵 (문제)
   └─ totalEnvelope = null 가능성
```

---

## 5. 해결 방안

### 방안 A: 즉시 해결 (Quick Fix)

**파일**: `src/SpatialView/ViewModels/MainViewModel.cs`

```csharp
// 변경 전 (라인 1351)
if (LayerPanelViewModel.Layers.Count == 1 && !_isLoadingProject)

// 변경 후 - 레이어 추가 시마다 전체 범위로 줌
if (!_isLoadingProject)
{
    // 약간의 지연 후 전체 범위로 줌 (렌더링 완료 대기)
    System.Windows.Application.Current.Dispatcher.BeginInvoke(
        System.Windows.Threading.DispatcherPriority.Background,
        new Action(() => MapViewModel.ZoomToExtentCommand.Execute(null)));
}
```

### 방안 B: GetExtents() 조건 수정

**파일**: `src/SpatialView.Engine/Map.cs`

```csharp
// 변경 전 (라인 250)
if (!layer.Enabled || !layer.Visible) continue;

// 변경 후 - Extent 계산 시에는 Visible만 확인
if (!layer.Visible) continue;
```

### 방안 C: 빈 레이어 처리 로직 수정

**파일**: `src/SpatialView/ViewModels/MainViewModel.cs`

```csharp
// 변경 전 (라인 1336-1344)
if (layerInfo.FeatureCount == 0)
{
    if (sharpMapLayer is IMapLayer mapLayer)
    {
        mapLayer.Enabled = false;
    }
    layerItem.IsVisible = false;
}

// 변경 후 - Enabled는 유지하되 렌더링만 스킵하도록 별도 플래그 사용
if (layerInfo.FeatureCount == 0)
{
    layerItem.IsEmpty = true;  // 새 속성 추가
    // Enabled는 true 유지 (Extent 계산에 포함되도록)
}
```

### 방안 D: 근본적 해결 (권장)

**파일**: `src/SpatialView.Engine/Map.cs`

```csharp
public Envelope? GetExtents()
{
    Envelope? totalEnvelope = null;

    foreach (var layer in _layers)
    {
        // Extent 계산 시에는 Enabled/Visible 무시하고 유효한 Extent만 확인
        var layerEnvelope = GetLayerEnvelope(layer);

        if (layerEnvelope != null && !layerEnvelope.IsNull)
        {
            if (totalEnvelope == null)
                totalEnvelope = new Envelope(layerEnvelope);
            else
                totalEnvelope.ExpandToInclude(layerEnvelope);
        }
    }

    System.Diagnostics.Debug.WriteLine($"[Map.GetExtents] 레이어 수: {_layers.Count}, 최종 범위: {totalEnvelope}");

    return totalEnvelope;
}
```

---

## 6. 관련 파일 목록

| 파일 | 역할 | 수정 필요 |
|------|------|----------|
| `src/SpatialView.Engine/Map.cs` | GetExtents(), ZoomToExtent() | ✅ |
| `src/SpatialView/ViewModels/MainViewModel.cs` | AddLayerToMap(), 빈 레이어 처리 | ✅ |
| `src/SpatialView/ViewModels/MapViewModel.cs` | ZoomToExtent 커맨드 | - |
| `src/SpatialView.Engine/Data/Layers/VectorLayer.cs` | Extent 속성, Enabled/Visible | - |
| `src/SpatialView.Infrastructure/GisEngine/SpatialViewVectorLayerAdapter.cs` | 레이어 어댑터 | - |
| `src/SpatialView.Infrastructure/GisEngine/CustomMapCanvasAdapter.cs` | EngineLayerCollectionAdapter | - |

---

## 7. 디버깅 방법

1. 디버그 로그 확인:
```
[Map.GetExtents] 레이어 수: {n}, 최종 범위: {envelope}
[Map.ZoomToExtents] totalEnvelope={envelope}
```

2. 브레이크포인트 설정:
   - `Map.cs:248` - `foreach (var layer in _layers)`
   - `Map.cs:250` - `if (!layer.Enabled || !layer.Visible) continue;`
   - `MainViewModel.cs:1347` - `MapViewModel.AddLayer(sharpMapLayer);`

3. 확인 사항:
   - `_layers.Count` 가 0인지 확인
   - 각 레이어의 `Enabled`, `Visible`, `Extent` 값 확인
   - `totalEnvelope`가 `null`인지 확인

---

## 8. 테스트 시나리오

1. **단일 레이어 로드**: Shapefile 하나 로드 후 전체 영역 버튼 클릭
2. **다중 레이어 로드**: 여러 Shapefile 로드 후 전체 영역 버튼 클릭
3. **빈 레이어 로드**: 피처가 없는 Shapefile 로드 후 전체 영역 버튼 클릭
4. **FileGDB 로드**: GDB 파일 로드 후 전체 영역 버튼 클릭

---

## 9. 우선순위

1. **높음**: `Map.cs:250` - Enabled 조건 수정
2. **중간**: `MainViewModel.cs:1351` - 자동 줌 조건 수정
3. **중간**: `MainViewModel.cs:1336` - 빈 레이어 비활성화 로직 수정
4. **낮음**: 레이어 어댑터 구조 리팩토링
