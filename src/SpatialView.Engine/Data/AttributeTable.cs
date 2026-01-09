using System.Collections;

namespace SpatialView.Engine.Data;

/// <summary>
/// 속성 테이블 기본 구현
/// </summary>
public class AttributeTable : IAttributeTable
{
    private readonly Dictionary<string, object?> _attributes;
    
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public AttributeTable()
    {
        _attributes = new Dictionary<string, object?>();
    }
    
    /// <summary>
    /// 기존 딕셔너리로부터 생성
    /// </summary>
    public AttributeTable(IDictionary<string, object?> attributes)
    {
        _attributes = new Dictionary<string, object?>(attributes);
    }
    
    /// <inheritdoc/>
    public ICollection<string> AttributeNames => _attributes.Keys;
    
    /// <inheritdoc/>
    public ICollection<object?> Values => _attributes.Values;
    
    /// <inheritdoc/>
    public int Count => _attributes.Count;
    
    /// <inheritdoc/>
    public object? this[string attributeName]
    {
        get => _attributes.TryGetValue(attributeName, out var value) ? value : null;
        set => _attributes[attributeName] = value;
    }
    
    /// <inheritdoc/>
    public object? this[int index]
    {
        get
        {
            if (index < 0 || index >= _attributes.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _attributes.Values.ElementAt(index);
        }
        set
        {
            if (index < 0 || index >= _attributes.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            var key = _attributes.Keys.ElementAt(index);
            _attributes[key] = value;
        }
    }
    
    /// <inheritdoc/>
    public bool Exists(string attributeName)
    {
        return _attributes.ContainsKey(attributeName);
    }
    
    /// <inheritdoc/>
    public void Add(string attributeName, object? value)
    {
        _attributes[attributeName] = value;
    }
    
    /// <inheritdoc/>
    public bool Remove(string attributeName)
    {
        return _attributes.Remove(attributeName);
    }
    
    /// <inheritdoc/>
    public void Clear()
    {
        _attributes.Clear();
    }
    
    /// <inheritdoc/>
    public T? GetValue<T>(string attributeName)
    {
        if (_attributes.TryGetValue(attributeName, out var value))
        {
            if (value is T typedValue)
                return typedValue;
            
            // 타입 변환 시도
            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
        return default(T);
    }
    
    /// <inheritdoc/>
    public T GetValue<T>(string attributeName, T defaultValue)
    {
        var value = GetValue<T>(attributeName);
        return value ?? defaultValue;
    }
    
    /// <inheritdoc/>
    public string[] GetNames()
    {
        return _attributes.Keys.ToArray();
    }
    
    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _attributes.GetEnumerator();
    }
    
    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    /// <summary>
    /// 속성 이름과 값으로 테이블 초기화
    /// </summary>
    public static AttributeTable Create(params (string name, object? value)[] attributes)
    {
        var table = new AttributeTable();
        foreach (var (name, value) in attributes)
        {
            table.Add(name, value);
        }
        return table;
    }
    
    public override string ToString()
    {
        var pairs = _attributes.Select(kv => $"{kv.Key}={kv.Value}");
        return $"AttributeTable[{string.Join(", ", pairs)}]";
    }
}