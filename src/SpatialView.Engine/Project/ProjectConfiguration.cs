namespace SpatialView.Engine.Project;

/// <summary>
/// 맵 설정
/// </summary>
public class MapSettings
{
    /// <summary>
    /// 배경색
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// 선택 색상
    /// </summary>
    public string SelectionColor { get; set; } = "#00FF00";

    /// <summary>
    /// 강조 색상
    /// </summary>
    public string HighlightColor { get; set; } = "#FFFF00";

    /// <summary>
    /// 안티앨리어싱 활성화
    /// </summary>
    public bool EnableAntialiasing { get; set; } = true;

    /// <summary>
    /// 레이블 표시
    /// </summary>
    public bool EnableLabels { get; set; } = true;

    /// <summary>
    /// 최소 축척
    /// </summary>
    public double MinScale { get; set; } = 0;

    /// <summary>
    /// 최대 축척
    /// </summary>
    public double MaxScale { get; set; } = double.MaxValue;

    /// <summary>
    /// 스냅 활성화
    /// </summary>
    public bool EnableSnapping { get; set; } = false;

    /// <summary>
    /// 스냅 허용 오차 (픽셀)
    /// </summary>
    public double SnapTolerance { get; set; } = 10;

    /// <summary>
    /// 그리드 표시
    /// </summary>
    public bool ShowGrid { get; set; } = false;

    /// <summary>
    /// 그리드 간격
    /// </summary>
    public double GridSpacing { get; set; } = 100;

    /// <summary>
    /// 좌표 표시 형식
    /// </summary>
    public string CoordinateFormat { get; set; } = "DD"; // DD, DMS, DDM
}

/// <summary>
/// 레이어 구성
/// </summary>
public class LayerConfiguration
{
    /// <summary>
    /// 레이어 ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 레이어 이름
    /// </summary>
    public string Name { get; set; } = "새 레이어";

    /// <summary>
    /// 레이어 타입
    /// </summary>
    public LayerType Type { get; set; } = LayerType.Vector;

    /// <summary>
    /// 데이터 소스 (파일 경로, 연결 문자열 등)
    /// </summary>
    public string DataSource { get; set; } = "";

    /// <summary>
    /// 데이터 소스 타입
    /// </summary>
    public DataSourceType DataSourceType { get; set; } = DataSourceType.Shapefile;

    /// <summary>
    /// 표시 여부
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 선택 가능 여부
    /// </summary>
    public bool IsSelectable { get; set; } = true;

    /// <summary>
    /// 편집 가능 여부
    /// </summary>
    public bool IsEditable { get; set; } = false;

    /// <summary>
    /// 최소 축척
    /// </summary>
    public double MinScale { get; set; } = 0;

    /// <summary>
    /// 최대 축척
    /// </summary>
    public double MaxScale { get; set; } = double.MaxValue;

    /// <summary>
    /// 투명도 (0-1)
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// 스타일 설정
    /// </summary>
    public StyleConfiguration? Style { get; set; }

    /// <summary>
    /// 레이블 스타일
    /// </summary>
    public LabelStyleConfiguration? LabelStyle { get; set; }

    /// <summary>
    /// 필터 (SQL WHERE 절)
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// 사용자 정의 속성
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// 레이어 타입
/// </summary>
public enum LayerType
{
    /// <summary>벡터 레이어</summary>
    Vector,
    /// <summary>래스터 레이어</summary>
    Raster,
    /// <summary>타일 레이어</summary>
    Tile,
    /// <summary>WMS 레이어</summary>
    WMS,
    /// <summary>그룹 레이어</summary>
    Group
}

/// <summary>
/// 데이터 소스 타입
/// </summary>
public enum DataSourceType
{
    /// <summary>Shapefile</summary>
    Shapefile,
    /// <summary>GeoJSON</summary>
    GeoJSON,
    /// <summary>GeoPackage</summary>
    GeoPackage,
    /// <summary>PostGIS</summary>
    PostGIS,
    /// <summary>SQL Server Spatial</summary>
    SqlServerSpatial,
    /// <summary>WMS</summary>
    WMS,
    /// <summary>WFS</summary>
    WFS,
    /// <summary>Vector Tiles</summary>
    VectorTiles,
    /// <summary>GeoTIFF</summary>
    GeoTIFF,
    /// <summary>메모리</summary>
    Memory
}

/// <summary>
/// 스타일 구성
/// </summary>
public class StyleConfiguration
{
    /// <summary>
    /// 스타일 타입
    /// </summary>
    public StyleType Type { get; set; } = StyleType.Simple;

    /// <summary>
    /// 채우기 색상
    /// </summary>
    public string? FillColor { get; set; }

    /// <summary>
    /// 선 색상
    /// </summary>
    public string? StrokeColor { get; set; }

    /// <summary>
    /// 선 두께
    /// </summary>
    public double StrokeWidth { get; set; } = 1.0;

    /// <summary>
    /// 심볼 (점 레이어)
    /// </summary>
    public string? Symbol { get; set; }

    /// <summary>
    /// 크기 (점 레이어)
    /// </summary>
    public double? Size { get; set; }

    /// <summary>
    /// 테마틱 맵 설정
    /// </summary>
    public ThematicStyleConfiguration? ThematicStyle { get; set; }
}

/// <summary>
/// 스타일 타입
/// </summary>
public enum StyleType
{
    /// <summary>단순 스타일</summary>
    Simple,
    /// <summary>유니크 값</summary>
    UniqueValue,
    /// <summary>등급 구분</summary>
    Graduated,
    /// <summary>비례 심볼</summary>
    Proportional,
    /// <summary>규칙 기반</summary>
    RuleBased
}

/// <summary>
/// 테마틱 스타일 구성
/// </summary>
public class ThematicStyleConfiguration
{
    /// <summary>
    /// 필드명
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>
    /// 분류 방법
    /// </summary>
    public ClassificationMethod Method { get; set; } = ClassificationMethod.EqualInterval;

    /// <summary>
    /// 클래스 개수
    /// </summary>
    public int ClassCount { get; set; } = 5;

    /// <summary>
    /// 색상 램프
    /// </summary>
    public string ColorRamp { get; set; } = "Blues";

    /// <summary>
    /// 클래스별 스타일
    /// </summary>
    public List<ClassStyle> Classes { get; set; } = new();
}

/// <summary>
/// 분류 방법
/// </summary>
public enum ClassificationMethod
{
    /// <summary>등간격</summary>
    EqualInterval,
    /// <summary>분위수</summary>
    Quantile,
    /// <summary>자연적 구분</summary>
    NaturalBreaks,
    /// <summary>표준편차</summary>
    StandardDeviation,
    /// <summary>수동</summary>
    Manual
}

/// <summary>
/// 클래스별 스타일
/// </summary>
public class ClassStyle
{
    /// <summary>
    /// 최소값
    /// </summary>
    public object? MinValue { get; set; }

    /// <summary>
    /// 최대값
    /// </summary>
    public object? MaxValue { get; set; }

    /// <summary>
    /// 레이블
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// 스타일
    /// </summary>
    public StyleConfiguration Style { get; set; } = new();
}

/// <summary>
/// 레이블 스타일 구성
/// </summary>
public class LabelStyleConfiguration
{
    /// <summary>
    /// 레이블 필드명
    /// </summary>
    public string FieldName { get; set; } = "";

    /// <summary>
    /// 폰트 패밀리
    /// </summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>
    /// 폰트 크기
    /// </summary>
    public double FontSize { get; set; } = 12;

    /// <summary>
    /// 폰트 색상
    /// </summary>
    public string FontColor { get; set; } = "#000000";

    /// <summary>
    /// 후광 색상
    /// </summary>
    public string? HaloColor { get; set; }

    /// <summary>
    /// 후광 두께
    /// </summary>
    public double HaloWidth { get; set; } = 0;

    /// <summary>
    /// 배치
    /// </summary>
    public LabelPlacement Placement { get; set; } = LabelPlacement.PointCenter;

    /// <summary>
    /// 최소 축척
    /// </summary>
    public double MinScale { get; set; } = 0;

    /// <summary>
    /// 최대 축척
    /// </summary>
    public double MaxScale { get; set; } = double.MaxValue;

    /// <summary>
    /// 충돌 회피
    /// </summary>
    public bool AvoidCollisions { get; set; } = true;

    /// <summary>
    /// 표현식 (고급)
    /// </summary>
    public string? Expression { get; set; }
}

/// <summary>
/// 레이블 배치
/// </summary>
public enum LabelPlacement
{
    /// <summary>점 중앙</summary>
    PointCenter,
    /// <summary>점 위</summary>
    PointAbove,
    /// <summary>점 아래</summary>
    PointBelow,
    /// <summary>점 왼쪽</summary>
    PointLeft,
    /// <summary>점 오른쪽</summary>
    PointRight,
    /// <summary>선 따라</summary>
    LineFollow,
    /// <summary>선 평행</summary>
    LineParallel,
    /// <summary>폴리곤 중앙</summary>
    PolygonCenter,
    /// <summary>폴리곤 경계</summary>
    PolygonBoundary
}

/// <summary>
/// 플러그인 구성
/// </summary>
public class PluginConfiguration
{
    /// <summary>
    /// 플러그인 ID
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 플러그인 이름
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 활성화 여부
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 플러그인 설정
    /// </summary>
    public Dictionary<string, object>? Settings { get; set; }
}