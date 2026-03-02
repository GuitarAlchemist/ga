namespace GaApi.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using GA.Core.Functional;
using Models;

/// <summary>
///     Base class for services using monadic patterns
///     Provides common functionality for caching, logging, and error handling
/// </summary>
public abstract class MonadicServiceBase<TService>(
    ILogger<TService> logger,
    IMemoryCache? cache = null)
{
    protected readonly IMemoryCache? Cache = cache;
    protected readonly ILogger<TService> Logger = logger;

    /// <summary>
    ///     Execute operation with Try monad
    /// </summary>
    protected Try<T> Execute<T>(Func<T> operation, string operationName) =>
        Try.Of(() =>
        {
            Logger.LogDebug("Executing {Operation}", operationName);
            var result = operation();
            Logger.LogDebug("{Operation} completed successfully", operationName);
            return result;
        });

    /// <summary>
    ///     Execute async operation with Try monad
    /// </summary>
    protected async Task<Try<T>> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default) =>
        await Try.OfAsync(async () =>
        {
            Logger.LogDebug("Executing {Operation}", operationName);
            var result = await operation();
            Logger.LogDebug("{Operation} completed successfully", operationName);
            return result;
        });

    /// <summary>
    ///     Execute operation with Result monad
    /// </summary>
    protected Result<T, string> ExecuteWithResult<T>(
        Func<T> operation,
        string operationName)
    {
        try
        {
            Logger.LogDebug("Executing {Operation}", operationName);
            var result = operation();
            Logger.LogDebug("{Operation} completed successfully", operationName);
            return Result<T, string>.Success(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Operation} failed", operationName);
            return Result<T, string>.Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Execute async operation with Result monad
    /// </summary>
    protected async Task<Result<T, string>> ExecuteWithResultAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Executing {Operation}", operationName);
            var result = await operation();
            Logger.LogDebug("{Operation} completed successfully", operationName);
            return Result<T, string>.Success(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Operation} failed", operationName);
            return Result<T, string>.Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Get from cache or execute operation
    /// </summary>
    protected Option<T> GetFromCache<T>(string cacheKey)
    {
        if (Cache == null)
        {
            return Option<T>.None;
        }

        return Cache.TryGetValue(cacheKey, out T? value) && value != null
            ? Option<T>.Some(value)
            : Option<T>.None;
    }

    /// <summary>
    ///     Get from cache or execute operation with fallback
    /// </summary>
    protected async Task<T> GetOrSetCacheAsync<T>(
        string cacheKey,
        Func<Task<T>> factory,
        TimeSpan? expiration = null)
    {
        if (Cache == null)
        {
            return await factory();
        }

        if (Cache.TryGetValue(cacheKey, out T? cachedValue) && cachedValue != null)
        {
            Logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
            return cachedValue;
        }

        Logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
        var value = await factory();

        var cacheExpiration = expiration ?? TimeSpan.FromMinutes(10);
        Cache.Set(cacheKey, value, cacheExpiration);

        return value;
    }

    /// <summary>
    ///     Get from cache or execute operation with Try monad
    /// </summary>
    protected async Task<Try<T>> GetOrSetCacheWithTryAsync<T>(
        string cacheKey,
        Func<Task<T>> factory,
        TimeSpan? expiration = null) =>
        await Try.OfAsync(async () => { return await GetOrSetCacheAsync(cacheKey, factory, expiration); });

    /// <summary>
    ///     Get from cache or execute operation with Result monad
    /// </summary>
    protected async Task<Result<T, string>> GetOrSetCacheWithResultAsync<T>(
        string cacheKey,
        Func<Task<T>> factory,
        TimeSpan? expiration = null)
    {
        try
        {
            var value = await GetOrSetCacheAsync(cacheKey, factory, expiration);
            return Result<T, string>.Success(value);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get or set cache for key: {CacheKey}", cacheKey);
            return Result<T, string>.Failure(ex.Message);
        }
    }

    /// <summary>
    ///     Invalidate cache entry
    /// </summary>
    protected void InvalidateCache(string cacheKey)
    {
        Cache?.Remove(cacheKey);
        Logger.LogDebug("Invalidated cache for key: {CacheKey}", cacheKey);
    }

    /// <summary>
    ///     Invalidate multiple cache entries
    /// </summary>
    protected void InvalidateCache(params string[] cacheKeys)
    {
        foreach (var key in cacheKeys)
        {
            InvalidateCache(key);
        }
    }

    /// <summary>
    ///     Execute operation with logging using Writer monad
    /// </summary>
    protected Writer<LogEntry, T> ExecuteWithLogging<T>(
        Func<T> operation,
        string operationName)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var result = operation();
            var duration = DateTime.UtcNow - startTime;

            var log = new LogEntry(
                DateTime.UtcNow,
                "INFO",
                $"{operationName} completed in {duration.TotalMilliseconds}ms"
            );

            return new Writer<LogEntry, T>(result, [log]);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

            var log = new LogEntry(
                DateTime.UtcNow,
                "ERROR",
                $"{operationName} failed after {duration.TotalMilliseconds}ms: {ex.Message}"
            );

            throw new InvalidOperationException($"{operationName} failed", ex);
        }
    }

    /// <summary>
    ///     Execute operation with retry using IO monad
    /// </summary>
    protected Io<T> ExecuteWithRetry<T>(
        Func<T> operation,
        int maxAttempts = 3,
        TimeSpan? delay = null)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(1);
        return Io.Of(operation).Retry(maxAttempts, retryDelay);
    }

    /// <summary>
    ///     Execute lazy operation
    /// </summary>
    protected LazyM<T> ExecuteLazy<T>(Func<T> operation) => LazyM.Of(operation);
}

/// <summary>
///     Log entry for Writer monad
/// </summary>
public record LogEntry(DateTime Timestamp, string Level, string Message);
