using Microsoft.Extensions.DependencyInjection;
using SpatialView.ViewModels;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Infrastructure.Services;
using SpatialView.Views.Dialogs;
using NetTopologySuite;
using System.IO;
using System.Windows.Threading;

namespace SpatialView;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly ServiceProvider _serviceProvider;
    private ILoggingService? _loggingService;
    private static bool _gdalInitialized = false;

    public App()
    {
        // 전역 예외 핸들러 등록
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        
        // GDAL/OGR 초기화 (FileGDB 등 지원)
        SpatialView.Engine.Extensions.GdalSupport.Initialize();
        
        // SpatialView 독립 엔진 초기화 (SharpMap 제거됨)
        InitializeSpatialViewEngine();
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        // LoggingService 참조 가져오기
        _loggingService = _serviceProvider.GetService<ILoggingService>();
    }
    
    /// <summary>
    /// GDAL/OGR 초기화 (FileGDB, GeoPackage 등 지원)
    /// 현재 비활성화 - MaxRev.Gdal.Core 패키지 참조 필요
    /// </summary>
    /*
    private static void InitializeGdal()
    {
        if (_gdalInitialized) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine("GDAL 초기화 시작...");
            
            // 1. PROJ 경로 먼저 설정 (SpatialCheckPro3 방식)
            SetupProjPath();
            
            // 2. MaxRev.Gdal.Core를 사용하여 GDAL 구성
            MaxRev.Gdal.Core.GdalBase.ConfigureAll();
            System.Diagnostics.Debug.WriteLine("GdalBase.ConfigureAll() 완료");
            
            // 3. OGR 드라이버 등록
            OSGeo.GDAL.Gdal.AllRegister();
            OSGeo.OGR.Ogr.RegisterAll();
            
            // 4. 예외 모드 활성화 (오류 디버깅에 중요!)
            OSGeo.GDAL.Gdal.UseExceptions();
            OSGeo.OGR.Ogr.UseExceptions();
            OSGeo.OSR.Osr.UseExceptions();
            
            // 5. UTF-8 인코딩 설정
            OSGeo.GDAL.Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
            OSGeo.GDAL.Gdal.SetConfigOption("SHAPE_ENCODING", "UTF-8");
            
            // 6. FileGDB 최적화 설정
            OSGeo.GDAL.Gdal.SetConfigOption("OPENFILEGDB_USE_SPATIAL_INDEX", "YES");
            OSGeo.GDAL.Gdal.SetConfigOption("FGDB_BULK_LOAD", "YES");
            
            _gdalInitialized = true;
            
            // 등록된 드라이버 확인
            var driverCount = OSGeo.OGR.Ogr.GetDriverCount();
            System.Diagnostics.Debug.WriteLine($"GDAL 초기화 완료: {driverCount}개 OGR 드라이버 등록됨");
            
            // OpenFileGDB 드라이버 확인
            var openFileGdbDriver = OSGeo.OGR.Ogr.GetDriverByName("OpenFileGDB");
            if (openFileGdbDriver != null)
            {
                System.Diagnostics.Debug.WriteLine("OpenFileGDB 드라이버 사용 가능 ✓");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("경고: OpenFileGDB 드라이버가 등록되지 않았습니다!");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GDAL 초기화 오류: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"  Stack: {ex.StackTrace}");
            
            // 오류를 파일에 기록
            try
            {
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpatialView", "Logs");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, $"gdal_error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(logFile, $"GDAL 초기화 오류\n{ex.Message}\n\n{ex.StackTrace}");
            }
            catch { }
        }
    }
    */
    
    /// <summary>
    /// MaxRev.Gdal.Core가 제공하는 PROJ 경로 설정 (현재 비활성화)
    /// </summary>
    /*
    private static void SetupProjPath()
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // MaxRev.Gdal.Core가 제공하는 PROJ 경로들 (우선순위순)
            string[] possibleProjPaths = new[]
            {
                Path.Combine(appDir, "runtimes", "win-x64", "native", "maxrev.gdal.core.libshared"),
                Path.Combine(appDir, "runtimes", "win-x64", "native"),
            };

            foreach (var path in possibleProjPaths)
            {
                if (Directory.Exists(path))
                {
                    var dbPath = Path.Combine(path, "proj.db");
                    if (File.Exists(dbPath))
                    {
                        // 환경변수 설정
                        Environment.SetEnvironmentVariable("PROJ_LIB", path);
                        Environment.SetEnvironmentVariable("PROJ_DATA", path);
                        Environment.SetEnvironmentVariable("PROJ_NETWORK", "OFF");
                        
                        // PATH에 추가
                        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                        if (!currentPath.Contains(path))
                        {
                            Environment.SetEnvironmentVariable("PATH", path + ";" + currentPath);
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"PROJ 경로 설정: {path}");
                        return;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine("PROJ 경로를 찾을 수 없음 - GdalBase.ConfigureAll()이 자동 설정");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PROJ 경로 설정 오류: {ex.Message}");
        }
    }
    */
    
    /// <summary>
    /// GDAL 초기화 상태 확인
    /// </summary>
    public static bool IsGdalInitialized => _gdalInitialized;
    
    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _loggingService?.Error("처리되지 않은 UI 예외", e.Exception, "App");
        
        try
        {
            var logDir = _loggingService?.LogDirectory;
            var dialog = new ErrorDialog(
                "오류가 발생했습니다",
                e.Exception.Message,
                $"예외 유형: {e.Exception.GetType().FullName}\n\n{e.Exception.StackTrace}",
                logDir);
            dialog.ShowDialog();
        }
        catch
        {
            // 다이얼로그 표시 실패 시 기본 메시지박스 사용
            System.Windows.MessageBox.Show(
                $"오류가 발생했습니다:\n\n{e.Exception.Message}",
                "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        
        e.Handled = true;
    }
    
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        _loggingService?.Error("치명적 예외", ex, "App");
        
        System.Windows.MessageBox.Show(
            $"치명적 오류가 발생하여 프로그램을 종료합니다:\n\n{ex?.Message}",
            "치명적 오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
    }
    
    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _loggingService?.Error("비동기 작업 예외", e.Exception, "Task");
        e.SetObserved();
    }

    /// <summary>
    /// SpatialView 독립 엔진 초기화
    /// SharpMap 의존성 완전 제거됨
    /// </summary>
    private static void InitializeSpatialViewEngine()
    {
        try
        {
            // SpatialView.Engine의 GeoAPI 서비스 초기화
            // NetTopologySuite를 기본 지오메트리 모델로 사용
            NtsGeometryServices.Instance = NtsGeometryServices.Instance;
            
            System.Diagnostics.Debug.WriteLine("SpatialView 독립 엔진 초기화 완료 (SharpMap 제거됨)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SpatialView 엔진 초기화 오류: {ex.Message}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// DI Container에 서비스 등록
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // Logging (가장 먼저 등록)
        services.AddSingleton<ILoggingService, LoggingService>();
        
        // Core Factories
        services.AddSingleton<Core.Factories.IMapFactory, Infrastructure.Factories.MapFactory>();
        services.AddSingleton<Core.Factories.ILayerFactory, Infrastructure.Factories.LayerFactory>();
        services.AddSingleton<Core.Factories.IStyleFactory, Infrastructure.Factories.StyleFactory>();
        services.AddSingleton<Core.Factories.ITileSourceFactory, Infrastructure.Factories.TileSourceFactory>();
        
        // Services
        services.AddSingleton<IBaseMapService, BaseMapService>();
        services.AddSingleton<IDataLoaderService, DataLoaderService>();
        services.AddSingleton<IProjectService, ProjectService>();
        // ColorPaletteService is created directly in MainViewModel
        
        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MapViewModel>();
        services.AddSingleton<LayerPanelViewModel>();
        services.AddSingleton<AttributePanelViewModel>();
        
        // Views
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
    
    /// <summary>
    /// ServiceProvider 접근을 위한 정적 속성
    /// </summary>
    public static App CurrentApp => (App)Current;
    public IServiceProvider Services => _serviceProvider;
}
