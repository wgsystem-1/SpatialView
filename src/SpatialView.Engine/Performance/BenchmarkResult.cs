using System;

namespace SpatialView.Engine.Performance;

/// <summary>
/// 벤치마크 결과
/// </summary>
public class BenchmarkResult
{
    /// <summary>
    /// 테스트 이름
    /// </summary>
    public string TestName { get; set; } = "";
    
    /// <summary>
    /// 실행 시간
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// 처리된 항목 수
    /// </summary>
    public int ItemCount { get; set; }
    
    /// <summary>
    /// 초당 처리량
    /// </summary>
    public double Throughput => Duration.TotalSeconds > 0 ? ItemCount / Duration.TotalSeconds : 0;
    
    /// <summary>
    /// 메모리 사용량 (MB)
    /// </summary>
    public double MemoryUsageMB { get; set; }
    
    /// <summary>
    /// 성공 여부
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// 오류 메시지
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 추가 메트릭
    /// </summary>
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    
    /// <summary>
    /// 요약 문자열
    /// </summary>
    public override string ToString()
    {
        return $"{TestName}: {Duration.TotalMilliseconds:F2}ms, {ItemCount} items, {Throughput:F2} items/sec";
    }
}