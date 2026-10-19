using System;
using System.Collections.Concurrent;
using Project.Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Project.Core.Caching
{
    public class InMemoryCacheProvider : ICacheProvider
    {
        private readonly IMemoryCache _memoryCache;

        public InMemoryCacheProvider()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        public object? Get(string key)
        {
            _memoryCache.TryGetValue(key, out var value);
            return value;
        }

        public void Set(string key, object value, TimeSpan? absoluteExpiration = null)
        {
            var options = new MemoryCacheEntryOptions();
            if (absoluteExpiration.HasValue)
                options.SetAbsoluteExpiration(absoluteExpiration.Value);

            _memoryCache.Set(key, value, options);
        }

        public bool Exists(string key)
        {
            return _memoryCache.TryGetValue(key, out _);
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
        }
    }
}