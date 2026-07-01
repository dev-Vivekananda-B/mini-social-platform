using System.Collections.Concurrent;

namespace SocialApi.Caching;

public class RequestCoalescer<TKey, TValue>
{
    private readonly ConcurrentDictionary<TKey, Lazy<Task<TValue>>> _inFlight = new();

    public async Task<TValue> GetOrComputeAsync(TKey key, Func<Task<TValue>> factory)
    {
        // Only one thread will execute the factory if the key is missing.
        var lazyTask = _inFlight.GetOrAdd(key, 
            k => new Lazy<Task<TValue>>(() => factory()));

        try
        {
            return await lazyTask.Value;
        }
        finally
        {
            // Once resolved, remove it so future requests go through the standard cache flow
            _inFlight.TryRemove(key, out _);
        }
    }
}