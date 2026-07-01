namespace SocialApi.Caching;

public interface ICache<TKey, TValue>
{
    TValue Get(TKey key);
    void Put(TKey key, TValue value);
    void Invalidate(TKey key);
}