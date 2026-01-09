# SpatialView

**WPF 기반 GIS 뷰어 애플리케이션**

SpatialView는 .NET 8.0과 WPF를 기반으로 한 경량 GIS(Geographic Information System) 뷰어입니다. GDAL/OGR 라이브러리를 활용하여 다양한 공간 데이터 포맷을 지원하며, 자체 개발한 렌더링 엔진을 통해 빠르고 유연한 지도 시각화를 제공합니다.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D6?style=flat-square&logo=windows)
![GDAL](https://img.shields.io/badge/GDAL-3.12-green?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)

## ✨ 주요 기능

### 📁 데이터 포맷 지원
- **Shapefile** (.shp) - ESRI Shapefile
- **GeoJSON** (.geojson, .json) - GeoJSON 포맷
- **FileGDB** (.gdb) - ESRI File Geodatabase (다중 레이어 지원)

### 🗺️ 지도 조작
- **줌**: 마우스 휠, 줌 인/아웃 버튼, 영역 선택 줌
- **팬**: 마우스 드래그
- **전체 보기**: 모든 레이어 범위로 자동 줌
- **피처 선택**: 클릭으로 피처 선택 및 속성 조회

### 📊 레이어 관리
- 다중 레이어 지원
- 레이어 가시성 토글
- 투명도 조절 (0% ~ 100%)
- 레이어 순서 변경 (드래그 앤 드롭)
- 레이어별 줌 (Zoom to Layer)

### 🎨 스타일링
- **색상 팔레트**: 다양한 색상 테마 제공
- **채움 색상**: 폴리곤 내부 색상
- **외곽선**: 색상, 두께 설정
- **포인트 심볼**: Circle, Square, Triangle, Diamond, Cross, Star
- **투명도**: 레이어별 투명도 조절

### 💾 프로젝트 관리
- 프로젝트 저장/불러오기 (.svproj)
- 레이어 설정 보존 (색상, 투명도, 가시성)
- 지도 뷰 상태 저장 (중심점, 줌 레벨)

## 🏗️ 아키텍처

```
SpatialView/
├── src/
│   ├── SpatialView/                 # WPF 애플리케이션
│   │   ├── ViewModels/              # MVVM ViewModel
│   │   ├── Views/                   # XAML Views
│   │   └── Converters/              # 값 변환기
│   │
│   ├── SpatialView.Core/            # 추상화 계층
│   │   ├── GisEngine/               # 인터페이스 정의
│   │   ├── Factories/               # 팩토리 인터페이스
│   │   └── Services/                # 서비스 인터페이스
│   │
│   ├── SpatialView.Engine/          # 렌더링 엔진
│   │   ├── Geometry/                # 지오메트리 클래스
│   │   ├── Rendering/               # WPF 렌더러
│   │   ├── Data/                    # 데이터 소스
│   │   └── Styling/                 # 스타일 시스템
│   │
│   └── SpatialView.Infrastructure/  # 외부 라이브러리 어댑터
│       ├── DataProviders/           # GDAL 데이터 프로바이더
│       ├── GisEngine/               # 엔진 어댑터
│       └── Services/                # 서비스 구현
│
└── docs/                            # 문서
```

### 기술 스택
- **프레임워크**: .NET 8.0, WPF
- **아키텍처**: MVVM (Model-View-ViewModel)
- **DI 컨테이너**: Microsoft.Extensions.DependencyInjection
- **MVVM 툴킷**: CommunityToolkit.Mvvm
- **GIS 라이브러리**: GDAL/OGR (MaxRev.Gdal.Core)

## 🚀 시작하기

### 요구 사항
- Windows 10/11 (x64)
- .NET 8.0 SDK 이상
- Visual Studio 2022 또는 VS Code

### 빌드

```bash
# 저장소 클론
git clone https://github.com/wgsystem-1/SpatialView.git
cd SpatialView

# 솔루션 빌드
dotnet build SpatialView.sln

# 애플리케이션 실행
dotnet run --project src/SpatialView/SpatialView.csproj
```

### Release 빌드

```bash
dotnet build src/SpatialView/SpatialView.csproj --configuration Release
```

빌드 결과물: `src/SpatialView/bin/Release/net8.0-windows/win-x64/`

## 📖 사용법

### 데이터 열기
1. **파일 → 열기** 또는 `Ctrl+O`
2. 지원되는 파일 형식 선택 (Shapefile, GeoJSON, FileGDB)
3. FileGDB의 경우 로드할 레이어 선택

### 지도 조작
| 동작 | 방법 |
|------|------|
| 줌 인/아웃 | 마우스 휠 |
| 팬 | 마우스 드래그 |
| 영역 줌 | 도구 모음에서 "영역 확대" 선택 후 드래그 |
| 전체 보기 | 도구 모음에서 "전체 보기" 클릭 |
| 피처 선택 | 도구 모음에서 "선택" 선택 후 클릭 |

### 레이어 관리
- **가시성**: 레이어 패널에서 체크박스 토글
- **투명도**: 슬라이더로 조절
- **순서 변경**: 드래그 앤 드롭
- **삭제**: 레이어 우클릭 → 삭제

### 프로젝트 저장
1. **파일 → 저장** 또는 `Ctrl+S`
2. `.svproj` 파일로 저장
3. 레이어 설정, 지도 뷰 상태가 모두 저장됨

## 🗂️ 지원 좌표계

- **대한민국**: EPSG:5179, EPSG:5186, EPSG:5174
- **글로벌**: EPSG:4326 (WGS84), EPSG:3857 (Web Mercator)
- 기타 GDAL이 지원하는 모든 좌표계

## 📝 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

## 🤝 기여

버그 리포트, 기능 제안, Pull Request를 환영합니다!

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📧 연락처

프로젝트 관련 문의사항은 [Issues](https://github.com/wgsystem-1/SpatialView/issues)를 통해 남겨주세요.

---

**SpatialView** - Simple, Fast, Flexible GIS Viewer
