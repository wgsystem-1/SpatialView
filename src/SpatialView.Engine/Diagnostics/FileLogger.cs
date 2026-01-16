using System;
using System.IO;

namespace SpatialView.Engine.Diagnostics;

/// <summary>
/// 간단한 파일 로거 (Release 모드에서도 작동)
/// </summary>
public static class FileLogger
{
    private static readonly object _lock = new object();
    private static string? _logFilePath;
    private static bool _initialized = false;

    public static bool Initialize()
    {
        if (_initialized) return _logFilePath != null;

        try
        {
            var baseLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // LOCALAPPDATA가 비어있을 수 있는 환경을 대비한 폴백
            if (string.IsNullOrWhiteSpace(baseLocal))
            {
                baseLocal = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
            }

            var logDir = Path.Combine(baseLocal, "SpatialView", "Logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"spatialview_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // 무한 재귀 방지: Log() 호출 전에 _initialized를 true로 설정
            _initialized = true;

            Log($"=== SpatialView Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            Log($"Log file: {_logFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileLogger 초기화 실패: {ex}");
            _initialized = false; // 초기화 실패 시 다시 시도 가능하도록
            _logFilePath = null;
            return false;
        }
    }

    public static void Log(string message)
    {
        if (!_initialized) Initialize();
        if (_logFilePath == null) return;

        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
        }
        catch
        {
            // 로깅 실패는 무시
        }
    }

    public static string? GetLogFilePath()
    {
        if (!_initialized) Initialize();
        return _logFilePath;
    }
}
