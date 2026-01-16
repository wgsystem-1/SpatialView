# QGIS 렌더링 알고리즘 심층 분석 - C# 구현 기술 문서

QGIS의 렌더링 시스템은 **레이어 기반 심볼 아키텍처**, **PAL 라벨링 엔진**, **PROJ 좌표 변환 파이프라인**을 핵심으로 구성된다. C# 구현 시 SkiaSharp(그래픽스), NetTopologySuite(지오메트리), ProjNet(좌표 변환)의 조합이 가장 효과적이며, 대부분의 알고리즘을 **85% 이상 재현** 가능하다. 라벨 배치 엔진만 별도 구현이 필요하다.

---

## 1. 벡터 데이터 렌더링 알고리즘

### Point Geometry 렌더링 핵심 로직

QGIS의 마커 렌더링은 `QgsMarkerSymbolLayer` 계층 구조를 따른다. 핵심 소스 파일은 `src/core/symbology/qgsmarkersymbollayer.cpp`이다.

**Simple Marker 알고리즘 흐름:**

```
1. 크기 계산: scaledSize = convertToPainterUnits(mSize, mSizeUnit, mSizeMapUnitScale)
2. 변환 행렬 구성: transform.scale(half, half) → transform.rotate(mAngle)
3. 앵커 포인트 기준 오프셋 계산
4. 정규화 좌표(-1~+1)로 정의된 도형을 변환하여 렌더링
```

**마커 오프셋 계산 공식:**
```
offsetX = offset.x × cos(angle) - offset.y × sin(angle)
offsetY = offset.x × sin(angle) + offset.y × cos(angle)
```

**C# 구현 예시 (SkiaSharp):**
```csharp
public void RenderMarker(SKCanvas canvas, SKPoint point, MarkerDefinition marker)
{
    var path = new SKPath();
    CreateMarkerShape(path, marker.Shape); // 정규화된 -1~+1 좌표
    
    var transform = SKMatrix.CreateScale(marker.Size / 2, marker.Size / 2);
    if (marker.Rotation != 0)
        transform = transform.PostConcat(SKMatrix.CreateRotationDegrees(marker.Rotation));
    transform = transform.PostConcat(SKMatrix.CreateTranslation(point.X, point.Y));
    
    path.Transform(transform);
    canvas.DrawPath(path, fillPaint);
    canvas.DrawPath(path, strokePaint);
}
```

**지원 마커 형태**: Circle, Square, Diamond, Pentagon, Hexagon, Triangle, Star, Cross, Arrow 등 **30종 이상**의 기본 도형을 `Qgis::MarkerShape` enum으로 정의한다.

### Line Geometry 렌더링

**Simple Line**의 핵심은 Qt의 `QPen` 속성 매핑이다:

| QGIS 속성 | C# SkiaSharp 매핑 |
|-----------|-------------------|
| `mWidth` | `SKPaint.StrokeWidth` |
| `mPenStyle` | `SKPathEffect.CreateDash()` |
| `mPenCapStyle` | `SKPaint.StrokeCap` |
| `mPenJoinStyle` | `SKPaint.StrokeJoin` |
| `mCustomDashVector` | `SKPathEffect.CreateDash(float[], phase)` |

**Marker Line 배치 알고리즘**은 라인을 따라 일정 간격으로 마커를 배치한다:

```csharp
public IEnumerable<MarkerPosition> PlaceMarkersAlongLine(
    IEnumerable<Coordinate> line, double interval, MarkerPlacement placement)
{
    double totalLength = CalculateLineLength(line);
    double currentDist = placement.HasFlag(FirstVertex) ? 0 : interval;
    
    while (currentDist < totalLength)
    {
        var (point, angle) = GetPointAndAngleAtDistance(line, currentDist);
        yield return new MarkerPosition(point, angle);
        currentDist += interval;
    }
}
```

**Arrow Symbol**은 화살표 머리와 몸체를 별도의 폴리곤으로 생성한다. 곡선 화살표는 3개 이상의 정점이 필요하며 원호 보간을 적용한다.

### Polygon Geometry 렌더링

**Gradient Fill 알고리즘:**

```csharp
public SKShader CreateGradientShader(GradientFillSettings settings, SKRect bounds)
{
    var p1 = new SKPoint(
        bounds.Left + settings.ReferencePoint1.X * bounds.Width,
        bounds.Top + settings.ReferencePoint1.Y * bounds.Height);
    var p2 = new SKPoint(
        bounds.Left + settings.ReferencePoint2.X * bounds.Width,
        bounds.Top + settings.ReferencePoint2.Y * bounds.Height);
    
    return settings.GradientType switch
    {
        GradientType.Linear => SKShader.CreateLinearGradient(p1, p2, colors, positions, tileMode),
        GradientType.Radial => SKShader.CreateRadialGradient(center, radius, colors, positions, tileMode),
        GradientType.Conical => SKShader.CreateSweepGradient(center, colors, positions),
        _ => throw new NotSupportedException()
    };
}
```

**Shapeburst Fill**은 거리 변환(Distance Transform)을 사용하여 경계에서 내부로 그라디언트를 생성한다:

1. 폴리곤을 마스크 이미지로 래스터화
2. 각 내부 픽셀에서 경계까지의 최단 거리 계산
3. 거리값을 정규화하여 색상 램프 적용
4. 선택적 가우시안 블러 적용 (0-17 픽셀 반경)

**구현 난이도**: Shapeburst는 거리 변환 알고리즘 구현이 필요하므로 **High** 수준이다.

---

## 2. 래스터 데이터 렌더링 알고리즘

### Singleband Gray Renderer

단일 밴드를 그레이스케일로 변환한다. **대비 향상(Contrast Enhancement)** 알고리즘이 핵심이다.

```csharp
public int EnhanceContrast(double value, ContrastEnhancement method, double min, double max)
{
    return method switch
    {
        NoEnhancement => (int)value,
        StretchToMinMax => (int)(255.0 * (value - min) / (max - min)),
        StretchAndClip => value < min || value > max ? -1 : (int)(255.0 * (value - min) / (max - min)),
        ClipToMinMax => value < min || value > max ? -1 : (int)value,
        _ => 0
    };
}
```

### Hillshade Renderer - Horn's 공식

**3×3 커널을 사용한 경사도/방위각 계산:**

```
           x11  x12  x13
           x21 [x22] x23
           x31  x32  x33

derX = ((x13 + 2×x23 + x33) - (x11 + 2×x21 + x31)) / (8 × cellXSize)
derY = ((x31 + 2×x32 + x33) - (x11 + 2×x12 + x13)) / (8 × cellYSize)
```

**음영기복 계산 공식:**
```csharp
double zenithRad = (90 - altitude) * Math.PI / 180.0;
double azimuthRad = -azimuth * Math.PI / 180.0;
double cosZenith = Math.Cos(zenithRad);
double sinZenith = Math.Sin(zenithRad);

double hillshade = Math.Clamp(
    (sinZenith * 254.0 - 
     (derY * Math.Cos(azimuthRad) * cosZenith * zFactor * 254.0 - 
      derX * Math.Sin(azimuthRad) * cosZenith * zFactor * 254.0)) /
    Math.Sqrt(1 + zFactor * zFactor * (derX * derX + derY * derY)),
    0.0, 255.0);
```

**다방향 음영기복(Multi-directional)**은 225°, 270°, 315°, 360° 4방향의 가중 평균을 사용한다.

### 리샘플링 알고리즘

| 알고리즘 | 커널 크기 | 용도 | C# 구현 |
|---------|----------|------|---------|
| Nearest Neighbor | 1×1 | 분류형 데이터 | `Math.Round(x), Math.Round(y)` |
| Bilinear | 2×2 | 연속형 데이터 | 4픽셀 선형 보간 |
| Cubic | 4×4 | 고품질 스무딩 | Keys(1981) 가중치 함수 |
| Lanczos | 6×6 | 최고 품질 | sinc 함수 기반 |

**Bilinear 보간 공식:**
```csharp
double Bilinear(double x, double y, double[,] pixels)
{
    int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
    double fx = x - x0, fy = y - y0;
    
    return pixels[y0, x0] * (1 - fx) * (1 - fy) +
           pixels[y0, x0 + 1] * fx * (1 - fy) +
           pixels[y0 + 1, x0] * (1 - fx) * fy +
           pixels[y0 + 1, x0 + 1] * fx * fy;
}
```

---

## 3. 렌더러 유형별 알고리즘

### Graduated Renderer 분류 방법

**Jenks Natural Breaks (동적 프로그래밍):**

이 알고리즘은 클래스 내 분산을 최소화하고 클래스 간 분산을 최대화한다. **GVF(Goodness of Variance Fit) = (SDAM - SDCM) / SDAM**으로 품질을 평가한다.

```csharp
public static double[] JenksBreaks(double[] values, int numClasses)
{
    var sorted = values.OrderBy(v => v).ToArray();
    int n = sorted.Length;
    
    // 분산 조합 행렬 (DP 테이블)
    double[,] mat1 = new double[n + 1, numClasses + 1]; // 하한 인덱스
    double[,] mat2 = new double[n + 1, numClasses + 1]; // 누적 분산
    
    // 초기화 및 DP 채우기...
    // O(k × n²) 시간 복잡도
    
    // 역추적으로 breaks 추출
    return ExtractBreaks(mat1, sorted, numClasses);
}
```

**복잡도**: O(k × n²) 시간, O(k × n) 공간

**Pretty Breaks**는 R의 `pretty()` 함수를 구현하여 1, 2, 5 × 10^k 형태의 "보기 좋은" 숫자로 반올림한다.

### Heatmap Renderer - 커널 밀도 추정

**밀도 계산 공식:**
```
density(x, y) = Σ K(d / h) × w
```

**커널 함수:**

| 커널 | 공식 (u = d/h, u ≤ 1) |
|------|----------------------|
| Quartic (기본) | (15/16) × (1 - u²)² |
| Epanechnikov | (3/4) × (1 - u²) |
| Triangular | 1 - u |
| Triweight | (35/32) × (1 - u²)³ |

```csharp
public double KernelValue(double distance, double bandwidth, KernelShape shape)
{
    double u = distance / bandwidth;
    if (u > 1) return 0;
    
    return shape switch
    {
        Quartic => 0.9375 * Math.Pow(1 - u * u, 2),
        Epanechnikov => 0.75 * (1 - u * u),
        Triangular => 1 - u,
        _ => 0
    };
}
```

### Point Cluster Renderer

**클러스터링 알고리즘:**
1. 허용 거리(tolerance) 내의 점들을 그룹화
2. 각 클러스터의 중심점(centroid) 계산
3. Ring/Concentric/Grid 패턴으로 개별 점 배치

```csharp
public static PointF[] DisplaceRing(PointF center, int count, double radius)
{
    var points = new PointF[count];
    double angleStep = 2 * Math.PI / count;
    for (int i = 0; i < count; i++)
    {
        double angle = i * angleStep;
        points[i] = new PointF(
            center.X + (float)(radius * Math.Cos(angle)),
            center.Y + (float)(radius * Math.Sin(angle)));
    }
    return points;
}
```

---

## 4. 심볼로지 시스템

### Symbol Layer 구조

```
QgsSymbol
├── mLayers: QgsSymbolLayerList (스택 구조)
├── mOpacity: double
└── render 시 각 레이어를 bottom-to-top 순서로 합성
```

**합성(Compositing) 모드**는 Qt의 `QPainter::CompositionMode`를 사용:
- Normal, Multiply, Screen, Overlay, Darken, Lighten
- Color Dodge, Color Burn, Hard Light, Soft Light
- Difference, Subtract, Addition

### Data-Defined 속성 처리

```csharp
public class PropertyCollection
{
    private Dictionary<PropertyKey, Expression> properties;
    
    public object Evaluate(PropertyKey key, Feature feature, RenderContext context)
    {
        if (!properties.TryGetValue(key, out var expr))
            return GetDefaultValue(key);
        
        // 표현식 컨텍스트 변수 설정
        context.SetVariable("@value", GetOriginalValue(key));
        context.SetVariable("@geometry_part_num", feature.PartIndex);
        
        return expr.Evaluate(feature, context);
    }
}
```

**주요 Data-Defined 속성**: FillColor, StrokeColor, StrokeWidth, Size, Angle, Offset, Opacity 등

---

## 5. 라벨 렌더링

### PAL 라벨 배치 알고리즘

QGIS는 **PAL(Placement Automated Labeling)** 라이브러리를 사용한다. 소스 위치: `src/core/pal/`

**Point 배치 모드:**

| 모드 | 설명 |
|------|------|
| AroundPoint | 포인트 주변 원형 배치 |
| OverPoint | 포인트 중앙 |
| OrderedPositions | 8방향 우선순위: TR → TL → BR → BL → R → L → T → B |

**Line 배치:**
- Parallel: 라인을 따라 수평
- Curved: 라인 곡률을 따라 문자 개별 배치
- Horizontal: 라인 위에 수평

### 충돌 감지 알고리즘

1. R-tree 공간 인덱스에 후보와 장애물 삽입
2. 바운딩 박스 교차 테스트로 1차 필터링
3. GEOS를 사용한 정밀 교차 테스트
4. 충돌 시 비용(cost)에 페널티 추가

```csharp
public bool HasConflict(LabelCandidate candidate, SpatialIndex<LabelCandidate> index)
{
    var potentialConflicts = index.Query(candidate.BoundingBox);
    return potentialConflicts.Any(c => 
        GEOSIntersects(candidate.Geometry, c.Geometry));
}
```

### 곡선 라벨 렌더링

```csharp
public IEnumerable<CharacterPosition> PlaceCurvedText(
    string text, IEnumerable<Coordinate> path, Font font)
{
    double currentOffset = startOffset;
    double previousAngle = 0;
    
    foreach (char ch in text)
    {
        var (point, angle) = GetPointAndAngleAtDistance(path, currentOffset);
        
        // 각도 제약 검사 (기본 20-60도)
        if (Math.Abs(angle - previousAngle) > maxAngleBetweenChars)
            yield break; // 후보 거부
        
        yield return new CharacterPosition(ch, point, angle);
        
        currentOffset += MeasureCharWidth(ch, font) + letterSpacing;
        previousAngle = angle;
    }
}
```

**뒤집힘 감지**: 평균 텍스트 각도가 90° < angle < 270°이면 방향 반전

### 텍스트 렌더링 순서

1. **Shadow** (드롭 섀도우) - 오프셋, 블러, 불투명도
2. **Background** (배경 도형) - 텍스트 뒤 사각형/타원
3. **Buffer** (Halo) - 텍스트 외곽선 스트로크
4. **Text** (본문) - 메인 텍스트

```csharp
// Buffer(Halo) 렌더링 - 경로 스트로크
var textPath = new SKPath();
textPath.AddText(text, point.X, point.Y, font);

using var bufferPaint = new SKPaint {
    Style = SKPaintStyle.Stroke,
    StrokeWidth = bufferSize * 2,
    Color = bufferColor,
    StrokeJoin = SKStrokeJoin.Round
};
canvas.DrawPath(textPath, bufferPaint);
canvas.DrawPath(textPath, textPaint); // 텍스트 채우기
```

---

## 6. 좌표 변환 및 투영

### PROJ 파이프라인 아키텍처

QGIS는 PROJ 라이브러리를 통해 좌표 변환을 수행한다. **Helmert 7-파라미터 변환**이 기본이다.

```
proj=pipeline
  step proj=cart ellps=WGS84           // 측지 → 직교
  step proj=helmert x=-81.07 y=-89.36 z=-115.75 ...  // 데이텀 변환
  step proj=cart inv ellps=GRS80       // 직교 → 측지
```

**C# 구현 (ProjNet):**
```csharp
var csFactory = new CoordinateSystemFactory();
var ctFactory = new CoordinateTransformationFactory();

var wgs84 = csFactory.CreateFromWkt(WKT_WGS84);
var webMercator = csFactory.CreateFromWkt(WKT_WEB_MERCATOR);

var transform = ctFactory.CreateFromCoordinateSystems(wgs84, webMercator);
double[] result = transform.MathTransform.Transform(new[] { lon, lat });
```

**라이브러리 비교:**

| 라이브러리 | 정확도 | PROJ 호환성 |
|-----------|--------|-------------|
| ProjNet | 양호 | ~85% |
| DotSpatial.Projections | 양호 | ~80% |

**Gap**: 그리드 기반 데이텀 변환(NTv2)은 별도 구현 필요

---

## 7. 타일 렌더링

### Web Mercator (EPSG:3857) 타일 계산

```csharp
public const double EARTH_RADIUS = 6378137.0;
public const int TILE_SIZE = 256;

public static (int tileX, int tileY) LonLatToTile(double lon, double lat, int zoom)
{
    int n = 1 << zoom;
    double latRad = lat * Math.PI / 180.0;
    
    int tileX = (int)Math.Floor((lon + 180.0) / 360.0 * n);
    int tileY = (int)Math.Floor((1.0 - Math.Log(Math.Tan(latRad) + 
        1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
    
    return (tileX, tileY);
}

public static double GetScaleDenominator(int zoom)
{
    // OGC 표준: 0.28mm/pixel
    double metersPerPixel = (2 * Math.PI * EARTH_RADIUS) / (TILE_SIZE * (1 << zoom));
    return metersPerPixel / 0.00028;
}
// Zoom 0: ~559M, Zoom 18: ~2132
```

### 줌 레벨별 심볼 스케일링

```csharp
public double GetSymbolSize(double baseSize, int zoom, int referenceZoom = 10)
{
    double scaleFactor = Math.Pow(2, zoom - referenceZoom);
    return baseSize * scaleFactor;
}
```

### Anti-aliasing 설정

```csharp
using var paint = new SKPaint {
    IsAntialias = true,
    FilterQuality = SKFilterQuality.High
};

// 또는 System.Drawing
graphics.SmoothingMode = SmoothingMode.HighQuality;
graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
```

---

## 8. 성능 최적화 기법

### R-tree 공간 인덱스

NetTopologySuite의 `STRtree`(Sort-Tile-Recursive) 또는 `HPRtree` 사용:

```csharp
var index = new STRtree<Feature>();
foreach (var feature in features)
    index.Insert(feature.Geometry.EnvelopeInternal, feature);
index.Build(); // 첫 쿼리 전 빌드 필수

// 검색
var results = index.Query(searchEnvelope)
    .Where(f => f.Geometry.Intersects(searchPolygon));
```

**성능 팁:**
- `HPRtree`는 STRtree보다 **~20% 빠름**
- 반복 테스트에는 `PreparedGeometryFactory.Prepare()`로 **10-100배 속도 향상**

### Douglas-Peucker 단순화

```csharp
public static List<Point> DouglasPeucker(List<Point> points, double epsilon)
{
    if (points.Count < 3) return new List<Point>(points);
    
    double dmax = 0;
    int index = 0;
    
    for (int i = 1; i < points.Count - 1; i++)
    {
        double d = PerpendicularDistance(points[i], points[0], points[^1]);
        if (d > dmax) { dmax = d; index = i; }
    }
    
    if (dmax > epsilon)
    {
        var left = DouglasPeucker(points.Take(index + 1).ToList(), epsilon);
        var right = DouglasPeucker(points.Skip(index).ToList(), epsilon);
        left.RemoveAt(left.Count - 1);
        left.AddRange(right);
        return left;
    }
    return new List<Point> { points[0], points[^1] };
}
```

**복잡도**: O(n²) 최악, O(n log n) 평균

**Visvalingam-Whyatt**는 삼각형 면적 기반으로 위상 보존에 유리하다.

### 캐시 전략

```csharp
public class MapRenderCache
{
    // L1: 심볼 캐시 (가장 빠름)
    private readonly LRUCache<int, RenderedSymbol> symbolCache;
    
    // L2: 타일 캐시
    private readonly LRUCache<TileKey, byte[]> tileCache;
    
    // L3: 디스크 캐시
    private readonly DiskTileCache diskCache;
    
    // 공간 기반 무효화
    public void InvalidateRegion(Envelope editedArea)
    {
        var affectedKeys = tileIndex.Query(editedArea);
        foreach (var key in affectedKeys)
            tileCache.Remove(key);
    }
}
```

---

## C# 구현 라이브러리 매핑 요약

| QGIS 기능 | C# 라이브러리 | 구현 난이도 |
|-----------|--------------|------------|
| 벡터 렌더링 | SkiaSharp | **Low** |
| 래스터 처리 | SkiaSharp (SKBitmap) | **Low** |
| 지오메트리 연산 | NetTopologySuite | **Low** |
| 공간 인덱싱 | NetTopologySuite (STRtree) | **Low** |
| 좌표 변환 | ProjNet | **Medium** |
| 텍스트 렌더링 | SkiaSharp + HarfBuzzSharp | **Medium** |
| 라벨 배치 엔진 | **커스텀 구현 필요** | **High** |
| Hillshade | 커스텀 구현 | **Medium** |
| Shapeburst Fill | 커스텀 구현 (거리 변환) | **High** |

---

## 권장 아키텍처

```
┌─────────────────────────────────────────────────┐
│              Rendering Layer                     │
│  SkiaSharp (SKCanvas, SKPath, SKPaint, SKShader)│
└─────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────┐
│            Geometry/Spatial Layer               │
│  NetTopologySuite (Geometry, STRtree, Buffer)   │
└─────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────┐
│           Coordinate System Layer               │
│  ProjNet / DotSpatial.Projections               │
└─────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────┐
│                 Data Layer                       │
│  NetTopologySuite.IO (Shapefile, GeoJSON, WKB)  │
└─────────────────────────────────────────────────┘
```

---

## 우선순위 기반 구현 로드맵

**Phase 1 (기본 렌더링):**
1. Simple Marker/Line/Fill 렌더러
2. 기본 CRS 변환 (WGS84 ↔ Web Mercator)
3. 타일 피라미드 생성

**Phase 2 (고급 심볼로지):**
1. Gradient/Pattern Fill
2. Marker Line, Arrow Symbol
3. Categorized/Graduated 렌더러

**Phase 3 (라벨링):**
1. Point 라벨 배치
2. 충돌 감지 (R-tree 기반)
3. Line/Polygon 라벨 배치

**Phase 4 (최적화):**
1. 멀티스레드 렌더링
2. LOD 기반 단순화
3. 타일 캐싱 시스템

---

## 참고 소스코드 위치

| 모듈 | QGIS 소스 경로 |
|------|---------------|
| 심볼 레이어 | `src/core/symbology/qgs*symbollayer.cpp` |
| 래스터 렌더러 | `src/core/raster/qgs*renderer.cpp` |
| 피처 렌더러 | `src/core/symbology/qgsfeaturerenderer.cpp` |
| PAL 라벨링 | `src/core/pal/*.cpp` |
| 좌표 변환 | `src/core/qgscoordinatetransform.cpp` |
| 타일 매트릭스 | `src/core/vectortile/qgstilematrix.cpp` |

모든 소스는 **GitHub qgis/QGIS** 저장소에서 확인 가능하다.