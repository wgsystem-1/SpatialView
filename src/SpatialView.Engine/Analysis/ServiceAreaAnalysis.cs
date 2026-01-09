using SpatialView.Engine.Geometry;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 서비스 영역 분석 모듈
/// 등시선(Isochrone) 생성, 시설물 배치 최적화, 접근성 분석 등
/// </summary>
public static class ServiceAreaAnalysis
{
    /// <summary>
    /// 등시선(Isochrone) 생성 - 특정 시간 내 도달 가능한 영역
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="startPoints">시작점들</param>
    /// <param name="timeIntervals">시간 간격들 (분 단위)</param>
    /// <param name="travelMode">이동 모드</param>
    /// <returns>각 시간 간격별 등시선 폴리곤</returns>
    public static IsochroneResult GenerateIsochrones(
        RoadNetwork network, 
        IEnumerable<ICoordinate> startPoints, 
        IEnumerable<int> timeIntervals,
        TravelMode travelMode = TravelMode.Driving)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (startPoints == null) throw new ArgumentNullException(nameof(startPoints));

        var result = new IsochroneResult();
        var startPointsList = startPoints.ToList();
        var timeIntervalsList = timeIntervals.OrderBy(t => t).ToList();

        // 각 시작점에 대해 가장 가까운 네트워크 노드 찾기
        var startNodes = startPointsList.Select(point => FindNearestNode(network, point)).ToList();

        // 각 시간 간격에 대해 등시선 생성
        foreach (var timeMinutes in timeIntervalsList)
        {
            var timeSeconds = timeMinutes * 60.0;
            var reachableNodes = new HashSet<string>();

            // 모든 시작점에서 도달 가능한 노드들 수집
            foreach (var startNode in startNodes)
            {
                if (startNode == null) continue;

                var serviceArea = NetworkAnalysis.CalculateServiceArea(
                    network, startNode, maxTime: timeSeconds);
                    
                foreach (var node in serviceArea.ReachableNodes)
                {
                    reachableNodes.Add(node);
                }
            }

            // 도달 가능한 노드들로부터 등시선 폴리곤 생성
            var isochronePolygon = GenerateIsochronePolygon(network, reachableNodes, timeSeconds);
            
            result.Isochrones.Add(new IsochroneRing
            {
                TimeMinutes = timeMinutes,
                Polygon = isochronePolygon,
                ReachableNodes = reachableNodes.ToList()
            });
        }

        result.StartPoints = startPointsList;
        result.TravelMode = travelMode;
        return result;
    }

    /// <summary>
    /// 시설물 배치 최적화 - 최대 커버리지 문제
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="candidateLocations">후보 위치들</param>
    /// <param name="demandPoints">수요 지점들</param>
    /// <param name="maxDistance">최대 서비스 거리</param>
    /// <param name="numFacilities">배치할 시설 수</param>
    /// <returns>최적 시설 배치 결과</returns>
    public static FacilityLocationResult OptimizeFacilityLocation(
        RoadNetwork network,
        IEnumerable<ICoordinate> candidateLocations,
        IEnumerable<DemandPoint> demandPoints,
        double maxDistance,
        int numFacilities)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (candidateLocations == null) throw new ArgumentNullException(nameof(candidateLocations));
        if (demandPoints == null) throw new ArgumentNullException(nameof(demandPoints));

        var candidates = candidateLocations.ToList();
        var demands = demandPoints.ToList();
        
        // 그리디 알고리즘으로 최대 커버리지 해결
        var selectedFacilities = new List<ICoordinate>();
        var coveredDemands = new HashSet<DemandPoint>();
        
        for (int i = 0; i < numFacilities && selectedFacilities.Count < candidates.Count; i++)
        {
            var bestCandidate = FindBestCandidateLocation(
                network, candidates, demands, selectedFacilities, coveredDemands, maxDistance);
                
            if (bestCandidate != null)
            {
                selectedFacilities.Add(bestCandidate);
                
                // 새로 커버되는 수요 지점들 추가
                var newlyCovered = GetCoveredDemands(network, bestCandidate, demands, maxDistance)
                    .Where(d => !coveredDemands.Contains(d));
                    
                foreach (var demand in newlyCovered)
                {
                    coveredDemands.Add(demand);
                }
            }
        }

        return new FacilityLocationResult
        {
            SelectedFacilities = selectedFacilities,
            CoveredDemands = coveredDemands.ToList(),
            TotalCoverage = coveredDemands.Sum(d => d.Weight),
            CoveragePercentage = demands.Count > 0 ? (double)coveredDemands.Count / demands.Count * 100 : 0
        };
    }

    /// <summary>
    /// P-Median 문제 해결 - 총 이동 거리 최소화
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="candidateLocations">후보 위치들</param>
    /// <param name="demandPoints">수요 지점들</param>
    /// <param name="numFacilities">배치할 시설 수</param>
    /// <returns>P-Median 최적화 결과</returns>
    public static PMedianResult SolvePMedian(
        RoadNetwork network,
        IEnumerable<ICoordinate> candidateLocations,
        IEnumerable<DemandPoint> demandPoints,
        int numFacilities)
    {
        var candidates = candidateLocations.ToList();
        var demands = demandPoints.ToList();
        
        // 거리 매트릭스 계산
        var distanceMatrix = CalculateDistanceMatrix(network, candidates, demands);
        
        // 그리디 heuristic으로 P-Median 해결
        var selectedFacilities = new List<int>(); // 후보 위치 인덱스
        
        for (int i = 0; i < numFacilities; i++)
        {
            int bestCandidate = FindBestPMedianCandidate(distanceMatrix, selectedFacilities, demands.Count);
            if (bestCandidate >= 0)
            {
                selectedFacilities.Add(bestCandidate);
            }
        }

        // 할당 계산
        var assignments = new Dictionary<int, int>(); // 수요점 -> 시설
        var totalCost = 0.0;
        
        for (int j = 0; j < demands.Count; j++)
        {
            var minDistance = double.MaxValue;
            var assignedFacility = -1;
            
            foreach (var facility in selectedFacilities)
            {
                var distance = distanceMatrix[facility, j];
                if (distance < minDistance)
                {
                    minDistance = distance;
                    assignedFacility = facility;
                }
            }
            
            if (assignedFacility >= 0)
            {
                assignments[j] = assignedFacility;
                totalCost += minDistance * demands[j].Weight;
            }
        }

        return new PMedianResult
        {
            SelectedFacilities = selectedFacilities.Select(i => candidates[i]).ToList(),
            Assignments = assignments,
            TotalCost = totalCost,
            AverageCost = demands.Count > 0 ? totalCost / demands.Sum(d => d.Weight) : 0
        };
    }

    /// <summary>
    /// 접근성 분석 - 각 지점에서 시설까지의 접근성 평가
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="facilities">시설 위치들</param>
    /// <param name="evaluationPoints">평가 지점들</param>
    /// <param name="maxDistance">최대 고려 거리</param>
    /// <returns>접근성 분석 결과</returns>
    public static AccessibilityAnalysis AnalyzeAccessibility(
        RoadNetwork network,
        IEnumerable<ICoordinate> facilities,
        IEnumerable<ICoordinate> evaluationPoints,
        double maxDistance = double.MaxValue)
    {
        var facilityList = facilities.ToList();
        var evaluationList = evaluationPoints.ToList();
        var accessibilityScores = new Dictionary<ICoordinate, AccessibilityMetrics>();

        foreach (var point in evaluationList)
        {
            var nearestNode = FindNearestNode(network, point);
            if (nearestNode == null) continue;

            var distances = new List<double>();
            var travelTimes = new List<double>();

            foreach (var facility in facilityList)
            {
                var facilityNode = FindNearestNode(network, facility);
                if (facilityNode == null) continue;

                var path = NetworkAnalysis.FindShortestPath(network, nearestNode, facilityNode);
                if (path != null && path.TotalDistance <= maxDistance)
                {
                    distances.Add(path.TotalDistance);
                    travelTimes.Add(path.TotalTime);
                }
            }

            if (distances.Any())
            {
                accessibilityScores[point] = new AccessibilityMetrics
                {
                    NearestFacilityDistance = distances.Min(),
                    NearestFacilityTime = travelTimes.Min(),
                    AverageDistance = distances.Average(),
                    AverageTime = travelTimes.Average(),
                    AccessibleFacilities = distances.Count,
                    AccessibilityScore = CalculateAccessibilityScore(distances.Min(), distances.Count)
                };
            }
            else
            {
                accessibilityScores[point] = new AccessibilityMetrics
                {
                    NearestFacilityDistance = double.MaxValue,
                    NearestFacilityTime = double.MaxValue,
                    AccessibilityScore = 0
                };
            }
        }

        return new AccessibilityAnalysis
        {
            Facilities = facilityList,
            EvaluationPoints = evaluationList,
            AccessibilityScores = accessibilityScores,
            OverallAccessibility = accessibilityScores.Values.Average(m => m.AccessibilityScore)
        };
    }

    #region Private Helper Methods

    /// <summary>
    /// 가장 가까운 네트워크 노드 찾기
    /// </summary>
    private static string? FindNearestNode(RoadNetwork network, ICoordinate point)
    {
        var minDistance = double.MaxValue;
        string? nearestNode = null;

        foreach (var kvp in network.Nodes)
        {
            var node = kvp.Value;
            var distance = Math.Sqrt(Math.Pow(point.X - node.X, 2) + Math.Pow(point.Y - node.Y, 2));
            
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestNode = kvp.Key;
            }
        }

        return nearestNode;
    }

    /// <summary>
    /// 등시선 폴리곤 생성
    /// </summary>
    private static Polygon? GenerateIsochronePolygon(RoadNetwork network, HashSet<string> reachableNodes, double timeSeconds)
    {
        if (reachableNodes.Count < 3) return null;

        // 도달 가능한 노드들의 좌표 수집
        var reachableCoords = reachableNodes
            .Where(nodeId => network.Nodes.ContainsKey(nodeId))
            .Select(nodeId => network.Nodes[nodeId])
            .Select(node => (ICoordinate)new Coordinate(node.X, node.Y))
            .ToList();

        if (reachableCoords.Count < 3) return null;

        // Convex Hull 생성 (단순화된 등시선)
        try
        {
            return Geoprocessing.ConvexHull(reachableCoords);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 최적 후보 위치 찾기 (최대 커버리지)
    /// </summary>
    private static ICoordinate? FindBestCandidateLocation(
        RoadNetwork network,
        List<ICoordinate> candidates,
        List<DemandPoint> demands,
        List<ICoordinate> selectedFacilities,
        HashSet<DemandPoint> coveredDemands,
        double maxDistance)
    {
        var bestCandidate = (ICoordinate?)null;
        var bestCoverage = 0.0;

        foreach (var candidate in candidates)
        {
            if (selectedFacilities.Contains(candidate)) continue;

            var newCoverage = GetCoveredDemands(network, candidate, demands, maxDistance)
                .Where(d => !coveredDemands.Contains(d))
                .Sum(d => d.Weight);

            if (newCoverage > bestCoverage)
            {
                bestCoverage = newCoverage;
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    /// <summary>
    /// 커버되는 수요 지점들 찾기
    /// </summary>
    private static IEnumerable<DemandPoint> GetCoveredDemands(
        RoadNetwork network, ICoordinate facility, IEnumerable<DemandPoint> demands, double maxDistance)
    {
        var facilityNode = FindNearestNode(network, facility);
        if (facilityNode == null) yield break;

        foreach (var demand in demands)
        {
            var demandNode = FindNearestNode(network, demand.Location);
            if (demandNode == null) continue;

            var path = NetworkAnalysis.FindShortestPath(network, facilityNode, demandNode);
            if (path != null && path.TotalDistance <= maxDistance)
            {
                yield return demand;
            }
        }
    }

    /// <summary>
    /// 거리 매트릭스 계산
    /// </summary>
    private static double[,] CalculateDistanceMatrix(
        RoadNetwork network, List<ICoordinate> candidates, List<DemandPoint> demands)
    {
        var matrix = new double[candidates.Count, demands.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidateNode = FindNearestNode(network, candidates[i]);
            
            for (int j = 0; j < demands.Count; j++)
            {
                var demandNode = FindNearestNode(network, demands[j].Location);
                
                if (candidateNode != null && demandNode != null)
                {
                    var path = NetworkAnalysis.FindShortestPath(network, candidateNode, demandNode);
                    matrix[i, j] = path?.TotalDistance ?? double.MaxValue;
                }
                else
                {
                    matrix[i, j] = double.MaxValue;
                }
            }
        }

        return matrix;
    }

    /// <summary>
    /// P-Median 최적 후보 찾기
    /// </summary>
    private static int FindBestPMedianCandidate(
        double[,] distanceMatrix, List<int> selectedFacilities, int numDemands)
    {
        var numCandidates = distanceMatrix.GetLength(0);
        var bestCandidate = -1;
        var bestCost = double.MaxValue;

        for (int i = 0; i < numCandidates; i++)
        {
            if (selectedFacilities.Contains(i)) continue;

            var totalCost = 0.0;
            var tempSelected = new List<int>(selectedFacilities) { i };

            for (int j = 0; j < numDemands; j++)
            {
                var minDistance = tempSelected.Select(f => distanceMatrix[f, j]).Min();
                totalCost += minDistance;
            }

            if (totalCost < bestCost)
            {
                bestCost = totalCost;
                bestCandidate = i;
            }
        }

        return bestCandidate;
    }

    /// <summary>
    /// 접근성 점수 계산
    /// </summary>
    private static double CalculateAccessibilityScore(double nearestDistance, int accessibleCount)
    {
        // 거리 기반 점수 (가까울수록 높음) + 선택지 기반 점수
        var distanceScore = Math.Max(0, 100 - nearestDistance / 1000); // 1km당 1점 감점
        var choiceScore = Math.Min(50, accessibleCount * 5); // 접근 가능한 시설 수 × 5점
        
        return distanceScore + choiceScore;
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// 이동 모드
/// </summary>
public enum TravelMode
{
    Driving,
    Walking,
    PublicTransit,
    Cycling
}

/// <summary>
/// 등시선 생성 결과
/// </summary>
public class IsochroneResult
{
    public List<IsochroneRing> Isochrones { get; set; } = new();
    public List<ICoordinate> StartPoints { get; set; } = new();
    public TravelMode TravelMode { get; set; }
}

/// <summary>
/// 등시선 링
/// </summary>
public class IsochroneRing
{
    public int TimeMinutes { get; set; }
    public Polygon? Polygon { get; set; }
    public List<string> ReachableNodes { get; set; } = new();
}

/// <summary>
/// 수요 지점
/// </summary>
public class DemandPoint
{
    public ICoordinate Location { get; set; } = null!;
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Attributes { get; set; } = new();
}

/// <summary>
/// 시설 배치 최적화 결과
/// </summary>
public class FacilityLocationResult
{
    public List<ICoordinate> SelectedFacilities { get; set; } = new();
    public List<DemandPoint> CoveredDemands { get; set; } = new();
    public double TotalCoverage { get; set; }
    public double CoveragePercentage { get; set; }
}

/// <summary>
/// P-Median 결과
/// </summary>
public class PMedianResult
{
    public List<ICoordinate> SelectedFacilities { get; set; } = new();
    public Dictionary<int, int> Assignments { get; set; } = new(); // 수요점 -> 시설
    public double TotalCost { get; set; }
    public double AverageCost { get; set; }
}

/// <summary>
/// 접근성 분석 결과
/// </summary>
public class AccessibilityAnalysis
{
    public List<ICoordinate> Facilities { get; set; } = new();
    public List<ICoordinate> EvaluationPoints { get; set; } = new();
    public Dictionary<ICoordinate, AccessibilityMetrics> AccessibilityScores { get; set; } = new();
    public double OverallAccessibility { get; set; }
}

/// <summary>
/// 접근성 지표
/// </summary>
public class AccessibilityMetrics
{
    public double NearestFacilityDistance { get; set; }
    public double NearestFacilityTime { get; set; }
    public double AverageDistance { get; set; }
    public double AverageTime { get; set; }
    public int AccessibleFacilities { get; set; }
    public double AccessibilityScore { get; set; }
}

#endregion