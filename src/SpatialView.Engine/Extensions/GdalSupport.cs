using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SpatialView.Engine.Extensions;

/// <summary>
/// GDAL 지원 헬퍼 (GDAL/OGR 초기화 및 상태 관리)
/// </summary>
public static partial class GdalSupport
{
    private static readonly object _lock = new();
    private static bool _initialized;
    private static string? _initError;
    
    /// <summary>
    /// GDAL이 사용 가능한지 확인
    /// </summary>
    public static bool IsAvailable => _initialized && string.IsNullOrEmpty(_initError);

    /// <summary>
    /// 지원되는 GDAL 벡터 포맷들
    /// </summary>
    public static readonly string[] SupportedVectorFormats = new[]
    {
        ".shp",     // ESRI Shapefile (자체 구현으로 지원)
        ".geojson", ".json",  // GeoJSON (자체 구현으로 지원)
        ".kml",     // KML (GDAL 필요)
        ".gpx",     // GPX (GDAL 필요)
        ".gml",     // GML (GDAL 필요)
        ".sqlite", ".gpkg",   // SQLite/GeoPackage (자체 구현으로 부분 지원)
        ".csv",     // CSV (GDAL 필요)
        ".tab", ".mif",       // MapInfo (GDAL 필요)
        ".dgn",     // MicroStation (GDAL 필요)
        ".dxf"      // AutoCAD (GDAL 필요)
    };

    /// <summary>
    /// 지원되는 GDAL 래스터 포맷들
    /// </summary>
    public static readonly string[] SupportedRasterFormats = new[]
    {
        ".tif", ".tiff",    // GeoTIFF (GDAL 필요)
        ".png", ".jpg", ".jpeg", ".bmp", ".gif",  // 이미지 (GDAL 필요)
        ".jp2",             // JPEG 2000 (GDAL 필요)
        ".ecw",             // ECW (GDAL 필요)
        ".sid",             // MrSID (GDAL 필요)
        ".img",             // Erdas Imagine (GDAL 필요)
        ".nc",              // NetCDF (GDAL 필요)
        ".grib",            // GRIB (GDAL 필요)
        ".asc"              // ASCII Grid (GDAL 필요)
    };

    /// <summary>
    /// 파일이 GDAL이 필요한 포맷인지 확인
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>GDAL 필요 여부</returns>
    public static bool RequiresGdal(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // 자체 구현으로 지원되는 포맷들 (GDAL 불필요)
        var nativeSupported = new[] { ".shp", ".geojson", ".json", ".sqlite" };
        
        if (nativeSupported.Contains(extension))
            return false;

        // GDAL이 필요한 포맷들
        return SupportedVectorFormats.Contains(extension) || 
               SupportedRasterFormats.Contains(extension);
    }

    /// <summary>
    /// GDAL 초기화 (추후 구현)
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public static bool Initialize()
    {
        lock (_lock)
        {
            if (_initialized && string.IsNullOrEmpty(_initError))
                return true;
            
            try
            {
                System.Diagnostics.Debug.WriteLine("GDAL 초기화 시작...");
                
                // 실행 경로 기준 네이티브/데이터 경로 계산
                var baseDir = AppContext.BaseDirectory;
                var nativeDir = Path.Combine(baseDir, "win-x64");
                var gdalDataDir = Path.Combine(nativeDir, "gdal-data");
                var gdalDataDirNested = Path.Combine(gdalDataDir, "gdal-data"); // robocopy로 중첩된 경우
                var projDir = Path.Combine(nativeDir, "projlib");
                var gdalDllPath = Path.Combine(nativeDir, "gdal.dll");
                var gdalWrapPath = Path.Combine(nativeDir, "gdal_wrap.dll");
                
                // 환경 변수 설정 (경로가 존재할 때만 적용)
                SafePrependEnvPath("PATH", nativeDir);
                
                // GDAL_DATA 우선순위: 중첩 gdal-data > gdal-data > nativeDir
                if (Directory.Exists(gdalDataDirNested))
                    SafeSetEnv("GDAL_DATA", gdalDataDirNested);
                else if (Directory.Exists(gdalDataDir))
                    SafeSetEnv("GDAL_DATA", gdalDataDir);
                else
                    SafeSetEnv("GDAL_DATA", nativeDir);
                
                // PROJ_LIB: projlib 있으면 그쪽, 없으면 proj.db가 있는 nativeDir
                if (Directory.Exists(projDir))
                    SafeSetEnv("PROJ_LIB", projDir);
                else if (File.Exists(Path.Combine(nativeDir, "proj.db")))
                    SafeSetEnv("PROJ_LIB", nativeDir);

                // 네이티브 DLL 경로를 우선 검색하도록 설정
                SafeSetDllDirectory(nativeDir);

                // 네이티브 DLL을 절대경로로 선로딩하여 P/Invoke 경로 문제 방지
                SafeLoadNative(gdalDllPath);
                SafeLoadNative(gdalWrapPath);
                SafeLoadNative(Path.Combine(nativeDir, "ogr.dll"));
                SafeLoadNative(Path.Combine(nativeDir, "ogr_wrap.dll"));
                SafeLoadNative(Path.Combine(nativeDir, "osr.dll"));
                SafeLoadNative(Path.Combine(nativeDir, "osr_wrap.dll"));
                SafeLoadNative(Path.Combine(nativeDir, "FileGDBAPI.dll"));
                
                // MaxRev.Gdal.Core가 제공하는 경로/네이티브 구성
                GdalBase.ConfigureAll();
                
                // OGR/GDAL 드라이버 등록
                Gdal.AllRegister();
                Ogr.RegisterAll();
                
                // 예외 모드 활성화 (디버깅 용이)
                Gdal.UseExceptions();
                Ogr.UseExceptions();
                Osr.UseExceptions();
                
                // 공통 옵션
                Gdal.SetConfigOption("GDAL_FILENAME_IS_UTF8", "YES");
                Gdal.SetConfigOption("SHAPE_ENCODING", "UTF-8");
                Gdal.SetConfigOption("OPENFILEGDB_USE_SPATIAL_INDEX", "YES");
                Gdal.SetConfigOption("FGDB_BULK_LOAD", "YES");
                
                _initError = null;
                _initialized = true;
                
                System.Diagnostics.Debug.WriteLine($"GDAL 초기화 완료 - OGR 드라이버 수: {Ogr.GetDriverCount()}");
                return true;
            }
            catch (Exception ex)
            {
                _initError = ex.Message;
                _initialized = false;
                System.Diagnostics.Debug.WriteLine($"GDAL 초기화 실패: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// GDAL 정리 (추후 구현)
    /// </summary>
    public static void Cleanup()
    {
        lock (_lock)
        {
            if (!_initialized) return;
            // GDAL C# 바인딩은 명시적 정리가 필요하지 않지만,
            // 플래그를 초기화하여 재초기화를 허용한다.
            _initialized = false;
        }
    }

    /// <summary>
    /// GDAL 버전 정보 (추후 구현)
    /// </summary>
    public static string GetVersion()
    {
        if (!IsAvailable)
        {
            return _initError != null 
                ? $"GDAL unavailable: {_initError}" 
                : "GDAL not initialized";
        }
        
        try
        {
            return Gdal.VersionInfo("RELEASE_NAME") ?? "GDAL version unknown";
        }
        catch
        {
            return "GDAL version unknown";
        }
    }
    
    /// <summary>
    /// 마지막 초기화 오류 메시지
    /// </summary>
    public static string? GetLastError() => _initError;

    private static void SafeSetEnv(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;
        try { Environment.SetEnvironmentVariable(key, value); }
        catch { }
    }

    private static void SafePrependEnvPath(string key, string? path)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var current = Environment.GetEnvironmentVariable(key) ?? string.Empty;
            if (!current.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                        .Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
            {
                var newVal = string.IsNullOrEmpty(current) ? path : $"{path}{Path.PathSeparator}{current}";
                Environment.SetEnvironmentVariable(key, newVal);
            }
        }
        catch { }
    }

    private static void SafeLoadNative(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            return;
        try
        {
            NativeLibrary.Load(fullPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GDAL] Native load failed: {fullPath} - {ex.Message}");
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetDllDirectory(string lpPathName);

    private static void SafeSetDllDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        try
        {
            SetDllDirectory(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GDAL] SetDllDirectory failed: {path} - {ex.Message}");
        }
    }
}