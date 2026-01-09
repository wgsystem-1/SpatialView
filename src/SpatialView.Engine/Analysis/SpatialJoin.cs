using SpatialView.Engine.Data;
using SpatialView.Engine.SpatialIndex;
using System.Collections.Concurrent;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 공간 조인 연산을 수행하는 클래스
/// </summary>
public class SpatialJoin
{
    private readonly SpatialJoinOptions _options;
    
    public SpatialJoin(SpatialJoinOptions? options = null)
    {
        _options = options ?? new SpatialJoinOptions();
    }
    
    /// <summary>
    /// 두 피처 컬렉션 간의 공간 조인 수행
    /// </summary>
    public IEnumerable<JoinedFeature> Join(
        IEnumerable<IFeature> leftFeatures,
        IEnumerable<IFeature> rightFeatures,
        SpatialRelation relation = SpatialRelation.Intersects)
    {
        // 오른쪽 피처들을 R-Tree에 인덱싱
        var spatialIndex = BuildSpatialIndex(rightFeatures);
        
        // 병렬 처리 옵션 설정
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism
        };
        
        var results = new ConcurrentBag<JoinedFeature>();
        
        // 왼쪽 피처들에 대해 조인 수행
        Parallel.ForEach(leftFeatures, parallelOptions, leftFeature =>
        {
            if (leftFeature.Geometry == null || leftFeature.BoundingBox == null)
                return;
            
            // 후보 피처 찾기
            var candidates = spatialIndex.Query(leftFeature.BoundingBox);
            
            foreach (var rightFeature in candidates)
            {
                if (rightFeature.Geometry == null)
                    continue;
                
                // 공간 관계 검사
                if (CheckSpatialRelation(leftFeature, rightFeature, relation))
                {
                    var joined = CreateJoinedFeature(leftFeature, rightFeature);
                    results.Add(joined);
                    
                    // One-to-one 조인인 경우 첫 번째 매치에서 중단
                    if (_options.JoinType == JoinType.OneToOne)
                        break;
                }
            }
        });
        
        return results;
    }
    
    /// <summary>
    /// 공간 조인 통계 계산
    /// </summary>
    public SpatialJoinStatistics CalculateStatistics(
        IEnumerable<IFeature> leftFeatures,
        IEnumerable<IFeature> rightFeatures,
        SpatialRelation relation = SpatialRelation.Intersects)
    {
        var stats = new SpatialJoinStatistics();
        var leftList = leftFeatures.ToList();
        var rightList = rightFeatures.ToList();
        
        stats.LeftFeatureCount = leftList.Count;
        stats.RightFeatureCount = rightList.Count;
        
        var spatialIndex = BuildSpatialIndex(rightList);
        var matchedLeft = new HashSet<object>();
        var matchedRight = new HashSet<object>();
        
        foreach (var leftFeature in leftList)
        {
            if (leftFeature.Geometry == null || leftFeature.BoundingBox == null)
                continue;
            
            var candidates = spatialIndex.Query(leftFeature.BoundingBox);
            var hasMatch = false;
            
            foreach (var rightFeature in candidates)
            {
                if (rightFeature.Geometry == null)
                    continue;
                
                if (CheckSpatialRelation(leftFeature, rightFeature, relation))
                {
                    hasMatch = true;
                    matchedRight.Add(rightFeature.Id);
                    stats.TotalMatches++;
                }
            }
            
            if (hasMatch)
                matchedLeft.Add(leftFeature.Id);
        }
        
        stats.LeftMatchedCount = matchedLeft.Count;
        stats.RightMatchedCount = matchedRight.Count;
        
        return stats;
    }
    
    /// <summary>
    /// 가장 가까운 피처 찾기
    /// </summary>
    public IEnumerable<NearestFeature> FindNearest(
        IEnumerable<IFeature> sourceFeatures,
        IEnumerable<IFeature> targetFeatures,
        int k = 1)
    {
        var targetList = targetFeatures.ToList();
        if (targetList.Count == 0)
            yield break;
        
        foreach (var source in sourceFeatures)
        {
            if (source.Geometry == null)
                continue;
            
            var nearestList = new SortedList<double, IFeature>(k + 1);
            
            foreach (var target in targetList)
            {
                if (target.Geometry == null)
                    continue;
                
                var distance = CalculateDistance(source.Geometry, target.Geometry);
                
                if (nearestList.Count < k || distance < nearestList.Keys[nearestList.Count - 1])
                {
                    nearestList.Add(distance, target);
                    
                    if (nearestList.Count > k)
                        nearestList.RemoveAt(nearestList.Count - 1);
                }
            }
            
            foreach (var kvp in nearestList)
            {
                yield return new NearestFeature
                {
                    SourceFeature = source,
                    TargetFeature = kvp.Value,
                    Distance = kvp.Key
                };
            }
        }
    }
    
    /// <summary>
    /// 공간 집계 (예: 각 지역별 점의 개수)
    /// </summary>
    public IEnumerable<AggregatedFeature> SpatialAggregate(
        IEnumerable<IFeature> polygonFeatures,
        IEnumerable<IFeature> pointFeatures,
        AggregateFunction function,
        string? aggregateField = null)
    {
        var spatialIndex = BuildSpatialIndex(pointFeatures);
        
        foreach (var polygon in polygonFeatures)
        {
            if (polygon.Geometry == null || polygon.BoundingBox == null)
                continue;
            
            var candidates = spatialIndex.Query(polygon.BoundingBox);
            var containedFeatures = new List<IFeature>();
            
            foreach (var point in candidates)
            {
                if (point.Geometry == null)
                    continue;
                
                if (polygon.Geometry.Contains(point.Geometry))
                {
                    containedFeatures.Add(point);
                }
            }
            
            var aggregateValue = CalculateAggregate(containedFeatures, function, aggregateField);
            
            yield return new AggregatedFeature
            {
                BaseFeature = polygon,
                AggregateValue = aggregateValue,
                FeatureCount = containedFeatures.Count,
                AggregateFunction = function,
                AggregateField = aggregateField
            };
        }
    }
    
    private RTree<IFeature> BuildSpatialIndex(IEnumerable<IFeature> features)
    {
        var index = new RTree<IFeature>();
        
        foreach (var feature in features)
        {
            if (feature.BoundingBox != null)
            {
                index.Insert(feature.BoundingBox, feature);
            }
        }
        
        return index;
    }
    
    private bool CheckSpatialRelation(IFeature left, IFeature right, SpatialRelation relation)
    {
        if (left.Geometry == null || right.Geometry == null)
            return false;
        
        return relation switch
        {
            SpatialRelation.Intersects => left.Geometry.Intersects(right.Geometry),
            SpatialRelation.Contains => left.Geometry.Contains(right.Geometry),
            SpatialRelation.Within => right.Geometry.Contains(left.Geometry),
            SpatialRelation.Touches => left.Geometry.Touches(right.Geometry),
            SpatialRelation.Crosses => left.Geometry.Crosses(right.Geometry),
            SpatialRelation.Overlaps => left.Geometry.Overlaps(right.Geometry),
            SpatialRelation.Disjoint => left.Geometry.Disjoint(right.Geometry),
            _ => false
        };
    }
    
    private JoinedFeature CreateJoinedFeature(IFeature left, IFeature right)
    {
        var joined = new JoinedFeature
        {
            LeftFeature = left,
            RightFeature = right,
            Id = $"{left.Id}_{right.Id}"
        };
        
        // 속성 병합
        var mergedAttributes = new AttributeTable();
        
        // 왼쪽 속성 추가 (접두사 포함)
        if (left.Attributes != null)
        {
            foreach (var name in left.Attributes.GetNames())
            {
                var key = _options.LeftPrefix + name;
                mergedAttributes[key] = left.Attributes[name];
            }
        }
        
        // 오른쪽 속성 추가 (접두사 포함)
        if (right.Attributes != null)
        {
            foreach (var name in right.Attributes.GetNames())
            {
                var key = _options.RightPrefix + name;
                mergedAttributes[key] = right.Attributes[name];
            }
        }
        
        joined.MergedAttributes = mergedAttributes;
        
        // 기하 병합 옵션에 따라 처리
        joined.Geometry = _options.GeometryMergeOption switch
        {
            GeometryMergeOption.UseLeft => left.Geometry,
            GeometryMergeOption.UseRight => right.Geometry,
            GeometryMergeOption.Union => left.Geometry, // 실제로는 Union 연산 필요
            _ => left.Geometry
        };
        
        return joined;
    }
    
    private double CalculateDistance(Geometry.IGeometry geom1, Geometry.IGeometry geom2)
    {
        // 간단한 중심점 거리 계산
        var center1 = geom1.Centroid;
        var center2 = geom2.Centroid;
        
        if (center1 == null || center2 == null)
            return double.MaxValue;
        
        var dx = center2.X - center1.X;
        var dy = center2.Y - center1.Y;
        
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    private object CalculateAggregate(List<IFeature> features, AggregateFunction function, string? field)
    {
        if (features.Count == 0)
            return function == AggregateFunction.Count ? 0 : null!;
        
        switch (function)
        {
            case AggregateFunction.Count:
                return features.Count;
            
            case AggregateFunction.Sum:
                if (string.IsNullOrEmpty(field)) return 0.0;
                return features.Sum(f => Convert.ToDouble(f.Attributes?[field] ?? 0));
            
            case AggregateFunction.Average:
                if (string.IsNullOrEmpty(field)) return 0.0;
                return features.Average(f => Convert.ToDouble(f.Attributes?[field] ?? 0));
            
            case AggregateFunction.Min:
                if (string.IsNullOrEmpty(field)) return 0.0;
                return features.Min(f => Convert.ToDouble(f.Attributes?[field] ?? 0));
            
            case AggregateFunction.Max:
                if (string.IsNullOrEmpty(field)) return 0.0;
                return features.Max(f => Convert.ToDouble(f.Attributes?[field] ?? 0));
            
            default:
                return features.Count;
        }
    }
}

/// <summary>
/// 공간 조인 옵션
/// </summary>
public class SpatialJoinOptions
{
    /// <summary>
    /// 조인 타입
    /// </summary>
    public JoinType JoinType { get; set; } = JoinType.OneToMany;
    
    /// <summary>
    /// 왼쪽 테이블 속성 접두사
    /// </summary>
    public string LeftPrefix { get; set; } = "left_";
    
    /// <summary>
    /// 오른쪽 테이블 속성 접두사
    /// </summary>
    public string RightPrefix { get; set; } = "right_";
    
    /// <summary>
    /// 기하 병합 옵션
    /// </summary>
    public GeometryMergeOption GeometryMergeOption { get; set; } = GeometryMergeOption.UseLeft;
    
    /// <summary>
    /// 최대 병렬 처리 수준
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}

/// <summary>
/// 조인 타입
/// </summary>
public enum JoinType
{
    /// <summary>
    /// 일대일 조인
    /// </summary>
    OneToOne,
    
    /// <summary>
    /// 일대다 조인
    /// </summary>
    OneToMany
}

/// <summary>
/// 기하 병합 옵션
/// </summary>
public enum GeometryMergeOption
{
    /// <summary>
    /// 왼쪽 기하 사용
    /// </summary>
    UseLeft,
    
    /// <summary>
    /// 오른쪽 기하 사용
    /// </summary>
    UseRight,
    
    /// <summary>
    /// 합집합
    /// </summary>
    Union
}

/// <summary>
/// 공간 관계
/// </summary>
public enum SpatialRelation
{
    Intersects,
    Contains,
    Within,
    Touches,
    Crosses,
    Overlaps,
    Disjoint
}

/// <summary>
/// 집계 함수
/// </summary>
public enum AggregateFunction
{
    Count,
    Sum,
    Average,
    Min,
    Max
}

/// <summary>
/// 조인된 피처
/// </summary>
public class JoinedFeature : Feature
{
    /// <summary>
    /// 왼쪽 피처
    /// </summary>
    public IFeature LeftFeature { get; set; } = null!;
    
    /// <summary>
    /// 오른쪽 피처
    /// </summary>
    public IFeature RightFeature { get; set; } = null!;
    
    /// <summary>
    /// 병합된 속성
    /// </summary>
    public IAttributeTable MergedAttributes { get; set; } = null!;
}

/// <summary>
/// 가장 가까운 피처 결과
/// </summary>
public class NearestFeature
{
    /// <summary>
    /// 소스 피처
    /// </summary>
    public IFeature SourceFeature { get; set; } = null!;
    
    /// <summary>
    /// 타겟 피처
    /// </summary>
    public IFeature TargetFeature { get; set; } = null!;
    
    /// <summary>
    /// 거리
    /// </summary>
    public double Distance { get; set; }
}

/// <summary>
/// 집계된 피처
/// </summary>
public class AggregatedFeature : Feature
{
    /// <summary>
    /// 기본 피처 (폴리곤)
    /// </summary>
    public IFeature BaseFeature { get; set; } = null!;
    
    /// <summary>
    /// 집계 값
    /// </summary>
    public object AggregateValue { get; set; } = null!;
    
    /// <summary>
    /// 포함된 피처 수
    /// </summary>
    public int FeatureCount { get; set; }
    
    /// <summary>
    /// 사용된 집계 함수
    /// </summary>
    public AggregateFunction AggregateFunction { get; set; }
    
    /// <summary>
    /// 집계 필드명
    /// </summary>
    public string? AggregateField { get; set; }
}

/// <summary>
/// 공간 조인 통계
/// </summary>
public class SpatialJoinStatistics
{
    /// <summary>
    /// 왼쪽 피처 총 개수
    /// </summary>
    public int LeftFeatureCount { get; set; }
    
    /// <summary>
    /// 오른쪽 피처 총 개수
    /// </summary>
    public int RightFeatureCount { get; set; }
    
    /// <summary>
    /// 매칭된 왼쪽 피처 수
    /// </summary>
    public int LeftMatchedCount { get; set; }
    
    /// <summary>
    /// 매칭된 오른쪽 피처 수
    /// </summary>
    public int RightMatchedCount { get; set; }
    
    /// <summary>
    /// 총 매치 수
    /// </summary>
    public int TotalMatches { get; set; }
    
    /// <summary>
    /// 매칭 비율 (왼쪽)
    /// </summary>
    public double LeftMatchRatio => LeftFeatureCount > 0 ? 
        (double)LeftMatchedCount / LeftFeatureCount : 0;
    
    /// <summary>
    /// 매칭 비율 (오른쪽)
    /// </summary>
    public double RightMatchRatio => RightFeatureCount > 0 ? 
        (double)RightMatchedCount / RightFeatureCount : 0;
}