namespace SpatialView.Engine.SpatialIndex;

/// <summary>
/// Quadtree 공간 인덱스 구현
/// 포인트 데이터에 최적화된 공간 인덱스
/// </summary>
public class Quadtree<T> : ISpatialIndex<T>
{
    private readonly int _maxItems;
    private readonly int _maxDepth;
    private QuadNode _root;
    private int _size;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public Quadtree(Geometry.Envelope bounds) : this(bounds, 10, 8)
    {
    }
    
    /// <summary>
    /// 상세 설정을 포함하는 생성자
    /// </summary>
    /// <param name="bounds">전체 영역</param>
    /// <param name="maxItems">노드당 최대 항목 수</param>
    /// <param name="maxDepth">최대 트리 깊이</param>
    public Quadtree(Geometry.Envelope bounds, int maxItems, int maxDepth)
    {
        _maxItems = maxItems;
        _maxDepth = maxDepth;
        _root = new QuadNode(bounds, 0);
        _size = 0;
    }
    
    /// <inheritdoc/>
    public int Count => _size;
    
    /// <inheritdoc/>
    public bool IsEmpty => _size == 0;
    
    /// <inheritdoc/>
    public void Insert(Geometry.Envelope envelope, T item)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        
        if (!_root.Bounds.Contains(envelope))
        {
            // 트리 범위 확장
            ExpandRoot(envelope);
        }
        
        Insert(_root, envelope, item);
        _size++;
    }
    
    /// <inheritdoc/>
    public bool Remove(Geometry.Envelope envelope, T item)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        
        var removed = Remove(_root, envelope, item);
        if (removed)
        {
            _size--;
        }
        return removed;
    }
    
    /// <inheritdoc/>
    public IList<T> Query(Geometry.Envelope searchEnvelope)
    {
        if (searchEnvelope == null) throw new ArgumentNullException(nameof(searchEnvelope));
        
        var result = new List<T>();
        Query(_root, searchEnvelope, result);
        return result;
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        var bounds = _root.Bounds;
        _root = new QuadNode(bounds, 0);
        _size = 0;
    }
    
    private void Insert(QuadNode node, Geometry.Envelope envelope, T item)
    {
        if (node.IsLeaf)
        {
            node.Items.Add(new Item(envelope, item));
            
            if (node.Items.Count > _maxItems && node.Depth < _maxDepth)
            {
                Subdivide(node);
            }
        }
        else
        {
            var quadrant = GetQuadrant(node, envelope);
            if (quadrant != -1)
            {
                Insert(node.Children![quadrant], envelope, item);
            }
            else
            {
                node.Items.Add(new Item(envelope, item));
            }
        }
    }
    
    private bool Remove(QuadNode node, Geometry.Envelope envelope, T item)
    {
        // 노드의 항목 검색
        for (int i = 0; i < node.Items.Count; i++)
        {
            var nodeItem = node.Items[i];
            if (nodeItem.Envelope.Equals(envelope) && 
                EqualityComparer<T>.Default.Equals(nodeItem.Value, item))
            {
                node.Items.RemoveAt(i);
                return true;
            }
        }
        
        // 자식 노드 검색
        if (!node.IsLeaf)
        {
            foreach (var child in node.Children!)
            {
                if (child.Bounds.Intersects(envelope))
                {
                    if (Remove(child, envelope, item))
                    {
                        // 자식 노드가 비었으면 병합 고려
                        TryMerge(node);
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    private void Query(QuadNode node, Geometry.Envelope searchEnvelope, List<T> result)
    {
        if (!node.Bounds.Intersects(searchEnvelope))
        {
            return;
        }
        
        // 노드의 항목 검색
        foreach (var item in node.Items)
        {
            if (searchEnvelope.Intersects(item.Envelope))
            {
                result.Add(item.Value);
            }
        }
        
        // 자식 노드 검색
        if (!node.IsLeaf)
        {
            foreach (var child in node.Children!)
            {
                Query(child, searchEnvelope, result);
            }
        }
    }
    
    private void Subdivide(QuadNode node)
    {
        var bounds = node.Bounds;
        var midX = (bounds.MinX + bounds.MaxX) / 2;
        var midY = (bounds.MinY + bounds.MaxY) / 2;
        
        node.Children = new QuadNode[4];
        node.Children[0] = new QuadNode(
            new Geometry.Envelope(bounds.MinX, midX, bounds.MinY, midY), 
            node.Depth + 1); // SW
        node.Children[1] = new QuadNode(
            new Geometry.Envelope(midX, bounds.MaxX, bounds.MinY, midY), 
            node.Depth + 1); // SE
        node.Children[2] = new QuadNode(
            new Geometry.Envelope(bounds.MinX, midX, midY, bounds.MaxY), 
            node.Depth + 1); // NW
        node.Children[3] = new QuadNode(
            new Geometry.Envelope(midX, bounds.MaxX, midY, bounds.MaxY), 
            node.Depth + 1); // NE
        
        // 기존 항목 재분배
        var items = new List<Item>(node.Items);
        node.Items.Clear();
        
        foreach (var item in items)
        {
            Insert(node, item.Envelope, item.Value);
        }
    }
    
    private void TryMerge(QuadNode node)
    {
        if (node.IsLeaf) return;
        
        int totalItems = node.Items.Count;
        foreach (var child in node.Children!)
        {
            totalItems += CountItems(child);
        }
        
        if (totalItems <= _maxItems)
        {
            // 모든 항목 수집
            var allItems = new List<Item>(node.Items);
            CollectAllItems(node, allItems);
            
            // 자식 노드 제거
            node.Children = null;
            node.Items = allItems;
        }
    }
    
    private int CountItems(QuadNode node)
    {
        int count = node.Items.Count;
        if (!node.IsLeaf)
        {
            foreach (var child in node.Children!)
            {
                count += CountItems(child);
            }
        }
        return count;
    }
    
    private void CollectAllItems(QuadNode node, List<Item> items)
    {
        if (!node.IsLeaf)
        {
            foreach (var child in node.Children!)
            {
                items.AddRange(child.Items);
                CollectAllItems(child, items);
            }
        }
    }
    
    private int GetQuadrant(QuadNode node, Geometry.Envelope envelope)
    {
        var bounds = node.Bounds;
        var midX = (bounds.MinX + bounds.MaxX) / 2;
        var midY = (bounds.MinY + bounds.MaxY) / 2;
        
        bool inWest = envelope.MaxX <= midX;
        bool inEast = envelope.MinX >= midX;
        bool inSouth = envelope.MaxY <= midY;
        bool inNorth = envelope.MinY >= midY;
        
        if (inWest && inSouth) return 0; // SW
        if (inEast && inSouth) return 1; // SE
        if (inWest && inNorth) return 2; // NW
        if (inEast && inNorth) return 3; // NE
        
        return -1; // 여러 사분면에 걸침
    }
    
    private void ExpandRoot(Geometry.Envelope envelope)
    {
        // 새 범위 계산
        var newBounds = _root.Bounds.Copy();
        newBounds.ExpandToInclude(envelope);
        
        // 새 루트 생성
        var newRoot = new QuadNode(newBounds, 0);
        
        // 기존 트리 이동
        var oldItems = new List<Item>();
        CollectAllItems(_root, oldItems);
        oldItems.AddRange(_root.Items);
        
        _root = newRoot;
        
        // 모든 항목 재삽입
        foreach (var item in oldItems)
        {
            Insert(_root, item.Envelope, item.Value);
        }
    }
    
    /// <summary>
    /// Quadtree 노드
    /// </summary>
    private class QuadNode
    {
        public Geometry.Envelope Bounds { get; }
        public List<Item> Items { get; set; }
        public QuadNode[]? Children { get; set; }
        public int Depth { get; }
        
        public bool IsLeaf => Children == null;
        
        public QuadNode(Geometry.Envelope bounds, int depth)
        {
            Bounds = bounds;
            Items = new List<Item>();
            Depth = depth;
        }
    }
    
    /// <summary>
    /// Quadtree 항목
    /// </summary>
    private class Item
    {
        public Geometry.Envelope Envelope { get; }
        public T Value { get; }
        
        public Item(Geometry.Envelope envelope, T value)
        {
            Envelope = envelope;
            Value = value;
        }
    }
}