using System.Collections.Concurrent;

namespace MinimalApi.Business {
    public enum CacheDecision
    {
        ReturnLocal,
        ReturnDistributed,
        ReturnFactory,
        SetDistributed
    }

    public static class Decide
    {
        public static (CacheDecision decision, TValue value) GetAsync<TKey, TValue>(
            ConcurrentDictionary<TKey, TValue> localCache,
            Func<TKey, (bool, string)> getDistributedValue,
            TKey key,
            Func<string, TValue> deserialize)
        {
            if (localCache.TryGetValue(key, out var localValue))
                return (CacheDecision.ReturnLocal, localValue);

            var (hasValue, distributedValue) = getDistributedValue(key);
            if (hasValue)
            {
                var value = deserialize(distributedValue);
                localCache[key] = value;
                return (CacheDecision.ReturnDistributed, value);
            }

            return (CacheDecision.ReturnFactory, default);
        }
    }
}