using System.Collections.Concurrent;

namespace SpatialView.Engine.Memory;

/// <summary>
/// 제네릭 객체 풀
/// 자주 생성/소멸되는 객체의 재사용을 통한 메모리 최적화
/// </summary>
public class ObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> _objects = new();
    private readonly Func<T> _objectGenerator;
    private readonly Action<T>? _resetAction;
    private readonly int _maxSize;
    private int _currentSize;
    
    /// <summary>
    /// 생성자
    /// </summary>
    /// <param name="objectGenerator">객체 생성 함수</param>
    /// <param name="resetAction">객체 재사용 전 리셋 액션</param>
    /// <param name="maxSize">풀의 최대 크기</param>
    public ObjectPool(Func<T> objectGenerator, Action<T>? resetAction = null, int maxSize = 100)
    {
        _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        _resetAction = resetAction;
        _maxSize = maxSize;
        _currentSize = 0;
    }
    
    /// <summary>
    /// 풀에서 객체 가져오기
    /// </summary>
    public T Rent()
    {
        if (_objects.TryTake(out T? item))
        {
            Interlocked.Decrement(ref _currentSize);
            return item;
        }
        
        return _objectGenerator();
    }
    
    /// <summary>
    /// 객체를 풀에 반환
    /// </summary>
    public void Return(T item)
    {
        if (item == null) return;
        
        _resetAction?.Invoke(item);
        
        if (_currentSize < _maxSize)
        {
            _objects.Add(item);
            Interlocked.Increment(ref _currentSize);
        }
    }
    
    /// <summary>
    /// 풀 비우기
    /// </summary>
    public void Clear()
    {
        while (_objects.TryTake(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }
    
    /// <summary>
    /// 현재 풀에 있는 객체 수
    /// </summary>
    public int Count => _currentSize;
}

/// <summary>
/// 자동 반환을 위한 풀링된 객체 래퍼
/// </summary>
public sealed class PooledObject<T> : IDisposable where T : class
{
    private readonly ObjectPool<T> _pool;
    private T? _object;
    
    public PooledObject(ObjectPool<T> pool, T obj)
    {
        _pool = pool;
        _object = obj;
    }
    
    public T Object => _object ?? throw new ObjectDisposedException(nameof(PooledObject<T>));
    
    public void Dispose()
    {
        if (_object != null)
        {
            _pool.Return(_object);
            _object = null;
        }
    }
}

/// <summary>
/// 풀링 가능한 객체 인터페이스
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// 객체를 초기 상태로 리셋
    /// </summary>
    void Reset();
}

/// <summary>
/// 메모리 풀 관리자
/// </summary>
public static class MemoryPoolManager
{
    private static readonly Dictionary<Type, object> _pools = new();
    private static readonly object _lock = new();
    
    /// <summary>
    /// 특정 타입의 객체 풀 등록
    /// </summary>
    public static void RegisterPool<T>(Func<T> generator, Action<T>? reset = null, int maxSize = 100) where T : class
    {
        lock (_lock)
        {
            _pools[typeof(T)] = new ObjectPool<T>(generator, reset, maxSize);
        }
    }
    
    /// <summary>
    /// 객체 대여
    /// </summary>
    public static T Rent<T>() where T : class, new()
    {
        var pool = GetOrCreatePool<T>();
        return pool.Rent();
    }
    
    /// <summary>
    /// 객체 반환
    /// </summary>
    public static void Return<T>(T obj) where T : class, new()
    {
        if (obj == null) return;
        
        var pool = GetOrCreatePool<T>();
        pool.Return(obj);
    }
    
    /// <summary>
    /// 자동 반환되는 객체 대여
    /// </summary>
    public static PooledObject<T> RentPooled<T>() where T : class, new()
    {
        var pool = GetOrCreatePool<T>();
        var obj = pool.Rent();
        return new PooledObject<T>(pool, obj);
    }
    
    private static ObjectPool<T> GetOrCreatePool<T>() where T : class, new()
    {
        lock (_lock)
        {
            if (!_pools.TryGetValue(typeof(T), out var pool))
            {
                Action<T>? resetAction = null;
                if (typeof(IPoolable).IsAssignableFrom(typeof(T)))
                {
                    resetAction = obj => ((IPoolable)obj).Reset();
                }
                
                pool = new ObjectPool<T>(() => new T(), resetAction);
                _pools[typeof(T)] = pool;
            }
            
            return (ObjectPool<T>)pool;
        }
    }
    
    /// <summary>
    /// 모든 풀 비우기
    /// </summary>
    public static void ClearAll()
    {
        lock (_lock)
        {
            _pools.Clear();
        }
    }
}