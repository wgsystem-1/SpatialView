# QGIS 렌더링 알고리즘 분석
> 대상: QGIS Desktop(주로 3.x 계열)에서 **레이어(벡터/래스터) + 라벨**이 화면에 그려지는 내부 파이프라인을 “구조/알고리즘 관점”으로 정리  
> 목적: QGIS와 유사한 렌더링 모델(레이어 순서·심볼 레벨·피처 순서·라벨링·캐시·병렬 렌더)을 타 플랫폼(WPF 등)으로 이식/참조할 수 있도록 분석

---

## 1) 한 장 요약(핵심 흐름)

QGIS의 지도 렌더링은 크게 아래 흐름으로 이해하면 정확합니다.

1. **QgsMapSettings**로 렌더 설정(Extent/크기/레이어 목록 등) 구성  
2. **QgsMapRendererJob**(비동기 Job) 생성 → **백그라운드 렌더링 시작**  
3. Job이 각 레이어에 대해 **QgsMapLayerRenderer**(레이어 렌더러 스냅샷) 준비  
4. 레이어 렌더(순차 또는 병렬) → **레이어 순서대로 합성**  
5. 라벨/다이어그램은 **라벨링 엔진**이 후보 생성·충돌해결·배치 → 최종 드로잉  
6. **캐시(QgsMapRendererCache)**가 활성화되어 있으면 동일 조건 렌더 결과를 재사용하고, 관련 레이어가 repaint를 요청하면 캐시를 무효화

- QgsMapSettings는 “렌더링 구성”, 실제 렌더링은 QgsMapRendererJob 서브클래스가 수행합니다.  
  (Extent/Output size/Layers 설정 필요)  
  출처: https://qgis.org/pyqgis/3.40/core/QgsMapSettings.html
- 렌더링은 QgsMapRendererJob로 수행하며, 비동기(Non-blocking) 설계를 강조합니다.  
  출처: https://api.qgis.org/api/classQgsMapRendererJob.html

---

## 2) 입력 데이터 구조: QgsMapSettings / QgsRenderContext

### 2.1 QgsMapSettings (렌더 설정)
QgsMapSettings는 렌더링에 필요한 핵심 설정(Extent, 출력 이미지 크기, 레이어 목록 등)을 담습니다.  
이 객체를 기반으로 렌더링 Job이 생성됩니다.  
출처: https://qgis.org/pyqgis/3.40/core/QgsMapSettings.html  
출처(개발자북 예제): https://docs.qgis.org/latest/en/docs/pyqgis_developer_cookbook/composer.html

### 2.2 QgsRenderContext (렌더 컨텍스트)
QgsRenderContext는 렌더링 중 사용되는 컨텍스트(스케일/extent/map2pixel 등)와 “그릴 대상”인 **QPainter**를 가리킵니다.

- QgsRenderContext는 “destination QPainter”를 설정/사용하는 API를 제공합니다.  
  출처: https://qgis.org/pyqgis/3.40/core/QgsRenderContext.html (setPainter)  
  출처(API): https://api.qgis.org/api/classQgsRenderContext.html

---

## 3) 렌더링 Job 모델: 비동기 / 병렬 / 순차

### 3.1 QgsMapRendererJob (추상 베이스)
- 비동기 렌더링을 전제로 설계되어, 호출자를 블로킹하지 않도록 합니다.  
  출처: https://api.qgis.org/api/classQgsMapRendererJob.html
- PyQGIS 문서에서도 start() 후 finished() 시그널을 통해 완료를 받는 패턴을 안내합니다.  
  출처: https://qgis.org/pyqgis/3.44/core/QgsMapRendererJob.html

### 3.2 QgsMapRendererParallelJob (레이어 병렬 렌더)
- “모든 레이어를 병렬로 렌더링”하는 Job 구현체입니다.  
- 렌더 중에도 renderedImage()로 프리뷰를 볼 수 있다고 명시합니다.  
  출처: https://qgis.org/pyqgis/3.40/core/QgsMapRendererParallelJob.html

### 3.3 QgsMapRendererSequentialJob (레이어 순차 렌더)
- 하나의 백그라운드 스레드에서 순차 렌더링(이미지로 출력)하는 구현체가 존재합니다.  
  출처: https://api.qgis.org/api/3.40/classQgsMapRendererSequentialJob.html

---

## 4) 레이어 순서(1차 Z-Order): “레이어 패널 순서”

QGIS의 기본 규칙은 매우 명확합니다.

- **레이어 목록의 아래쪽 레이어가 먼저 그려지고**,  
- **위쪽 레이어가 나중에 그려져 화면에서 위에 보입니다.**  

출처: https://docs.qgis.org/latest/en/docs/training_manual/basic_map/preparation.html

> 즉, “레이어 패널 순서”가 **1차 렌더 우선순위**입니다.

---

## 5) 레이어 렌더러 스냅샷: QgsMapLayerRenderer

QGIS는 렌더링이 보통 “백그라운드 스레드”에서 수행되므로,  
렌더링 동안 원본 레이어 객체가 변경될 수 있는 문제를 피하기 위해 **렌더에 필요한 구조를 따로 보관**합니다.

- QgsMapLayerRenderer는 “레이어 렌더링에 필요한 정보를 캡슐화”하며,  
  렌더는 백그라운드에서 이뤄지기 때문에 원본 레이어로부터 독립된 구조를 유지해야 한다고 설명합니다.  
  출처: https://qgis.org/pyqgis/master/core/QgsMapLayerRenderer.html

또한, 레이어는 렌더 컨텍스트에 맞는 MapLayerRenderer 인스턴스를 생성할 수 있습니다.

- QgsMapLayer::createMapRenderer(...)는 렌더용 QgsMapLayerRenderer를 반환하고,  
  QgsMapRendererJob의 prepareJobs()에서 참조된다고 API 문서에 나옵니다.  
  출처: https://api.qgis.org/api/2.4/classQgsMapLayer.html (createMapRenderer)

---

## 6) 벡터 레이어 렌더링 알고리즘(핵심)

### 6.1 기본 구조: QgsVectorLayerRenderer + QgsFeatureRenderer
벡터 렌더링은 개념적으로 아래 순서로 볼 수 있습니다.

1. **QgsFeatureRenderer.startRender(context, fields)**  
2. Extent/필터 조건을 만족하는 **Feature Iterator로 피처를 순회**  
3. 피처별로 심볼/스타일을 적용해 드로잉  
4. **QgsFeatureRenderer.stopRender(context)**

- startRender()/stopRender() 호출 순서가 필수이며,  
  스레드 안전하지 않아서 “메인 스레드가 아닌 곳에서는 clone한 렌더러로 start/stop을 호출”하라고 경고합니다.  
  출처: https://qgis.org/pyqgis/3.40/core/QgsFeatureRenderer.html  
  출처(API): https://api.qgis.org/api/classQgsFeatureRenderer.html

### 6.2 Symbol Levels(심볼 레벨) = 2차 Z-Order (레이어 내부)
QGIS는 “심볼 레벨” 기능으로, 한 레이어 안에서도 심볼 레이어(외곽선/채움/케이싱 등)의 그리기 순서를 제어합니다.

- 심볼 레벨을 켜면 각 심볼 레이어에 “레벨 번호”를 지정하며, **0이 바닥(먼저 그림)**입니다.  
  출처: https://docs.qgis.org/latest/en/docs/training_manual/basic_map/symbology.html

구현 측면에서 QGIS 벡터 레이어 렌더러는  
심볼 레벨을 고려한 렌더 함수(예: drawRendererV2Levels)가 존재하고, startRender가 선행되어야 함을 문서에 명시합니다.  
출처: https://api.qgis.org/api/2.4/classQgsVectorLayerRenderer.html (drawRendererV2Levels)

### 6.3 “렌더링 최적화(지오메트리 단순화)” = 속도 핵심
QGIS는 렌더 컨텍스트에서 “렌더링 최적화(지오메트리 단순화)”가 가능한지 판단하고,  
단순화 설정을 제공하는 구조를 갖고 있습니다.

- QgsRenderContext.useRenderingOptimization(): 지오메트리 단순화 가능 여부  
- QgsRenderContext.vectorSimplifyMethod(): 단순화 설정(벡터)  
  출처: https://api.qgis.org/api/classQgsRenderContext.html

QgsVectorSimplifyMethod는 “단순화가 provider에서 실행될지(지원 시) / fetch 후 로컬에서 실행될지” 같은 실행 위치 제어를 포함합니다.  
출처: https://qgis.org/pyqgis/3.44/core/QgsVectorSimplifyMethod.html

> 정리: QGIS는 “화면에서 구분 안 되는 디테일”을 줄여서 렌더링 속도를 올릴 수 있는 공식 메커니즘을 제공합니다.

---

## 7) 라벨링 알고리즘(핵심): Labeling Engine + PAL

QGIS 라벨링은 “레이어 렌더링과 독립된 문제”에 가깝게 취급됩니다.  
즉, **(1) 후보 생성 → (2) 충돌 해결 → (3) 배치 결과로 최종 드로잉**이라는 “2단계 이상” 구조입니다.

### 7.1 QgsLabelingEngine: 충돌 없는 레이아웃 계산
QgsLabelingEngine 문서 요지:
- 입력: label provider 목록 + map settings  
- 출력: 주어진 map view에서 **라벨 간 충돌이 없도록** 레이아웃을 계산  
- 드로잉: 계산된 결과 라벨은 다시 provider가 draw  
출처: https://api.qgis.org/api/classQgsLabelingEngine.html

### 7.2 PAL(pal::Pal): Extent 기반 문제 추출 + 최적화 탐색
PAL은 라벨 배치 문제를 “현재 지도 Extent”에 대해 추출하고 해결하는 흐름을 가집니다.

- pal::Pal.extractProblem(extent, ...): 지정 Extent에 대해 라벨링 문제를 추출하며, **Extent 안의 피처만 고려**한다고 명시합니다.  
  출처: https://api.qgis.org/api/classpal_1_1Pal.html

또한 PAL의 검색 방법(SearchMethod)은 알고리즘/품질/속도 트레이드오프를 공개적으로 설명합니다.

- CHAIN: “최악이지만 가장 빠름(worst but fastest)”  
- POPMUSIC_TABU_CHAIN: “최고지만 가장 느림(best but slowest)”  
- POPMUSIC_TABU / POPMUSIC_CHAIN 등 단계적 옵션  
  출처: https://api.qgis.org/api/3.4/namespacepal.html  
  출처(pal.h): https://api.qgis.org/api/2.14/pal_8h.html

QGIS는 라벨링 엔진 설정에서 placement engine version / search method 등을 제어하는 API를 제공합니다.  
출처: https://qgis.org/pyqgis/3.40/core/QgsLabelingEngineSettings.html

### 7.3 라벨의 “z-index” = 라벨끼리의 우선순위(레이어 간 포함)
QGIS는 라벨 렌더 순서를 z-index로 제어합니다.

- “Label z-index는 라벨이 렌더링되는 순서를 결정하며,  
  z-index가 높은 라벨은(모든 레이어의 라벨 중에서도) 더 위에 그려진다”고 사용자 매뉴얼에 명시합니다.  
  출처: https://docs.qgis.org/3.10/en/docs/user_manual/style_library/label_settings.html

또한 API에서 QgsPalLayerSettings.zIndex 속성을 제공하며, “높은 z-index가 위에 렌더”된다고 설명합니다.  
출처: https://qgis.org/pyqgis/master/core/QgsPalLayerSettings.html

### 7.4 Obstacles(장애물): 라벨이 피해야 할 피처/영역
최신 사용자 문서에서,
- obstacle은 “QGIS가 다른 라벨/다이어그램이 그 위에 올라가지 않도록 피하려는(feature) 대상”이라고 설명합니다.  
  출처: https://docs.qgis.org/latest/en/docs/user_manual/style_library/label_settings.html

또한, obstacle geometry를 별도로 지정해 “라벨 충돌 판정용 모양을 변경(예: 포인트 주변 버퍼)”할 수 있는 API가 있습니다.  
출처: https://qgis.org/pyqgis/3.44/core/QgsLabelObstacleSettings.html

---

## 8) 라벨 캐싱과 결과 접근

QgsMapRendererJob에는 라벨링 결과 접근과 캐시 여부 확인 API가 존재합니다.

- takeLabelingResults(): 라벨링 결과 접근(내부 라벨링 엔진 결과)  
- usedCachedLabels(): “캐시된 라벨링 솔루션을 사용했는지” 반환  
  출처: https://qgis.org/pyqgis/3.44/core/QgsMapRendererJob.html

> 즉 QGIS는 라벨 배치를 “매번 처음부터” 풀지 않고, 조건이 맞으면 캐시를 사용할 수 있는 설계를 포함합니다.

---

## 9) 렌더 캐시: QgsMapRendererCache / QgsMapCanvas

### 9.1 QgsMapRendererCache (렌더 이미지 캐시)
QgsMapRendererCache는 “map rendering job 결과 이미지”를 캐시하며,  
의존 레이어가 repaintRequested()를 발생시키면 해당 캐시 이미지를 제거(무효화)한다고 설명합니다.  
출처: https://qgis.org/pyqgis/master/core/QgsMapRendererCache.html

### 9.2 QgsMapCanvas에서 캐시 사용
QgsMapCanvas는 캐시가 활성화되어 있으면 map renderer cache를 반환할 수 있습니다.  
출처: https://qgis.org/pyqgis/3.44/gui/QgsMapCanvas.html

캐시 활성화는 setCachingEnabled(true)로 켤 수 있다는 예시가 공개 Q&A에도 널리 공유됩니다.  
출처: https://gis.stackexchange.com/questions/162088/how-to-re-render-only-a-single-layer-on-a-mapcanvas

---

## 10) “QGIS식 렌더 우선순위”를 구성하는 3단계(정리)

QGIS의 화면 결과는 보통 아래 3개의 우선순위 축이 합쳐져 결정됩니다.

1) **레이어 순서(레이어 패널)**: 아래 먼저 → 위 나중(최상위)  
- 출처: https://docs.qgis.org/latest/en/docs/training_manual/basic_map/preparation.html

2) **레이어 내부 심볼 레벨(Symbol Levels)**: 0이 바닥  
- 출처: https://docs.qgis.org/latest/en/docs/training_manual/basic_map/symbology.html  
- 구현 힌트(벡터 렌더러에 symbol levels 전용 렌더가 존재): https://api.qgis.org/api/2.4/classQgsVectorLayerRenderer.html

3) **라벨 z-index(레이어 간 포함)**: 높은 z-index 라벨이 위  
- 출처: https://docs.qgis.org/3.10/en/docs/user_manual/style_library/label_settings.html  
- API: https://qgis.org/pyqgis/master/core/QgsPalLayerSettings.html

> “피처(Feature) 그리기 순서(Control feature rendering order)” 같은 옵션은 별도 축으로 존재하지만,
> 본 문서는 내부 알고리즘의 큰 줄기(레이어/심볼 레벨/라벨)를 중심으로 정리했습니다.

---

## 11) 렌더링 파이프라인 의사코드(pseudocode)

아래는 QGIS 렌더링을 “알고리즘 관점”에서 가장 짧게 표현한 형태입니다.

```pseudo
function RenderMap(layers, extent, outputSize):
    settings = QgsMapSettings(extent, outputSize, layers)
    job = QgsMapRendererJobSubclass(settings)  // sequential or parallel
    job.start()  // async
    wait job.finished()

    image = job.renderedImage()

    labelingResults = job.takeLabelingResults()
    // (optional) inspect usedCachedLabels()

    return image
```

레이어 병렬 Job(ParallelJob)의 개념은 다음과 같습니다.

```pseudo
function ParallelRender(layers):
    parallel_for each layer in layers:
        layerRenderer = layer.createMapRenderer(renderContext)
        layerImage = layerRenderer.renderToImage()

    final = compose(layerImages in layer panel order)
    labels = solveLabelPlacement(allLabelProviders, mapSettings)
    draw(labels on final)
    return final
```

(레이어 병렬 렌더 설명 출처: https://qgis.org/pyqgis/3.40/core/QgsMapRendererParallelJob.html)  
(라벨 엔진 입력/출력 설명 출처: https://api.qgis.org/api/classQgsLabelingEngine.html)

---

## 12) 타 플랫폼(WPF 등)으로 이식 시, “QGIS에서 가져올 핵심” (간단 요약)

QGIS를 참고해 WPF 렌더러를 설계할 때 가장 효과적인 5가지는 아래입니다.

1. **렌더 설정(Extent/size/layers)을 하나의 Settings 객체로 고정** (QgsMapSettings 스타일)
2. **레이어 렌더는 스냅샷 렌더러로 분리** (QgsMapLayerRenderer 스타일)
3. **레이어 병렬 렌더 + 순서 합성** (QgsMapRendererParallelJob 스타일)
4. **지오메트리 단순화/최적화는 컨텍스트 플래그로 통제** (QgsRenderContext + QgsVectorSimplifyMethod 스타일)
5. **라벨은 별도 엔진(후처리)로 해결** + z-index/obstacle 지원 (QgsLabelingEngine + PAL 스타일)

---

## 참고자료(원문 링크)
- QgsMapSettings: https://qgis.org/pyqgis/3.40/core/QgsMapSettings.html  
- PyQGIS Developer Cookbook(맵 렌더링 예제): https://docs.qgis.org/latest/en/docs/pyqgis_developer_cookbook/composer.html  
- QgsMapRendererJob(비동기 렌더): https://api.qgis.org/api/classQgsMapRendererJob.html  
- QgsMapRendererParallelJob(레이어 병렬): https://qgis.org/pyqgis/3.40/core/QgsMapRendererParallelJob.html  
- 레이어 순서 규칙(레이어 패널): https://docs.qgis.org/latest/en/docs/training_manual/basic_map/preparation.html  
- 심볼 레벨(Symbol Levels): https://docs.qgis.org/latest/en/docs/training_manual/basic_map/symbology.html  
- QgsVectorLayerRenderer(심볼 레벨 렌더 함수): https://api.qgis.org/api/2.4/classQgsVectorLayerRenderer.html  
- QgsFeatureRenderer(start/stopRender & 스레드): https://qgis.org/pyqgis/3.40/core/QgsFeatureRenderer.html  
- QgsRenderContext(최적화/단순화): https://api.qgis.org/api/classQgsRenderContext.html  
- QgsVectorSimplifyMethod(단순화 실행 위치 등): https://qgis.org/pyqgis/3.44/core/QgsVectorSimplifyMethod.html  
- QgsLabelingEngine(충돌 없는 레이아웃): https://api.qgis.org/api/classQgsLabelingEngine.html  
- pal::Pal(Extent 문제 추출): https://api.qgis.org/api/classpal_1_1Pal.html  
- pal::SearchMethod(알고리즘 트레이드오프): https://api.qgis.org/api/3.4/namespacepal.html  
- Label z-index 사용자 매뉴얼: https://docs.qgis.org/3.10/en/docs/user_manual/style_library/label_settings.html  
- QgsPalLayerSettings(zIndex): https://qgis.org/pyqgis/master/core/QgsPalLayerSettings.html  
- Obstacles 사용자 매뉴얼: https://docs.qgis.org/latest/en/docs/user_manual/style_library/label_settings.html  
- QgsLabelObstacleSettings(API): https://qgis.org/pyqgis/3.44/core/QgsLabelObstacleSettings.html  
- QgsMapRendererCache(렌더 캐시): https://qgis.org/pyqgis/master/core/QgsMapRendererCache.html  
- QgsMapCanvas(cache): https://qgis.org/pyqgis/3.44/gui/QgsMapCanvas.html  
