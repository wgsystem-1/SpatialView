using System.Diagnostics;
using SpatialView.Engine.Events;

namespace SpatialView.Engine.Performance;

/// <summary>
/// 실시간 성능 모니터링 시스템
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly Timer _monitoringTimer;
    private readonly PerformanceCounterCollection _counters;
    private readonly object _lock = new();
    private bool _disposed;
    
    /// <summary>
    /// 성능 임계값 초과 이벤트
    /// </summary>
    public event EventHandler<PerformanceAlertEventArgs>? PerformanceAlert;
    
    public PerformanceMonitor(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _counters = new PerformanceCounterCollection();
        
        // 1초마다 모니터링
        _monitoringTimer = new Timer(MonitorPerformance, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        
        // 이벤트 구독
        SubscribeToEvents();
    }
    
    /// <summary>
    /// 작업 시작 기록
    /// </summary>
    public IDisposable BeginOperation(string operationName)
    {
        return new OperationScope(this, operationName);
    }
    
    /// <summary>
    /// 현재 성능 통계 가져오기
    /// </summary>
    public PerformanceStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new PerformanceStatistics
            {
                OperationCounters = _counters.GetSnapshot(),
                SystemMetrics = GetSystemMetrics(),
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// 성능 임계값 설정
    /// </summary>
    public void SetThreshold(string metricName, double threshold)
    {
        _counters.SetThreshold(metricName, threshold);
    }
    
    private void SubscribeToEvents()
    {
        // 렌더링 이벤트 구독
        _eventBus.Subscribe<RenderingStartedEvent>(OnRenderingStarted);
        _eventBus.Subscribe<RenderingCompletedEvent>(OnRenderingCompleted);
        
        // 데이터 로드 이벤트 구독
        _eventBus.Subscribe<DataLoadStartedEvent>(OnDataLoadStarted);
        _eventBus.Subscribe<DataLoadCompletedEvent>(OnDataLoadCompleted);
    }
    
    private void OnRenderingStarted(RenderingStartedEvent e)
    {
        _counters.StartOperation($"Rendering_{e.LayerId}");
    }
    
    private void OnRenderingCompleted(RenderingCompletedEvent e)
    {
        _counters.EndOperation($"Rendering_{e.LayerId}");
        _counters.IncrementCounter("RenderedFeatures", e.FeatureCount);
    }
    
    private void OnDataLoadStarted(DataLoadStartedEvent e)
    {
        _counters.StartOperation($"DataLoad_{e.SourceName}");
    }
    
    private void OnDataLoadCompleted(DataLoadCompletedEvent e)
    {
        _counters.EndOperation($"DataLoad_{e.SourceName}");
        _counters.IncrementCounter("LoadedFeatures", e.FeatureCount);
    }
    
    private void MonitorPerformance(object? state)
    {
        if (_disposed) return;
        
        try
        {
            var stats = GetStatistics();
            
            // 임계값 검사
            foreach (var counter in stats.OperationCounters)
            {
                if (_counters.IsThresholdExceeded(counter.Key, counter.Value.AverageTime))
                {
                    PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs
                    {
                        MetricName = counter.Key,
                        CurrentValue = counter.Value.AverageTime,
                        Threshold = _counters.GetThreshold(counter.Key),
                        AlertType = PerformanceAlertType.SlowOperation
                    });
                }
            }
            
            // 메모리 사용량 검사
            if (stats.SystemMetrics.MemoryUsageMB > 1000) // 1GB 초과
            {
                PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs
                {
                    MetricName = "Memory",
                    CurrentValue = stats.SystemMetrics.MemoryUsageMB,
                    Threshold = 1000,
                    AlertType = PerformanceAlertType.HighMemoryUsage
                });
            }
            
            // CPU 사용량 검사
            if (stats.SystemMetrics.CpuUsagePercent > 80) // 80% 초과
            {
                PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs
                {
                    MetricName = "CPU",
                    CurrentValue = stats.SystemMetrics.CpuUsagePercent,
                    Threshold = 80,
                    AlertType = PerformanceAlertType.HighCpuUsage
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Performance monitoring error: {ex.Message}");
        }
    }
    
    private SystemMetrics GetSystemMetrics()
    {
        var process = Process.GetCurrentProcess();
        
        return new SystemMetrics
        {
            MemoryUsageMB = process.WorkingSet64 / (1024.0 * 1024.0),
            CpuUsagePercent = GetCpuUsage(),
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount
        };
    }
    
    private double GetCpuUsage()
    {
        // 간단한 CPU 사용량 추정
        // 실제 구현에서는 PerformanceCounter 사용
        return Environment.ProcessorCount > 0 ? 
            (double)Environment.TickCount / Environment.ProcessorCount / 1000 % 100 : 0;
    }
    
    internal void RecordOperation(string operationName, double elapsedMs)
    {
        lock (_lock)
        {
            _counters.RecordOperation(operationName, elapsedMs);
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _monitoringTimer?.Dispose();
    }
    
    /// <summary>
    /// 작업 범위
    /// </summary>
    private class OperationScope : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        
        public OperationScope(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.RecordOperation(_operationName, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}

/// <summary>
/// 성능 카운터 컬렉션
/// </summary>
internal class PerformanceCounterCollection
{
    private readonly Dictionary<string, OperationMetrics> _operations = new();
    private readonly Dictionary<string, long> _counters = new();
    private readonly Dictionary<string, double> _thresholds = new();
    private readonly Dictionary<string, Stopwatch> _activeOperations = new();
    
    public void StartOperation(string name)
    {
        _activeOperations[name] = Stopwatch.StartNew();
    }
    
    public void EndOperation(string name)
    {
        if (_activeOperations.TryGetValue(name, out var stopwatch))
        {
            stopwatch.Stop();
            RecordOperation(name, stopwatch.Elapsed.TotalMilliseconds);
            _activeOperations.Remove(name);
        }
    }
    
    public void RecordOperation(string name, double elapsedMs)
    {
        if (!_operations.ContainsKey(name))
        {
            _operations[name] = new OperationMetrics { Name = name };
        }
        
        _operations[name].Record(elapsedMs);
    }
    
    public void IncrementCounter(string name, long value = 1)
    {
        if (!_counters.ContainsKey(name))
        {
            _counters[name] = 0;
        }
        
        _counters[name] += value;
    }
    
    public void SetThreshold(string metricName, double threshold)
    {
        _thresholds[metricName] = threshold;
    }
    
    public double GetThreshold(string metricName)
    {
        return _thresholds.TryGetValue(metricName, out var threshold) ? threshold : double.MaxValue;
    }
    
    public bool IsThresholdExceeded(string metricName, double value)
    {
        return _thresholds.TryGetValue(metricName, out var threshold) && value > threshold;
    }
    
    public Dictionary<string, OperationMetrics> GetSnapshot()
    {
        return new Dictionary<string, OperationMetrics>(_operations);
    }
}

/// <summary>
/// 작업 메트릭스
/// </summary>
public class OperationMetrics
{
    public string Name { get; set; } = "";
    public int Count { get; private set; }
    public double TotalTime { get; private set; }
    public double MinTime { get; private set; } = double.MaxValue;
    public double MaxTime { get; private set; }
    public double AverageTime => Count > 0 ? TotalTime / Count : 0;
    
    public void Record(double elapsedMs)
    {
        Count++;
        TotalTime += elapsedMs;
        MinTime = Math.Min(MinTime, elapsedMs);
        MaxTime = Math.Max(MaxTime, elapsedMs);
    }
}

/// <summary>
/// 성능 통계
/// </summary>
public class PerformanceStatistics
{
    public Dictionary<string, OperationMetrics> OperationCounters { get; set; } = new();
    public SystemMetrics SystemMetrics { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 시스템 메트릭스
/// </summary>
public class SystemMetrics
{
    public double MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}

/// <summary>
/// 성능 경고 이벤트 인자
/// </summary>
public class PerformanceAlertEventArgs : EventArgs
{
    public string MetricName { get; set; } = "";
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public PerformanceAlertType AlertType { get; set; }
}

/// <summary>
/// 성능 경고 타입
/// </summary>
public enum PerformanceAlertType
{
    SlowOperation,
    HighMemoryUsage,
    HighCpuUsage,
    TooManyThreads
}

// 성능 관련 이벤트들
public class RenderingStartedEvent : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public object? Source { get; set; }
    public string LayerId { get; set; } = "";
}

public class RenderingCompletedEvent : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public object? Source { get; set; }
    public string LayerId { get; set; } = "";
    public int FeatureCount { get; set; }
}

public class DataLoadStartedEvent : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public object? Source { get; set; }
    public string SourceName { get; set; } = "";
}

public class DataLoadCompletedEvent : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public object? Source { get; set; }
    public string SourceName { get; set; } = "";
    public int FeatureCount { get; set; }
}