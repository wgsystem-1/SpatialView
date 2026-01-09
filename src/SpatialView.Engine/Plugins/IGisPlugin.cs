namespace SpatialView.Engine.Plugins;

/// <summary>
/// GIS 플러그인 인터페이스
/// </summary>
public interface IGisPlugin : IDisposable
{
    /// <summary>
    /// 플러그인 고유 식별자
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 플러그인 이름
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 플러그인 설명
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 플러그인 버전
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// 플러그인 작성자
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 플러그인 상태
    /// </summary>
    PluginState State { get; }

    /// <summary>
    /// 플러그인 타입
    /// </summary>
    PluginType Type { get; }

    /// <summary>
    /// 필요한 최소 엔진 버전
    /// </summary>
    Version MinEngineVersion { get; }

    /// <summary>
    /// 의존성 플러그인 ID 목록
    /// </summary>
    IReadOnlyList<string> Dependencies { get; }

    /// <summary>
    /// 플러그인 초기화
    /// </summary>
    /// <param name="context">플러그인 컨텍스트</param>
    Task<bool> InitializeAsync(IPluginContext context);

    /// <summary>
    /// 플러그인 시작
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// 플러그인 중지
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 플러그인 설정 가져오기
    /// </summary>
    IPluginSettings? GetSettings();

    /// <summary>
    /// 플러그인 설정 적용
    /// </summary>
    void ApplySettings(IPluginSettings settings);
}

/// <summary>
/// 플러그인 상태
/// </summary>
public enum PluginState
{
    /// <summary>초기화되지 않음</summary>
    NotInitialized,
    /// <summary>초기화 중</summary>
    Initializing,
    /// <summary>초기화됨</summary>
    Initialized,
    /// <summary>시작됨</summary>
    Started,
    /// <summary>중지됨</summary>
    Stopped,
    /// <summary>오류 발생</summary>
    Error,
    /// <summary>비활성화됨</summary>
    Disabled
}

/// <summary>
/// 플러그인 타입
/// </summary>
[Flags]
public enum PluginType
{
    /// <summary>도구 플러그인</summary>
    Tool = 1,
    /// <summary>데이터 프로바이더</summary>
    DataProvider = 2,
    /// <summary>분석 도구</summary>
    Analysis = 4,
    /// <summary>렌더러</summary>
    Renderer = 8,
    /// <summary>변환기</summary>
    Converter = 16,
    /// <summary>UI 확장</summary>
    UIExtension = 32,
    /// <summary>서비스</summary>
    Service = 64
}

/// <summary>
/// 플러그인 컨텍스트
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 맵 캔버스
    /// </summary>
    Map.IMapCanvas MapCanvas { get; }

    /// <summary>
    /// 레이어 컬렉션
    /// </summary>
    Data.Layers.ILayerCollection Layers { get; }

    /// <summary>
    /// 플러그인 매니저
    /// </summary>
    IPluginManager PluginManager { get; }

    /// <summary>
    /// 이벤트 버스
    /// </summary>
    Events.IEventBus EventBus { get; }

    /// <summary>
    /// 로거
    /// </summary>
    IPluginLogger Logger { get; }

    /// <summary>
    /// 서비스 제공자
    /// </summary>
    IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 플러그인 데이터 디렉토리 경로
    /// </summary>
    string PluginDataDirectory { get; }
}

/// <summary>
/// 플러그인 설정 인터페이스
/// </summary>
public interface IPluginSettings
{
    /// <summary>
    /// 설정을 JSON으로 직렬화
    /// </summary>
    string ToJson();

    /// <summary>
    /// JSON에서 설정 로드
    /// </summary>
    void FromJson(string json);

    /// <summary>
    /// 기본값으로 초기화
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// 설정 유효성 검사
    /// </summary>
    bool Validate(out string? errorMessage);
}

/// <summary>
/// 플러그인 로거
/// </summary>
public interface IPluginLogger
{
    void LogDebug(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
}

/// <summary>
/// 도구 플러그인 인터페이스
/// </summary>
public interface IToolPlugin : IGisPlugin
{
    /// <summary>
    /// 도구 이름
    /// </summary>
    string ToolName { get; }

    /// <summary>
    /// 도구 아이콘 (리소스 경로 또는 Base64)
    /// </summary>
    string? ToolIcon { get; }

    /// <summary>
    /// 도구 카테고리
    /// </summary>
    string ToolCategory { get; }

    /// <summary>
    /// 도구 활성화
    /// </summary>
    void Activate();

    /// <summary>
    /// 도구 비활성화
    /// </summary>
    void Deactivate();

    /// <summary>
    /// 도구 활성 상태
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// 마우스 다운 이벤트 처리
    /// </summary>
    bool OnMouseDown(MouseEventArgs e);

    /// <summary>
    /// 마우스 이동 이벤트 처리
    /// </summary>
    bool OnMouseMove(MouseEventArgs e);

    /// <summary>
    /// 마우스 업 이벤트 처리
    /// </summary>
    bool OnMouseUp(MouseEventArgs e);

    /// <summary>
    /// 키 다운 이벤트 처리
    /// </summary>
    bool OnKeyDown(KeyEventArgs e);
}

/// <summary>
/// 분석 플러그인 인터페이스
/// </summary>
public interface IAnalysisPlugin : IGisPlugin
{
    /// <summary>
    /// 분석 이름
    /// </summary>
    string AnalysisName { get; }

    /// <summary>
    /// 입력 파라미터 정의
    /// </summary>
    IEnumerable<IAnalysisParameter> GetParameters();

    /// <summary>
    /// 분석 실행
    /// </summary>
    Task<IAnalysisResult> ExecuteAsync(Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// 분석 유효성 검사
    /// </summary>
    bool ValidateParameters(Dictionary<string, object> parameters, out string? errorMessage);

    /// <summary>
    /// 진행률 변경 이벤트
    /// </summary>
    event EventHandler<ProgressEventArgs>? ProgressChanged;
}

/// <summary>
/// 데이터 프로바이더 플러그인 인터페이스
/// </summary>
public interface IDataProviderPlugin : IGisPlugin
{
    /// <summary>
    /// 지원하는 파일 확장자
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// 지원하는 데이터 타입
    /// </summary>
    DataProviderCapabilities Capabilities { get; }

    /// <summary>
    /// 데이터 소스 생성
    /// </summary>
    Data.Sources.IDataSource? CreateDataSource(string connectionString, Dictionary<string, object>? options = null);

    /// <summary>
    /// 연결 테스트
    /// </summary>
    Task<bool> TestConnectionAsync(string connectionString);

    /// <summary>
    /// 메타데이터 가져오기
    /// </summary>
    Task<DataSourceMetadata?> GetMetadataAsync(string connectionString);
}

/// <summary>
/// 마우스 이벤트 인수
/// </summary>
public class MouseEventArgs : EventArgs
{
    public int X { get; set; }
    public int Y { get; set; }
    public MouseButton Button { get; set; }
    public int ClickCount { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public Geometry.ICoordinate? WorldCoordinate { get; set; }
    public bool Handled { get; set; }
}

/// <summary>
/// 키보드 이벤트 인수
/// </summary>
public class KeyEventArgs : EventArgs
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public bool Handled { get; set; }
}

/// <summary>
/// 마우스 버튼
/// </summary>
public enum MouseButton
{
    Left,
    Middle,
    Right,
    XButton1,
    XButton2
}

/// <summary>
/// 키
/// </summary>
public enum Key
{
    None,
    Escape,
    Enter,
    Space,
    Delete,
    // ... 기타 키들
}

/// <summary>
/// 수정자 키
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

/// <summary>
/// 분석 파라미터
/// </summary>
public interface IAnalysisParameter
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }
    Type DataType { get; }
    object? DefaultValue { get; }
    bool IsRequired { get; }
    object? MinValue { get; }
    object? MaxValue { get; }
    IEnumerable<object>? AllowedValues { get; }
}

/// <summary>
/// 분석 결과
/// </summary>
public interface IAnalysisResult
{
    bool Success { get; }
    string? ErrorMessage { get; }
    Dictionary<string, object> Results { get; }
    TimeSpan ExecutionTime { get; }
}

/// <summary>
/// 진행률 이벤트 인수
/// </summary>
public class ProgressEventArgs : EventArgs
{
    public int Progress { get; set; }
    public string? Message { get; set; }
    public bool CanCancel { get; set; }
}

/// <summary>
/// 데이터 프로바이더 기능
/// </summary>
[Flags]
public enum DataProviderCapabilities
{
    None = 0,
    Read = 1,
    Write = 2,
    Create = 4,
    Delete = 8,
    SpatialIndex = 16,
    AttributeIndex = 32,
    Transaction = 64,
    BulkInsert = 128
}

/// <summary>
/// 데이터 소스 메타데이터
/// </summary>
public class DataSourceMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DataSourceType Type { get; set; }
    public Geometry.Envelope? Extent { get; set; }
    public string? SpatialReference { get; set; }
    public long? FeatureCount { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// 데이터 소스 타입
/// </summary>
public enum DataSourceType
{
    Unknown,
    File,
    Database,
    WebService,
    Memory
}

/// <summary>
/// 필드 메타데이터
/// </summary>
public class FieldMetadata
{
    public string Name { get; set; } = string.Empty;
    public Type DataType { get; set; } = typeof(object);
    public int? Length { get; set; }
    public int? Precision { get; set; }
    public bool IsNullable { get; set; } = true;
    public bool IsPrimaryKey { get; set; }
    public bool IsIndexed { get; set; }
}