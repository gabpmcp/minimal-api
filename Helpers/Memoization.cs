using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MinimalApi.Helpers
{
    public class Memoization
    {
        private static readonly ConcurrentDictionary<string, object> Cache = new ConcurrentDictionary<string, object>();

        public static Func<T, TResult> Memoize<T, TResult>(Func<T, TResult> func)
        {
            return arg =>
            {
                var key = $"{func.Method.Name}_{arg}";
                if (Cache.TryGetValue(key, out var cachedValue))
                {
                    return (TResult)cachedValue;
                }

                var result = func(arg);
                Cache.TryAdd(key, result);
                return result;
            };
        }

        public static Func<T, Task<TResult>> Memoize<T, TResult>(Func<T, Task<TResult>> func)
        {
            return arg =>
            {
                var key = $"{func.Method.Name}_{arg}";
                var lazyTask = Cache.GetOrAdd(key, _ => new Lazy<Task<object>>(async () => await func(arg)));

                return (Task<TResult>)lazyTask;
            };
        }
    }
}