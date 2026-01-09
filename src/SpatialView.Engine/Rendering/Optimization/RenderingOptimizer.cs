using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Engine.Rendering.Optimization;

/// <summary>
/// 통합 렌더링 최적화 매니저
/// LOD, 클러스터링, 뷰포트 컬링을 통합 관리
/// </summary>
public class RenderingOptimizer
{
    private readonly LevelOfDetail _lodSystem;
    private readonly FeatureClustering _clustering;
    private readonly ViewportCulling _viewportCulling;
    private readonly RenderingOptions _options;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="options">렌더링 최적화 옵션</param>
    public RenderingOptimizer(RenderingOptions? options = null)
    {
        _options = options ?? RenderingOptions.CreateDefault();
        _lodSystem = new LevelOfDetail();
        _clustering = new FeatureClustering(_options.ClusterRadius, _options.MinPointsForCluster);
        _viewportCulling = new ViewportCulling();
    }
    
    /// <summary>
    /// 피처들을 최적화하여 렌더링 준비
    /// </summary>
    /// <param name="features">원본 피처 목록</param>
    /// <param name="viewport">현재 뷰포트</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <returns>최적화된 렌더링 결과</returns>
    public OptimizedRenderingResult OptimizeFeatures(IEnumerable<IFeature> features, 
        Envelope viewport, double zoomLevel)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new OptimizedRenderingResult();
        
        // 1. 뷰포트 컬링 - 화면에 보이는 영역만 선택
        var visibleFeatures = _viewportCulling.CullFeatures(features, viewport, zoomLevel, 1.0).ToList();
        result.AfterViewportCulling = visibleFeatures.Count;
        
        // 2. LOD 필터링 - 줌 레벨에 따른 피처 필터링 및 지오메트리 단순화
        IEnumerable<IFeature> lodFilteredFeatures = visibleFeatures;
        if (_options.EnableLod && zoomLevel <= _options.MaxLodZoomLevel)
        {
            lodFilteredFeatures = _lodSystem.FilterFeatures(visibleFeatures, zoomLevel, viewport);
        }
        result.AfterLodFiltering = lodFilteredFeatures.Count();
        
        // 3. 클러스터링 - 가까운 포인트들을 클러스터링
        IEnumerable<IClusterableFeature> finalFeatures = lodFilteredFeatures
            .Select(f => new SingleFeatureCluster(f))
            .Cast<IClusterableFeature>();
        
        if (_options.EnableClustering && zoomLevel <= _options.MaxClusteringZoomLevel)
        {
            finalFeatures = _clustering.ClusterFeatures(lodFilteredFeatures, viewport, zoomLevel);
        }
        
        result.FinalFeatures = finalFeatures.ToList();
        result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        result.OptimizationRatio = CalculateOptimizationRatio(features.Count(), result.FinalFeatures.Count);
        
        return result;
    }
    
    /// <summary>
    /// 특정 레이어에 대한 최적화 수행
    /// </summary>
    /// <param name="layer">대상 레이어</param>
    /// <param name="viewport">현재 뷰포트</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <returns>최적화된 렌더링 결과</returns>
    public OptimizedRenderingResult OptimizeLayer(Data.Layers.ILayer layer, 
        Envelope viewport, double zoomLevel)
    {
        var features = layer.GetFeatures(viewport);
        return OptimizeFeatures(features, viewport, zoomLevel);
    }
    
    /// <summary>
    /// 복수 레이어에 대한 일괄 최적화
    /// </summary>
    /// <param name="layers">대상 레이어 목록</param>
    /// <param name="viewport">현재 뷰포트</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <returns>레이어별 최적화 결과</returns>
    public Dictionary<Data.Layers.ILayer, OptimizedRenderingResult> OptimizeLayers(
        IEnumerable<Data.Layers.ILayer> layers, Envelope viewport, double zoomLevel)
    {
        var results = new Dictionary<Data.Layers.ILayer, OptimizedRenderingResult>();
        
        foreach (var layer in layers)
        {
            if (layer.Visible && IsLayerVisibleAtZoom(layer, zoomLevel))
            {
                results[layer] = OptimizeLayer(layer, viewport, zoomLevel);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 동적 LOD 설정 업데이트
    /// </summary>
    /// <param name="zoomLevel">줌 레벨</param>
    /// <param name="lodLevel">LOD 설정</param>
    public void UpdateLodLevel(int zoomLevel, LodLevel lodLevel)
    {
        _lodSystem.AddLodLevel(zoomLevel, lodLevel);
    }
    
    /// <summary>
    /// 렌더링 성능 통계 수집
    /// </summary>
    /// <param name="results">렌더링 결과들</param>
    /// <returns>성능 통계</returns>
    public RenderingPerformanceStats CollectPerformanceStats(
        IEnumerable<OptimizedRenderingResult> results)
    {
        var stats = new RenderingPerformanceStats();
        var resultList = results.ToList();
        
        stats.TotalLayers = resultList.Count;
        stats.TotalProcessingTimeMs = resultList.Sum(r => r.ProcessingTimeMs);
        stats.AverageOptimizationRatio = resultList.Average(r => r.OptimizationRatio);
        stats.TotalFeaturesProcessed = resultList.Sum(r => r.AfterViewportCulling);
        stats.TotalFeaturesRendered = resultList.Sum(r => r.FinalFeatures.Count);
        
        return stats;
    }
    
    private bool IsLayerVisibleAtZoom(Data.Layers.ILayer layer, double zoomLevel)
    {
        return zoomLevel >= layer.MinimumZoom && zoomLevel <= layer.MaximumZoom;
    }
    
    private double CalculateOptimizationRatio(int originalCount, int optimizedCount)
    {
        if (originalCount == 0) return 0.0;
        return (double)(originalCount - optimizedCount) / originalCount * 100.0;
    }
}

/// <summary>
/// 최적화된 렌더링 결과
/// </summary>
public class OptimizedRenderingResult
{
    /// <summary>
    /// 뷰포트 컬링 후 피처 수
    /// </summary>
    public int AfterViewportCulling { get; set; }
    
    /// <summary>
    /// LOD 필터링 후 피처 수
    /// </summary>
    public int AfterLodFiltering { get; set; }
    
    /// <summary>
    /// 최종 렌더링할 피처들 (클러스터 포함)
    /// </summary>
    public List<IClusterableFeature> FinalFeatures { get; set; } = new();
    
    /// <summary>
    /// 처리 시간 (밀리초)
    /// </summary>
    public long ProcessingTimeMs { get; set; }
    
    /// <summary>
    /// 최적화 비율 (%)
    /// </summary>
    public double OptimizationRatio { get; set; }
}

/// <summary>
/// 렌더링 최적화 옵션
/// </summary>
public class RenderingOptions
{
    /// <summary>
    /// LOD 시스템 활성화
    /// </summary>
    public bool EnableLod { get; set; } = true;
    
    /// <summary>
    /// 클러스터링 활성화
    /// </summary>
    public bool EnableClustering { get; set; } = true;
    
    /// <summary>
    /// 뷰포트 컬링 활성화
    /// </summary>
    public bool EnableViewportCulling { get; set; } = true;
    
    /// <summary>
    /// LOD가 적용되는 최대 줌 레벨
    /// </summary>
    public double MaxLodZoomLevel { get; set; } = 15.0;
    
    /// <summary>
    /// 클러스터링이 적용되는 최대 줌 레벨
    /// </summary>
    public double MaxClusteringZoomLevel { get; set; } = 10.0;
    
    /// <summary>
    /// 클러스터 반지름 (맵 단위)
    /// </summary>
    public double ClusterRadius { get; set; } = 50.0;
    
    /// <summary>
    /// 클러스터 최소 포인트 수
    /// </summary>
    public int MinPointsForCluster { get; set; } = 2;
    
    /// <summary>
    /// 성능 모드
    /// </summary>
    public PerformanceMode PerformanceMode { get; set; } = PerformanceMode.Balanced;
    
    /// <summary>
    /// 기본 설정 생성
    /// </summary>
    public static RenderingOptions CreateDefault()
    {
        return new RenderingOptions();
    }
    
    /// <summary>
    /// 고성능 모드 설정
    /// </summary>
    public static RenderingOptions CreateHighPerformance()
    {
        return new RenderingOptions
        {
            EnableLod = true,
            EnableClustering = true,
            EnableViewportCulling = true,
            MaxLodZoomLevel = 20.0,
            MaxClusteringZoomLevel = 15.0,
            ClusterRadius = 100.0,
            MinPointsForCluster = 3,
            PerformanceMode = PerformanceMode.HighPerformance
        };
    }
    
    /// <summary>
    /// 고품질 모드 설정
    /// </summary>
    public static RenderingOptions CreateHighQuality()
    {
        return new RenderingOptions
        {
            EnableLod = false,
            EnableClustering = false,
            EnableViewportCulling = true,
            PerformanceMode = PerformanceMode.HighQuality
        };
    }
}

/// <summary>
/// 성능 모드
/// </summary>
public enum PerformanceMode
{
    /// <summary>
    /// 고품질 (최적화 최소화)
    /// </summary>
    HighQuality,
    
    /// <summary>
    /// 균형 (기본값)
    /// </summary>
    Balanced,
    
    /// <summary>
    /// 고성능 (최적화 최대화)
    /// </summary>
    HighPerformance
}

/// <summary>
/// 렌더링 성능 통계
/// </summary>
public class RenderingPerformanceStats
{
    /// <summary>
    /// 처리된 레이어 수
    /// </summary>
    public int TotalLayers { get; set; }
    
    /// <summary>
    /// 총 처리 시간 (밀리초)
    /// </summary>
    public long TotalProcessingTimeMs { get; set; }
    
    /// <summary>
    /// 평균 최적화 비율 (%)
    /// </summary>
    public double AverageOptimizationRatio { get; set; }
    
    /// <summary>
    /// 처리된 총 피처 수
    /// </summary>
    public int TotalFeaturesProcessed { get; set; }
    
    /// <summary>
    /// 실제 렌더링된 총 피처 수
    /// </summary>
    public int TotalFeaturesRendered { get; set; }
}