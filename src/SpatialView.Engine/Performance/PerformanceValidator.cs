namespace SpatialView.Engine.Performance;

/// <summary>
/// 성능 검증 시스템
/// 정의된 성능 요구사항을 충족하는지 검증
/// </summary>
public class PerformanceValidator
{
    private readonly PerformanceRequirements _requirements;
    
    public PerformanceValidator(PerformanceRequirements? requirements = null)
    {
        _requirements = requirements ?? PerformanceRequirements.CreateDefault();
    }
    
    /// <summary>
    /// 전체 성능 검증
    /// </summary>
    public async Task<ValidationReport> ValidatePerformanceAsync()
    {
        var benchmark = new PerformanceBenchmark();
        var performanceReport = await benchmark.RunFullBenchmarkAsync();
        
        var report = new ValidationReport
        {
            PerformanceReport = performanceReport,
            Requirements = _requirements,
            ValidationResults = new List<ValidationResult>()
        };
        
        // 각 요구사항 검증
        ValidateSpatialIndexing(report, performanceReport);
        ValidateRenderingOptimization(report, performanceReport);
        ValidateMemoryUsage(report, performanceReport);
        ValidateScalability(report, performanceReport);
        ValidateConcurrency(report, performanceReport);
        
        report.OverallPassed = report.ValidationResults.All(r => r.Passed);
        report.PassedCount = report.ValidationResults.Count(r => r.Passed);
        report.FailedCount = report.ValidationResults.Count(r => !r.Passed);
        
        return report;
    }
    
    private void ValidateSpatialIndexing(ValidationReport report, PerformanceReport perfReport)
    {
        if (perfReport.SpatialIndexResults == null) return;
        
        // R-Tree 성능 검증
        if (perfReport.SpatialIndexResults.RTreePerformance != null)
        {
            var rtree = perfReport.SpatialIndexResults.RTreePerformance;
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Spatial Indexing",
                TestName = "R-Tree Insertion Performance",
                RequiredValue = _requirements.MaxRTreeInsertionTime,
                ActualValue = rtree.InsertionTime,
                Passed = rtree.InsertionTime <= _requirements.MaxRTreeInsertionTime,
                Message = $"R-Tree insertion time: {rtree.InsertionTime:F2}ms (required: <{_requirements.MaxRTreeInsertionTime}ms)"
            });
            
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Spatial Indexing",
                TestName = "R-Tree Search Performance",
                RequiredValue = _requirements.MaxRTreeSearchTime,
                ActualValue = rtree.SearchTime,
                Passed = rtree.SearchTime <= _requirements.MaxRTreeSearchTime,
                Message = $"R-Tree search time: {rtree.SearchTime:F2}ms (required: <{_requirements.MaxRTreeSearchTime}ms)"
            });
        }
    }
    
    private void ValidateRenderingOptimization(ValidationReport report, PerformanceReport perfReport)
    {
        if (perfReport.RenderingOptimizationResults == null) return;
        
        // LOD 성능 검증
        if (perfReport.RenderingOptimizationResults.LodPerformance != null)
        {
            var lod = perfReport.RenderingOptimizationResults.LodPerformance;
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Rendering Optimization",
                TestName = "LOD Processing Time",
                RequiredValue = _requirements.MaxLodProcessingTime,
                ActualValue = lod.AverageProcessingTime,
                Passed = lod.AverageProcessingTime <= _requirements.MaxLodProcessingTime,
                Message = $"LOD processing time: {lod.AverageProcessingTime:F2}ms (required: <{_requirements.MaxLodProcessingTime}ms)"
            });
        }
        
        // 클러스터링 성능 검증
        if (perfReport.RenderingOptimizationResults.ClusteringPerformance != null)
        {
            var clustering = perfReport.RenderingOptimizationResults.ClusteringPerformance;
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Rendering Optimization",
                TestName = "Clustering Processing Time",
                RequiredValue = _requirements.MaxClusteringTime,
                ActualValue = clustering.AverageProcessingTime,
                Passed = clustering.AverageProcessingTime <= _requirements.MaxClusteringTime,
                Message = $"Clustering time: {clustering.AverageProcessingTime:F2}ms (required: <{_requirements.MaxClusteringTime}ms)"
            });
        }
    }
    
    private void ValidateMemoryUsage(ValidationReport report, PerformanceReport perfReport)
    {
        if (perfReport.MemoryUsageResults == null) return;
        
        // 객체 풀 효율성 검증
        if (perfReport.MemoryUsageResults.ObjectPoolEfficiency != null)
        {
            var pool = perfReport.MemoryUsageResults.ObjectPoolEfficiency;
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Memory Optimization",
                TestName = "Object Pool Efficiency",
                RequiredValue = _requirements.MinPoolEfficiencyRatio,
                ActualValue = pool.EfficiencyRatio,
                Passed = pool.EfficiencyRatio >= _requirements.MinPoolEfficiencyRatio,
                Message = $"Object pool efficiency: {pool.EfficiencyRatio:F2}x (required: >{_requirements.MinPoolEfficiencyRatio}x)"
            });
        }
        
        // 압축 효율성 검증
        if (perfReport.MemoryUsageResults.CompressionEfficiency != null)
        {
            var compression = perfReport.MemoryUsageResults.CompressionEfficiency;
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Memory Optimization",
                TestName = "Geometry Compression Ratio",
                RequiredValue = _requirements.MinCompressionRatio,
                ActualValue = compression.CompressionRatio,
                Passed = compression.CompressionRatio >= _requirements.MinCompressionRatio,
                Message = $"Compression ratio: {compression.CompressionRatio:F2}x (required: >{_requirements.MinCompressionRatio}x)"
            });
        }
    }
    
    private void ValidateScalability(ValidationReport report, PerformanceReport perfReport)
    {
        if (perfReport.LargeDatasetResults == null) return;
        
        report.ValidationResults.Add(new ValidationResult
        {
            Category = "Scalability",
            TestName = "Linear Scaling",
            RequiredValue = 1.0,
            ActualValue = perfReport.LargeDatasetResults.IsLinearScaling ? 1.0 : 0.0,
            Passed = perfReport.LargeDatasetResults.IsLinearScaling,
            Message = $"Scalability: {(perfReport.LargeDatasetResults.IsLinearScaling ? "Linear" : "Non-linear")}"
        });
        
        // 100K 피처 처리 시간 검증
        if (perfReport.LargeDatasetResults.ProcessingTimes.TryGetValue(100000, out var time100k))
        {
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Scalability",
                TestName = "100K Features Processing",
                RequiredValue = _requirements.Max100KFeaturesProcessingTime,
                ActualValue = time100k,
                Passed = time100k <= _requirements.Max100KFeaturesProcessingTime,
                Message = $"100K features processing: {time100k:F2}ms (required: <{_requirements.Max100KFeaturesProcessingTime}ms)"
            });
        }
    }
    
    private void ValidateConcurrency(ValidationReport report, PerformanceReport perfReport)
    {
        if (perfReport.ConcurrencyResults == null) return;
        
        // 멀티스레드 성능 향상 검증
        if (perfReport.ConcurrencyResults.ThreadPerformance.TryGetValue(1, out var single) &&
            perfReport.ConcurrencyResults.ThreadPerformance.TryGetValue(4, out var quad))
        {
            var speedup = single.TotalTime / quad.TotalTime;
            report.ValidationResults.Add(new ValidationResult
            {
                Category = "Concurrency",
                TestName = "Multi-thread Speedup (4 threads)",
                RequiredValue = _requirements.MinThreadSpeedup,
                ActualValue = speedup,
                Passed = speedup >= _requirements.MinThreadSpeedup,
                Message = $"4-thread speedup: {speedup:F2}x (required: >{_requirements.MinThreadSpeedup}x)"
            });
        }
    }
}

/// <summary>
/// 성능 요구사항
/// </summary>
public class PerformanceRequirements
{
    // 공간 인덱싱 요구사항
    public double MaxRTreeInsertionTime { get; set; } = 100.0; // ms
    public double MaxRTreeSearchTime { get; set; } = 10.0; // ms
    public double MaxQuadTreeInsertionTime { get; set; } = 80.0; // ms
    public double MaxQuadTreeSearchTime { get; set; } = 8.0; // ms
    
    // 렌더링 최적화 요구사항
    public double MaxLodProcessingTime { get; set; } = 50.0; // ms
    public double MaxClusteringTime { get; set; } = 30.0; // ms
    public double MaxRenderingTime { get; set; } = 16.67; // ms (60 FPS)
    
    // 메모리 최적화 요구사항
    public double MinPoolEfficiencyRatio { get; set; } = 2.0; // 2x faster
    public double MinCompressionRatio { get; set; } = 3.0; // 3:1 compression
    public double MaxMemoryUsageMB { get; set; } = 500.0; // MB
    
    // 확장성 요구사항
    public double Max100KFeaturesProcessingTime { get; set; } = 1000.0; // ms
    public bool RequireLinearScaling { get; set; } = true;
    
    // 동시성 요구사항
    public double MinThreadSpeedup { get; set; } = 2.5; // 4 threads = 2.5x speedup
    
    public static PerformanceRequirements CreateDefault()
    {
        return new PerformanceRequirements();
    }
    
    public static PerformanceRequirements CreateStrict()
    {
        return new PerformanceRequirements
        {
            MaxRTreeInsertionTime = 50.0,
            MaxRTreeSearchTime = 5.0,
            MaxLodProcessingTime = 30.0,
            MaxClusteringTime = 20.0,
            MinPoolEfficiencyRatio = 3.0,
            MinCompressionRatio = 4.0,
            Max100KFeaturesProcessingTime = 500.0,
            MinThreadSpeedup = 3.0
        };
    }
}

/// <summary>
/// 검증 보고서
/// </summary>
public class ValidationReport
{
    public PerformanceReport? PerformanceReport { get; set; }
    public PerformanceRequirements? Requirements { get; set; }
    public List<ValidationResult> ValidationResults { get; set; } = new();
    public bool OverallPassed { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    
    public void GenerateHtmlReport(string filePath)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>GIS Engine Performance Validation Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .passed {{ color: green; }}
        .failed {{ color: red; }}
        table {{ border-collapse: collapse; width: 100%; margin: 20px 0; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
        .summary {{ background-color: #f0f0f0; padding: 10px; margin: 20px 0; }}
    </style>
</head>
<body>
    <h1>GIS Engine Performance Validation Report</h1>
    <div class='summary'>
        <h2>Summary</h2>
        <p>Overall Result: <span class='{(OverallPassed ? "passed" : "failed")}'>{(OverallPassed ? "PASSED" : "FAILED")}</span></p>
        <p>Passed Tests: {PassedCount}</p>
        <p>Failed Tests: {FailedCount}</p>
        <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
    
    <h2>Test Results</h2>
    <table>
        <tr>
            <th>Category</th>
            <th>Test Name</th>
            <th>Required</th>
            <th>Actual</th>
            <th>Result</th>
            <th>Details</th>
        </tr>";
        
        foreach (var result in ValidationResults)
        {
            html += $@"
        <tr>
            <td>{result.Category}</td>
            <td>{result.TestName}</td>
            <td>{result.RequiredValue:F2}</td>
            <td>{result.ActualValue:F2}</td>
            <td class='{(result.Passed ? "passed" : "failed")}'>{(result.Passed ? "PASS" : "FAIL")}</td>
            <td>{result.Message}</td>
        </tr>";
        }
        
        html += @"
    </table>
</body>
</html>";
        
        File.WriteAllText(filePath, html);
    }
}

/// <summary>
/// 검증 결과
/// </summary>
public class ValidationResult
{
    public string Category { get; set; } = "";
    public string TestName { get; set; } = "";
    public double RequiredValue { get; set; }
    public double ActualValue { get; set; }
    public bool Passed { get; set; }
    public string Message { get; set; } = "";
}