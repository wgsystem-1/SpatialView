using System.Diagnostics;

namespace SpatialView.Engine.Indexing;

/// <summary>
/// R-tree 공간 인덱스 구현
/// 지리공간 객체의 빠른 검색을 위한 트리 기반 인덱스
/// </summary>
/// <typeparam name="T">인덱싱할 객체 타입</typeparam>
public class RTreeIndex<T> : ISpatialIndex<T>
{
    private readonly int _maxEntries;
    private readonly int _minEntries;
    private RTreeNode? _root;
    private int _count;
    private readonly SpatialIndexStatistics _statistics;
    private readonly Stopwatch _queryTimer = new();
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="maxEntries">노드당 최대 엔트리 수 (기본값: 16)</param>
    public RTreeIndex(int maxEntries = 16)
    {
        if (maxEntries < 4) throw new ArgumentException("MaxEntries must be at least 4", nameof(maxEntries));
        
        _maxEntries = maxEntries;
        _minEntries = maxEntries / 2;
        _statistics = new SpatialIndexStatistics();
        Clear();
    }
    
    /// <inheritdoc/>
    public int Count => _count;
    
    /// <inheritdoc/>
    public Geometry.Envelope? Bounds => _root?.Envelope;
    
    /// <inheritdoc/>
    public SpatialIndexStatistics Statistics => _statistics;
    
    /// <inheritdoc/>
    public void Insert(Geometry.Envelope envelope, T item)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        if (item == null) throw new ArgumentNullException(nameof(item));
        
        var entry = new RTreeEntry(envelope, item);
        
        if (_root == null)
        {
            _root = new RTreeNode(true);
            _statistics.NodeCount = 1;
        }
        
        var splitResult = InsertEntry(_root, entry);
        
        // 루트가 분할되면 새로운 루트 생성
        if (splitResult != null)
        {
            var newRoot = new RTreeNode(false);
            newRoot.Children.Add(_root);
            newRoot.Children.Add(splitResult);
            newRoot.UpdateEnvelope();
            _root = newRoot;
            _statistics.NodeCount++;
            _statistics.Depth++;
        }
        
        _count++;
        UpdateStatistics();
    }
    
    /// <inheritdoc/>
    public bool Remove(Geometry.Envelope envelope, T item)
    {
        if (envelope == null || item == null || _root == null) return false;
        
        var entry = new RTreeEntry(envelope, item);
        var removed = RemoveEntry(_root, entry);
        
        if (removed)
        {
            _count--;
            
            // 루트가 비어있거나 자식이 하나뿐이면 축소
            if (_root.IsLeaf && _root.Entries.Count == 0)
            {
                _root = null;
                _statistics.NodeCount = 0;
                _statistics.Depth = 0;
            }
            else if (!_root.IsLeaf && _root.Children.Count == 1)
            {
                _root = _root.Children[0];
                _statistics.NodeCount--;
                _statistics.Depth--;
            }
            
            UpdateStatistics();
        }
        
        return removed;
    }
    
    /// <inheritdoc/>
    public IEnumerable<T> Query(Geometry.Envelope envelope)
    {
        if (envelope == null || _root == null) yield break;
        
        _queryTimer.Restart();
        
        foreach (var item in QueryNode(_root, envelope))
        {
            yield return item;
        }
        
        _queryTimer.Stop();
        UpdateQueryStatistics(_queryTimer.ElapsedMilliseconds);
    }
    
    /// <inheritdoc/>
    public IEnumerable<T> Query(Geometry.ICoordinate coordinate)
    {
        if (coordinate == null) yield break;
        
        var pointEnvelope = new Geometry.Envelope(coordinate.X, coordinate.X, coordinate.Y, coordinate.Y);
        
        foreach (var item in Query(pointEnvelope))
        {
            yield return item;
        }
    }
    
    /// <inheritdoc/>
    public IEnumerable<T> Query(Geometry.IGeometry geometry)
    {
        if (geometry?.Envelope == null) yield break;
        
        foreach (var item in Query(geometry.Envelope))
        {
            yield return item;
        }
    }
    
    /// <inheritdoc/>
    public T? FindNearest(Geometry.ICoordinate coordinate, double? maxDistance = null)
    {
        var nearest = FindKNearest(coordinate, 1, maxDistance).FirstOrDefault();
        return nearest;
    }
    
    /// <inheritdoc/>
    public IEnumerable<T> FindKNearest(Geometry.ICoordinate coordinate, int k, double? maxDistance = null)
    {
        if (coordinate == null || k <= 0 || _root == null) yield break;
        
        _queryTimer.Restart();
        
        var candidates = new List<(double Distance, T Item)>();
        
        // 모든 항목을 검사하여 거리 계산 (단순 구현)
        foreach (var item in GetAll())
        {
            // T가 IFeature인 경우 지오메트리에서 거리 계산
            if (item is Data.IFeature feature && feature.Geometry?.Envelope != null)
            {
                var distance = CalculateDistance(coordinate, feature.Geometry.Envelope);
                
                if (maxDistance == null || distance <= maxDistance)
                {
                    candidates.Add((distance, item));
                }
            }
        }
        
        // 거리순으로 정렬하여 K개 반환
        var result = candidates.OrderBy(c => c.Distance).Take(k).Select(c => c.Item);
        
        _queryTimer.Stop();
        UpdateQueryStatistics(_queryTimer.ElapsedMilliseconds);
        
        foreach (var item in result)
        {
            yield return item;
        }
    }
    
    /// <inheritdoc/>
    public IEnumerable<T> GetAll()
    {
        if (_root == null) yield break;
        
        foreach (var item in GetAllFromNode(_root))
        {
            yield return item;
        }
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        _root = null;
        _count = 0;
        _statistics.ItemCount = 0;
        _statistics.NodeCount = 0;
        _statistics.Depth = 0;
        _statistics.EstimatedMemoryUsage = 0;
        _statistics.QueryCount = 0;
        _statistics.AverageQueryTime = 0;
    }
    
    /// <inheritdoc/>
    public void Optimize()
    {
        if (_root == null || _count == 0) return;
        
        // 모든 엔트리를 수집
        var allEntries = new List<RTreeEntry>();
        CollectAllEntries(_root, allEntries);
        
        // 인덱스 재구성
        Clear();
        
        // 벌크 로딩을 통한 최적화된 트리 구성
        BulkLoad(allEntries);
        
        UpdateStatistics();
    }
    
    #region 내부 메서드
    
    /// <summary>
    /// 엔트리 삽입
    /// </summary>
    private RTreeNode? InsertEntry(RTreeNode node, RTreeEntry entry)
    {
        if (node.IsLeaf)
        {
            // 리프 노드에 직접 추가
            node.Entries.Add(entry);
            node.UpdateEnvelope();
            
            // 오버플로우 체크
            if (node.Entries.Count > _maxEntries)
            {
                return SplitNode(node);
            }
            
            return null;
        }
        else
        {
            // 최적의 자식 노드 선택
            var bestChild = ChooseBestChild(node, entry.Envelope);
            var splitResult = InsertEntry(bestChild, entry);
            
            if (splitResult != null)
            {
                // 자식이 분할됨
                node.Children.Add(splitResult);
                node.UpdateEnvelope();
                
                if (node.Children.Count > _maxEntries)
                {
                    return SplitNode(node);
                }
            }
            
            return null;
        }
    }
    
    /// <summary>
    /// 엔트리 제거
    /// </summary>
    private bool RemoveEntry(RTreeNode node, RTreeEntry entry)
    {
        if (node.IsLeaf)
        {
            // 리프에서 직접 제거
            for (int i = 0; i < node.Entries.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(node.Entries[i].Item, entry.Item))
                {
                    node.Entries.RemoveAt(i);
                    node.UpdateEnvelope();
                    return true;
                }
            }
            return false;
        }
        else
        {
            // 자식 노드에서 제거
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if (child.Envelope.Intersects(entry.Envelope))
                {
                    if (RemoveEntry(child, entry))
                    {
                        // 자식이 언더플로우되면 제거
                        if ((child.IsLeaf && child.Entries.Count < _minEntries) ||
                            (!child.IsLeaf && child.Children.Count < _minEntries))
                        {
                            node.Children.RemoveAt(i);
                            _statistics.NodeCount--;
                            
                            // 제거된 자식의 엔트리들을 재분배
                            RedistributeEntries(child, node);
                        }
                        
                        node.UpdateEnvelope();
                        return true;
                    }
                }
            }
            return false;
        }
    }
    
    /// <summary>
    /// 노드에서 검색
    /// </summary>
    private IEnumerable<T> QueryNode(RTreeNode node, Geometry.Envelope envelope)
    {
        if (!node.Envelope.Intersects(envelope)) yield break;
        
        if (node.IsLeaf)
        {
            foreach (var entry in node.Entries)
            {
                if (entry.Envelope.Intersects(envelope))
                {
                    yield return entry.Item;
                }
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                foreach (var item in QueryNode(child, envelope))
                {
                    yield return item;
                }
            }
        }
    }
    
    /// <summary>
    /// 노드에서 모든 항목 가져오기
    /// </summary>
    private IEnumerable<T> GetAllFromNode(RTreeNode node)
    {
        if (node.IsLeaf)
        {
            foreach (var entry in node.Entries)
            {
                yield return entry.Item;
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                foreach (var item in GetAllFromNode(child))
                {
                    yield return item;
                }
            }
        }
    }
    
    /// <summary>
    /// 최적의 자식 노드 선택
    /// </summary>
    private RTreeNode ChooseBestChild(RTreeNode node, Geometry.Envelope envelope)
    {
        RTreeNode? bestChild = null;
        double bestEnlargement = double.MaxValue;
        double bestArea = double.MaxValue;
        
        foreach (var child in node.Children)
        {
            var enlargement = child.Envelope.GetEnlargement(envelope);
            var area = child.Envelope.Area;
            
            if (enlargement < bestEnlargement || 
                (Math.Abs(enlargement - bestEnlargement) < double.Epsilon && area < bestArea))
            {
                bestChild = child;
                bestEnlargement = enlargement;
                bestArea = area;
            }
        }
        
        return bestChild!;
    }
    
    /// <summary>
    /// 노드 분할
    /// </summary>
    private RTreeNode SplitNode(RTreeNode node)
    {
        var newNode = new RTreeNode(node.IsLeaf);
        
        if (node.IsLeaf)
        {
            // 리프 노드 분할
            var entries = node.Entries.ToList();
            node.Entries.Clear();
            
            // 선형 분할 알고리즘
            LinearSplit(entries, node, newNode);
        }
        else
        {
            // 내부 노드 분할
            var children = node.Children.ToList();
            node.Children.Clear();
            
            // 자식 노드들을 두 그룹으로 분할
            LinearSplitChildren(children, node, newNode);
        }
        
        node.UpdateEnvelope();
        newNode.UpdateEnvelope();
        _statistics.NodeCount++;
        
        return newNode;
    }
    
    /// <summary>
    /// 선형 분할 알고리즘 (엔트리용)
    /// </summary>
    private void LinearSplit(List<RTreeEntry> entries, RTreeNode node1, RTreeNode node2)
    {
        if (entries.Count < 2) return;
        
        // 가장 먼 두 엔트리 찾기
        var (seed1, seed2) = FindLinearSeeds(entries);
        
        node1.Entries.Add(seed1);
        node2.Entries.Add(seed2);
        entries.Remove(seed1);
        entries.Remove(seed2);
        
        // 나머지 엔트리들을 분배
        foreach (var entry in entries)
        {
            var enlargement1 = node1.Envelope.GetEnlargement(entry.Envelope);
            var enlargement2 = node2.Envelope.GetEnlargement(entry.Envelope);
            
            if (enlargement1 < enlargement2 || 
                (Math.Abs(enlargement1 - enlargement2) < double.Epsilon && node1.Entries.Count <= node2.Entries.Count))
            {
                node1.Entries.Add(entry);
            }
            else
            {
                node2.Entries.Add(entry);
            }
        }
        
        // 최소 엔트리 수 보장
        while (node1.Entries.Count < _minEntries && node2.Entries.Count > _minEntries)
        {
            var lastEntry = node2.Entries.Last();
            node2.Entries.RemoveAt(node2.Entries.Count - 1);
            node1.Entries.Add(lastEntry);
        }
        
        while (node2.Entries.Count < _minEntries && node1.Entries.Count > _minEntries)
        {
            var lastEntry = node1.Entries.Last();
            node1.Entries.RemoveAt(node1.Entries.Count - 1);
            node2.Entries.Add(lastEntry);
        }
    }
    
    /// <summary>
    /// 자식 노드 분할
    /// </summary>
    private void LinearSplitChildren(List<RTreeNode> children, RTreeNode node1, RTreeNode node2)
    {
        if (children.Count < 2) return;
        
        // 간단한 분할: 절반씩 나누기
        var half = children.Count / 2;
        
        for (int i = 0; i < half; i++)
        {
            node1.Children.Add(children[i]);
        }
        
        for (int i = half; i < children.Count; i++)
        {
            node2.Children.Add(children[i]);
        }
    }
    
    /// <summary>
    /// 선형 시드 찾기
    /// </summary>
    private (RTreeEntry, RTreeEntry) FindLinearSeeds(List<RTreeEntry> entries)
    {
        double maxSeparation = -1;
        RTreeEntry? seed1 = null, seed2 = null;
        
        for (int i = 0; i < entries.Count - 1; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                var separation = CalculateSeparation(entries[i].Envelope, entries[j].Envelope);
                if (separation > maxSeparation)
                {
                    maxSeparation = separation;
                    seed1 = entries[i];
                    seed2 = entries[j];
                }
            }
        }
        
        return (seed1 ?? entries[0], seed2 ?? entries[1]);
    }
    
    /// <summary>
    /// 두 엔벨로프 간의 분리 정도 계산
    /// </summary>
    private double CalculateSeparation(Geometry.Envelope env1, Geometry.Envelope env2)
    {
        var combinedArea = env1.Union(env2).Area;
        var individualArea = env1.Area + env2.Area;
        return combinedArea - individualArea;
    }
    
    /// <summary>
    /// 엔트리 재분배
    /// </summary>
    private void RedistributeEntries(RTreeNode removedNode, RTreeNode parentNode)
    {
        // 제거된 노드의 엔트리들을 다른 노드들에 재분배
        var entriesToRedistribute = new List<RTreeEntry>();
        CollectAllEntries(removedNode, entriesToRedistribute);
        
        foreach (var entry in entriesToRedistribute)
        {
            InsertEntry(_root!, entry);
        }
    }
    
    /// <summary>
    /// 모든 엔트리 수집
    /// </summary>
    private void CollectAllEntries(RTreeNode node, List<RTreeEntry> entries)
    {
        if (node.IsLeaf)
        {
            entries.AddRange(node.Entries);
        }
        else
        {
            foreach (var child in node.Children)
            {
                CollectAllEntries(child, entries);
            }
        }
    }
    
    /// <summary>
    /// 벌크 로딩
    /// </summary>
    private void BulkLoad(List<RTreeEntry> entries)
    {
        if (entries.Count == 0) return;
        
        // 간단한 벌크 로딩: 정렬 후 순차 삽입
        entries.Sort((e1, e2) => e1.Envelope.MinX.CompareTo(e2.Envelope.MinX));
        
        foreach (var entry in entries)
        {
            Insert(entry.Envelope, entry.Item);
        }
    }
    
    /// <summary>
    /// 거리 계산
    /// </summary>
    private double CalculateDistance(Geometry.ICoordinate coordinate, Geometry.Envelope envelope)
    {
        var centerX = (envelope.MinX + envelope.MaxX) / 2;
        var centerY = (envelope.MinY + envelope.MaxY) / 2;
        
        var dx = coordinate.X - centerX;
        var dy = coordinate.Y - centerY;
        
        return Math.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// 통계 업데이트
    /// </summary>
    private void UpdateStatistics()
    {
        _statistics.ItemCount = _count;
        _statistics.Depth = CalculateDepth();
        _statistics.EstimatedMemoryUsage = EstimateMemoryUsage();
    }
    
    /// <summary>
    /// 검색 통계 업데이트
    /// </summary>
    private void UpdateQueryStatistics(double queryTime)
    {
        _statistics.QueryCount++;
        _statistics.AverageQueryTime = 
            ((_statistics.AverageQueryTime * (_statistics.QueryCount - 1)) + queryTime) / _statistics.QueryCount;
    }
    
    /// <summary>
    /// 트리 깊이 계산
    /// </summary>
    private int CalculateDepth()
    {
        return _root == null ? 0 : CalculateNodeDepth(_root);
    }
    
    /// <summary>
    /// 노드 깊이 계산
    /// </summary>
    private int CalculateNodeDepth(RTreeNode node)
    {
        if (node.IsLeaf) return 1;
        
        int maxDepth = 0;
        foreach (var child in node.Children)
        {
            maxDepth = Math.Max(maxDepth, CalculateNodeDepth(child));
        }
        
        return maxDepth + 1;
    }
    
    /// <summary>
    /// 메모리 사용량 추정
    /// </summary>
    private long EstimateMemoryUsage()
    {
        // 대략적인 추정치
        const long nodeSize = 64; // 노드당 대략적인 크기
        const long entrySize = 32; // 엔트리당 대략적인 크기
        
        return (_statistics.NodeCount * nodeSize) + (_count * entrySize);
    }
    
    #endregion
    
    #region 내부 클래스
    
    /// <summary>
    /// R-tree 노드
    /// </summary>
    private class RTreeNode
    {
        public List<RTreeEntry> Entries { get; }
        public List<RTreeNode> Children { get; }
        public bool IsLeaf { get; }
        public Geometry.Envelope Envelope { get; private set; }
        
        public RTreeNode(bool isLeaf)
        {
            IsLeaf = isLeaf;
            Entries = new List<RTreeEntry>();
            Children = new List<RTreeNode>();
            Envelope = new Geometry.Envelope();
        }
        
        public void UpdateEnvelope()
        {
            if (IsLeaf)
            {
                if (Entries.Count > 0)
                {
                    Envelope = Entries[0].Envelope;
                    for (int i = 1; i < Entries.Count; i++)
                    {
                        Envelope = Envelope.Union(Entries[i].Envelope);
                    }
                }
            }
            else
            {
                if (Children.Count > 0)
                {
                    Envelope = Children[0].Envelope;
                    for (int i = 1; i < Children.Count; i++)
                    {
                        Envelope = Envelope.Union(Children[i].Envelope);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// R-tree 엔트리
    /// </summary>
    private class RTreeEntry
    {
        public Geometry.Envelope Envelope { get; }
        public T Item { get; }
        
        public RTreeEntry(Geometry.Envelope envelope, T item)
        {
            Envelope = envelope;
            Item = item;
        }
    }
    
    #endregion
}