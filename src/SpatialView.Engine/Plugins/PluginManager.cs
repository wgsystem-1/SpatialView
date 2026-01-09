using System.Collections.Concurrent;
using System.Reflection;

namespace SpatialView.Engine.Plugins;

/// <summary>
/// 플러그인 관리자 구현
/// </summary>
public class PluginManager : IPluginManager
{
    private readonly List<string> _pluginDirectories = new();
    private readonly ConcurrentDictionary<string, IGisPlugin> _plugins = new();
    private readonly ConcurrentDictionary<string, PluginMetadata> _metadata = new();
    private readonly ConcurrentDictionary<string, IPluginSettings> _settings = new();
    private readonly IPluginLoader _loader;
    private readonly IPluginContext _defaultContext;
    private readonly object _lockObject = new();
    private bool _disposed;

    public IReadOnlyList<IGisPlugin> Plugins => _plugins.Values.ToList();
    public IReadOnlyList<string> PluginDirectories => _pluginDirectories.AsReadOnly();

    public event EventHandler<PluginEventArgs>? PluginLoaded;
    public event EventHandler<PluginEventArgs>? PluginUnloaded;
    public event EventHandler<PluginErrorEventArgs>? PluginError;
    public event EventHandler<PluginStateChangedEventArgs>? PluginStateChanged;

    public PluginManager(IPluginLoader loader, IPluginContext defaultContext)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _defaultContext = defaultContext ?? throw new ArgumentNullException(nameof(defaultContext));

        // 기본 플러그인 디렉토리 추가
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        AddPluginDirectory(Path.Combine(baseDirectory, "Plugins"));
    }

    public void AddPluginDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be empty", nameof(directory));

        lock (_lockObject)
        {
            if (!_pluginDirectories.Contains(directory))
            {
                _pluginDirectories.Add(directory);
                
                // 디렉토리가 없으면 생성
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
    }

    public bool RemovePluginDirectory(string directory)
    {
        lock (_lockObject)
        {
            return _pluginDirectories.Remove(directory);
        }
    }

    public async Task<IReadOnlyList<PluginInfo>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        var allPlugins = new List<PluginInfo>();

        foreach (var directory in _pluginDirectories.ToList())
        {
            if (!Directory.Exists(directory))
                continue;

            try
            {
                var plugins = await _loader.DiscoverPluginsAsync(directory, cancellationToken);
                allPlugins.AddRange(plugins);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error discovering plugins in {directory}: {ex.Message}");
            }
        }

        return allPlugins;
    }

    public async Task<IGisPlugin?> LoadPluginAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // 어셈블리에서 플러그인 타입 찾기
            var types = await _loader.FindPluginTypesAsync(assemblyPath, cancellationToken);
            if (types.Count == 0)
                return null;

            // 첫 번째 플러그인 타입 로드
            var pluginInfo = new PluginInfo
            {
                AssemblyPath = assemblyPath,
                TypeName = types[0].FullName ?? string.Empty,
                IsValid = true
            };

            return await LoadPluginAsync(pluginInfo, cancellationToken);
        }
        catch (Exception ex)
        {
            OnPluginError(null!, ex, PluginErrorLevel.Error);
            return null;
        }
    }

    public async Task<IGisPlugin?> LoadPluginAsync(PluginInfo pluginInfo, CancellationToken cancellationToken = default)
    {
        if (pluginInfo == null)
            throw new ArgumentNullException(nameof(pluginInfo));

        if (!pluginInfo.IsValid)
        {
            System.Diagnostics.Debug.WriteLine($"Invalid plugin info: {pluginInfo.ValidationError}");
            return null;
        }

        try
        {
            // 플러그인 로드
            var plugin = await _loader.LoadPluginAsync(pluginInfo, cancellationToken);
            if (plugin == null)
                return null;

            // 이미 로드된 플러그인인지 확인
            if (_plugins.ContainsKey(plugin.Id))
            {
                System.Diagnostics.Debug.WriteLine($"Plugin {plugin.Id} is already loaded");
                return _plugins[plugin.Id];
            }

            // 의존성 확인
            if (!CheckDependencies(plugin))
            {
                await _loader.UnloadPluginAsync(plugin);
                return null;
            }

            // 플러그인 등록
            if (!_plugins.TryAdd(plugin.Id, plugin))
            {
                await _loader.UnloadPluginAsync(plugin);
                return null;
            }

            // 메타데이터 저장
            SaveMetadata(plugin);

            // 설정 로드
            var settings = await LoadPluginSettingsAsync(plugin.Id);
            if (settings != null)
            {
                plugin.ApplySettings(settings);
            }

            // 이벤트 발생
            OnPluginLoaded(plugin);

            return plugin;
        }
        catch (Exception ex)
        {
            OnPluginError(null!, ex, PluginErrorLevel.Error);
            return null;
        }
    }

    public async Task<IReadOnlyList<IGisPlugin>> LoadPluginsAsync(IEnumerable<PluginInfo> pluginInfos, CancellationToken cancellationToken = default)
    {
        var loadedPlugins = new List<IGisPlugin>();
        var tasks = pluginInfos.Select(async info =>
        {
            var plugin = await LoadPluginAsync(info, cancellationToken);
            if (plugin != null)
            {
                lock (loadedPlugins)
                {
                    loadedPlugins.Add(plugin);
                }
            }
        });

        await Task.WhenAll(tasks);
        return loadedPlugins;
    }

    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin == null)
            return false;

        return await UnloadPluginAsync(plugin);
    }

    public async Task<bool> UnloadPluginAsync(IGisPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        try
        {
            // 플러그인이 시작된 상태면 먼저 중지
            if (plugin.State == PluginState.Started)
            {
                await StopPluginAsync(plugin);
            }

            // 플러그인 제거
            if (!_plugins.TryRemove(plugin.Id, out _))
                return false;

            // 설정 저장
            var settings = plugin.GetSettings();
            if (settings != null)
            {
                await SavePluginSettingsAsync(plugin.Id, settings);
            }

            // 플러그인 언로드
            await _loader.UnloadPluginAsync(plugin);

            // 리소스 해제
            plugin.Dispose();

            // 이벤트 발생
            OnPluginUnloaded(plugin);

            return true;
        }
        catch (Exception ex)
        {
            OnPluginError(plugin, ex, PluginErrorLevel.Error);
            return false;
        }
    }

    public async Task UnloadAllPluginsAsync()
    {
        var plugins = _plugins.Values.ToList();
        var tasks = plugins.Select(UnloadPluginAsync);
        await Task.WhenAll(tasks);
    }

    public async Task<bool> InitializePluginAsync(IGisPlugin plugin, IPluginContext context)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        try
        {
            var oldState = plugin.State;
            OnPluginStateChanged(plugin, oldState, PluginState.Initializing);

            var result = await plugin.InitializeAsync(context ?? _defaultContext);
            
            var newState = result ? PluginState.Initialized : PluginState.Error;
            OnPluginStateChanged(plugin, PluginState.Initializing, newState);

            return result;
        }
        catch (Exception ex)
        {
            OnPluginError(plugin, ex, PluginErrorLevel.Error);
            OnPluginStateChanged(plugin, plugin.State, PluginState.Error);
            return false;
        }
    }

    public async Task<bool> StartPluginAsync(IGisPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (plugin.State != PluginState.Initialized && plugin.State != PluginState.Stopped)
        {
            System.Diagnostics.Debug.WriteLine($"Cannot start plugin {plugin.Id} in state {plugin.State}");
            return false;
        }

        try
        {
            var oldState = plugin.State;
            await plugin.StartAsync();
            OnPluginStateChanged(plugin, oldState, PluginState.Started);
            return true;
        }
        catch (Exception ex)
        {
            OnPluginError(plugin, ex, PluginErrorLevel.Error);
            OnPluginStateChanged(plugin, plugin.State, PluginState.Error);
            return false;
        }
    }

    public async Task<bool> StopPluginAsync(IGisPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        if (plugin.State != PluginState.Started)
        {
            System.Diagnostics.Debug.WriteLine($"Cannot stop plugin {plugin.Id} in state {plugin.State}");
            return false;
        }

        try
        {
            var oldState = plugin.State;
            await plugin.StopAsync();
            OnPluginStateChanged(plugin, oldState, PluginState.Stopped);
            return true;
        }
        catch (Exception ex)
        {
            OnPluginError(plugin, ex, PluginErrorLevel.Error);
            return false;
        }
    }

    public IGisPlugin? GetPlugin(string pluginId)
    {
        _plugins.TryGetValue(pluginId, out var plugin);
        return plugin;
    }

    public T? GetPlugin<T>() where T : class, IGisPlugin
    {
        return _plugins.Values.OfType<T>().FirstOrDefault();
    }

    public IEnumerable<T> GetPlugins<T>() where T : class, IGisPlugin
    {
        return _plugins.Values.OfType<T>();
    }

    public IEnumerable<IGisPlugin> GetPlugins(PluginType type)
    {
        return _plugins.Values.Where(p => (p.Type & type) == type);
    }

    public bool IsPluginEnabled(string pluginId)
    {
        var plugin = GetPlugin(pluginId);
        return plugin != null && plugin.State != PluginState.Disabled;
    }

    public async Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled)
    {
        var plugin = GetPlugin(pluginId);
        if (plugin == null)
            return false;

        if (enabled)
        {
            if (plugin.State == PluginState.Disabled)
            {
                // 플러그인 활성화
                OnPluginStateChanged(plugin, PluginState.Disabled, PluginState.NotInitialized);
                return await InitializePluginAsync(plugin, _defaultContext);
            }
        }
        else
        {
            if (plugin.State != PluginState.Disabled)
            {
                // 플러그인 비활성화
                if (plugin.State == PluginState.Started)
                {
                    await StopPluginAsync(plugin);
                }
                OnPluginStateChanged(plugin, plugin.State, PluginState.Disabled);
            }
        }

        return true;
    }

    public bool CheckDependencies(IGisPlugin plugin)
    {
        if (plugin.Dependencies == null || plugin.Dependencies.Count == 0)
            return true;

        foreach (var dependency in plugin.Dependencies)
        {
            if (!_plugins.ContainsKey(dependency))
            {
                System.Diagnostics.Debug.WriteLine($"Plugin {plugin.Id} has unmet dependency: {dependency}");
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<string> ResolveDependencies(IGisPlugin plugin)
    {
        var resolved = new List<string>();
        var visited = new HashSet<string>();
        
        ResolveDependenciesRecursive(plugin.Id, plugin.Dependencies, resolved, visited);
        
        return resolved;
    }

    private void ResolveDependenciesRecursive(string pluginId, IReadOnlyList<string> dependencies, List<string> resolved, HashSet<string> visited)
    {
        if (visited.Contains(pluginId))
            return;

        visited.Add(pluginId);

        foreach (var dependency in dependencies)
        {
            var depPlugin = GetPlugin(dependency);
            if (depPlugin != null)
            {
                ResolveDependenciesRecursive(dependency, depPlugin.Dependencies, resolved, visited);
            }
        }

        if (!resolved.Contains(pluginId))
        {
            resolved.Add(pluginId);
        }
    }

    public async Task<IPluginSettings?> LoadPluginSettingsAsync(string pluginId)
    {
        // 캐시에서 확인
        if (_settings.TryGetValue(pluginId, out var cachedSettings))
            return cachedSettings;

        try
        {
            // 설정 파일 경로
            var settingsPath = GetPluginSettingsPath(pluginId);
            if (!File.Exists(settingsPath))
                return null;

            // JSON 읽기
            var json = await File.ReadAllTextAsync(settingsPath);
            
            // 플러그인 가져오기
            var plugin = GetPlugin(pluginId);
            if (plugin == null)
                return null;

            // 설정 객체 생성 및 로드
            var settings = plugin.GetSettings() ?? CreateDefaultSettings(plugin);
            settings.FromJson(json);

            // 유효성 검사
            if (!settings.Validate(out var errorMessage))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid plugin settings for {pluginId}: {errorMessage}");
                return null;
            }

            // 캐시에 저장
            _settings.TryAdd(pluginId, settings);
            
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading plugin settings for {pluginId}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SavePluginSettingsAsync(string pluginId, IPluginSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        try
        {
            // 유효성 검사
            if (!settings.Validate(out var errorMessage))
            {
                System.Diagnostics.Debug.WriteLine($"Invalid plugin settings for {pluginId}: {errorMessage}");
                return false;
            }

            // 설정 파일 경로
            var settingsPath = GetPluginSettingsPath(pluginId);
            var directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // JSON으로 저장
            var json = settings.ToJson();
            await File.WriteAllTextAsync(settingsPath, json);

            // 캐시 업데이트
            _settings.AddOrUpdate(pluginId, settings, (_, _) => settings);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving plugin settings for {pluginId}: {ex.Message}");
            return false;
        }
    }

    public PluginMetadata? GetPluginMetadata(string pluginId)
    {
        _metadata.TryGetValue(pluginId, out var metadata);
        return metadata;
    }

    private void SaveMetadata(IGisPlugin plugin)
    {
        var metadata = new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Description = plugin.Description,
            Version = plugin.Version,
            Author = plugin.Author,
            ReleaseDate = DateTime.Now
        };

        _metadata.AddOrUpdate(plugin.Id, metadata, (_, _) => metadata);
    }

    private string GetPluginSettingsPath(string pluginId)
    {
        var settingsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginSettings");
        return Path.Combine(settingsDirectory, $"{pluginId}.json");
    }

    private static IPluginSettings CreateDefaultSettings(IGisPlugin plugin)
    {
        return new DefaultPluginSettings();
    }

    #region Event Handlers

    protected virtual void OnPluginLoaded(IGisPlugin plugin)
    {
        PluginLoaded?.Invoke(this, new PluginEventArgs(plugin, $"Plugin {plugin.Name} loaded"));
    }

    protected virtual void OnPluginUnloaded(IGisPlugin plugin)
    {
        PluginUnloaded?.Invoke(this, new PluginEventArgs(plugin, $"Plugin {plugin.Name} unloaded"));
    }

    protected virtual void OnPluginError(IGisPlugin plugin, Exception exception, PluginErrorLevel errorLevel)
    {
        PluginError?.Invoke(this, new PluginErrorEventArgs(plugin, exception, errorLevel));
    }

    protected virtual void OnPluginStateChanged(IGisPlugin plugin, PluginState oldState, PluginState newState)
    {
        PluginStateChanged?.Invoke(this, new PluginStateChangedEventArgs(plugin, oldState, newState));
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            UnloadAllPluginsAsync().Wait(TimeSpan.FromSeconds(30));
        }
        catch
        {
            // 무시
        }

        _plugins.Clear();
        _metadata.Clear();
        _settings.Clear();
        _disposed = true;
    }
}

/// <summary>
/// 기본 플러그인 설정
/// </summary>
internal class DefaultPluginSettings : IPluginSettings
{
    private Dictionary<string, object> _settings = new();

    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(_settings);
    }

    public void FromJson(string json)
    {
        _settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
    }

    public void ResetToDefaults()
    {
        _settings.Clear();
    }

    public bool Validate(out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }
}