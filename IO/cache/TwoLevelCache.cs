using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using StackExchange.Redis;
using Newtonsoft.Json;
using MinimalApi.Business;

namespace MinimalApi.IO.Cache {
    public class TwoLevelCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _localCache = new ConcurrentDictionary<TKey, TValue>();

        public async Task<TValue> GetAsync(
            TKey key,
            Func<Task<TValue>> factory,
            Func<TKey, (bool, string)> getDistributedValue,
            Func<TKey, string, Task<bool>> setDistributedValue,
            Func<string, TValue> deserialize,
            Func<TValue, string> serialize)
        {
            var (decision, value) = Decide.GetAsync(_localCache, getDistributedValue, key, deserialize);

            return decision switch
            {
                CacheDecision.ReturnLocal => value,
                CacheDecision.ReturnDistributed => value,
                CacheDecision.ReturnFactory => await GetFromFactoryAsync(key, factory, setDistributedValue, serialize),
                _ => throw new InvalidOperationException("Invalid cache decision")
            };
        }

        private async Task<TValue> GetFromFactoryAsync(
            TKey key,
            Func<Task<TValue>> factory,
            Func<TKey, string, Task<bool>> setDistributedValue,
            Func<TValue, string> serialize)
        {
            var newValue = await factory();
            _localCache[key] = newValue;
            await setDistributedValue(key, serialize(newValue));
            
            return newValue;
        }

        public async Task SetAsync(
            TKey key,
            TValue value,
            // IDatabase distributedCache,
            Func<TKey, string, Task<bool>> stringSetAsync,
            Func<TValue, string> serialize)
        {
            _localCache[key] = value;
            // await distributedCache.StringSetAsync(key.ToString(), serialize(value));
            await stringSetAsync(key, serialize(value));
        }

        public async Task<bool> RemoveAsync(
            TKey key,
            // IDatabase distributedCache,
            Func<TKey, Task<bool>> keyDeleteAsync
        )
        {
            _localCache.TryRemove(key, out var _);
            // return await distributedCache.KeyDeleteAsync(key.ToString());
            return await keyDeleteAsync(key);
        }
    }
}