using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace SpatialView.Engine.Plugins;

/// <summary>
/// 플러그인 로더 구현
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly Dictionary<string, PluginLoadContext> _loadContexts = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// 플러그인 인터페이스 타입
    /// </summary>
    private static readonly Type PluginInterfaceType = typeof(IGisPlugin);

    public async Task<IReadOnlyList<PluginInfo>> DiscoverPluginsAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Directory cannot be empty", nameof(directory));

        if (!Directory.Exists(directory))
            return Array.Empty<PluginInfo>();

        var pluginInfos = new List<PluginInfo>();

        await Task.Run(() =>
        {
            // DLL 파일 검색
            var dllFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories);

            foreach (var dllFile in dllFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var plugins = DiscoverPluginsInAssembly(dllFile);
                    pluginInfos.AddRange(plugins);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error discovering plugins in {dllFile}: {ex.Message}");
                }
            }
        }, cancellationToken);

        return pluginInfos;
    }

    public async Task<IGisPlugin?> LoadPluginAsync(PluginInfo pluginInfo, CancellationToken cancellationToken = default)
    {
        if (pluginInfo == null)
            throw new ArgumentNullException(nameof(pluginInfo));

        if (!pluginInfo.IsValid)
            return null;

        return await Task.Run(() =>
        {
            try
            {
                // 로드 컨텍스트 생성 또는 가져오기
                var loadContext = GetOrCreateLoadContext(pluginInfo.AssemblyPath);

                // 어셈블리 로드
                var assembly = loadContext.LoadFromAssemblyPath(pluginInfo.AssemblyPath);

                // 타입 가져오기
                var pluginType = assembly.GetType(pluginInfo.TypeName);
                if (pluginType == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Type {pluginInfo.TypeName} not found in assembly");
                    return null;
                }

                // 인터페이스 구현 확인
                if (!PluginInterfaceType.IsAssignableFrom(pluginType))
                {
                    System.Diagnostics.Debug.WriteLine($"Type {pluginInfo.TypeName} does not implement IGisPlugin");
                    return null;
                }

                // 인스턴스 생성
                var plugin = Activator.CreateInstance(pluginType) as IGisPlugin;
                if (plugin == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create instance of {pluginInfo.TypeName}");
                    return null;
                }

                return plugin;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading plugin: {ex.Message}");
                return null;
            }
        }, cancellationToken);
    }

    public async Task<bool> UnloadPluginAsync(IGisPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        return await Task.Run(() =>
        {
            try
            {
                var assembly = plugin.GetType().Assembly;
                var assemblyLocation = assembly.Location;

                lock (_lockObject)
                {
                    if (_loadContexts.TryGetValue(assemblyLocation, out var loadContext))
                    {
                        // 로드 컨텍스트 언로드 (참조가 모두 해제되어야 함)
                        loadContext.Unload();
                        _loadContexts.Remove(assemblyLocation);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unloading plugin: {ex.Message}");
                return false;
            }
        });
    }

    public async Task<IReadOnlyList<Type>> FindPluginTypesAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path cannot be empty", nameof(assemblyPath));

        return await Task.Run(() =>
        {
            var pluginTypes = new List<Type>();

            try
            {
                var loadContext = GetOrCreateLoadContext(assemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                foreach (var type in assembly.GetExportedTypes())
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (type.IsClass && !type.IsAbstract && PluginInterfaceType.IsAssignableFrom(type))
                    {
                        pluginTypes.Add(type);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding plugin types in {assemblyPath}: {ex.Message}");
            }

            return (IReadOnlyList<Type>)pluginTypes;
        }, cancellationToken);
    }

    private List<PluginInfo> DiscoverPluginsInAssembly(string assemblyPath)
    {
        var pluginInfos = new List<PluginInfo>();

        try
        {
            // 메타데이터만 로드하여 빠르게 스캔
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new System.Reflection.PortableExecutable.PEReader(stream);
            
            if (!peReader.HasMetadata)
                return pluginInfos;

            var metadataReader = peReader.GetMetadataReader();
            
            // 어셈블리 참조 확인 (IGisPlugin이 포함되어 있는지)
            bool hasPluginReference = false;
            foreach (var handle in metadataReader.AssemblyReferences)
            {
                var reference = metadataReader.GetAssemblyReference(handle);
                var name = metadataReader.GetString(reference.Name);
                if (name.Contains("SpatialView.Engine"))
                {
                    hasPluginReference = true;
                    break;
                }
            }

            if (!hasPluginReference)
                return pluginInfos;

            // 실제 어셈블리 로드하여 플러그인 검색
            var loadContext = GetOrCreateLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && PluginInterfaceType.IsAssignableFrom(type))
                {
                    var pluginInfo = CreatePluginInfo(assemblyPath, type);
                    pluginInfos.Add(pluginInfo);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error discovering plugins in {assemblyPath}: {ex.Message}");
        }

        return pluginInfos;
    }

    private static PluginInfo CreatePluginInfo(string assemblyPath, Type pluginType)
    {
        var info = new PluginInfo
        {
            AssemblyPath = assemblyPath,
            TypeName = pluginType.FullName ?? pluginType.Name,
            IsValid = true
        };

        try
        {
            // 임시 인스턴스 생성하여 메타데이터 추출
            var tempInstance = Activator.CreateInstance(pluginType) as IGisPlugin;
            if (tempInstance != null)
            {
                info.Id = tempInstance.Id;
                info.Name = tempInstance.Name;
                info.Description = tempInstance.Description;
                info.Version = tempInstance.Version;
                info.Author = tempInstance.Author;
                info.Type = tempInstance.Type;
                info.Dependencies = tempInstance.Dependencies.ToList();

                // 리소스 정리
                tempInstance.Dispose();
            }
        }
        catch (Exception ex)
        {
            info.IsValid = false;
            info.ValidationError = $"Failed to create plugin instance: {ex.Message}";
        }

        return info;
    }

    private PluginLoadContext GetOrCreateLoadContext(string assemblyPath)
    {
        lock (_lockObject)
        {
            if (!_loadContexts.TryGetValue(assemblyPath, out var loadContext))
            {
                loadContext = new PluginLoadContext(assemblyPath);
                _loadContexts[assemblyPath] = loadContext;
            }

            return loadContext;
        }
    }
}

/// <summary>
/// 플러그인 로드 컨텍스트
/// 플러그인 어셈블리를 격리된 컨텍스트에서 로드
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 플러그인 디렉토리에서 의존성 해결
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // 기본 컨텍스트로 폴백
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}