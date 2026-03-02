namespace GA.AI.Service.Services;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

public interface ICachingService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory);
}

public class CachingService(
    HybridCache cache, 
    ILogger<CachingService> logger) : ICachingService
{
    private readonly ILogger<CachingService> _logger = logger;

    public async Task<T?> GetAsync<T>(string key)
    {
        // HybridCache does not easily allow plain 'Get' without 'GetOrCreate'.
        // We will try our best, but a missing fallback implies nullable semantic.
        try
        {
            return await cache.GetOrCreateAsync<T?>(key, async cancel => default);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = expiry.HasValue 
            ? new HybridCacheEntryOptions { Expiration = expiry }
            : null;
            
        await cache.SetAsync(key, value, options);
    }

    public async Task RemoveAsync(string key) => await cache.RemoveAsync(key);

    public async Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory)
    {
        return await cache.GetOrCreateAsync(
            key, 
            async cancel => 
            {
                _logger.LogDebug("Semantic cache MISS for key: {Key}", key);
                return await factory();
            },
            options: new HybridCacheEntryOptions 
            { 
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            }
        );
    }
}
