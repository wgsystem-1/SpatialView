namespace SpatialView.Core.Services.Interfaces;

/// <summary>
/// 로그 레벨
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// 로깅 서비스 인터페이스
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// 디버그 로그
    /// </summary>
    void Debug(string message, string? category = null);
    
    /// <summary>
    /// 정보 로그
    /// </summary>
    void Info(string message, string? category = null);
    
    /// <summary>
    /// 경고 로그
    /// </summary>
    void Warning(string message, string? category = null);
    
    /// <summary>
    /// 에러 로그
    /// </summary>
    void Error(string message, Exception? exception = null, string? category = null);
    
    /// <summary>
    /// 로그 디렉토리 경로
    /// </summary>
    string LogDirectory { get; }
    
    /// <summary>
    /// 오래된 로그 정리 (기본 7일)
    /// </summary>
    void CleanupOldLogs(int daysToKeep = 7);
}

