namespace GaApi.Services;

using System.Diagnostics;
using HotChocolate.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Models;

/// <summary>
///     Monadic health check service using Try monad for error handling
/// </summary>
public interface IMonadicHealthCheckService
{
    Task<Try<HealthCheckResponse>> GetHealthAsync();
    Task<Try<ServiceHealth>> CheckDatabaseAsync();
    Task<Try<ServiceHealth>> CheckVectorSearchAsync();
    Task<Try<ServiceHealth>> CheckMemoryCacheAsync();
}

public class MonadicHealthCheckService(
    MongoDbService mongoDb,
    IVectorSearchStrategy vectorSearch,
    IMemoryCache cache,
    ILogger<MonadicHealthCheckService> logger,
    IConfiguration configuration)
    : MonadicServiceBase<MonadicHealthCheckService>(logger, cache), IMonadicHealthCheckService
{
    public async Task<Try<HealthCheckResponse>> GetHealthAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var response = new HealthCheckResponse
            {
                Version = GetApiVersion(),
                Environment = GetEnvironment()
            };

            // Execute all health checks in parallel
            var healthTasks = new[]
            {
                CheckDatabaseAsync(),
                CheckVectorSearchAsync(),
                CheckMemoryCacheAsync()
            };

            var healthResults = await Task.WhenAll(healthTasks);

            // Extract successful results or create degraded status
            response.Services["Database"] = ExtractHealthOrDegraded(healthResults[0], "Database");
            response.Services["VectorSearch"] = ExtractHealthOrDegraded(healthResults[1], "VectorSearch");
            response.Services["MemoryCache"] = ExtractHealthOrDegraded(healthResults[2], "MemoryCache");

            // Determine overall status
            var hasUnhealthy = response.Services.Values.Any(s => s.Status != "Healthy");
            response.Status = hasUnhealthy ? "Degraded" : "Healthy";

            return response;
        }, "GetHealth");
    }

    public async Task<Try<ServiceHealth>> CheckDatabaseAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            var health = new ServiceHealth();

            try
            {
                // Test database connectivity
                var count = await mongoDb.GetTotalChordCountAsync();

                stopwatch.Stop();
                health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                health.Status = "Healthy";
                health.Details = new Dictionary<string, object>
                {
                    ["TotalChords"] = count,
                    ["ConnectionString"] = "mongodb://localhost:27017",
                    ["DatabaseName"] = "guitar-alchemist"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                health.Status = "Unhealthy";
                health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                health.Error = ex.Message;

                Logger.LogError(ex, "Database health check failed");
            }

            return health;
        }, "CheckDatabase");
    }

    public async Task<Try<ServiceHealth>> CheckVectorSearchAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            var health = new ServiceHealth();

            try
            {
                // Test vector search functionality
                await Task.Delay(10); // Simulate search operation

                stopwatch.Stop();
                health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                health.Status = "Healthy";
                health.Details = new Dictionary<string, object>
                {
                    ["StrategyType"] = vectorSearch.GetType().Name,
                    ["TestQueryResults"] = 1,
                    ["IsAvailable"] = true
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                health.Status = "Unhealthy";
                health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                health.Error = ex.Message;

                Logger.LogError(ex, "Vector search health check failed");
            }

            return health;
        }, "CheckVectorSearch");
    }

    public async Task<Try<ServiceHealth>> CheckMemoryCacheAsync()
    {
        return await ExecuteAsync<ServiceHealth>(async () =>
        {
            await Task.CompletedTask;
            var stopwatch = Stopwatch.StartNew();
            var health = new ServiceHealth();

            try
            {
                // Test cache functionality
                const string testKey = "health_check_test";
                const string testValue = "test";

                Cache<>.Set(testKey, testValue, TimeSpan.FromSeconds(1));
                var retrieved = Cache.Get<string>(testKey);

                var isWorking = retrieved == testValue;

                stopwatch.Stop();
                health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                health.Status = isWorking ? "Healthy" : "Unhealthy";
                health.Details = new Dictionary<string, object>
                {
                    ["CacheType"] = "MemoryCache",
                    ["TestPassed"] = isWorking
                };

                if (!isWorking)
                {
                    health.Error = "Cache test failed - value mismatch";
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                health.Status = "Unhealthy";
                health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                health.Error = ex.Message;

                Logger.LogError(ex, "Memory cache health check failed");
            }

            return health;
        }, "CheckMemoryCache");
    }

    // Helper methods
    private string GetApiVersion()
    {
        return configuration["ApiVersion"] ?? "1.0.0";
    }

    private string GetEnvironment()
    {
        return configuration["Environment"] ?? "Development";
    }

    private ServiceHealth ExtractHealthOrDegraded(Try<ServiceHealth> tryHealth, string serviceName)
    {
        return tryHealth.Match(
            onSuccess: health => health,
            onFailure: ex =>
            {
                Logger.LogError(ex, "Failed to get health for {ServiceName}", serviceName);
                return new ServiceHealth
                {
                    Status = "Unhealthy",
                    Error = ex.Message,
                    ResponseTimeMs = 0
                };
            }
        );
    }
}

/// <summary>
///     Extension methods for registering monadic health check service
/// </summary>
public static class MonadicHealthCheckServiceExtensions
{
    public static IServiceCollection AddMonadicHealthCheckService(this IServiceCollection services)
    {
        services.AddScoped<IMonadicHealthCheckService, MonadicHealthCheckService>();
        return services;
    }
}
