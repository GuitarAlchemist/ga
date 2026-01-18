namespace GA.AI.Service.Services;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public interface ICachingService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory);
}

public class CachingService : ICachingService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachingService> _logger;

    public CachingService(IDistributedCache cache, ILogger<CachingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _cache.GetStringAsync(key);
        if (value == null) return default;
        return JsonSerializer.Deserialize<T>(value);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry;
        }
        var json = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(key, json, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
    }

    public async Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory)
    {
        var cached = await GetAsync<T>(key);
        if (cached != null)
        {
            return cached;
        }

        var value = await factory();
        await SetAsync(key, value);
        return value;
    }
}
