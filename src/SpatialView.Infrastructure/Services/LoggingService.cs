using System.Collections.Concurrent;
using System.Text;
using SpatialView.Core.Services.Interfaces;

namespace SpatialView.Infrastructure.Services;

/// <summary>
/// 파일 기반 로깅 서비스
/// </summary>
public class LoggingService : ILoggingService, IDisposable
{
    private readonly string _logDirectory;
    private readonly ConcurrentQueue<string> _logQueue = new();
    private readonly Timer _flushTimer;
    private readonly object _writeLock = new();
    private bool _disposed;
    
    public string LogDirectory => _logDirectory;
    
    public LoggingService()
    {
        // %LOCALAPPDATA%\SpatialView\Logs\
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDirectory = Path.Combine(localAppData, "SpatialView", "Logs");
        
        // 디렉토리 생성
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
        
        // 1초마다 로그 플러시
        _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        
        // 시작 시 오래된 로그 정리
        CleanupOldLogs();
        
        Info("SpatialView 시작", "App");
    }
    
    public void Debug(string message, string? category = null)
    {
        WriteLog(LogLevel.Debug, message, category);
    }
    
    public void Info(string message, string? category = null)
    {
        WriteLog(LogLevel.Info, message, category);
    }
    
    public void Warning(string message, string? category = null)
    {
        WriteLog(LogLevel.Warning, message, category);
    }
    
    public void Error(string message, Exception? exception = null, string? category = null)
    {
        var fullMessage = message;
        if (exception != null)
        {
            fullMessage += $"\n  Exception: {exception.GetType().Name}: {exception.Message}";
            fullMessage += $"\n  StackTrace: {exception.StackTrace}";
            
            if (exception.InnerException != null)
            {
                fullMessage += $"\n  InnerException: {exception.InnerException.Message}";
            }
        }
        
        WriteLog(LogLevel.Error, fullMessage, category);
    }
    
    public void CleanupOldLogs(int daysToKeep = 7)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var logFiles = Directory.GetFiles(_logDirectory, "*.log");
            
            foreach (var file in logFiles)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    try
                    {
                        File.Delete(file);
                        System.Diagnostics.Debug.WriteLine($"오래된 로그 삭제: {fileInfo.Name}");
                    }
                    catch
                    {
                        // 삭제 실패해도 무시
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"로그 정리 오류: {ex.Message}");
        }
    }
    
    private void WriteLog(LogLevel level, string message, string? category)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpper().PadRight(7);
        var categoryStr = string.IsNullOrEmpty(category) ? "" : $"[{category}] ";
        
        var logLine = $"{timestamp} [{levelStr}] {categoryStr}{message}";
        
        _logQueue.Enqueue(logLine);
        
        // 콘솔에도 출력 (디버그용)
        System.Diagnostics.Debug.WriteLine(logLine);
    }
    
    private void FlushLogs(object? state)
    {
        if (_logQueue.IsEmpty) return;
        
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var logFilePath = Path.Combine(_logDirectory, $"spatialview_{today}.log");
        
        var sb = new StringBuilder();
        while (_logQueue.TryDequeue(out var line))
        {
            sb.AppendLine(line);
        }
        
        if (sb.Length > 0)
        {
            lock (_writeLock)
            {
                try
                {
                    File.AppendAllText(logFilePath, sb.ToString(), Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"로그 쓰기 오류: {ex.Message}");
                }
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Info("SpatialView 종료", "App");
        
        _flushTimer.Dispose();
        
        // 남은 로그 즉시 플러시
        FlushLogs(null);
    }
}

