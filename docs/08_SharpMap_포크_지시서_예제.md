# SharpMap 포크 지시서

> 목적: SharpMap 1.2.0을 포크하여, 회사 자체 **GIS 엔진 코어**로 흡수·장기 유지보수 가능한 구조로 만들기 위한 단계별 지침.[github+1](https://github.com/SharpMap/SharpMap/releases)

------

## 1. 포크 전략 개요

## 1.1 목표

- SharpMap을 **“외부 라이브러리”가 아니라 “초기 베이스 코드”**로 보고,
  - 우리 네이밍 규칙/폴더 구조/빌드 체계로 정리
  - NTS/ .NET 버전 의존성 관리
  - 향후 자체 기능(렌더링, 공간 연산)을 추가/교체할 수 있는 구조 확보.[issues.ecosyste+1](https://issues.ecosyste.ms/hosts/GitHub/repositories/SharpMap%2FSharpMap/issues)

## 1.2 기본 정책

- **공식 SharpMap 1.2.0을 기준 태그로 포크**한다.[nuget+1](https://www.nuget.org/packages/SharpMap)
- 외부 공개 계획이 없다면, 사내 Git 서버(또는 Private GitHub)로 mirror 후, **독립 버전 정책(예: `CompanyMap 0.x`)**으로 관리한다.
- SharpMap 공식 저장소와는 **코드 메인라인을 분리**하고, 필요 시 특정 커밋만 cherry-pick 한다.[github+1](https://github.com/SharpMap)

------

## 2. 초기 포크 및 리포지토리 정리

## 2.1 Git 포크/클론

1. GitHub `SharpMap/SharpMap` 리포지토리를 포크한다.[github](https://github.com/SharpMap/SharpMap)
2. 사내/개인 리포로 rename 예시
   - `Company.GIS.SharpMapBase` (내부 용도 명확화)
3. `main/master` 브랜치에서 **SharpMap 1.2.0 태그 기준 브랜치**를 생성
   - 예: `sharpmap-1.2.0-base`.[github](https://github.com/SharpMap/SharpMap/releases)

## 2.2 솔루션/프로젝트 구조 점검

- 원본 솔루션에서 주요 프로젝트:
  - `SharpMap` (Core)
  - `SharpMap.UI` (WinForms 컨트롤 등)[libraries](https://libraries.io/nuget/SharpMap.UI)
  - `SharpMap.Extensions` (NTS/Spatialite/PostGIS 등 확장)[nuget](https://www.nuget.org/packages/SharpMap.Extensions/)
- 우리 구조 제안 (예시):

```
text/src
  /Engine.Core        (SharpMap Core 리팩토링 버전)
  /Engine.UI.WinForms (SharpMap.UI 일부만 가져와 래핑)
  /Engine.Extensions  (NTS/DB 확장, 필요 기능만)
  /Samples            (실행 예제; MVP 뷰어 프로토타입)
/docs
  SharpMap_포크_지시서.md
  설계_DDD_스케치.md
```

------

## 3. 의존성(NTS·.NET) 정책

## 3.1 1차 단계: 공식 조합 고정

- NuGet 기준 SharpMap 1.2.0은 **`NetTopologySuite.Core (>= 1.15.3)`**에 맞춰져 있다.[nuget+1](https://www.nuget.org/packages/SharpMap)
- 초기 안정화 단계에서는 다음 조합에 고정:

| 컴포넌트         | 버전                                                         |
| ---------------- | ------------------------------------------------------------ |
| .NET             | .NET 6 또는 .NET 8 (netstandard2.0 호환)                     |
| SharpMap Base    | 1.2.0 태그 기준 포크[github](https://github.com/SharpMap/SharpMap/releases) |
| NetTopologySuite | 1.15.3 계열 (Core/Features/IO)[nuget+1](https://www.nuget.org/packages/SharpMap) |

- NTS 2.x 업그레이드는 **2단계 리팩토링 과제**로 분리한다.[issues.ecosyste](https://issues.ecosyste.ms/hosts/GitHub/repositories/SharpMap%2FSharpMap/issues)

## 3.2 NTS 2.x 관련 메모

- SharpMap 1.2 + NTS 2.4.0 사용 시 `IGeometryServices` 등 타입 누락 이슈가 보고되어 있음.[issues.ecosyste](https://issues.ecosyste.ms/hosts/GitHub/repositories/SharpMap%2FSharpMap/issues)
- NTS 2.x 적용 시 해야 할 작업:
  - SharpMap 내부에서 NTS API 호출부 전체 점검
  - 변경된 네임스페이스/클래스 구조에 맞게 수정
  - 좌표변환/정밀도 모델 설정 로직 재검토.[sharpgis+1](https://www.sharpgis.net/post/Using-NetTopologySuite-in-SharpMap)

------

## 4. 네이밍·API·레이어 구조 정리

## 4.1 네임스페이스 리브랜딩

- SharpMap 원본 네임스페이스 예:
  - `SharpMap`, `SharpMap.Layers`, `SharpMap.Data`, `SharpMap.Rendering` 등.[github](https://github.com/SharpMap/SharpMap)
- 사내 엔진 네임스페이스 예:

```
csharpCompany.Gis.Engine        // Core geometry & map engine
Company.Gis.Engine.Layers
Company.Gis.Engine.Data
Company.Gis.Engine.Rendering
Company.Gis.Engine.UI.WinForms
```

- 초기 단계에서는 **단순 alias 네임스페이스**로 두고, 점진적으로 내부 타입 분리/정리.

## 4.2 레이어 구조(논리)

- Core Layer
  - Geometry/Envelope/CRS/Transform 등 기본 타입
- Data Layer
  - FeatureProvider, PostGIS/File Provider 등
- Rendering Layer
  - 스타일, 심볼, 라벨 엔진
- UI Layer
  - MapControl/MapBox (WinForms → 향후 WPF wrapper)

이 구조대로 `SharpMap`의 클래스를 맵핑해 가면서, “엔진 코어”와 “UI/컨트롤” 의존성을 분리한다.[geomusings+1](https://blog.geomusings.com/2007/08/14/sharpmap-and-wpf/)

------

## 5. 리팩토링 단계별 가이드

## 5.1 0단계 – 빌드/테스트 확보

- 포크 직후 해야 할 일:
  - 포크 솔루션을 **.NET 6/8로 빌드** 가능한 상태로 만든다.
  - 최소 예제(샘플 맵 로딩, Shapefile 한 개 표시)를 실행해서, 현재 동작 상태를 “스냅샷”으로 기록.
  - 간단한 단위 테스트/통합 테스트 프로젝트 추가:
    - Geometry 생성/교차 테스트 (NTS vs SharpMap 결과 비교)
    - Shapefile 로딩/렌더링 스모크 테스트

## 5.2 1단계 – 불필요 코드/기능 분리

- 당장 쓰지 않을 기능(예: WebForms용 컨트롤, 오래된 Provider, 사용 계획 없는 DB 등)을 **별 폴더로 이동 또는 프로젝트에서 제외**:
  - 예: WebForms, GDI+ 특정 샘플, 사용하지 않을 UI 확장 등.[github](https://github.com/SharpMap/SharpMap)
- “Core 엔진”에 필요한 최소 집합만 남긴다:
  - 기본 레이어 타입(VectorLayer, RasterLayer)
  - 기본 Provider(Shapefile, PostGIS)
  - 투영/좌표변환 필수 요소

## 5.3 2단계 – 스타일/렌더링 설계 정리

- 렌더링·스타일 클래스를 **WPF/모던 UI와 연계 가능한 형태**로 정리:
  - 색상/폰트/심볼 정의를 DTO 스타일로 재구성
  - GDI+ 의존 코드는 “Adapter”로 감싸고, 향후 WPF 렌더러로 교체 가능하도록 인터페이스 분리.

## 5.4 3단계 – NTS 2.x 및 향후 확장 대비

- 내부에서 NTS 의존 부분을 한 레이어로 모은다:
  - 예: `Company.Gis.Engine.Geometry.NtsAdapter`
- 이 레이어를 통해서만 NTS에 접근하도록 리팩토링해, 나중에 NTS 버전 변경 시 영향 범위를 축소한다.[sharpgis+1](https://www.sharpgis.net/post/Using-NetTopologySuite-in-SharpMap)

------

## 6. SharpMap → 자사 엔진 전환 전략

## 6.1 Adapter 패턴 적용

- 제품 코드에서 SharpMap 타입을 직접 사용하지 않고, **추상 인터페이스**를 경유:

```
csharppublic interface IMapCanvas { ... }
public interface IMapLayer { ... }
public interface IFeatureSource { ... }
```

- SharpMap 기반 구현:

```
csharppublic class SharpMapCanvas : IMapCanvas { ... }
public class SharpMapVectorLayer : IMapLayer { ... }
```

- 나중에 **자체 구현 렌더러**를 만들면, 동일 인터페이스를 구현하는 새 클래스를 추가하고, DI 컨테이너로 교체 가능.

## 6.2 “엔진 코어 버전” 관리

- SharpMap 원본 버전과 별도로 **엔진 코어 자체 버전**을 부여:
  - 예: `Engine.Core 0.1.0 (SharpMap 1.2.0 기반)`
  - `Engine.Core 0.2.0 (SharpMap 1.2.0 + NTS 2.x 호환 리팩토링)`

------

## 7. 문서화 및 코드 스타일

## 7.1 코드 스타일 통일

- Cursor/사내 표준에 맞춰:
  - 네이밍 규칙(CamelCase, PascalCase 등)
  - 주석 스타일(XML Doc)
  - nullable reference types 사용 여부
     를 SharpMap 포크 코드에도 일괄 적용(단계적).

## 7.2 변경 이력 문서

- `/docs/CHANGELOG_EngineCore.md` 에 다음을 기록:
  - SharpMap 원본 대비 변경점
  - 제거한 기능/Provider 목록
  - NTS/ .NET 버전 변경 기록
  - 주요 버그 수정 내용 (이슈 번호 링크 포함).[github+1](https://github.com/SharpMap/SharpMap/issues)

------

## 8. 리스크 및 체크 포인트

- **라이선스**
  - SharpMap의 라이선스(MIT/BSD 계열)와 NTS의 라이선스를 확인하여, 상용 제품 포함 시 조건을 문서화한다.[nettopologysuite.github+1](https://nettopologysuite.github.io/)
- **성능/정확도 회귀**
  - 리팩토링으로 인한 공간 연산 결과 오차/성능 저하를 방지하기 위해,
    - 테스트 케이스를 작게라도 먼저 확보하고,
    - PostGIS/GEOS 결과와 비교 검증하는 단계를 포함한다.[postgis+1](https://postgis.net/docs/manual-3.4/postgis-ko_KR.html)

------

## 9. 첫 작업 TODO (요약 체크리스트)

1. Git 포크 + `sharpmap-1.2.0-base` 브랜치 생성.[github](https://github.com/SharpMap/SharpMap/releases)
2. .NET 6/8 솔루션으로 빌드 정리 (패키지 참조 → PackageReference).[nuget](https://www.nuget.org/packages/SharpMap)
3. SharpMap / SharpMap.UI / SharpMap.Extensions를 `/src/Engine.*` 구조로 재배치.
4. NTS 1.15.3 계열에 고정, 빌드·샘플 실행 확인.[nuget+1](https://www.nuget.org/packages/SharpMap.Extensions/)
5. 사용하지 않을 기능/Provider 분리 또는 주석 처리.
6. 최소 샘플(Shapefile 로딩 2D 뷰어)을 `Samples/MvpViewer`로 작성.
7. Adapter 인터페이스(IMapCanvas, IMapLayer, IFeatureSource 등) 정의 시작.

------

이 파일을 Cursor에서 열어 두고,

- 9번 체크리스트를 순서대로 이행하면서
- 각 단계에서 추가로 세분화가 필요하면 “1단계 리팩토링 지시서”, “NTS 2.x 마이그레이션 지시서” 같은 보조 문서를 더 파생시키면 된다.

1. https://github.com/SharpMap/SharpMap
2. https://github.com/SharpMap/SharpMap/releases
3. https://www.nuget.org/packages/SharpMap
4. https://issues.ecosyste.ms/hosts/GitHub/repositories/SharpMap%2FSharpMap/issues
5. https://github.com/SharpMap
6. https://libraries.io/nuget/SharpMap.UI
7. https://www.nuget.org/packages/SharpMap.Extensions/
8. https://www.sharpgis.net/post/Using-NetTopologySuite-in-SharpMap
9. https://blog.geomusings.com/2007/08/14/sharpmap-and-wpf/
10. https://github.com/SharpMap/SharpMap/issues
11. [https://nettopologysuite.github.io](https://nettopologysuite.github.io/)
12. https://postgis.net/docs/manual-3.4/postgis-ko_KR.html
13. http://postgis.net/workshops/postgis-intro/spatial_relationships.html