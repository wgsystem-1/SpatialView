# SpatialView - Database Schema (데이터베이스 설계)

---

## 1. Project File Structure (.svproj - JSON)

프로젝트 파일은 JSON 형식으로 저장되며, 다음 구조를 따릅니다.

### 1.1 Entity Relationship Diagram

```mermaid
erDiagram
    PROJECT {
        string id PK "GUID - 프로젝트 고유 ID"
        string version "파일 포맷 버전 (1.0)"
        string name "프로젝트 이름"
        datetime createdAt "생성 일시 (ISO 8601)"
        datetime modifiedAt "수정 일시 (ISO 8601)"
    }
    
    MAP_SETTINGS {
        float centerX "지도 중심 X (경도, Longitude)"
        float centerY "지도 중심 Y (위도, Latitude)"
        float zoomLevel "Zoom Level"
        string srid "좌표계 SRID (EPSG:4326)"
        string baseMapType "배경지도 (OSM/Bing/None)"
        float rotation "회전 각도 (degrees)"
    }
    
    LAYER {
        string id PK "GUID - Layer 고유 ID"
        string name "Layer 표시명"
        int displayOrder "표시 순서 (0=최하단)"
        boolean visible "표시 여부"
        float opacity "투명도 (0.0~1.0)"
        string sourceType "Source 유형 (File/Database)"
        string sourcePath "파일 경로 (상대경로)"
        string geometryType "Point/LineString/Polygon"
    }
    
    LAYER_STYLE {
        string fillColor "채우기 색상 (#RRGGBB)"
        float fillOpacity "채우기 투명도 (0.0~1.0)"
        string strokeColor "선 색상 (#RRGGBB)"
        float strokeWidth "선 두께 (px)"
        string strokeStyle "선 스타일 (Solid/Dash/Dot)"
        string pointShape "Point 모양 (Circle/Square/Triangle)"
        float pointSize "Point 크기 (px)"
    }
    
    DB_CONNECTION {
        string id PK "GUID - Connection 고유 ID"
        string name "연결 이름"
        string dbType "DB 유형 (PostGIS/SQLServer)"
        string host "호스트 주소"
        int port "포트 번호"
        string database "Database명"
        string schema "Schema (default: public)"
        string encryptedCredentials "암호화된 인증정보 (DPAPI)"
    }
    
    PROJECT ||--|| MAP_SETTINGS : has
    PROJECT ||--o{ LAYER : contains
    PROJECT ||--o{ DB_CONNECTION : contains
    LAYER ||--|| LAYER_STYLE : has
```

### 1.2 Project File JSON Example

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0",
  "name": "서울시 도로현황 분석",
  "createdAt": "2024-01-15T09:30:00Z",
  "modifiedAt": "2024-01-15T14:22:00Z",
  "mapSettings": {
    "centerX": 126.9780,
    "centerY": 37.5665,
    "zoomLevel": 12,
    "srid": "EPSG:4326",
    "baseMapType": "OSM",
    "rotation": 0
  },
  "layers": [
    {
      "id": "layer-001",
      "name": "행정구역",
      "displayOrder": 0,
      "visible": true,
      "opacity": 0.7,
      "sourceType": "File",
      "sourcePath": "./data/seoul_boundary.shp",
      "geometryType": "Polygon",
      "style": {
        "fillColor": "#4CAF50",
        "fillOpacity": 0.3,
        "strokeColor": "#2E7D32",
        "strokeWidth": 2,
        "strokeStyle": "Solid"
      }
    },
    {
      "id": "layer-002",
      "name": "도로망",
      "displayOrder": 1,
      "visible": true,
      "opacity": 1.0,
      "sourceType": "File",
      "sourcePath": "./data/roads.geojson",
      "geometryType": "LineString",
      "style": {
        "strokeColor": "#FF5722",
        "strokeWidth": 1.5,
        "strokeStyle": "Solid"
      }
    }
  ],
  "dbConnections": []
}
```

---

## 2. Application Settings (SQLite - Local Storage)

로컬 애플리케이션 설정은 SQLite 데이터베이스에 저장됩니다.

### 2.1 Settings Entity Diagram

```mermaid
erDiagram
    APP_SETTINGS {
        string key PK "설정 Key"
        string value "설정 Value"
        string category "Category (General/Map/UI)"
        datetime updatedAt "수정 일시"
    }
    
    RECENT_FILES {
        int id PK "Auto Increment ID"
        string filePath "파일 전체 경로"
        string fileType "파일 유형 (shp/geojson/svproj)"
        string displayName "표시 이름"
        datetime accessedAt "마지막 접근 일시"
        boolean isPinned "고정 여부"
    }
    
    SAVED_DB_CONNECTIONS {
        string id PK "GUID"
        string name "연결 이름"
        string dbType "DB 유형"
        string host "호스트"
        int port "포트"
        string database "DB명"
        string username "사용자명"
        blob encryptedPassword "암호화된 비밀번호 (DPAPI)"
        datetime createdAt "생성 일시"
        datetime lastUsedAt "마지막 사용 일시"
    }
    
    USER_PREFERENCES {
        string key PK "환경설정 Key"
        string value "환경설정 Value"
        string dataType "Data Type (string/int/bool/float)"
    }
```

### 2.2 SQL Schema

```sql
-- App Settings Table
CREATE TABLE IF NOT EXISTS app_settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    category TEXT NOT NULL DEFAULT 'General',
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Recent Files Table
CREATE TABLE IF NOT EXISTS recent_files (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_path TEXT NOT NULL UNIQUE,
    file_type TEXT NOT NULL,
    display_name TEXT NOT NULL,
    accessed_at TEXT NOT NULL DEFAULT (datetime('now')),
    is_pinned INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_recent_files_accessed ON recent_files(accessed_at DESC);

-- Saved DB Connections Table
CREATE TABLE IF NOT EXISTS saved_db_connections (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    db_type TEXT NOT NULL,
    host TEXT NOT NULL,
    port INTEGER NOT NULL,
    database TEXT NOT NULL,
    username TEXT,
    encrypted_password BLOB,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    last_used_at TEXT
);

-- User Preferences Table
CREATE TABLE IF NOT EXISTS user_preferences (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    data_type TEXT NOT NULL DEFAULT 'string'
);
```

### 2.3 Default Settings

#### APP_SETTINGS

| key | value | category |
|-----|-------|----------|
| DefaultCRS | EPSG:4326 | Map |
| DefaultBaseMap | OSM | Map |
| TileCache | true | Map |
| WindowWidth | 1280 | UI |
| WindowHeight | 720 | UI |
| WindowState | Normal | UI |
| LayerPanelWidth | 280 | UI |
| AttributePanelHeight | 200 | UI |
| LastProjectPath | (empty) | General |
| AutoSave | true | General |
| AutoSaveInterval | 300 | General |

#### USER_PREFERENCES

| key | value | dataType |
|-----|-------|----------|
| Theme | Light | string |
| Language | ko-KR | string |
| ShowWelcomeDialog | true | bool |
| MaxRecentFiles | 10 | int |
| ConfirmOnDelete | true | bool |
| DefaultOpacity | 1.0 | float |

---

## 3. In-Memory Data Models (메모리 내 데이터 모델)

### 3.1 Layer Runtime Model

```mermaid
erDiagram
    LAYER_ITEM {
        guid Id "Layer 고유 ID"
        string Name "Layer 이름"
        int Order "표시 순서"
        bool IsVisible "표시 여부"
        float Opacity "투명도"
        ILayer SharpMapLayer "SharpMap Layer 참조"
        IProvider DataProvider "Data Provider 참조"
        Envelope Extent "공간 범위"
        int FeatureCount "Feature 개수"
        string CRS "좌표계"
    }
    
    FEATURE {
        int Id "Feature ID (FID)"
        IGeometry Geometry "NTS Geometry"
        Dictionary Attributes "속성 Dictionary"
        bool IsSelected "선택 여부"
        bool IsHighlighted "강조 표시 여부"
    }
    
    STYLE {
        Color FillColor "채우기 색상"
        Color StrokeColor "선 색상"
        float StrokeWidth "선 두께"
        StrokeStyle LineStyle "선 스타일"
        PointSymbol Symbol "Point 심볼"
        float SymbolSize "심볼 크기"
    }
    
    LAYER_ITEM ||--o{ FEATURE : contains
    LAYER_ITEM ||--|| STYLE : has
```

### 3.2 Map State Model

```mermaid
erDiagram
    MAP_STATE {
        Envelope CurrentExtent "현재 표시 범위"
        Point CenterPoint "지도 중심"
        float ZoomLevel "Zoom Level"
        float Scale "축척 (1:N)"
        string CurrentCRS "현재 좌표계"
        BaseMapType ActiveBaseMap "활성 배경지도"
    }
    
    SELECTION_STATE {
        guid ActiveLayerId "활성 Layer ID"
        List SelectedFeatureIds "선택된 Feature IDs"
        IGeometry SelectionGeometry "선택 영역 Geometry"
    }
    
    UI_STATE {
        bool IsLayerPanelOpen "Layer Panel 열림 상태"
        bool IsAttributePanelOpen "Attribute Panel 열림 상태"
        string CurrentTool "현재 활성 도구"
        Point LastMousePosition "마지막 마우스 위치"
    }
    
    MAP_STATE ||--|| SELECTION_STATE : has
    MAP_STATE ||--|| UI_STATE : has
```

---

## 4. File Path Conventions (파일 경로 규칙)

### 4.1 Project File (.svproj)

- **위치**: 사용자가 선택한 위치
- **레이어 경로**: 프로젝트 파일 기준 **상대 경로(Relative Path)** 저장
- **절대 경로 변환**: 로드 시 `Path.GetFullPath()` 사용

### 4.2 Application Data

| Data Type | Location |
|-----------|----------|
| **Settings DB** | `%APPDATA%\SpatialView\settings.db` |
| **Tile Cache** | `%LOCALAPPDATA%\SpatialView\TileCache\` |
| **Log Files** | `%LOCALAPPDATA%\SpatialView\Logs\` |
| **Temp Files** | `%TEMP%\SpatialView\` |

### 4.3 Example Paths

```
Windows:
├── %APPDATA%\SpatialView\
│   └── settings.db              # SQLite 설정 DB
├── %LOCALAPPDATA%\SpatialView\
│   ├── TileCache\               # 배경지도 타일 캐시
│   │   ├── OSM\
│   │   └── Bing\
│   └── Logs\                    # 로그 파일
│       └── app_2024-01-15.log
└── %TEMP%\SpatialView\          # 임시 파일
```

