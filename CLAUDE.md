# CLAUDE.md - SpatialView 프로젝트 정보

## 프로젝트 개요

SpatialView는 **WPF 기반**의 GIS(Geographic Information System) 뷰어 애플리케이션입니다.

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

```bash
# WPF 앱 빌드
dotnet build src/SpatialView/SpatialView.csproj

# WPF 앱 실행
dotnet run --project src/SpatialView/SpatialView.csproj

# 전체 솔루션 빌드
dotnet build SpatialView.sln
```

## 개발 시 유의사항

1. **플랫폼**: x64 빌드 권장 (GDAL 네이티브 라이브러리)
2. **GDAL**: MaxRev.Gdal.Core 사용 중, OpenFileGDB 드라이버 활성화됨
3. **좌표계**: 대한민국 좌표계 (EPSG:5179, 5186 등) 및 WGS84 (EPSG:4326)
4. **DI 컨테이너**: Microsoft.Extensions.DependencyInjection 사용

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

- `docs/` 폴더 내 문서 참조
