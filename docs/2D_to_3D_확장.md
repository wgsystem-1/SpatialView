# 2D_to_3D_확장.md
## 2D GIS Viewer → 3D LiDAR·Terrain Analysis Platform 확장 전략

---

## 1. 문서 목적

본 문서는 현재 2D 기반 GIS 뷰어(SHP, GeoJSON, FileGDB 지원)를
오픈소스 기반 기술만을 활용하여,
상용 LiDAR·3D 지형 분석 프로그램 수준으로 확장하기 위한
포맷, 처리 파이프라인, 아키텍처, 기술 스택을 정의한다.

목표 수준:
- Quick Terrain Modeler / Global Mapper 급 기능 범위
- 상용 배포 가능 (라이선스 제약 회피)
- 대용량 데이터 처리 전제

---

## 2. 현재 시스템 상태 (AS-IS)

### 2.1 지원 데이터 포맷 (2D)
- SHP (ESRI Shapefile)
- GeoJSON
- FileGDB (읽기 중심)

### 2.2 핵심 기능
- 2D 지도 렌더링
- 레이어 On/Off
- 속성 조회
- 기본 좌표계 처리

### 2.3 기술 한계
- Z값 미활용
- 포인트클라우드 미지원
- 래스터/DEM/DSM 미지원
- 3D 분석 불가

---

## 3. 목표 시스템 범위 (TO-BE)

### 3.1 지원 데이터 포맷 전체 목록

#### (1) 벡터 (2D / 2.5D / 3D)
- SHP (Z/M 포함)
- GeoJSON / GeoJSON-T
- GPKG (GeoPackage)
- DXF / DWG (2.5D)
- KML / KMZ
- CityGML (확장 대상)

#### (2) 포인트클라우드
- LAS / LAZ
- E57 (확장 가능)
- XYZ / TXT

#### (3) 래스터 / 지형
- GeoTIFF (DEM / DSM / DTM)
- IMG
- ASC Grid
- COG (Cloud Optimized GeoTIFF)

#### (4) 공간 DB
- PostGIS (2D / 3D / PointCloud)
- SpatiaLite

---

## 4. 오픈소스 기반 포맷 처리 기술 스택

### 4.1 벡터 / 래스터 포맷

| 기능 | 오픈소스 | 라이선스 |
|----|--------|--------|
| SHP / GeoJSON / GPKG | GDAL/OGR | MIT |
| FileGDB (읽기) | GDAL OpenFileGDB | MIT |
| DXF/DWG | GDAL DXF Driver | MIT |
| KML/KMZ | GDAL | MIT |
| DEM/GeoTIFF | GDAL | MIT |

※ 상용 배포 가능, 소스 공개 의무 없음

---

### 4.2 포인트클라우드

| 기능 | 오픈소스 | 라이선스 |
|----|--------|--------|
| LAS/LAZ I/O | PDAL | BSD |
| 필터링 / 분류 | PDAL | BSD |
| 인덱싱 | PDAL | BSD |
| 압축 | LASzip (간접) | LGPL 주의 |

※ LASzip은 직접 수정 없이 PDAL 경유 사용 권장

---

## 5. 2D → 3D 확장 핵심 개념

### 5.1 Z 값 확장 전략 (2D → 2.5D)

- SHP/GeoJSON의 Z 필드 활성화
- 속성 기반 고도 보간
- DEM 기반 Z 보강
- 2D 벡터 → 3D Extrusion

---

### 5.2 진정한 3D 데이터 도입

- 포인트클라우드 직접 렌더링
- TIN / Mesh 생성
- DSM / DTM 자동 생성
- 래스터 → 3D Surface 변환

---

## 6. 데이터 처리 파이프라인

[Data Load]
↓
[Format Adapter (GDAL / PDAL)]
↓
[Spatial Index (Quadtree / Octree)]
↓
[2D / 3D Renderer]
↓
[Analysis Engine]
↓
[Export / Share]


---

## 7. 3D 렌더링 아키텍처

### 7.1 렌더링 엔진 전략

- GPU 기반
- 대용량 Out-of-Core 처리
- LOD(Level of Detail) 적용

### 7.2 기술 선택지

| 영역 | 기술 |
|----|----|
| 데스크톱 | OpenGL / Vulkan |
| 3D Scene | 자체 Scene Graph |
| 포인트클라우드 | Octree 기반 |
| 지형 | Heightmap / Mesh |

---

## 8. 분석 기능 확장 계획

### 8.1 기본 분석
- 거리 / 면적 / 높이
- 단면(Profile)
- 경사도 / 향

### 8.2 고급 분석
- 시선 분석 (LOS)
- 체적 분석 (Cut & Fill)
- 침수 / 가시권 시뮬레이션

---

## 9. 출력 및 공유 포맷

### 9.1 벡터 / 래스터
- SHP, GeoJSON, GPKG
- GeoTIFF (DEM/DSM)

### 9.2 시각 결과
- Image (PNG, JPG)
- Video (MP4)
- Web 3D (Cesium / Potree 연계)

---

## 10. 라이선스 안전 전략 (중요)

### 10.1 사용 가능 (안전)
- GDAL (MIT)
- PDAL (BSD)
- PROJ (MIT)
- PostGIS (GPL – 서버 분리 권장)

### 10.2 주의 필요
- LASzip (LGPL)
- OpenSceneGraph (LGPL)

※ LGPL 라이브러리는 동적 링크 + 수정 금지 원칙 유지

---

## 11. 단계별 확장 로드맵

### Phase 1
- 2D → 2.5D (Z 활성화)
- DEM 로딩
- 3D 카메라 도입

### Phase 2
- LAS/LAZ 로딩
- 포인트클라우드 렌더링
- 기본 분석

### Phase 3
- DSM/DTM 생성
- 체적/시선 분석
- 대용량 최적화

### Phase 4
- AI 분류 연계
- 플러그인 구조
- Web 3D 연계

---

## 12. 결론

본 확장은
- 오픈소스 기반
- 상용 라이선스 안전
- 기술적으로 검증된 스택을 활용하여
2D GIS Viewer를
전문 3D 지형·점군 분석 플랫폼으로 진화시키는 전략이다.
