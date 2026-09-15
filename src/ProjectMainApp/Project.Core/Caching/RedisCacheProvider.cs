using System.Text.Json;
using StackExchange.Redis;

namespace Project.Core.Caching;

public class RedisCacheProvider : ICacheProvider
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;

    public RedisCacheProvider(IConnectionMultiplexer connection)
    {
        _connection = connection;
        _database = _connection.GetDatabase();
    }

    public object? Get(string key)
    {
        var value = _database.StringGet(key);
        if (value.IsNullOrEmpty)
            return null;

        var json = value.ToString();
        return JsonSerializer.Deserialize<object>(json);
    }

    public void Set(string key, object value, TimeSpan? absoluteExpiration = null)
    {
        var json = JsonSerializer.Serialize(value);
        var expiry = absoluteExpiration ?? TimeSpan.FromMinutes(5);
        _database.StringSet(key, json, expiry);
    }

    public bool Exists(string key)
    {
        return _database.KeyExists(key);
    }

    public void Remove(string key)
    {
        _database.KeyDelete(key);
    }
}
