namespace GaApi.Services;

/// <summary>
///     Caching service with different strategies for regular vs semantic data
/// </summary>
public interface ICachingService
{
    /// <summary>
    ///     Get or create a cached value for regular data (longer TTL, more permissive)
    /// </summary>
    Task<T> GetOrCreateRegularAsync<T>(string key, Func<Task<T>> factory);

    /// <summary>
    ///     Get or create a cached value for semantic data (shorter TTL, more selective)
    /// </summary>
    Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory);

    /// <summary>
    ///     Remove a cached value
    /// </summary>
    void Remove(string key);

    /// <summary>
    ///     Clear all cached values
    /// </summary>
    void Clear();

    /// <summary>
    ///     Get cache statistics
    /// </summary>
    CacheStatistics GetStatistics();
}