namespace GA.MusicTheory.Service.Services;

using System.Diagnostics;
using GA.Core.Functional;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;

/// <summary>
///     Monadic health check service using Try monad for error handling
/// </summary>
public interface IMonadicHealthCheckService
{
    Task<Try<HealthCheckResponse>> GetHealthAsync();
    Task<Try<ServiceHealth>> CheckDatabaseAsync();
    Task<Try<ServiceHealth>> CheckMemoryCacheAsync();
}

public class MonadicHealthCheckService(
    MongoDbService mongoDb,
    IMemoryCache cache,
    ILogger<MonadicHealthCheckService> logger,
    IConfiguration configuration)
    : MonadicServiceBase<MonadicHealthCheckService>(logger, cache), IMonadicHealthCheckService
{
    public async Task<Try<HealthCheckResponse>> GetHealthAsync() =>
        await ExecuteAsync(async () =>
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
                CheckMemoryCacheAsync()
            };

            var healthResults = await Task.WhenAll(healthTasks);

            // Extract successful results or create degraded status
            response.Services["Database"] = ExtractHealthOrDegraded(healthResults[0], "Database");
            response.Services["MemoryCache"] = ExtractHealthOrDegraded(healthResults[1], "MemoryCache");

            // Determine overall status
            var hasUnhealthy = response.Services.Values.Any(s => s.Status != "Healthy");
            response.Status = hasUnhealthy ? "Degraded" : "Healthy";

            return response;
        }, "GetHealth");

    public async Task<Try<ServiceHealth>> CheckDatabaseAsync() =>
        await ExecuteAsync(async () =>
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

    public async Task<Try<ServiceHealth>> CheckMemoryCacheAsync() =>
        await ExecuteAsync<ServiceHealth>(async () =>
        {
            await Task.CompletedTask;
            var stopwatch = Stopwatch.StartNew();
            var health = new ServiceHealth();

            try
            {
                // Test cache functionality
                const string testKey = "health_check_test";
                const string testValue = "test";

                Cache?.Set(testKey, testValue, TimeSpan.FromSeconds(1));
                var retrieved = Cache?.Get<string>(testKey);

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

    // Helper methods
    private string GetApiVersion() => configuration["ApiVersion"] ?? "1.0.0";

    private string GetEnvironment() => configuration["Environment"] ?? "Development";

    private ServiceHealth ExtractHealthOrDegraded(Try<ServiceHealth> tryHealth, string serviceName) =>
        tryHealth.Match(
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
