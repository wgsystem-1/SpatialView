namespace SpatialView.Engine.Plugins;

/// <summary>
/// 플러그인 관리자 인터페이스
/// </summary>
public interface IPluginManager : IDisposable
{
    /// <summary>
    /// 등록된 모든 플러그인
    /// </summary>
    IReadOnlyList<IGisPlugin> Plugins { get; }

    /// <summary>
    /// 플러그인 디렉토리 목록
    /// </summary>
    IReadOnlyList<string> PluginDirectories { get; }

    /// <summary>
    /// 플러그인 로드 이벤트
    /// </summary>
    event EventHandler<PluginEventArgs>? PluginLoaded;

    /// <summary>
    /// 플러그인 언로드 이벤트
    /// </summary>
    event EventHandler<PluginEventArgs>? PluginUnloaded;

    /// <summary>
    /// 플러그인 오류 이벤트
    /// </summary>
    event EventHandler<PluginErrorEventArgs>? PluginError;

    /// <summary>
    /// 플러그인 상태 변경 이벤트
    /// </summary>
    event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;

    /// <summary>
    /// 플러그인 디렉토리 추가
    /// </summary>
    void AddPluginDirectory(string directory);

    /// <summary>
    /// 플러그인 디렉토리 제거
    /// </summary>
    bool RemovePluginDirectory(string directory);

    /// <summary>
    /// 플러그인 검색
    /// </summary>
    Task<IReadOnlyList<PluginInfo>> DiscoverPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 플러그인 로드 (DLL 경로)
    /// </summary>
    Task<IGisPlugin?> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 플러그인 로드 (플러그인 정보)
    /// </summary>
    Task<IGisPlugin?> LoadPluginAsync(PluginInfo pluginInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 여러 플러그인 동시 로드
    /// </summary>
    Task<IReadOnlyList<IGisPlugin>> LoadPluginsAsync(IEnumerable<PluginInfo> pluginInfos, CancellationToken cancellationToken = default);

    /// <summary>
    /// 플러그인 언로드
    /// </summary>
    Task<bool> UnloadPluginAsync(string pluginId);

    /// <summary>
    /// 플러그인 언로드
    /// </summary>
    Task<bool> UnloadPluginAsync(IGisPlugin plugin);

    /// <summary>
    /// 모든 플러그인 언로드
    /// </summary>
    Task UnloadAllPluginsAsync();

    /// <summary>
    /// 플러그인 초기화
    /// </summary>
    Task<bool> InitializePluginAsync(IGisPlugin plugin, IPluginContext context);

    /// <summary>
    /// 플러그인 시작
    /// </summary>
    Task<bool> StartPluginAsync(IGisPlugin plugin);

    /// <summary>
    /// 플러그인 중지
    /// </summary>
    Task<bool> StopPluginAsync(IGisPlugin plugin);

    /// <summary>
    /// ID로 플러그인 찾기
    /// </summary>
    IGisPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// 타입으로 플러그인 찾기
    /// </summary>
    T? GetPlugin<T>() where T : class, IGisPlugin;

    /// <summary>
    /// 타입으로 플러그인들 찾기
    /// </summary>
    IEnumerable<T> GetPlugins<T>() where T : class, IGisPlugin;

    /// <summary>
    /// 플러그인 타입으로 찾기
    /// </summary>
    IEnumerable<IGisPlugin> GetPlugins(PluginType type);

    /// <summary>
    /// 플러그인 활성화 상태 확인
    /// </summary>
    bool IsPluginEnabled(string pluginId);

    /// <summary>
    /// 플러그인 활성화/비활성화
    /// </summary>
    Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled);

    /// <summary>
    /// 플러그인 의존성 확인
    /// </summary>
    bool CheckDependencies(IGisPlugin plugin);

    /// <summary>
    /// 플러그인 의존성 해결
    /// </summary>
    IReadOnlyList<string> ResolveDependencies(IGisPlugin plugin);

    /// <summary>
    /// 플러그인 설정 로드
    /// </summary>
    Task<IPluginSettings?> LoadPluginSettingsAsync(string pluginId);

    /// <summary>
    /// 플러그인 설정 저장
    /// </summary>
    Task<bool> SavePluginSettingsAsync(string pluginId, IPluginSettings settings);

    /// <summary>
    /// 플러그인 메타데이터 가져오기
    /// </summary>
    PluginMetadata? GetPluginMetadata(string pluginId);
}

/// <summary>
/// 플러그인 이벤트 인수
/// </summary>
public class PluginEventArgs : EventArgs
{
    public IGisPlugin Plugin { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public PluginEventArgs(IGisPlugin plugin, string message = "")
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        Message = message;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// 플러그인 오류 이벤트 인수
/// </summary>
public class PluginErrorEventArgs : PluginEventArgs
{
    public Exception Exception { get; }
    public PluginErrorLevel ErrorLevel { get; }

    public PluginErrorEventArgs(IGisPlugin plugin, Exception exception, PluginErrorLevel errorLevel = PluginErrorLevel.Error) 
        : base(plugin, exception.Message)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        ErrorLevel = errorLevel;
    }
}

/// <summary>
/// 플러그인 상태 변경 이벤트 인수
/// </summary>
public class PluginStateChangedEventArgs : PluginEventArgs
{
    public PluginState OldState { get; }
    public PluginState NewState { get; }

    public PluginStateChangedEventArgs(IGisPlugin plugin, PluginState oldState, PluginState newState) 
        : base(plugin)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// 플러그인 오류 레벨
/// </summary>
public enum PluginErrorLevel
{
    /// <summary>정보</summary>
    Info,
    /// <summary>경고</summary>
    Warning,
    /// <summary>오류</summary>
    Error,
    /// <summary>치명적 오류</summary>
    Fatal
}

/// <summary>
/// 플러그인 정보
/// </summary>
public class PluginInfo
{
    public string AssemblyPath { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Version? Version { get; set; }
    public string? Author { get; set; }
    public PluginType? Type { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
}

/// <summary>
/// 플러그인 메타데이터
/// </summary>
public class PluginMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Version Version { get; set; } = new Version(1, 0, 0, 0);
    public string Author { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? License { get; set; }
    public string? Copyright { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> CustomData { get; set; } = new();
    public DateTime? ReleaseDate { get; set; }
    public string? UpdateUrl { get; set; }
    public string? DocumentationUrl { get; set; }
}

/// <summary>
/// 플러그인 로더 인터페이스
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// 플러그인 검색
    /// </summary>
    Task<IReadOnlyList<PluginInfo>> DiscoverPluginsAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// 플러그인 로드
    /// </summary>
    Task<IGisPlugin?> LoadPluginAsync(PluginInfo pluginInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 플러그인 언로드
    /// </summary>
    Task<bool> UnloadPluginAsync(IGisPlugin plugin);

    /// <summary>
    /// 어셈블리에서 플러그인 타입 찾기
    /// </summary>
    Task<IReadOnlyList<Type>> FindPluginTypesAsync(string assemblyPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// 플러그인 호스트 인터페이스
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// 플러그인 매니저
    /// </summary>
    IPluginManager PluginManager { get; }

    /// <summary>
    /// 서비스 컨테이너
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// 플러그인 컨텍스트 생성
    /// </summary>
    IPluginContext CreatePluginContext(IGisPlugin plugin);

    /// <summary>
    /// 플러그인 서비스 등록
    /// </summary>
    void RegisterPluginService<T>(T service) where T : class;

    /// <summary>
    /// 플러그인 서비스 해제
    /// </summary>
    bool UnregisterPluginService<T>() where T : class;
}