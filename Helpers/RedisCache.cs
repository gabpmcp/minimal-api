using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace MinimalApi.Helpers {
    public class RedisCache<TKey>
    {
        private readonly IDatabase _database;

        public RedisCache(string connectionString)
        {
            var redis = ConnectionMultiplexer.Connect(connectionString);
            _database = redis.GetDatabase();
        }

        public (bool hasValue, string value) GetDistributedValue(TKey key)
        {
            var value = _database.StringGet(key.ToString());
            return (value.HasValue, value);
        }

        public async Task<bool> SetDistributedValue(TKey key, string value)
        {
            return await _database.StringSetAsync(key.ToString(), value);
        }

        public async Task<bool> RemoveDistributedValue(TKey key)
        {
            return await _database.KeyDeleteAsync(key.ToString());
        }
    }
}
