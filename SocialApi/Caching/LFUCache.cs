namespace SocialApi.Caching;

public class LFUCache<TKey, TValue> : ICache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, CacheNode> _cacheMap;
    private readonly Dictionary<int, LinkedList<TKey>> _freqMap;
    private int _minFreq;
    private readonly object _lock = new();

    public LFUCache(int capacity)
    {
        if (capacity <= 0) throw new ArgumentException("Capacity must be > 0");
        _capacity = capacity;
        _cacheMap = new Dictionary<TKey, CacheNode>();
        _freqMap = new Dictionary<int, LinkedList<TKey>>();
        _minFreq = 0;
    }

    public TValue? Get(TKey key)
    {
        lock (_lock)
        {
            if (!_cacheMap.TryGetValue(key, out var node))
            {
                return default;
            }

            UpdateFrequency(key, node);
            return node.Value;
        }
    }

    public void Put(TKey key, TValue value)
    {
        if (_capacity <= 0) return;

        lock (_lock)
        {
            if (_cacheMap.TryGetValue(key, out var node))
            {
                // Key exists: update its value and bump its access frequency
                node.Value = value;
                UpdateFrequency(key, node);
                return;
            }

            // Key does not exist: handle eviction if at capacity
            if (_cacheMap.Count >= _capacity)
            {
                EvictLeastFrequentlyUsed();
            }

            // Insert new node with an initial frequency of 1
            var newNode = new CacheNode(value, 1);
            _cacheMap[key] = newNode;
            
            _minFreq = 1; // New items always reset the baseline minimum frequency to 1
            if (!_freqMap.TryGetValue(1, out var list))
            {
                list = new LinkedList<TKey>();
                _freqMap[1] = list;
            }
            list.AddFirst(key);
        }
    }

    public void Invalidate(TKey key)
    {
        lock (_lock)
        {
            if (!_cacheMap.TryGetValue(key, out var node)) return;

            // Remove from the frequency tracking list
            if (_freqMap.TryGetValue(node.Frequency, out var list))
            {
                list.Remove(key);
                if (list.Count == 0 && node.Frequency == _minFreq)
                {
                    // Note: If the minFreq list becomes empty via invalidation, 
                    // we don't strictly need to scan for the next minFreq, because 
                    // the next "Put" operation will naturally reset _minFreq to 1.
                }
            }

            _cacheMap.Remove(key);
        }
    }

    private void UpdateFrequency(TKey key, CacheNode node)
    {
        int oldFreq = node.Frequency;
        int newFreq = oldFreq + 1;
        node.Frequency = newFreq;

        // 1. Remove key from the old frequency linked list
        if (_freqMap.TryGetValue(oldFreq, out var oldList))
        {
            oldList.Remove(key);
            // If the list becomes empty and it was the minimum frequency tier, increment minFreq
            if (oldList.Count == 0 && oldFreq == _minFreq)
            {
                _minFreq++;
            }
        }

        // 2. Add key to the new frequency linked list
        if (!_freqMap.TryGetValue(newFreq, out var newList))
        {
            newList = new LinkedList<TKey>();
            _freqMap[newFreq] = newList;
        }
        newList.AddFirst(key);
    }

    private void EvictLeastFrequentlyUsed()
    {
        // Find the linked list matching the lowest active frequency count
        if (!_freqMap.TryGetValue(_minFreq, out var minFreqList) || minFreqList.Count == 0) 
            return;

        // The item at the tail of the list is the oldest item within that frequency bracket
        var oldestNode = minFreqList.Last;
        if (oldestNode != null)
        {
            TKey keyToEvict = oldestNode.Value;
            minFreqList.RemoveLast();
            _cacheMap.Remove(keyToEvict);
        }
    }

    private class CacheNode
    {
        public TValue Value { get; set; }
        public int Frequency { get; set; }

        public CacheNode(TValue value, int frequency)
        {
            Value = value;
            Frequency = frequency;
        }
    }
}