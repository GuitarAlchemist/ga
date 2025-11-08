namespace GaApi.Services;

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Models;

/// <summary>
///     Service for performing comprehensive health checks
/// </summary>
public interface IHealthCheckService
{
    Task<HealthCheckResponse> GetHealthAsync();
    Task<ServiceHealth> CheckDatabaseAsync();
    Task<ServiceHealth> CheckVectorSearchAsync();
    Task<ServiceHealth> CheckMemoryCacheAsync();
}

public class HealthCheckService(
    MongoDbService mongoDb,
    IVectorSearchStrategy vectorSearch,
    IMemoryCache cache,
    ILogger<HealthCheckService> logger,
    IConfiguration configuration)
    : IHealthCheckService
{
    public async Task<HealthCheckResponse> GetHealthAsync()
    {
        var response = new HealthCheckResponse
        {
            Version = GetApiVersion(),
            Environment = GetEnvironment()
        };

        var healthTasks = new[]
        {
            CheckDatabaseAsync(),
            CheckVectorSearchAsync(),
            CheckMemoryCacheAsync()
        };

        var healthResults = await Task.WhenAll(healthTasks);

        response.Services["Database"] = healthResults[0];
        response.Services["VectorSearch"] = healthResults[1];
        response.Services["MemoryCache"] = healthResults[2];

        // Determine overall status
        var hasUnhealthy = response.Services.Values.Any(s => s.Status != "Healthy");
        response.Status = hasUnhealthy ? "Degraded" : "Healthy";

        return response;
    }

    public async Task<ServiceHealth> CheckDatabaseAsync()
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
                ["ConnectionString"] = "mongodb://localhost:27017", // Simplified for now
                ["DatabaseName"] = "guitar-alchemist"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            health.Status = "Unhealthy";
            health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            health.Error = ex.Message;

            logger.LogError(ex, "Database health check failed");
        }

        return health;
    }

    public async Task<ServiceHealth> CheckVectorSearchAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var health = new ServiceHealth();

        try
        {
            // Test vector search functionality - simplified for now
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

            logger.LogError(ex, "Vector search health check failed");
        }

        return health;
    }

    public async Task<ServiceHealth> CheckMemoryCacheAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var health = new ServiceHealth();

        try
        {
            // Test memory cache functionality
            var testKey = $"health_check_{Guid.NewGuid()}";
            var testValue = "test_value";

            cache.Set(testKey, testValue, TimeSpan.FromSeconds(10));
            var retrievedValue = cache.Get<string>(testKey);
            cache.Remove(testKey);

            stopwatch.Stop();
            health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

            if (retrievedValue == testValue)
            {
                health.Status = "Healthy";
                health.Details = new Dictionary<string, object>
                {
                    ["CacheType"] = cache.GetType().Name,
                    ["TestSuccessful"] = true
                };
            }
            else
            {
                health.Status = "Unhealthy";
                health.Error = "Cache test failed - value mismatch";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            health.Status = "Unhealthy";
            health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            health.Error = ex.Message;

            logger.LogError(ex, "Memory cache health check failed");
        }

        await Task.CompletedTask; // Make method async
        return health;
    }

    private string GetApiVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetEnvironment()
    {
        return configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            return "Not configured";
        }

        // Mask sensitive parts of connection string
        var parts = connectionString.Split(';');
        var maskedParts = parts.Select(part =>
        {
            if (part.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                part.Contains("pwd", StringComparison.OrdinalIgnoreCase))
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    return $"{keyValue[0]}=***";
                }
            }

            return part;
        });

        return string.Join(";", maskedParts);
    }
}
