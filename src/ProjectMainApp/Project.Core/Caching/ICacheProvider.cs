namespace Project.Core.Caching;

public interface ICacheProvider 
{
    // Get item from cache
    object? Get(string key);

    // Set item in cache with optional expiration
    void Set(string key, object value, TimeSpan? absoluteExpiration = null);

    // Check if a key exists
    bool Exists(string key);

    // Remove item from cache
    void Remove(string key);
    
}