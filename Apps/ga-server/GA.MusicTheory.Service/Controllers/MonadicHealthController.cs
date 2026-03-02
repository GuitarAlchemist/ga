namespace GA.MusicTheory.Service.Controllers;

using Services;
using Models;
using Microsoft.AspNetCore.Mvc;
using GA.Core.Functional;

/// <summary>
///     Controller demonstrating monadic health check service integration
/// </summary>
[ApiController]
[Route("api/monadic/health")]
[Produces("application/json")]
public class MonadicHealthController(
    IMonadicHealthCheckService healthCheckService,
    ILogger<MonadicHealthController> logger) : ControllerBase
{
    /// <summary>
    ///     Get overall health status using Try monad
    /// </summary>
    /// <returns>Health check response or error</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth()
    {
        var tryHealth = await healthCheckService.GetHealthAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                logger.LogError(ex, "Health check failed");
                return StatusCode(503, new HealthCheckResponse
                {
                    Status = "Unhealthy",
                    Services = new Dictionary<string, ServiceHealth>
                    {
                        ["HealthCheck"] = new()
                        {
                            Status = "Unhealthy",
                            Error = ex.Message
                        }
                    }
                });
            }
        );
    }

    /// <summary>
    ///     Check database health using Try monad
    /// </summary>
    /// <returns>Database health status or error</returns>
    [HttpGet("database")]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckDatabase()
    {
        var tryHealth = await healthCheckService.CheckDatabaseAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                logger.LogError(ex, "Database health check failed");
                return StatusCode(503, new ServiceHealth
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                });
            }
        );
    }

    /// <summary>
    ///     Check memory cache health using Try monad
    /// </summary>
    /// <returns>Memory cache health status or error</returns>
    [HttpGet("cache")]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckMemoryCache()
    {
        var tryHealth = await healthCheckService.CheckMemoryCacheAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                logger.LogError(ex, "Memory cache health check failed");
                return StatusCode(503, new ServiceHealth
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                });
            }
        );
    }

    /// <summary>
    ///     Get detailed health report with all service checks.
    ///     Demonstrates composing multiple Try monads.
    /// </summary>
    /// <returns>Detailed health report</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DetailedHealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        // Execute all health checks in parallel using collection expression
        var results = await Task.WhenAll([
            healthCheckService.CheckDatabaseAsync(),
            healthCheckService.CheckMemoryCacheAsync()
        ]);

        var report = new DetailedHealthReport
        {
            Timestamp = DateTime.UtcNow,
            Database = ExtractHealthOrError(results[0]),
            MemoryCache = ExtractHealthOrError(results[1])
        };

        // Determine overall status using collection expression
        var allHealthy = (ServiceHealth[])[report.Database, report.MemoryCache]
            is var checks && checks.All(h => h.Status == "Healthy");

        report.OverallStatus = allHealthy ? "Healthy" : "Degraded";

        var statusCode = allHealthy ? 200 : 503;
        return StatusCode(statusCode, report);
    }

    /// <summary>
    ///     Extract health or create error health from Try monad
    /// </summary>
    private static ServiceHealth ExtractHealthOrError(Try<ServiceHealth> tryHealth) =>
        tryHealth.Match(
            onSuccess: health => health,
            onFailure: ex => new ServiceHealth
            {
                Status = "Unhealthy",
                Error = ex.Message
            }
        );
}
