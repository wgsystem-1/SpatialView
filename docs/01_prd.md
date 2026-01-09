# SpatialView - Product Requirements Document (PRD)
## 제품 요구사항 정의서

---

## 1. Background (배경)

GIS(Geographic Information System) 분석가들은 업무 중 다양한 포맷의 공간 데이터를 다룹니다. 그러나 현재 시장의 GIS 도구들은 다음과 같은 한계를 가지고 있습니다:

| 기존 도구 | 문제점 |
|----------|--------|
| **QGIS** | 기능이 방대하여 UI가 복잡하고 학습곡선(Learning Curve)이 높음 |
| **ArcGIS** | 고가의 라이선스 비용으로 개인/소규모 조직 접근성 낮음 |
| **기타 뷰어** | FileGDB 등 특정 포맷 미지원, 성능 이슈 |

**SpatialView**는 25년 GIS 경력의 도메인 전문 지식을 바탕으로, **심플하면서도 강력한** 공간정보 뷰어를 개발하여 이러한 문제를 해결합니다.

---

## 2. Goal (목표)

### 2.1 Primary Goal (주요 목표)
- 다양한 GIS 포맷(SHP, GeoJSON, KML, DXF, GPX, FileGDB)을 **하나의 도구**에서 열고 관리할 수 있는 Windows Desktop Application 개발

### 2.2 Secondary Goals (부가 목표)
- 직관적인 Flat Colorful UI로 복잡한 GIS 도구의 진입장벽 낮추기
- 자동 좌표계(CRS) 감지로 사용자 편의성 향상
- 대용량 파일 로딩 성능 최적화(Performance Optimization)

### 2.3 Long-term Vision (장기 비전)
```
뷰어 (MVP) → 편집 (Editing) → 검수 (Validation) → AI 자동 수정 (AI Auto-fix)
```

---

## 3. Target User (타겟 사용자)

| Attribute | Description |
|-----------|-------------|
| **Persona** | GIS Analyst (GIS 분석가) |
| **Experience** | GIS 도구 사용 경험 있음, 다양한 데이터 포맷에 익숙 |
| **Use Context** | 여러 출처의 공간 데이터를 빠르게 시각화하고 레이어로 통합 분석 |
| **Pain Point** | 단순 뷰잉을 위해 무거운 QGIS를 띄우거나, 비싼 ArcGIS 라이선스 필요 |
| **Desired Outcome** | 가볍고 빠르게 다양한 포맷을 열어보고 속성(Attribute) 확인 |

---

## 4. MVP Scope (MVP 범위)

| Priority | Feature | Description | Status |
|----------|---------|-------------|--------|
| **P0** | Multi-format Loading (다중 포맷 로딩) | SHP, GeoJSON, KML, DXF, GPX, FileGDB 파일 열기 | MVP |
| **P0** | Layer Management (레이어 관리) | 레이어 ON/OFF, 순서 변경, 투명도(Opacity) 조절 | MVP |
| **P0** | Base Map (배경지도) | OSM, Bing Maps 배경지도 표시 | MVP |
| **P1** | Attribute Table (속성 테이블) | 피처(Feature) 속성 조회, 필터링, 통계 | MVP+ |
| **P1** | Project File (프로젝트 파일) | 작업 상태 저장/불러오기 (.svproj) | MVP+ |
| **P2** | Feature Editing (피처 편집) | 피처 추가/수정/삭제 | Future |
| **P2** | Attribute Editing (속성 편집) | 속성 테이블 직접 편집 | Future |
| **P3** | Data Validation (데이터 검수) | Topology(토폴로지) 오류 검출 | Future |
| **P3** | AI Auto-fix (AI 자동 수정) | 오류 자동 탐지 및 수정 제안 | Future |

---

## 5. User Stories

| ID | User Story | Acceptance Criteria | Priority |
|----|------------|---------------------|----------|
| **FEAT-001** | 사용자는 SHP 파일을 Drag & Drop으로 열 수 있다 | 파일 Drop 시 5초 내 지도에 표시됨 | P0 |
| **FEAT-002** | 사용자는 GeoJSON 파일을 열 수 있다 | .geojson, .json 확장자 지원 | P0 |
| **FEAT-003** | 사용자는 FileGDB 폴더를 열 수 있다 | 레이어 선택 후 로드 가능 | P0 |
| **FEAT-004** | 사용자는 여러 레이어를 겹쳐서 볼 수 있다 | Layer Panel에서 순서 변경 가능 | P0 |
| **FEAT-005** | 사용자는 레이어별 투명도를 조절할 수 있다 | 0~100% Slider 제공 | P0 |
| **FEAT-006** | 사용자는 배경지도 위에 데이터를 표시할 수 있다 | OSM/Bing 선택 가능 | P0 |
| **FEAT-007** | 사용자는 피처를 클릭하여 속성을 확인할 수 있다 | Popup 또는 Panel에 속성 표시 | P1 |
| **FEAT-008** | 사용자는 속성 테이블에서 필터링할 수 있다 | 조건식으로 피처 필터 | P1 |
| **FEAT-009** | 사용자는 현재 작업 상태를 저장할 수 있다 | .svproj 파일로 저장/열기 | P1 |
| **FEAT-010** | 사용자는 좌표계를 수동 설정 없이 사용할 수 있다 | .prj 파일 자동 인식 | P0 |

---

## 6. Non-goals (제외 사항)

다음 기능은 **MVP 범위에서 제외**됩니다:

- ❌ Web Browser Version (웹 브라우저 버전)
- ❌ Mobile Application (모바일 앱)
- ❌ Real-time Collaboration (실시간 협업)
- ❌ Advanced Spatial Analysis (고급 공간 분석) - Buffer, Overlay 등
- ❌ Map Publishing (지도 출판) - 고품질 인쇄용 지도 제작
- ❌ WMS/WFS Service Connection (외부 서비스 연동) - MVP 이후

---

## 7. Benchmark Summary (벤치마킹 요약)

### 7.1 SharpMap (Base Library)

| Aspect | Description |
|--------|-------------|
| **Type** | Open-source .NET GIS Library |
| **License** | LGPL |
| **Strengths** | 다양한 Provider 지원, .NET Native, 활발한 커뮤니티 |
| **Supported Formats** | Shapefile, PostGIS, WMS, Custom Provider 확장 가능 |
| **적용 방향** | GIS Engine의 핵심으로 사용, 추가 포맷은 GDAL로 확장 |

### 7.2 Competitive Analysis (경쟁 분석)

| Tool | Pros | Cons | SpatialView 차별화 |
|------|------|------|-------------------|
| **QGIS** | 무료, 강력한 분석 기능, Plugin 생태계 | 복잡한 UI, 느린 시작 속도 | → Simple UI, 빠른 시작 |
| **ArcGIS Pro** | 업계 표준, 안정성, 기술 지원 | 고가 라이선스 ($100+/월) | → 무료/저가 |
| **Global Mapper** | 다양한 포맷, 좋은 성능 | 유료 ($549+) | → 무료로 유사 기능 |
| **FME Data Inspector** | 뛰어난 포맷 지원 | 뷰잉 전용, FME 종속 | → 독립 실행형, 편집 가능 |

---

## 8. Success Metrics (성공 지표)

| Metric | Target | Measurement |
|--------|--------|-------------|
| **MVP Completion** (MVP 완성도) | 100% | 핵심 기능 체크리스트 |
| **File Load Time** (파일 로딩 시간) | < 5초 (100MB 기준) | Performance Test |
| **Supported Formats** (지원 포맷) | 6개 이상 | 포맷 테스트 |
| **User Satisfaction** (사용자 만족) | 본인 업무에 실제 활용 | Self Feedback |

---

## 9. Timeline (일정)

| Milestone | Duration | Deliverable |
|-----------|----------|-------------|
| **M0: Project Setup** | 1주 | Solution 구조, NuGet Package, 기본 설정 |
| **M1: Basic Map View** | 2주 | 지도 표시, 배경지도, Zoom/Pan |
| **M2: File Loading** | 3주 | SHP, GeoJSON, FileGDB 로딩 |
| **M3: Layer Management** | 2주 | Layer Panel, 투명도, 순서 관리 |
| **M4: Attribute Table** | 2주 | 속성 조회, 필터링, 선택 |
| **M5: Project File** | 1주 | 저장/불러오기, 최근 파일 |
| **Total** | **~11주** | MVP 완성 |

---

## 10. Stakeholders (이해관계자)

| Role | Responsibility |
|------|----------------|
| **Product Owner** | 요구사항 정의, 우선순위 결정 (본인) |
| **Developer** | 설계 및 구현 (본인 + AI Coding Tool) |
| **Initial Users** | Feedback 제공 (본인 + 팀/회사) |

