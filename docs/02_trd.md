# SpatialView - Technical Requirements Document (TRD)
## 기술 요구사항 정의서

---

## 1. System Architecture (시스템 아키텍처)

### 1.1 High-Level Architecture (고수준 아키텍처)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        SpatialView Application                          │
│                         (Windows Desktop)                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    Presentation Layer (UI)                       │   │
│  │  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐    │   │
│  │  │MainWindow │  │ LayerPanel│  │ MapControl│  │ AttrPanel │    │   │
│  │  │  (XAML)   │  │  (XAML)   │  │  (XAML)   │  │  (XAML)   │    │   │
│  │  └───────────┘  └───────────┘  └───────────┘  └───────────┘    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                  │                                      │
│                                  ▼                                      │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                   ViewModel Layer (MVVM)                         │   │
│  │  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐    │   │
│  │  │ MainVM    │  │ LayerVM   │  │  MapVM    │  │ AttrVM    │    │   │
│  │  └───────────┘  └───────────┘  └───────────┘  └───────────┘    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                  │                                      │
│                                  ▼                                      │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                      Service Layer                               │   │
│  │  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐    │   │
│  │  │DataLoader │  │ LayerSvc  │  │ProjectSvc │  │ StyleSvc  │    │   │
│  │  │ Service   │  │           │  │           │  │           │    │   │
│  │  └───────────┘  └───────────┘  └───────────┘  └───────────┘    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                  │                                      │
│                                  ▼                                      │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                   Data Access Layer                              │   │
│  │  ┌─────────────────────────────────────────────────────────┐    │   │
│  │  │                    Data Providers                        │    │   │
│  │  │  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐      │    │   │
│  │  │  │ SHP │ │JSON │ │ KML │ │ DXF │ │ GDB │ │ DB  │      │    │   │
│  │  │  └─────┘ └─────┘ └─────┘ └─────┘ └─────┘ └─────┘      │    │   │
│  │  └─────────────────────────────────────────────────────────┘    │   │
│  │  ┌───────────────────┐  ┌───────────────────┐                   │   │
│  │  │    SharpMap       │  │     BruTile       │                   │   │
│  │  │   (GIS Engine)    │  │  (Tile Loading)   │                   │   │
│  │  └───────────────────┘  └───────────────────┘                   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         External Resources                              │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐          │
│  │  Files  │ │ SQLite  │ │ PostGIS │ │SQLServer│ │OSM Tiles│          │
│  │(SHP,etc)│ │         │ │         │ │         │ │         │          │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘ └─────────┘          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Layer Diagram (레이어 다이어그램)

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **Presentation** | SpatialView | XAML Views, User Controls, Dialogs |
| **ViewModel** | SpatialView | Data Binding, Commands, UI State |
| **Service** | SpatialView.Core | Business Logic, Interfaces |
| **Data Access** | SpatialView.Infrastructure | File I/O, DB Connection, External API |

### 1.3 Project References (프로젝트 참조)

```
SpatialView (WPF App)
    ├── → SpatialView.Core
    └── → SpatialView.Infrastructure

SpatialView.Infrastructure
    └── → SpatialView.Core

SpatialView.Core
    └── (No internal dependencies)
```

---

## 2. Tech Stack (기술 스택)

### 2.1 Core Technologies (핵심 기술)

| Category | Technology | Version | Rationale (선정 이유) |
|----------|------------|---------|----------------------|
| **Runtime** | .NET | 8.0 LTS | 최신 LTS 버전, 성능 향상, 장기 지원 |
| **UI Framework** | WPF | - | Windows Native, MVVM Pattern 지원, 풍부한 컨트롤 |
| **Language** | C# | 12.0 | Primary Constructors, Pattern Matching |
| **MVVM Toolkit** | CommunityToolkit.Mvvm | 8.2.x | Source Generator, Boilerplate 감소 |
| **DI Container** | Microsoft.Extensions.DependencyInjection | 8.0.x | .NET 표준, 경량 |

### 2.2 GIS Libraries (GIS 라이브러리)

| Category | Technology | Version | Rationale (선정 이유) |
|----------|------------|---------|----------------------|
| **GIS Engine** | SharpMap | 2.0.x | 사용자 요청, 다양한 Provider 지원 |
| **Geometry** | NetTopologySuite (NTS) | 2.5.x | 공간 연산 표준, JTS Porting |
| **Coordinate Transform** | ProjNet | 2.0.x | 좌표계 변환, EPSG 지원 |
| **Tile Map** | BruTile | 5.0.x | OSM, Bing, WMS Tile 로딩 |
| **GDAL Binding** | MaxRev.Gdal.Core | 3.8.x | FileGDB, DXF 등 추가 포맷 |

### 2.3 Data Access (데이터 액세스)

| Category | Technology | Version | Rationale (선정 이유) |
|----------|------------|---------|----------------------|
| **Local DB** | SQLite | via Microsoft.Data.Sqlite 8.0.x | 경량 내장 DB, 설정 파일 저장 |
| **Spatial DB** | SpatiaLite | - | SQLite 공간 확장, 로컬 공간 쿼리 |
| **PostgreSQL** | Npgsql | 8.0.x | PostGIS 연동용 .NET Driver |
| **SQL Server** | Microsoft.Data.SqlClient | 5.x | SQL Server Spatial 연동 |
| **JSON** | System.Text.Json | (built-in) | 프로젝트 파일, 설정 저장 |

### 2.4 UI/UX Libraries (UI/UX 라이브러리)

| Category | Technology | Version | Rationale (선정 이유) |
|----------|------------|---------|----------------------|
| **Design System** | Material Design In XAML | 5.0.x | Flat Colorful 디자인, 풍부한 컨트롤 |
| **Icons** | Material Design Icons | - | 일관된 아이콘 세트 |

---

## 3. Supported Formats (지원 포맷)

| Format | Extension | Provider | Read | Write | Notes |
|--------|-----------|----------|------|-------|-------|
| **Shapefile** | .shp | SharpMap.Providers.ShapeFile | ✅ | ✅ | .shx, .dbf, .prj 필요 |
| **GeoJSON** | .geojson, .json | NTS.IO.GeoJSON | ✅ | ✅ | UTF-8 Encoding |
| **KML** | .kml | SharpMap.Extensions / Custom | ✅ | ❌ | Google Earth 형식 |
| **KMZ** | .kmz | Unzip + KML | ✅ | ❌ | 압축된 KML |
| **GeoPackage** | .gpkg | GDAL/OGR | ✅ | ✅ | OGC 표준 |
| **FileGDB** | .gdb (folder) | GDAL/OGR (OpenFileGDB) | ✅ | ❌ | Esri 포맷 |
| **DXF** | .dxf | GDAL/OGR | ✅ | ❌ | AutoCAD 호환 |
| **GPX** | .gpx | Custom Parser | ✅ | ✅ | GPS Track |
| **PostGIS** | - | Npgsql + NTS | ✅ | ✅ | PostgreSQL 공간 확장 |
| **SQL Server Spatial** | - | SqlClient + NTS | ✅ | ✅ | MSSQL Geometry/Geography |

---

## 4. Data Lifecycle (데이터 생명주기)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           Data Lifecycle                                  │
└──────────────────────────────────────────────────────────────────────────┘

[1. INPUT]                    [2. PROCESS]                   [3. OUTPUT]
    │                              │                              │
    ▼                              ▼                              ▼
┌─────────┐    ┌─────────────────────────────────────┐    ┌─────────────┐
│ File    │───▶│  Format Detection                   │    │ Map Render  │
│ Drop/   │    │  ┌─────────────────────────────┐   │───▶│             │
│ Open    │    │  │ Extension → Provider Match  │   │    │ Layer Panel │
└─────────┘    │  └─────────────────────────────┘   │    │             │
               │               │                     │    │ Attr Table  │
┌─────────┐    │               ▼                     │    └─────────────┘
│ DB      │───▶│  ┌─────────────────────────────┐   │
│ Connect │    │  │ Data Loading (Async)        │   │    ┌─────────────┐
└─────────┘    │  │  - Read Geometry            │   │    │ Project     │
               │  │  - Read Attributes          │   │───▶│ File Save   │
┌─────────┐    │  │  - Detect CRS               │   │    │ (.svproj)   │
│ Project │───▶│  └─────────────────────────────┘   │    └─────────────┘
│ File    │    │               │                     │
│ (.svproj)    │               ▼                     │    ┌─────────────┐
└─────────┘    │  ┌─────────────────────────────┐   │    │ Export      │
               │  │ Layer Object Creation       │   │───▶│ (CSV, etc)  │
               │  │  - Apply Style              │   │    └─────────────┘
               │  │  - Add to Map               │   │
               │  └─────────────────────────────┘   │
               └─────────────────────────────────────┘

[Memory Management]
┌────────────────────────────────────────────────────────────────────────┐
│  Layer Removed  ───▶  Dispose Provider  ───▶  Release Memory          │
│  App Closed     ───▶  Save Settings     ───▶  Dispose All Resources   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Security Policy (보안 정책)

### 5.1 File System Security (파일 시스템 보안)

| Policy | Implementation |
|--------|----------------|
| **User Consent** | 사용자가 명시적으로 선택한 파일/폴더만 접근 |
| **Path Validation** | 경로 조작 공격 방지 (Path Traversal Prevention) |
| **Extension Validation** | 허용된 확장자만 처리 |

### 5.2 Database Security (데이터베이스 보안)

| Policy | Implementation |
|--------|----------------|
| **Connection String Encryption** | DPAPI를 사용한 암호화 저장 |
| **Parameterized Queries** | SQL Injection 방지 |
| **Minimal Privileges** | 필요한 최소 권한으로 연결 |

### 5.3 Network Security (네트워크 보안)

| Policy | Implementation |
|--------|----------------|
| **HTTPS Only** | Tile Server, 인증 서버 통신 시 HTTPS 필수 |
| **Certificate Validation** | SSL 인증서 유효성 검증 |
| **User-Agent** | 올바른 User-Agent 헤더 설정 (OSM 정책 준수) |

### 5.4 Application Security (애플리케이션 보안)

| Policy | Implementation |
|--------|----------------|
| **Code Signing** | 배포 시 Installer 코드 서명 |
| **Dependency Audit** | NuGet Package 취약점 정기 검사 |
| **Error Handling** | 민감 정보 노출 방지 (Stack Trace 등) |

---

## 6. Performance Requirements (성능 요구사항)

| Metric | Target | Measurement Method |
|--------|--------|--------------------|
| **App Startup** | < 3초 | Cold Start 측정 |
| **File Load (100MB SHP)** | < 5초 | Stopwatch Logging |
| **Pan/Zoom Response** | < 100ms | 체감 테스트 |
| **Memory (Idle)** | < 200MB | Task Manager |
| **Memory (10 Layers)** | < 1GB | Profiling |

---

## 7. Error Handling Strategy (오류 처리 전략)

```
Application Level
    └── App.xaml.cs: DispatcherUnhandledException
         └── Logging + 사용자 친화적 메시지 + 선택적 재시작

Service Level
    └── try-catch with specific exceptions
         └── DataLoadException → "파일을 열 수 없습니다: {이유}"
         └── DatabaseException → "DB 연결 실패: {서버}"

ViewModel Level  
    └── Command 실행 시 예외 처리
         └── UI에 오류 상태 표시
         └── 작업 Rollback (필요시)
```

---

## 8. Build & Deployment (빌드 및 배포)

### 8.1 Build Configuration

| Configuration | Purpose | Settings |
|---------------|---------|----------|
| **Debug** | 개발/디버깅 | 최적화 OFF, Debug Symbol 포함 |
| **Release** | 배포용 | 최적화 ON, Trimming 가능 |

### 8.2 Deployment Options

| Method | Description |
|--------|-------------|
| **ZIP Archive** | 빌드 결과물 압축 배포 (초기 단계) |
| **Installer (MSIX)** | 설치 마법사 제공 (안정화 후) |
| **Self-contained** | .NET Runtime 포함 배포 (의존성 없음) |

### 8.3 Target Platforms

| Platform | Support |
|----------|---------|
| **Windows 10** | ✅ (1809+) |
| **Windows 11** | ✅ |
| **macOS** | ❌ (Not planned) |
| **Linux** | ❌ (Not planned) |

