using SpatialView.Engine.Geometry;
using SpatialView.Engine.Data;

namespace SpatialView.Engine.Rendering.Optimization;

/// <summary>
/// 피처 클러스터링 시스템
/// 가까운 피처들을 그룹화하여 렌더링 성능 향상
/// </summary>
public class FeatureClustering
{
    private readonly double _clusterRadius;
    private readonly int _minPointsForCluster;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="clusterRadius">클러스터링 반지름 (맵 단위)</param>
    /// <param name="minPointsForCluster">클러스터를 만들기 위한 최소 점 개수</param>
    public FeatureClustering(double clusterRadius = 50.0, int minPointsForCluster = 2)
    {
        _clusterRadius = clusterRadius;
        _minPointsForCluster = minPointsForCluster;
    }
    
    /// <summary>
    /// 피처들을 클러스터링하여 최적화된 렌더링 리스트 반환
    /// </summary>
    /// <param name="features">원본 피처 목록</param>
    /// <param name="viewport">현재 뷰포트</param>
    /// <param name="zoomLevel">현재 줌 레벨</param>
    /// <returns>클러스터링된 피처 목록</returns>
    public IEnumerable<IClusterableFeature> ClusterFeatures(IEnumerable<IFeature> features, 
        Envelope viewport, double zoomLevel)
    {
        var pointFeatures = features
            .Where(f => f.Geometry?.GeometryType == GeometryType.Point)
            .ToList();
        
        var nonPointFeatures = features
            .Where(f => f.Geometry?.GeometryType != GeometryType.Point);
        
        var clusters = CreateClusters(pointFeatures, zoomLevel);
        var result = new List<IClusterableFeature>();
        
        // 클러스터된 포인트 추가
        result.AddRange(clusters);
        
        // 포인트가 아닌 피처들은 그대로 추가
        foreach (var feature in nonPointFeatures)
        {
            result.Add(new SingleFeatureCluster(feature));
        }
        
        return result;
    }
    
    /// <summary>
    /// 포인트 피처들을 클러스터링
    /// </summary>
    private List<IClusterableFeature> CreateClusters(List<IFeature> pointFeatures, double zoomLevel)
    {
        var clusters = new List<IClusterableFeature>();
        var processed = new HashSet<IFeature>();
        
        // 줌 레벨에 따른 동적 클러스터 반지름 조정
        var adaptiveRadius = _clusterRadius / Math.Pow(2, zoomLevel / 4.0);
        
        foreach (var feature in pointFeatures)
        {
            if (processed.Contains(feature))
                continue;
            
            var cluster = new List<IFeature> { feature };
            processed.Add(feature);
            
            var featurePoint = (Point)feature.Geometry!;
            
            // 반지름 내의 다른 피처들 찾기
            foreach (var otherFeature in pointFeatures)
            {
                if (processed.Contains(otherFeature))
                    continue;
                
                var otherPoint = (Point)otherFeature.Geometry!;
                var distance = CalculateDistance(featurePoint, otherPoint);
                
                if (distance <= adaptiveRadius)
                {
                    cluster.Add(otherFeature);
                    processed.Add(otherFeature);
                }
            }
            
            // 클러스터 생성
            if (cluster.Count >= _minPointsForCluster)
            {
                clusters.Add(new FeatureCluster(cluster, CalculateClusterCenter(cluster)));
            }
            else
            {
                // 클러스터 조건을 만족하지 않으면 개별 피처로 표시
                foreach (var individualFeature in cluster)
                {
                    clusters.Add(new SingleFeatureCluster(individualFeature));
                }
            }
        }
        
        return clusters;
    }
    
    /// <summary>
    /// 두 포인트 사이의 거리 계산
    /// </summary>
    private double CalculateDistance(Point point1, Point point2)
    {
        var dx = point1.X - point2.X;
        var dy = point1.Y - point2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// 클러스터의 중심점 계산
    /// </summary>
    private Point CalculateClusterCenter(List<IFeature> clusterFeatures)
    {
        var totalX = 0.0;
        var totalY = 0.0;
        var count = 0;
        
        foreach (var feature in clusterFeatures)
        {
            if (feature.Geometry is Point point)
            {
                totalX += point.X;
                totalY += point.Y;
                count++;
            }
        }
        
        if (count == 0)
            return new Point(0, 0);
        
        return new Point(totalX / count, totalY / count);
    }
}

/// <summary>
/// 클러스터링 가능한 피처 인터페이스
/// </summary>
public interface IClusterableFeature
{
    /// <summary>
    /// 이 객체가 클러스터인지 개별 피처인지
    /// </summary>
    bool IsCluster { get; }
    
    /// <summary>
    /// 클러스터에 포함된 피처 수 (개별 피처의 경우 1)
    /// </summary>
    int FeatureCount { get; }
    
    /// <summary>
    /// 표시할 지오메트리 (클러스터의 경우 중심점, 개별 피처의 경우 원본 지오메트리)
    /// </summary>
    IGeometry DisplayGeometry { get; }
    
    /// <summary>
    /// 원본 피처들 (클러스터의 경우 포함된 모든 피처, 개별 피처의 경우 자기 자신)
    /// </summary>
    IEnumerable<IFeature> Features { get; }
    
    /// <summary>
    /// 클러스터의 경계 영역
    /// </summary>
    Envelope BoundingBox { get; }
}

/// <summary>
/// 피처 클러스터 구현
/// </summary>
public class FeatureCluster : IClusterableFeature
{
    private readonly List<IFeature> _features;
    private readonly Point _centerPoint;
    
    public FeatureCluster(List<IFeature> features, Point centerPoint)
    {
        _features = features ?? throw new ArgumentNullException(nameof(features));
        _centerPoint = centerPoint ?? throw new ArgumentNullException(nameof(centerPoint));
    }
    
    public bool IsCluster => true;
    public int FeatureCount => _features.Count;
    public IGeometry DisplayGeometry => _centerPoint;
    public IEnumerable<IFeature> Features => _features;
    
    public Envelope BoundingBox
    {
        get
        {
            var envelope = new Envelope();
            foreach (var feature in _features)
            {
                if (feature.BoundingBox != null)
                {
                    envelope.ExpandToInclude(feature.BoundingBox);
                }
            }
            return envelope;
        }
    }
}

/// <summary>
/// 단일 피처 래퍼
/// </summary>
public class SingleFeatureCluster : IClusterableFeature
{
    private readonly IFeature _feature;
    
    public SingleFeatureCluster(IFeature feature)
    {
        _feature = feature ?? throw new ArgumentNullException(nameof(feature));
    }
    
    public bool IsCluster => false;
    public int FeatureCount => 1;
    public IGeometry DisplayGeometry => _feature.Geometry!;
    public IEnumerable<IFeature> Features => new[] { _feature };
    public Envelope BoundingBox => _feature.BoundingBox ?? new Envelope();
}

/// <summary>
/// 클러스터링 설정
/// </summary>
public class ClusteringOptions
{
    /// <summary>
    /// 줌 레벨별 클러스터링 활성화 여부
    /// </summary>
    public Dictionary<int, bool> ZoomLevelEnabled { get; set; } = new();
    
    /// <summary>
    /// 줌 레벨별 클러스터 반지름
    /// </summary>
    public Dictionary<int, double> ZoomLevelRadius { get; set; } = new();
    
    /// <summary>
    /// 클러스터 최소 피처 수
    /// </summary>
    public int MinFeaturesForCluster { get; set; } = 2;
    
    /// <summary>
    /// 클러스터 최대 피처 수
    /// </summary>
    public int MaxFeaturesPerCluster { get; set; } = 100;
    
    /// <summary>
    /// 기본 설정으로 초기화
    /// </summary>
    public static ClusteringOptions CreateDefault()
    {
        var options = new ClusteringOptions();
        
        // 낮은 줌 레벨에서만 클러스터링 활성화
        for (int zoom = 1; zoom <= 10; zoom++)
        {
            options.ZoomLevelEnabled[zoom] = true;
            options.ZoomLevelRadius[zoom] = 100.0 / Math.Pow(2, zoom / 3.0);
        }
        
        // 높은 줌 레벨에서는 클러스터링 비활성화
        for (int zoom = 11; zoom <= 20; zoom++)
        {
            options.ZoomLevelEnabled[zoom] = false;
        }
        
        return options;
    }
}