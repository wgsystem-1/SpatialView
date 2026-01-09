# SpatialView - AI Coding Prompts (AI 코딩 프롬프트 세트)

---

## 📌 사용 방법

각 Task의 내용을 **복사하여 Cursor AI 채팅창에 붙여넣기** 하세요.  
순서대로 진행하면 SpatialView MVP가 완성됩니다.

---

## [X]M0: Project Setup (프로젝트 초기화)

### [M0-Task1] Solution 및 Project 생성

```
[Context]
SpatialView라는 Windows Desktop GIS 뷰어를 개발합니다.
.NET 8과 WPF를 사용하며, MVVM Pattern을 적용합니다.

[Instruction]
1. Solution 폴더 구조를 생성하세요:
   - SpatialView.sln (Solution 파일)
   - src/SpatialView/ (메인 WPF App Project)
   - src/SpatialView.Core/ (핵심 Business Logic, Class Library)
   - src/SpatialView.Infrastructure/ (Data Access, 외부 연동)

2. 각 Project의 .csproj 파일을 생성하세요:
   - TargetFramework: net8.0-windows
   - UseWPF: true (WPF Project만)
   - Nullable: enable
   - ImplicitUsings: enable

[Constraint]
- .NET 8.0 사용
- Project 참조: SpatialView → Core, Infrastructure / Infrastructure → Core
- 불필요한 파일은 생성하지 마세요
```

---

### [M0-Task2] NuGet Package 설치

```
[Context]
SpatialView Project에 필요한 NuGet Package를 설치해야 합니다.
GIS 기능을 위해 SharpMap과 관련 Library를 사용합니다.

[Instruction]
아래 Package들을 각 Project에 추가하세요:

SpatialView (WPF App):
- CommunityToolkit.Mvvm (8.2.2)
- Microsoft.Extensions.DependencyInjection (8.0.0)
- MaterialDesignThemes (5.0.0)
- MaterialDesignColors (3.0.0)

SpatialView.Core:
- NetTopologySuite (2.5.0)
- NetTopologySuite.IO.GeoJSON (4.0.0)
- ProjNet (2.0.0)

SpatialView.Infrastructure:
- SharpMap (2.0.0)
- SharpMap.UI (2.0.0)
- BruTile (5.0.6)
- MaxRev.Gdal.Core (3.8.0)
- MaxRev.Gdal.WindowsRuntime.Minimal (3.8.0)
- Microsoft.Data.Sqlite (8.0.0)
- Npgsql (8.0.0)

[Constraint]
- PackageReference 형식으로 .csproj에 직접 추가
- 버전은 명시된 것 또는 호환되는 최신 안정 버전 사용
```

---

### [M0-Task3] 기본 폴더 구조 및 MVVM 설정

```
[Context]
MVVM Pattern에 맞는 폴더 구조와 DI Container를 설정합니다.
docs/07_coding_convention.md의 Project Structure를 참조하세요.

[Instruction]
1. SpatialView Project에 다음 폴더를 생성하세요:
   - Assets/Icons/
   - Converters/
   - Themes/
   - Views/
   - Views/Controls/
   - Views/Dialogs/
   - ViewModels/

2. App.xaml.cs에 DI Container를 구성하세요:
   - Microsoft.Extensions.DependencyInjection 사용
   - Service, ViewModel 등록

3. MaterialDesign Theme을 App.xaml에 설정하세요

[Constraint]
- docs/05_design_system.md의 Color Palette 적용
- Primary Color: #2196F3
- Light Theme 기본
```

---

## [X]M1: Basic Map View (기본 지도 뷰)

### [M1-Task1] MainWindow 기본 레이아웃

```
[Context]
SpatialView의 메인 화면 레이아웃을 구성합니다.
docs/05_design_system.md의 Main Layout Structure를 참조하세요.

[Instruction]
MainWindow.xaml을 다음 구조로 작성하세요:

1. 상단: Toolbar (48px Height)
   - Logo/App 이름 "SpatialView"
   - 파일 열기, 저장 버튼 (Icon)
   - 배경지도 선택 ComboBox

2. 좌측: Layer Panel (280px Width, Resizable)
   - Header "레이어"
   - ListBox로 레이어 목록 (Placeholder)
   - GridSplitter로 크기 조절

3. 중앙: Map View (남은 공간 전체)
   - Border로 영역 표시 (실제 Map은 다음 Task)
   
4. 하단: Status Bar (24px Height)
   - 마우스 좌표 (X, Y)
   - 현재 축척
   - 좌표계 정보

[Constraint]
- docs/05_design_system.md의 Color, Typography 적용
- Material Design Icon 사용
- GridSplitter로 Panel 크기 조절 가능
```

---

### [M1-Task2] MapViewModel 및 Map 초기화

```
[Context]
지도 표시를 위한 ViewModel과 SharpMap 초기화를 구현합니다.
MVVM Pattern을 따르며 CommunityToolkit.Mvvm을 사용합니다.

[Instruction]
1. MapViewModel Class를 생성하세요:

   Properties (ObservableProperty):
   - Map: SharpMap.Map 객체
   - MouseX: double (마우스 X 좌표)
   - MouseY: double (마우스 Y 좌표)
   - CurrentScale: double (현재 축척)
   - CoordinateSystem: string (EPSG Code)

   Commands (RelayCommand):
   - ZoomInCommand
   - ZoomOutCommand
   - ZoomToExtentCommand

   Methods:
   - InitializeMap(): 빈 지도 초기화, 기본 범위 설정

2. MainWindow에 SharpMap MapBox Control 배치

[Constraint]
- [ObservableProperty] Attribute 사용
- [RelayCommand] Attribute 사용
- 생성자에서 InitializeMap() 호출
- 초기 범위: 대한민국 (124, 33) ~ (132, 43)
```

---

### [M1-Task3] 배경지도 추가 (OpenStreetMap)

```
[Context]
OpenStreetMap을 배경지도로 표시합니다.
BruTile Library를 사용하여 Tile Map을 로드합니다.

[Instruction]
BaseMapService Class를 생성하세요:

1. IBaseMapService Interface 정의:
   - CreateOsmLayer(): TileLayer
   - CreateBingLayer(apiKey): TileLayer
   - GetAvailableBaseMaps(): List<BaseMapInfo>

2. BaseMapService 구현:
   - BruTile.Predefined.KnownTileSources 사용
   - OSM Tile Layer 생성
   - Map에 TileLayer로 추가

3. MapViewModel에 배경지도 전환 기능:
   - SelectedBaseMap Property
   - ChangeBaseMapCommand

[Constraint]
- OSM 사용 시 User-Agent Header 설정 (정책 준수)
- 초기 화면: 대한민국 중심 (126.9780, 37.5665)
- 배경지도 On/Off Toggle 가능
```

---

## [X]M2: File Loading (파일 로딩)

### [M2-Task1] IDataProvider Interface 및 Shapefile 로딩

```
[Context]
다양한 GIS 파일 포맷을 로드하기 위한 Provider Pattern을 구현합니다.
첫 번째로 Shapefile(.shp) 로딩을 구현합니다.

[Instruction]
1. IDataProvider Interface 정의 (SpatialView.Core):
   - Task<LayerInfo> LoadAsync(string filePath)
   - string[] SupportedExtensions { get; }
   - bool CanLoad(string filePath)

2. LayerInfo Model Class:
   - Id: Guid
   - Name: string
   - FilePath: string
   - GeometryType: GeometryType enum
   - FeatureCount: int
   - Extent: Envelope
   - CRS: string

3. ShapefileDataProvider 구현 (Infrastructure):
   - SharpMap.Data.Providers.ShapeFile 사용
   - .prj 파일에서 좌표계 자동 감지
   - .cpg 파일에서 코드페이지 감지
   - VectorLayer 생성 및 반환

[Constraint]
- Async/Await 사용 (비동기 로딩)
- .shp, .shx, .dbf 파일 존재 여부 확인
- 오류 시 명확한 Exception Message
```

---

### [M2-Task2] GeoJSON 로딩

```
[Context]
GeoJSON 파일을 로드하는 Provider를 구현합니다.
NetTopologySuite.IO.GeoJSON을 사용합니다.

[Instruction]
GeoJsonDataProvider Class를 구현하세요:

1. IDataProvider Interface 구현
2. 로딩 Process:
   - 파일 읽기 (UTF-8 Encoding)
   - GeoJsonReader로 FeatureCollection Parse
   - NTS Geometry → SharpMap Geometry 변환
   - GeometryFeatureProvider로 VectorLayer 생성

3. 지원 확장자: .geojson, .json

[Constraint]
- 대용량 파일은 Streaming 방식 고려
- 좌표계 없으면 WGS84 (EPSG:4326) 기본값
- 속성(Properties) 정보 유지
```

---

### [M2-Task3] Drag & Drop 파일 열기

```
[Context]
사용자가 파일을 App 창에 Drag & Drop하여 열 수 있어야 합니다.
docs/03_user_flow.md의 File Loading Flow를 참조하세요.

[Instruction]
1. MainWindow에 Drag & Drop 기능 추가:
   - AllowDrop="True" 설정
   - DragEnter Event: 유효한 파일인지 확인, Cursor 변경
   - Drop Event: 파일 경로 추출, DataProvider 호출

2. DataLoaderService 구현:
   - 확장자로 적절한 Provider 자동 선택
   - LoadFileAsync(string filePath) Method

3. 로딩 중 표시:
   - Progress Indicator 표시
   - 로딩 완료 시 Layer Panel에 추가

[Constraint]
- 여러 파일 동시 Drop 지원
- 미지원 포맷은 Message 표시
- 로딩 중에도 UI 응답성 유지 (async/await)
- Drop 영역 시각적 Feedback (Border 색상 변경)
```

---

### [M2-Task4] FileGDB 로딩 (GDAL)

```
[Context]
Esri FileGDB (.gdb 폴더)를 열 수 있어야 합니다.
GDAL/OGR의 OpenFileGDB Driver를 사용합니다.

[Instruction]
1. GDAL 초기화:
   - App Startup에서 GdalBase.ConfigureAll() 호출

2. FileGdbDataProvider 구현:
   - Ogr.Open()으로 DataSource 열기
   - Layer 목록 조회 (GDB는 여러 Layer 포함 가능)
   - 선택된 Layer를 VectorLayer로 변환

3. Layer 선택 Dialog:
   - GDB 내 Layer 목록 표시
   - CheckBox로 여러 Layer 선택 가능

[Constraint]
- OpenFileGDB Driver (읽기 전용)
- FolderBrowserDialog 사용 (.gdb는 폴더)
- 대용량 GDB 대응 (Feature 개수 표시)
```

---

## [X]M3: Layer Management (레이어 관리)

### [M3-Task1] LayerPanel UI 및 LayerItemViewModel

```
[Context]
레이어 패널에서 로드된 레이어들을 관리합니다.
docs/05_design_system.md의 Layer Item 디자인을 참조하세요.

[Instruction]
1. LayerItemViewModel 구현:
   - Id: Guid
   - Name: string (편집 가능)
   - IsVisible: bool
   - Opacity: double (0.0 ~ 1.0)
   - GeometryType: enum (Point/Line/Polygon)
   - FeatureCount: int

2. LayerPanelViewModel 구현:
   - Layers: ObservableCollection<LayerItemViewModel>
   - SelectedLayer: LayerItemViewModel
   - AddLayerCommand
   - RemoveLayerCommand

3. LayerPanel UserControl:
   - ListBox with ItemTemplate
   - Checkbox: 표시/숨김
   - Icon: Geometry Type
   - TextBlock: Layer 이름
   - Context Menu: 삭제, Zoom to Layer

[Constraint]
- Layer 변경 시 Map 자동 갱신
- 선택된 Layer 시각적 강조 (Border #2196F3)
- Double-click으로 이름 편집
```

---

### [M3-Task2] 레이어 순서 변경 (Drag & Drop)

```
[Context]
레이어의 표시 순서를 Drag & Drop으로 변경합니다.
아래 레이어가 먼저 렌더링됩니다.

[Instruction]
1. LayerPanel ListBox에 Drag & Drop 구현:
   - MouseDown: Drag 시작
   - MouseMove: Dragging 상태 처리
   - Drop: 순서 변경

2. 시각적 Feedback:
   - Drag 중인 Item 반투명 표시
   - Drop 위치 Indicator 표시

3. Map Layer 순서 동기화:
   - LayerPanel 순서 변경 시 Map.Layers 순서도 변경

[Constraint]
- 부드러운 Animation
- Drag 중에도 다른 작업 가능
- 순서 변경 후 Map 즉시 갱신
```

---

### [M3-Task3] 레이어 투명도 조절

```
[Context]
각 레이어의 투명도를 Slider로 조절합니다.
docs/05_design_system.md의 Slider 디자인을 참조하세요.

[Instruction]
1. LayerItem에 투명도 Slider 추가:
   - Layer Item 확장 시 Slider 표시
   - 범위: 0% (완전 투명) ~ 100% (불투명)
   - 우측에 현재 % 표시

2. 투명도 적용:
   - VectorLayer.Style의 Fill/Stroke Alpha 값 변경
   - TileLayer의 경우 Layer.Opacity 변경

3. UX 개선:
   - Slider 조작 시 실시간 Map 업데이트
   - Debounce 적용 (100ms)

[Constraint]
- 0%가 되면 IsVisible = false 자동 전환 제안
- Geometry Type별 투명도 적용 방식 차이 처리
- Map 갱신 성능 최적화
```

---

## [X]M4: Attribute Table (속성 테이블)

### [M4-Task1] AttributePanel UI

```
[Context]
피처의 속성을 테이블 형태로 조회합니다.
docs/05_design_system.md의 Data Grid 디자인을 참조하세요.

[Instruction]
1. AttributePanel UserControl 구현:
   - Header: Layer 선택 ComboBox, 닫기 버튼
   - DataGrid: 피처 속성 표시
   - Footer: 피처 수 표시

2. AttributeViewModel:
   - SelectedLayer: LayerItemViewModel
   - Features: DataTable 또는 List<FeatureRow>
   - SelectedFeature: FeatureRow
   - FilterText: string

3. DataGrid 설정:
   - AutoGenerateColumns = true (속성에 따라)
   - Virtualization 적용 (대용량 대응)
   - Column Header Click으로 정렬

[Constraint]
- Panel 접기/펼치기 Animation
- Row 선택 시 Map에서 해당 Feature Highlight
- 1만개 이상 Feature도 원활히 표시
```

---

### [M4-Task2] Feature 선택 및 Highlight

```
[Context]
지도에서 피처를 클릭하면 선택되고, 속성 테이블에서 해당 행이 선택됩니다.

[Instruction]
1. 지도 클릭 → Feature 선택:
   - Map Click Event Handler
   - Hit Test: 클릭 위치 근처 Feature 찾기
   - Tolerance 설정 (5 pixel)
   - 선택된 Feature Highlight Style 적용

2. Attribute Table → Map 동기화:
   - Table Row Click → Map Feature Highlight
   - Table Row Double-click → 해당 Feature로 Zoom

3. 다중 선택:
   - Ctrl+Click: 추가 선택
   - Shift+Click: 범위 선택

[Constraint]
- Highlight 색상: #FFEB3B (노란색) 테두리, 3px
- 선택 해제: ESC 또는 빈 공간 Click
- 성능: 1만개 Feature에서도 즉각 반응
```

---

## [X]M5: Project File (프로젝트 파일)

### [M5-Task1] 프로젝트 저장

```
[Context]
현재 작업 상태를 프로젝트 파일(.svproj)로 저장합니다.
docs/04_db_schema.md의 Project File Structure를 참조하세요.

[Instruction]
1. ProjectService 구현:
   - SaveProjectAsync(string filePath, Project project)
   - Project Model에 현재 상태 수집

2. 저장 데이터:
   - Map Settings (Center, Zoom, CRS, BaseMap)
   - Layers (Source Path, Style, Visibility, Opacity, Order)

3. 파일 경로 처리:
   - Layer Source Path: 프로젝트 파일 기준 상대 경로로 변환
   - SaveFileDialog: .svproj 확장자

[Constraint]
- System.Text.Json 사용
- Indent된 JSON 출력 (가독성)
- 저장 전 유효성 검사
- 저장 성공 시 Title Bar에 파일명 표시
```

---

### [M5-Task2] 프로젝트 불러오기

```
[Context]
저장된 프로젝트 파일을 열어 작업 상태를 복원합니다.

[Instruction]
1. LoadProjectAsync 구현:
   - JSON Parse
   - Version 호환성 Check
   - Layer 순차 로딩 (Progress 표시)
   - Map Settings 복원

2. 오류 처리:
   - Source 파일 없음: 경고 후 Skip
   - 잘못된 파일 형식: 명확한 Error Message
   - 부분 로드 성공: 결과 요약 표시

3. 최근 파일 목록:
   - Settings DB에 최근 Project 10개 저장
   - 시작 화면 또는 File Menu에서 빠른 접근

[Constraint]
- 기존 작업 있으면 저장 여부 확인 Dialog
- 로딩 중 Cancel 가능
- 상대 경로 → 절대 경로 변환
```

---

### [M5-Task3] 최근 파일 및 시작 화면

```
[Context]
앱 시작 시 최근 프로젝트 목록을 표시하고 빠르게 열 수 있습니다.

[Instruction]
1. Recent Files 관리:
   - SQLite에 최근 파일 저장
   - 파일 Open/Save 시 목록 업데이트
   - 최대 10개 유지

2. File Menu에 Recent Files:
   - 최근 프로젝트 목록 표시
   - Click으로 바로 열기
   - 없는 파일은 목록에서 제거

3. Welcome Dialog (선택):
   - 앱 시작 시 표시
   - 최근 프로젝트 목록
   - 새 프로젝트 / 파일 열기 버튼

[Constraint]
- 파일 경로 Full Path 표시 (Tooltip)
- 존재하지 않는 파일 자동 정리
- 고정(Pin) 기능 (삭제되지 않음)
```

---

## [X]M6: Polish & Optimization (마무리)

### [M6-Task1] Error Handling 및 Logging

```
[Context]
사용자 친화적인 오류 처리와 디버깅을 위한 로깅을 구현합니다.

[Instruction]
1. Global Exception Handler:
   - App.xaml.cs에 DispatcherUnhandledException 처리
   - 사용자 친화적 Error Dialog 표시
   - 오류 상세 정보 로깅

2. 작업별 Exception 처리:
   - 파일 열기 실패
   - DB 연결 실패
   - 메모리 부족

3. Logging:
   - %LOCALAPPDATA%\SpatialView\Logs\ 에 로그 저장
   - 날짜별 파일 분리
   - Log Level: Info, Warning, Error

[Constraint]
- Stack Trace는 로그에만 (사용자에게 미표시)
- 민감 정보 로깅 금지
- 7일 이상 된 로그 자동 삭제
```

---

### [M6-Task2] 성능 최적화

```
[Context]
대용량 데이터 처리 시 성능을 최적화합니다.

[Instruction]
1. Layer 로딩 최적화:
   - Async 로딩으로 UI Blocking 방지
   - Progress 표시
   - 취소 기능 (CancellationToken)

2. Map 렌더링 최적화:
   - Level of Detail (LOD) 적용
   - Viewport 외 Feature 제외
   - Tile Cache 활용

3. Attribute Table 최적화:
   - DataGrid Virtualization
   - Lazy Loading (Scroll 시 로드)

[Constraint]
- 1GB Shapefile: 5초 내 로딩
- 100만 Feature Layer: 원활한 Pan/Zoom
- Memory 사용량 1GB 이하 유지
```

