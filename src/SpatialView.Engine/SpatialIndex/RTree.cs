namespace SpatialView.Engine.SpatialIndex;

/// <summary>
/// R-Tree 공간 인덱스 구현
/// 대량의 공간 데이터를 효율적으로 검색하기 위한 트리 구조
/// </summary>
public class RTree<T> : ISpatialIndex<T>
{
    private readonly int _maxEntries;
    private readonly int _minEntries;
    private Node _root;
    private int _size;
    
    /// <summary>
    /// 기본 생성자 (최대 항목 수 = 10)
    /// </summary>
    public RTree() : this(10)
    {
    }
    
    /// <summary>
    /// 최대 항목 수를 지정하는 생성자
    /// </summary>
    /// <param name="maxEntries">노드당 최대 항목 수</param>
    public RTree(int maxEntries)
    {
        _maxEntries = Math.Max(4, maxEntries);
        _minEntries = Math.Max(2, (int)Math.Ceiling(_maxEntries * 0.4));
        _root = CreateNode(true);
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
        
        var entry = new Entry(envelope, item);
        Insert(entry, _root.Height);
        _size++;
    }
    
    /// <inheritdoc/>
    public bool Remove(Geometry.Envelope envelope, T item)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        
        var entry = new Entry(envelope, item);
        return Remove(entry, _root);
    }
    
    /// <inheritdoc/>
    public IList<T> Query(Geometry.Envelope searchEnvelope)
    {
        if (searchEnvelope == null) throw new ArgumentNullException(nameof(searchEnvelope));
        
        var result = new List<T>();
        if (!_root.Envelope.IsNull && _root.Envelope.Intersects(searchEnvelope))
        {
            Search(searchEnvelope, _root, result);
        }
        return result;
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        _root = CreateNode(true);
        _size = 0;
    }
    
    private void Insert(Entry entry, int level)
    {
        if (_root.IsLeaf)
        {
            InsertEntry(entry, _root);
        }
        else
        {
            var targetNode = ChooseSubtree(entry.Envelope, _root, level);
            InsertEntry(entry, targetNode);
        }
        
        if (_root.Entries.Count > _maxEntries)
        {
            var newNode = Split(_root);
            var newRoot = CreateNode(false);
            newRoot.AddChild(_root);
            newRoot.AddChild(newNode);
            _root = newRoot;
        }
    }
    
    private bool Remove(Entry entry, Node node)
    {
        if (node.IsLeaf)
        {
            for (int i = 0; i < node.Entries.Count; i++)
            {
                if (node.Entries[i].Envelope.Equals(entry.Envelope) && 
                    EqualityComparer<T>.Default.Equals(node.Entries[i].Item, entry.Item))
                {
                    node.Entries.RemoveAt(i);
                    node.UpdateEnvelope();
                    _size--;
                    return true;
                }
            }
        }
        else
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child.Envelope.Contains(entry.Envelope))
                {
                    if (Remove(entry, child))
                    {
                        if (child.Entries.Count < _minEntries)
                        {
                            // 언더플로우 처리
                            node.Children.RemoveAt(i);
                            ReInsert(child);
                        }
                        node.UpdateEnvelope();
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    private void Search(Geometry.Envelope searchEnvelope, Node node, List<T> result)
    {
        if (node.IsLeaf)
        {
            foreach (var entry in node.Entries)
            {
                if (searchEnvelope.Intersects(entry.Envelope))
                {
                    result.Add(entry.Item);
                }
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                if (searchEnvelope.Intersects(child.Envelope))
                {
                    Search(searchEnvelope, child, result);
                }
            }
        }
    }
    
    private Node ChooseSubtree(Geometry.Envelope envelope, Node node, int level)
    {
        if (node.IsLeaf || node.Height == level)
        {
            return node;
        }
        
        Node? bestChild = null;
        double minEnlargement = double.MaxValue;
        double minArea = double.MaxValue;
        
        foreach (var child in node.Children)
        {
            var enlargedEnvelope = child.Envelope.Copy();
            enlargedEnvelope.ExpandToInclude(envelope);
            var enlargement = enlargedEnvelope.Area - child.Envelope.Area;
            
            if (enlargement < minEnlargement || 
                (enlargement == minEnlargement && child.Envelope.Area < minArea))
            {
                bestChild = child;
                minEnlargement = enlargement;
                minArea = child.Envelope.Area;
            }
        }
        
        return ChooseSubtree(envelope, bestChild!, level);
    }
    
    private void InsertEntry(Entry entry, Node node)
    {
        node.Entries.Add(entry);
        node.Envelope.ExpandToInclude(entry.Envelope);
    }
    
    private Node Split(Node node)
    {
        var allEntries = new List<Entry>(node.Entries);
        var newNode = CreateNode(node.IsLeaf);
        
        // 간단한 분할 알고리즘: 중간값 기준 분할
        allEntries.Sort((a, b) => a.Envelope.Centre.X.CompareTo(b.Envelope.Centre.X));
        
        var splitIndex = allEntries.Count / 2;
        node.Entries.Clear();
        
        for (int i = 0; i < splitIndex; i++)
        {
            InsertEntry(allEntries[i], node);
        }
        
        for (int i = splitIndex; i < allEntries.Count; i++)
        {
            InsertEntry(allEntries[i], newNode);
        }
        
        return newNode;
    }
    
    private void ReInsert(Node node)
    {
        // 기존 항목들을 수집하여 최상위 레벨에서 다시 삽입
        var entries = new List<Entry>(node.Entries);
        foreach (var entry in entries)
        {
            Insert(entry, _root.Height); // 루트 레벨에서 다시 삽입
        }
    }
    
    private Node CreateNode(bool isLeaf)
    {
        return new Node(isLeaf);
    }
    
    /// <summary>
    /// R-Tree 노드
    /// </summary>
    private class Node
    {
        public bool IsLeaf { get; }
        public List<Entry> Entries { get; }
        public List<Node> Children { get; }
        public Geometry.Envelope Envelope { get; }
        public int Height { get; set; }
        
        public Node(bool isLeaf)
        {
            IsLeaf = isLeaf;
            Entries = new List<Entry>();
            Children = new List<Node>();
            Envelope = new Geometry.Envelope();
            Height = isLeaf ? 0 : 1;
        }
        
        public void AddChild(Node child)
        {
            Children.Add(child);
            Envelope.ExpandToInclude(child.Envelope);
            Height = child.Height + 1;
        }
        
        public void UpdateEnvelope()
        {
            var newEnvelope = new Geometry.Envelope();
            
            foreach (var entry in Entries)
            {
                newEnvelope.ExpandToInclude(entry.Envelope);
            }
            
            foreach (var child in Children)
            {
                newEnvelope.ExpandToInclude(child.Envelope);
            }
            
            Envelope.Init(newEnvelope);
        }
    }
    
    /// <summary>
    /// R-Tree 항목
    /// </summary>
    private class Entry
    {
        public Geometry.Envelope Envelope { get; }
        public T Item { get; }
        
        public Entry(Geometry.Envelope envelope, T item)
        {
            Envelope = envelope;
            Item = item;
        }
    }
}