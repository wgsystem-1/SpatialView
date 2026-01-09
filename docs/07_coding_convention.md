# SpatialView - Coding Convention (AI 협업 가이드)

---

## 1. Project Structure (프로젝트 구조)

```
SpatialView/
├── SpatialView.sln
├── docs/                               # 설계 문서
│   ├── 01_prd.md
│   ├── 02_trd.md
│   ├── 03_user_flow.md
│   ├── 04_db_schema.md
│   ├── 05_design_system.md
│   ├── 06_tasks_prompts.md
│   └── 07_coding_convention.md
│
├── src/
│   ├── SpatialView/                    # WPF Application (Presentation)
│   │   ├── SpatialView.csproj
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Assets/
│   │   │   ├── Icons/                  # SVG/XAML Icons
│   │   │   └── Images/
│   │   ├── Converters/                 # Value Converters
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   └── GeometryTypeToIconConverter.cs
│   │   ├── Themes/                     # XAML Styles & Resources
│   │   │   ├── Generic.xaml
│   │   │   ├── Colors.xaml
│   │   │   └── Controls.xaml
│   │   ├── Views/                      # XAML Views
│   │   │   ├── MainWindow.xaml
│   │   │   ├── Controls/
│   │   │   │   ├── LayerPanel.xaml
│   │   │   │   ├── MapControl.xaml
│   │   │   │   └── AttributePanel.xaml
│   │   │   └── Dialogs/
│   │   │       ├── LayerSelectDialog.xaml
│   │   │       └── SettingsDialog.xaml
│   │   └── ViewModels/                 # ViewModels (MVVM)
│   │       ├── MainViewModel.cs
│   │       ├── MapViewModel.cs
│   │       ├── LayerPanelViewModel.cs
│   │       ├── LayerItemViewModel.cs
│   │       └── AttributeViewModel.cs
│   │
│   ├── SpatialView.Core/               # Business Logic (Domain)
│   │   ├── SpatialView.Core.csproj
│   │   ├── Models/                     # Domain Models
│   │   │   ├── Project.cs
│   │   │   ├── LayerInfo.cs
│   │   │   ├── FeatureInfo.cs
│   │   │   └── StyleDefinition.cs
│   │   ├── Services/                   # Business Services
│   │   │   ├── Interfaces/
│   │   │   │   ├── IProjectService.cs
│   │   │   │   ├── IDataProvider.cs
│   │   │   │   └── ILayerService.cs
│   │   │   ├── ProjectService.cs
│   │   │   └── LayerService.cs
│   │   ├── Enums/
│   │   │   └── GeometryType.cs
│   │   └── Extensions/
│   │       ├── GeometryExtensions.cs
│   │       └── EnvelopeExtensions.cs
│   │
│   └── SpatialView.Infrastructure/     # Data Access (Infrastructure)
│       ├── SpatialView.Infrastructure.csproj
│       ├── DataProviders/              # File Format Providers
│       │   ├── ShapefileDataProvider.cs
│       │   ├── GeoJsonDataProvider.cs
│       │   ├── FileGdbDataProvider.cs
│       │   └── DataProviderFactory.cs
│       ├── Repositories/               # Data Persistence
│       │   ├── SettingsRepository.cs
│       │   └── RecentFilesRepository.cs
│       └── Services/                   # External Services
│           ├── BaseMapService.cs
│           └── TileCacheService.cs
│
└── tests/
    ├── SpatialView.Core.Tests/
    └── SpatialView.Infrastructure.Tests/
```

---

## 2. Naming Rules (네이밍 규칙)

### 2.1 General Naming Conventions

| Category | Convention | Example |
|----------|------------|---------|
| **Namespace** | PascalCase | `SpatialView.Core.Models` |
| **Class** | PascalCase (명사) | `LayerManager`, `ShapefileProvider` |
| **Interface** | I + PascalCase | `IDataProvider`, `ILayerService` |
| **Method** | PascalCase (동사) | `LoadAsync()`, `GetFeatures()` |
| **Async Method** | PascalCase + Async | `LoadLayerAsync()`, `SaveProjectAsync()` |
| **Property** | PascalCase | `SelectedLayer`, `IsVisible` |
| **Private Field** | _camelCase | `_layerService`, `_map` |
| **Parameter** | camelCase | `filePath`, `layerId` |
| **Local Variable** | camelCase | `result`, `tempLayer` |
| **Constant** | PascalCase | `MaxZoomLevel`, `DefaultSrid` |
| **Static Readonly** | PascalCase | `DefaultStyle`, `EmptyGuid` |
| **Enum** | PascalCase | `GeometryType.Point` |
| **Enum Member** | PascalCase | `GeometryType.LineString` |

### 2.2 XAML Naming Conventions

| Category | Convention | Example |
|----------|------------|---------|
| **UserControl** | PascalCase + Control/Panel | `LayerPanel`, `MapControl` |
| **Window** | PascalCase + Window/Dialog | `MainWindow`, `SettingsDialog` |
| **x:Name** | PascalCase | `LayerListBox`, `MapContainer` |
| **Style Key** | PascalCase + Style | `PrimaryButtonStyle`, `LayerItemStyle` |
| **Template Key** | PascalCase + Template | `LayerItemTemplate` |
| **Resource Key** | PascalCase | `PrimaryColor`, `HeaderFontSize` |

### 2.3 File Naming Conventions

| Category | Convention | Example |
|----------|------------|---------|
| **C# Class** | PascalCase.cs | `LayerService.cs` |
| **Interface** | IPascalCase.cs | `IDataProvider.cs` |
| **XAML View** | PascalCase.xaml | `MainWindow.xaml` |
| **ViewModel** | PascalCase + ViewModel.cs | `MapViewModel.cs` |
| **Test** | PascalCase + Tests.cs | `LayerServiceTests.cs` |

---

## 3. Coding Standards (코딩 표준)

### 3.1 Class Structure Order

```csharp
public class ExampleClass
{
    // 1. Constants
    private const int MaxRetryCount = 3;
    
    // 2. Static Fields
    private static readonly Logger _logger = new();
    
    // 3. Private Fields
    private readonly IDataProvider _dataProvider;
    private string _name;
    
    // 4. Constructors
    public ExampleClass(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }
    
    // 5. Properties
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    
    // 6. Public Methods
    public async Task LoadAsync() { }
    
    // 7. Private Methods
    private void ValidateInput() { }
}
```

### 3.2 ViewModel Pattern (CommunityToolkit.Mvvm)

```csharp
public partial class LayerViewModel : ObservableObject
{
    private readonly ILayerService _layerService;
    
    // ObservableProperty - 자동 PropertyChanged 생성
    [ObservableProperty]
    private string _name;
    
    [ObservableProperty]
    private bool _isVisible = true;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpacityPercent))]
    private double _opacity = 1.0;
    
    // Computed Property
    public string OpacityPercent => $"{Opacity * 100:F0}%";
    
    // RelayCommand - 자동 Command 생성
    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }
    
    [RelayCommand]
    private async Task LoadLayerAsync(string filePath)
    {
        await _layerService.LoadAsync(filePath);
    }
}
```

### 3.3 Dependency Injection

```csharp
// ✅ Good: Constructor Injection
public class MapViewModel
{
    private readonly IDataProvider _dataProvider;
    private readonly IProjectService _projectService;
    
    public MapViewModel(
        IDataProvider dataProvider, 
        IProjectService projectService)
    {
        _dataProvider = dataProvider;
        _projectService = projectService;
    }
}

// ❌ Bad: Service Locator / Direct Instantiation
public class MapViewModel
{
    private ShapefileDataProvider _provider = new();  // ❌
    
    public void Load()
    {
        var service = ServiceLocator.Get<IProjectService>();  // ❌
    }
}
```

### 3.4 Async/Await Pattern

```csharp
// ✅ Good: Proper Async Pattern
public async Task<LayerInfo> LoadLayerAsync(
    string filePath, 
    CancellationToken cancellationToken = default)
{
    try
    {
        var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var layer = await ParseLayerAsync(data, cancellationToken);
        return layer;
    }
    catch (OperationCanceledException)
    {
        _logger.Info("Layer loading cancelled");
        throw;
    }
    catch (Exception ex)
    {
        _logger.Error($"Failed to load layer: {ex.Message}");
        throw new DataLoadException($"파일을 열 수 없습니다: {filePath}", ex);
    }
}

// ❌ Bad: Blocking Async
public LayerInfo LoadLayer(string filePath)
{
    return LoadLayerAsync(filePath).Result;  // ❌ Deadlock 위험
}
```

---

## 4. AI Communication Rules (AI 협업 규칙)

### 4.1 코드 작성 전 계획 설명

AI에게 작업을 요청할 때, AI는 다음 순서를 따라야 합니다:

1. **계획 설명** - 어떤 파일을 생성/수정할지 먼저 설명
2. **구조 제시** - Class/Method 구조 간략히 설명
3. **코드 작성** - 실제 코드 구현
4. **테스트 방법** - 동작 확인 방법 안내

### 4.2 Prompt 작성 규칙

```
[Context]
현재 상황과 배경을 설명합니다.
어떤 파일/기능과 관련된 작업인지 명시합니다.

[Instruction]
구체적인 작업 내용을 단계별로 나열합니다.
1. 첫 번째 작업
2. 두 번째 작업

[Constraint]
지켜야 할 제약 조건을 명시합니다.
- 사용할 Library
- 따라야 할 Pattern
- 참조할 문서
```

### 4.3 문서 참조 규칙

| 문서 | 참조 상황 |
|------|----------|
| `01_prd.md` | 기능 요구사항, User Story 확인 |
| `02_trd.md` | 기술 스택, Architecture 확인 |
| `03_user_flow.md` | 사용자 흐름, UX 로직 확인 |
| `04_db_schema.md` | 데이터 구조, Model 설계 확인 |
| `05_design_system.md` | UI 스타일, 색상, 컴포넌트 확인 |
| `07_coding_convention.md` | 네이밍, 코딩 표준 확인 |

### 4.4 AI에게 전달할 규칙 템플릿

```
[AI 작업 규칙]

1. MVVM Pattern 준수
   - View에 Business Logic 금지
   - ViewModel에 UI 요소 직접 참조 금지
   - CommunityToolkit.Mvvm 사용

2. Dependency Injection 사용
   - 모든 Service는 Interface로 정의
   - Constructor Injection 사용
   - App.xaml.cs에서 DI Container 구성

3. Async 처리
   - I/O 작업은 모두 async/await 사용
   - UI Thread Blocking 금지
   - CancellationToken 지원

4. 예외 처리
   - 사용자 친화적 Error Message (한글)
   - 상세 정보는 Log에만 기록
   - Custom Exception 정의

5. 네이밍
   - docs/07_coding_convention.md 참조
   - 영문 PascalCase 기본

6. 코드 주석
   - 복잡한 로직에 한글 주석
   - Public API는 XML Doc Comment (영문)

7. 한 번에 하나의 기능만 구현
   - 작은 단위로 나누어 진행
   - 각 단계 완료 후 테스트 가능하게
```

---

## 5. Git Commit Convention (Git 커밋 규칙)

### 5.1 Commit Message Format

```
<type>: <subject>

[optional body]

[optional footer]
```

### 5.2 Commit Types

| Type | Description | Example |
|------|-------------|---------|
| **feat** | 새로운 기능 | `feat: 레이어 투명도 조절 기능 추가` |
| **fix** | 버그 수정 | `fix: Shapefile 인코딩 오류 수정` |
| **refactor** | 리팩토링 | `refactor: DataProvider 인터페이스 분리` |
| **style** | 코드 스타일 | `style: 들여쓰기 통일` |
| **docs** | 문서 수정 | `docs: README 설치 방법 추가` |
| **test** | 테스트 추가 | `test: LayerService 단위 테스트 추가` |
| **chore** | 기타 작업 | `chore: NuGet 패키지 업데이트` |

### 5.3 Branch Strategy

```
main                    # 안정 버전
├── develop             # 개발 통합
│   ├── feature/layer-management
│   ├── feature/file-loading
│   └── feature/attribute-table
└── release/v1.0        # 릴리스 준비
```

---

## 6. Code Review Checklist (코드 리뷰 체크리스트)

### 6.1 General

- [ ] 네이밍 규칙 준수
- [ ] MVVM Pattern 준수
- [ ] 불필요한 코드/주석 없음
- [ ] Magic Number 없음 (상수 사용)

### 6.2 Performance

- [ ] Async/Await 적절히 사용
- [ ] 대용량 데이터 처리 고려
- [ ] Memory Leak 없음 (Dispose 처리)

### 6.3 Error Handling

- [ ] 적절한 예외 처리
- [ ] 사용자 친화적 메시지
- [ ] 로깅 추가

### 6.4 UI/UX

- [ ] Design System 준수
- [ ] 반응성 있는 UI
- [ ] 접근성 고려

---

## 7. Quick Reference (빠른 참조)

### 7.1 자주 사용하는 NuGet Package

```xml
<!-- MVVM -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />

<!-- DI -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />

<!-- UI -->
<PackageReference Include="MaterialDesignThemes" Version="5.0.0" />

<!-- GIS -->
<PackageReference Include="SharpMap" Version="2.0.0" />
<PackageReference Include="NetTopologySuite" Version="2.5.0" />
<PackageReference Include="BruTile" Version="5.0.6" />
```

### 7.2 자주 사용하는 using

```csharp
// MVVM
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// GIS
using SharpMap;
using SharpMap.Layers;
using NetTopologySuite.Geometries;

// Async
using System.Threading;
using System.Threading.Tasks;
```

### 7.3 XAML Namespace

```xml
<!-- MaterialDesign -->
xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes"

<!-- Local -->
xmlns:vm="clr-namespace:SpatialView.ViewModels"
xmlns:ctrl="clr-namespace:SpatialView.Views.Controls"
xmlns:conv="clr-namespace:SpatialView.Converters"
```

