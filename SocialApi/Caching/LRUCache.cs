namespace SocialApi.Caching;

public class LRUCache<TKey, TValue> : ICache<TKey, TValue>
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new object(); // Thread safety for web concurrency

    public LRUCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentException("Capacity must be > 0");
        _capacity = capacity;
        _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _lruList = new LinkedList<CacheItem>();
    }

    public TValue Get(TKey key)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Value;
            }
            return default;
        }
    }

    public void Put(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
            }
            else if (_cacheMap.Count >= _capacity)
            {
                var last = _lruList.Last;
                _cacheMap.Remove(last.Value.Key);
                _lruList.RemoveLast();
            }

            var newNode = new LinkedListNode<CacheItem>(new CacheItem(key, value));
            _lruList.AddFirst(newNode);
            _cacheMap[key] = newNode;
        }
    }

    public void Invalidate(TKey key)
    {
        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cacheMap.Remove(key);
            }
        }
    }

    private class CacheItem
    {
        public TKey Key { get; }
        public TValue Value { get; }
        public CacheItem(TKey key, TValue value) { Key = key; Value = value; }
    }
}