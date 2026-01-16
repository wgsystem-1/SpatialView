# CLAUDE.md - SpatialView 프로젝트 정보

## 프로젝트 개요

SpatialView는 **WPF 기반**의 GIS(Geographic Information System) 뷰어 애플리케이션입니다.

## 📝 개발일지 (중요!)

> **모든 개발 작업은 반드시 `docs/개발일지.md`에 기록해야 합니다.**

### 기록 규칙
1. **날짜/시간 필수**: `YYYY-MM-DD HH:MM` 형식으로 작업 시간 기록
2. **작업 내용**: 구현한 기능, 수정한 버그, 변경 사항 상세 기술
3. **코드 변경**: 주요 코드 변경 사항은 코드 블록으로 기록
4. **향후 계획**: 예정된 작업, 알려진 버그 등 업데이트

### 기록 예시
```markdown
## 2026-01-10

### 14:30 - 스타일링 시스템 구현 완료

#### 구현 내용
- LayerRenderStyle 클래스 추가
- VectorRenderer 스타일 적용 메서드 추가

#### 수정 파일
- `src/SpatialView.Engine/Rendering/RenderContext.cs`
- `src/SpatialView.Engine/Rendering/VectorRenderer.cs`
```

## 기술 스택

- **프레임워크**: .NET 8.0, WPF
- **아키텍처**: MVVM (Model-View-ViewModel)
- **주요 라이브러리**:
  - GDAL/OGR (FileGDB, Shapefile 등 GIS 포맷)
  - CommunityToolkit.Mvvm (MVVM 지원)
  - Microsoft.Extensions.DependencyInjection (DI)

## 프로젝트 구조

```
SpatialView/
├── src/
│   ├── SpatialView.Core/           # 추상화 인터페이스 및 도메인 모델
│   ├── SpatialView.Engine/         # 지오메트리 및 데이터 처리 엔진
│   ├── SpatialView.Infrastructure/ # 외부 라이브러리 어댑터 (GDAL, DataProviders)
│   └── SpatialView/                # WPF 애플리케이션
│       ├── Converters/             # 값 변환기
│       ├── Resources/              # 아이콘, 스타일
│       ├── Themes/                 # 테마 리소스
│       ├── ViewModels/             # MVVM ViewModel
│       └── Views/                  # XAML Views, Controls, Dialogs
└── docs/                           # 문서
```

## 주요 기능

1. **파일 지원**: Shapefile (.shp), GeoJSON (.geojson), FileGDB (.gdb)
2. **지도 조작**: 줌 (마우스 휠), 팬 (드래그), 피처 선택
3. **레이어 관리**: 다중 레이어, 가시성 토글, 투명도, 순서변경
4. **속성 조회**: 레이어 정보, 피처 속성 표시
5. **FileGDB**: 레이어 선택 다이얼로그로 다중 레이어 로드

## 빌드 및 실행

> ⚠️ **중요**: 항상 **Release 모드 + win-x64** 빌드를 사용합니다.

```bash
# WPF 앱 빌드 (Release, win-x64) - 기본 빌드 명령
dotnet build src/SpatialView/SpatialView.csproj -c Release -r win-x64

# WPF 앱 실행
dotnet run --project src/SpatialView/SpatialView.csproj -c Release -r win-x64

# 전체 솔루션 빌드
dotnet build SpatialView.sln -c Release -r win-x64
```

### 빌드 출력 경로
```
g:\Project\SpatialView\src\SpatialView\bin\Release\net10.0-windows\win-x64\SpatialView.exe
```

## 개발 시 유의사항

1. **빌드 모드**: 항상 **Release + win-x64** 사용 (GDAL 네이티브 라이브러리 호환성)
2. **플랫폼**: x64 빌드 필수 (GDAL 네이티브 라이브러리)
2. **GDAL**: MaxRev.Gdal.Core 사용 중, OpenFileGDB 드라이버 활성화됨
3. **좌표계**: 대한민국 좌표계 (EPSG:5179, 5186 등) 및 WGS84 (EPSG:4326)
4. **DI 컨테이너**: Microsoft.Extensions.DependencyInjection 사용
5. **빌드**: 오류 및 경고를 모두 제거
6. **빌드결과**: 빌드 결과를 한글로 보고한다.보고내용은 오류/경고 갯수, 빌드시간, 빌드시 날짜와 시간(YYYY-MM-DD HH:MM)을 포함한다.

## 주요 클래스

| 클래스 | 역할 |
|--------|------|
| `MainWindow` | 메인 윈도우 |
| `MapControl` | 지도 렌더링 컨트롤 |
| `MapViewModel` | 지도 관련 ViewModel |
| `WpfMapRenderer` | WPF 기반 지도 렌더러 |
| `ShapefileDataProvider` | Shapefile 읽기 |
| `FileGdbDataProvider` | FileGDB 읽기 (GDAL/OGR) |
| `VectorLayer` | 벡터 레이어 |
| `SpatialViewVectorLayerAdapter` | 레이어 어댑터 |

## 참고 문서

- `docs/개발일지.md` - **개발 진행 상황, 버그 수정, 기능 추가 기록 (필수 참조)**
- `docs/` 폴더 내 기타 문서 참조

## GitHub 저장소

- **URL**: https://github.com/wgsystem-1/SpatialView
- **브랜치**: main
