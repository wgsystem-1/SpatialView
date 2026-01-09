using SpatialView.Engine.Geometry;
using System.Collections.Concurrent;

namespace SpatialView.Engine.Analysis;

/// <summary>
/// 네트워크 분석 모듈
/// 최단 경로 찾기, 도로망 분석 등을 수행
/// </summary>
public static class NetworkAnalysis
{
    /// <summary>
    /// Dijkstra 알고리즘을 사용한 최단 경로 찾기
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="start">출발 노드 ID</param>
    /// <param name="end">도착 노드 ID</param>
    /// <returns>최단 경로 (노드 ID 순서)</returns>
    public static PathResult? FindShortestPath(RoadNetwork network, string start, string end)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (!network.Nodes.ContainsKey(start) || !network.Nodes.ContainsKey(end))
            return null;

        var distances = new Dictionary<string, double>();
        var previous = new Dictionary<string, string>();
        var unvisited = new HashSet<string>();

        // 초기화
        foreach (var nodeId in network.Nodes.Keys)
        {
            distances[nodeId] = double.MaxValue;
            unvisited.Add(nodeId);
        }
        distances[start] = 0;

        while (unvisited.Count > 0)
        {
            // 가장 가까운 노드 선택
            var current = unvisited.OrderBy(n => distances[n]).First();
            unvisited.Remove(current);

            if (current == end) break;
            if (distances[current] == double.MaxValue) break;

            // 인접 노드들 검사
            if (network.Adjacency.TryGetValue(current, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (!unvisited.Contains(edge.ToNodeId)) continue;

                    var alternateDist = distances[current] + edge.Weight;
                    if (alternateDist < distances[edge.ToNodeId])
                    {
                        distances[edge.ToNodeId] = alternateDist;
                        previous[edge.ToNodeId] = current;
                    }
                }
            }
        }

        // 경로 재구성
        if (!previous.ContainsKey(end)) return null;

        var path = new List<string>();
        var currentNode = end;
        while (currentNode != start)
        {
            path.Add(currentNode);
            currentNode = previous[currentNode];
        }
        path.Add(start);
        path.Reverse();

        return new PathResult
        {
            Path = path,
            TotalDistance = distances[end],
            TotalTime = CalculateTravelTime(network, path)
        };
    }

    /// <summary>
    /// A* 알고리즘을 사용한 최단 경로 찾기
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="start">출발 노드 ID</param>
    /// <param name="end">도착 노드 ID</param>
    /// <returns>최단 경로</returns>
    public static PathResult? FindShortestPathAStar(RoadNetwork network, string start, string end)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (!network.Nodes.ContainsKey(start) || !network.Nodes.ContainsKey(end))
            return null;

        var openSet = new HashSet<string> { start };
        var cameFrom = new Dictionary<string, string>();
        var gScore = new Dictionary<string, double>();
        var fScore = new Dictionary<string, double>();

        // 초기화
        foreach (var nodeId in network.Nodes.Keys)
        {
            gScore[nodeId] = double.MaxValue;
            fScore[nodeId] = double.MaxValue;
        }
        gScore[start] = 0;
        fScore[start] = HeuristicDistance(network.Nodes[start], network.Nodes[end]);

        while (openSet.Count > 0)
        {
            // f 값이 가장 낮은 노드 선택
            var current = openSet.OrderBy(n => fScore[n]).First();
            
            if (current == end)
            {
                // 경로 재구성
                var path = ReconstructPath(cameFrom, current);
                return new PathResult
                {
                    Path = path,
                    TotalDistance = gScore[end],
                    TotalTime = CalculateTravelTime(network, path)
                };
            }

            openSet.Remove(current);

            // 인접 노드들 검사
            if (network.Adjacency.TryGetValue(current, out var edges))
            {
                foreach (var edge in edges)
                {
                    var neighbor = edge.ToNodeId;
                    var tentativeGScore = gScore[current] + edge.Weight;

                    if (tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = gScore[neighbor] + 
                            HeuristicDistance(network.Nodes[neighbor], network.Nodes[end]);

                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null; // 경로를 찾을 수 없음
    }

    /// <summary>
    /// 여러 목적지에 대한 최단 경로 찾기
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="start">출발 노드</param>
    /// <param name="destinations">목적지 노드들</param>
    /// <returns>각 목적지별 최단 경로</returns>
    public static Dictionary<string, PathResult?> FindMultipleShortestPaths(
        RoadNetwork network, string start, IEnumerable<string> destinations)
    {
        var results = new Dictionary<string, PathResult?>();
        
        foreach (var dest in destinations)
        {
            results[dest] = FindShortestPath(network, start, dest);
        }

        return results;
    }

    /// <summary>
    /// 네트워크의 연결성 분석
    /// </summary>
    /// <param name="network">분석할 도로망</param>
    /// <returns>연결성 분석 결과</returns>
    public static ConnectivityAnalysis AnalyzeConnectivity(RoadNetwork network)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));

        var components = FindConnectedComponents(network);
        var deadEnds = FindDeadEnds(network);
        var bridges = FindBridges(network);
        var articulationPoints = FindArticulationPoints(network);

        return new ConnectivityAnalysis
        {
            ConnectedComponents = components,
            DeadEnds = deadEnds,
            Bridges = bridges,
            ArticulationPoints = articulationPoints,
            IsFullyConnected = components.Count == 1
        };
    }

    /// <summary>
    /// 서비스 영역 계산 (특정 거리/시간 내 도달 가능한 영역)
    /// </summary>
    /// <param name="network">도로망</param>
    /// <param name="startNode">시작 노드</param>
    /// <param name="maxDistance">최대 거리</param>
    /// <param name="maxTime">최대 시간 (초)</param>
    /// <returns>서비스 영역에 포함된 노드들</returns>
    public static ServiceArea CalculateServiceArea(
        RoadNetwork network, string startNode, double? maxDistance = null, double? maxTime = null)
    {
        if (network == null) throw new ArgumentNullException(nameof(network));
        if (!network.Nodes.ContainsKey(startNode)) 
            throw new ArgumentException("Start node not found", nameof(startNode));

        var reachableNodes = new HashSet<string>();
        var distances = new Dictionary<string, double>();
        var times = new Dictionary<string, double>();
        var unvisited = new HashSet<string>(network.Nodes.Keys);

        // 초기화
        foreach (var nodeId in network.Nodes.Keys)
        {
            distances[nodeId] = double.MaxValue;
            times[nodeId] = double.MaxValue;
        }
        distances[startNode] = 0;
        times[startNode] = 0;

        while (unvisited.Count > 0)
        {
            var current = unvisited.OrderBy(n => distances[n]).FirstOrDefault();
            if (current == null || distances[current] == double.MaxValue) break;

            unvisited.Remove(current);

            // 제약 조건 확인
            bool withinDistance = maxDistance == null || distances[current] <= maxDistance.Value;
            bool withinTime = maxTime == null || times[current] <= maxTime.Value;

            if (withinDistance && withinTime)
            {
                reachableNodes.Add(current);
            }

            // 인접 노드 검사
            if (network.Adjacency.TryGetValue(current, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (!unvisited.Contains(edge.ToNodeId)) continue;

                    var newDistance = distances[current] + edge.Weight;
                    var newTime = times[current] + CalculateEdgeTravelTime(edge);

                    // 제약 조건을 만족하는 경우만 업데이트
                    bool distanceOk = maxDistance == null || newDistance <= maxDistance.Value;
                    bool timeOk = maxTime == null || newTime <= maxTime.Value;

                    if (distanceOk && timeOk && newDistance < distances[edge.ToNodeId])
                    {
                        distances[edge.ToNodeId] = newDistance;
                        times[edge.ToNodeId] = newTime;
                    }
                }
            }
        }

        return new ServiceArea
        {
            CenterNode = startNode,
            ReachableNodes = reachableNodes.ToList(),
            MaxDistance = maxDistance,
            MaxTime = maxTime
        };
    }

    #region Private Helper Methods

    /// <summary>
    /// 휴리스틱 거리 계산 (직선 거리)
    /// </summary>
    private static double HeuristicDistance(NetworkNode node1, NetworkNode node2)
    {
        var dx = node1.X - node2.X;
        var dy = node1.Y - node2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 경로 재구성
    /// </summary>
    private static List<string> ReconstructPath(Dictionary<string, string> cameFrom, string current)
    {
        var path = new List<string> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// 경로의 총 이동 시간 계산
    /// </summary>
    private static double CalculateTravelTime(RoadNetwork network, List<string> path)
    {
        double totalTime = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var fromNode = path[i];
            var toNode = path[i + 1];
            
            if (network.Adjacency.TryGetValue(fromNode, out var edges))
            {
                var edge = edges.FirstOrDefault(e => e.ToNodeId == toNode);
                if (edge != null)
                {
                    totalTime += CalculateEdgeTravelTime(edge);
                }
            }
        }
        return totalTime;
    }

    /// <summary>
    /// 엣지의 이동 시간 계산
    /// </summary>
    private static double CalculateEdgeTravelTime(NetworkEdge edge)
    {
        // 거리(m) / 속도(km/h) * 3600(초/시간) / 1000(m/km)
        return edge.Weight / (edge.SpeedLimit > 0 ? edge.SpeedLimit : 50) * 3.6;
    }

    /// <summary>
    /// 연결된 컴포넌트 찾기 (DFS 기반)
    /// </summary>
    private static List<List<string>> FindConnectedComponents(RoadNetwork network)
    {
        var visited = new HashSet<string>();
        var components = new List<List<string>>();

        foreach (var nodeId in network.Nodes.Keys)
        {
            if (!visited.Contains(nodeId))
            {
                var component = new List<string>();
                DFS(network, nodeId, visited, component);
                components.Add(component);
            }
        }

        return components;
    }

    /// <summary>
    /// 깊이 우선 탐색
    /// </summary>
    private static void DFS(RoadNetwork network, string nodeId, HashSet<string> visited, List<string> component)
    {
        visited.Add(nodeId);
        component.Add(nodeId);

        if (network.Adjacency.TryGetValue(nodeId, out var edges))
        {
            foreach (var edge in edges)
            {
                if (!visited.Contains(edge.ToNodeId))
                {
                    DFS(network, edge.ToNodeId, visited, component);
                }
            }
        }
    }

    /// <summary>
    /// 막다른 길 찾기
    /// </summary>
    private static List<string> FindDeadEnds(RoadNetwork network)
    {
        var deadEnds = new List<string>();

        foreach (var nodeId in network.Nodes.Keys)
        {
            var degree = 0;
            
            // 출차 엣지 수
            if (network.Adjacency.TryGetValue(nodeId, out var outEdges))
                degree += outEdges.Count;

            // 입차 엣지 수
            foreach (var kvp in network.Adjacency)
            {
                degree += kvp.Value.Count(e => e.ToNodeId == nodeId);
            }

            if (degree == 1)
                deadEnds.Add(nodeId);
        }

        return deadEnds;
    }

    /// <summary>
    /// 브릿지 엣지 찾기 (제거 시 네트워크가 분리되는 엣지)
    /// </summary>
    private static List<NetworkEdge> FindBridges(RoadNetwork network)
    {
        var bridges = new List<NetworkEdge>();
        
        // 각 엣지를 임시로 제거하고 연결성 확인
        foreach (var kvp in network.Adjacency)
        {
            var fromNode = kvp.Key;
            foreach (var edge in kvp.Value)
            {
                // 엣지 임시 제거
                var originalComponents = FindConnectedComponents(network);
                
                // 엣지를 제거한 임시 네트워크 생성
                var tempNetwork = CloneNetworkWithoutEdge(network, fromNode, edge);
                var newComponents = FindConnectedComponents(tempNetwork);
                
                // 컴포넌트 수가 증가했으면 브릿지
                if (newComponents.Count > originalComponents.Count)
                {
                    bridges.Add(edge);
                }
            }
        }

        return bridges;
    }

    /// <summary>
    /// 관절점 찾기 (제거 시 네트워크가 분리되는 노드)
    /// </summary>
    private static List<string> FindArticulationPoints(RoadNetwork network)
    {
        var articulationPoints = new List<string>();
        var originalComponents = FindConnectedComponents(network);

        foreach (var nodeId in network.Nodes.Keys)
        {
            // 노드를 임시로 제거한 네트워크 생성
            var tempNetwork = CloneNetworkWithoutNode(network, nodeId);
            var newComponents = FindConnectedComponents(tempNetwork);

            // 컴포넌트 수가 증가했으면 관절점
            if (newComponents.Count > originalComponents.Count)
            {
                articulationPoints.Add(nodeId);
            }
        }

        return articulationPoints;
    }

    /// <summary>
    /// 특정 엣지를 제거한 네트워크 복사본 생성
    /// </summary>
    private static RoadNetwork CloneNetworkWithoutEdge(RoadNetwork original, string fromNode, NetworkEdge edgeToRemove)
    {
        var clone = new RoadNetwork
        {
            Nodes = new Dictionary<string, NetworkNode>(original.Nodes),
            Adjacency = new Dictionary<string, List<NetworkEdge>>()
        };

        foreach (var kvp in original.Adjacency)
        {
            var edges = kvp.Value.Where(e => !(kvp.Key == fromNode && e.Equals(edgeToRemove))).ToList();
            clone.Adjacency[kvp.Key] = edges;
        }

        return clone;
    }

    /// <summary>
    /// 특정 노드를 제거한 네트워크 복사본 생성
    /// </summary>
    private static RoadNetwork CloneNetworkWithoutNode(RoadNetwork original, string nodeToRemove)
    {
        var clone = new RoadNetwork
        {
            Nodes = original.Nodes.Where(kvp => kvp.Key != nodeToRemove).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Adjacency = new Dictionary<string, List<NetworkEdge>>()
        };

        foreach (var kvp in original.Adjacency)
        {
            if (kvp.Key == nodeToRemove) continue;
            
            var edges = kvp.Value.Where(e => e.ToNodeId != nodeToRemove).ToList();
            clone.Adjacency[kvp.Key] = edges;
        }

        return clone;
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// 도로망 데이터 모델
/// </summary>
public class RoadNetwork
{
    /// <summary>
    /// 네트워크 노드들 (교차점, 끝점 등)
    /// </summary>
    public Dictionary<string, NetworkNode> Nodes { get; set; } = new();

    /// <summary>
    /// 인접성 리스트 (노드 간 연결 정보)
    /// </summary>
    public Dictionary<string, List<NetworkEdge>> Adjacency { get; set; } = new();

    /// <summary>
    /// 노드 추가
    /// </summary>
    public void AddNode(string id, double x, double y, Dictionary<string, object>? attributes = null)
    {
        Nodes[id] = new NetworkNode
        {
            Id = id,
            X = x,
            Y = y,
            Attributes = attributes ?? new Dictionary<string, object>()
        };

        if (!Adjacency.ContainsKey(id))
            Adjacency[id] = new List<NetworkEdge>();
    }

    /// <summary>
    /// 엣지 추가
    /// </summary>
    public void AddEdge(string fromId, string toId, double weight, 
        double speedLimit = 50, bool bidirectional = true, Dictionary<string, object>? attributes = null)
    {
        if (!Nodes.ContainsKey(fromId) || !Nodes.ContainsKey(toId))
            throw new ArgumentException("Both nodes must exist before adding edge");

        var edge = new NetworkEdge
        {
            FromNodeId = fromId,
            ToNodeId = toId,
            Weight = weight,
            SpeedLimit = speedLimit,
            Attributes = attributes ?? new Dictionary<string, object>()
        };

        Adjacency[fromId].Add(edge);

        if (bidirectional)
        {
            var reverseEdge = new NetworkEdge
            {
                FromNodeId = toId,
                ToNodeId = fromId,
                Weight = weight,
                SpeedLimit = speedLimit,
                Attributes = attributes ?? new Dictionary<string, object>()
            };
            Adjacency[toId].Add(reverseEdge);
        }
    }

    /// <summary>
    /// LineString으로부터 네트워크 생성
    /// </summary>
    public static RoadNetwork FromLineStrings(IEnumerable<LineString> roads, 
        Func<LineString, double>? speedLimitExtractor = null)
    {
        var network = new RoadNetwork();
        var nodeIndex = 0;

        foreach (var road in roads)
        {
            var coords = road.Coordinates.ToList();
            var speedLimit = speedLimitExtractor?.Invoke(road) ?? 50.0;

            for (int i = 0; i < coords.Count; i++)
            {
                var nodeId = $"node_{nodeIndex++}";
                network.AddNode(nodeId, coords[i].X, coords[i].Y);

                if (i > 0)
                {
                    var prevNodeId = $"node_{nodeIndex - 2}";
                    var distance = Math.Sqrt(
                        Math.Pow(coords[i].X - coords[i - 1].X, 2) +
                        Math.Pow(coords[i].Y - coords[i - 1].Y, 2));
                    
                    network.AddEdge(prevNodeId, nodeId, distance, speedLimit);
                }
            }
        }

        return network;
    }
}

/// <summary>
/// 네트워크 노드
/// </summary>
public class NetworkNode
{
    public string Id { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
}

/// <summary>
/// 네트워크 엣지
/// </summary>
public class NetworkEdge
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public double Weight { get; set; } // 거리 또는 비용
    public double SpeedLimit { get; set; } = 50; // km/h
    public Dictionary<string, object> Attributes { get; set; } = new();

    public override bool Equals(object? obj)
    {
        if (obj is NetworkEdge other)
        {
            return FromNodeId == other.FromNodeId && ToNodeId == other.ToNodeId;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FromNodeId, ToNodeId);
    }
}

/// <summary>
/// 경로 찾기 결과
/// </summary>
public class PathResult
{
    public List<string> Path { get; set; } = new();
    public double TotalDistance { get; set; }
    public double TotalTime { get; set; } // 초 단위
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 연결성 분석 결과
/// </summary>
public class ConnectivityAnalysis
{
    public List<List<string>> ConnectedComponents { get; set; } = new();
    public List<string> DeadEnds { get; set; } = new();
    public List<NetworkEdge> Bridges { get; set; } = new();
    public List<string> ArticulationPoints { get; set; } = new();
    public bool IsFullyConnected { get; set; }
}

/// <summary>
/// 서비스 영역 분석 결과
/// </summary>
public class ServiceArea
{
    public string CenterNode { get; set; } = string.Empty;
    public List<string> ReachableNodes { get; set; } = new();
    public double? MaxDistance { get; set; }
    public double? MaxTime { get; set; }
}

#endregion