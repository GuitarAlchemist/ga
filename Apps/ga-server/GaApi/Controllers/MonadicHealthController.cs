namespace GaApi.Controllers;

using Models;
using Services;

/// <summary>
///     Controller demonstrating monadic health check service integration
/// </summary>
[ApiController]
[Route("api/monadic/health")]
[Produces("application/json")]
public class MonadicHealthController : ControllerBase
{
    private readonly MonadicHealthCheckService _healthCheckService;
    private readonly ILogger<MonadicHealthController> _logger;

    public MonadicHealthController(
        MonadicHealthCheckService healthCheckService,
        ILogger<MonadicHealthController> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    ///     Get overall health status using Try monad
    /// </summary>
    /// <returns>Health check response or error</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth()
    {
        var tryHealth = await _healthCheckService.GetHealthAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                // Return 503 if any service is unhealthy, 200 otherwise
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                _logger.LogError(ex, "Health check failed");
                return StatusCode(503, new HealthCheckResponse
                {
                    Status = "Unhealthy",
                    Version = "Unknown",
                    Environment = "Unknown",
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
        var tryHealth = await _healthCheckService.CheckDatabaseAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                _logger.LogError(ex, "Database health check failed");
                return StatusCode(503, new ServiceHealth
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                });
            }
        );
    }

    /// <summary>
    ///     Check vector search health using Try monad
    /// </summary>
    /// <returns>Vector search health status or error</returns>
    [HttpGet("vector-search")]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckVectorSearch()
    {
        var tryHealth = await _healthCheckService.CheckVectorSearchAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                _logger.LogError(ex, "Vector search health check failed");
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
        var tryHealth = await _healthCheckService.CheckMemoryCacheAsync();

        return tryHealth.Match<IActionResult>(
            onSuccess: health =>
            {
                var statusCode = health.Status == "Healthy" ? 200 : 503;
                return StatusCode(statusCode, health);
            },
            onFailure: ex =>
            {
                _logger.LogError(ex, "Memory cache health check failed");
                return StatusCode(503, new ServiceHealth
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                });
            }
        );
    }

    /// <summary>
    ///     Get detailed health report with all service checks
    ///     Demonstrates composing multiple Try monads
    /// </summary>
    /// <returns>Detailed health report</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DetailedHealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDetailedHealth()
    {
        // Execute all health checks in parallel
        var healthTasks = new[]
        {
            _healthCheckService.CheckDatabaseAsync(),
            _healthCheckService.CheckVectorSearchAsync(),
            _healthCheckService.CheckMemoryCacheAsync()
        };

        var results = await Task.WhenAll(healthTasks);

        var report = new DetailedHealthReport
        {
            Timestamp = DateTime.UtcNow,
            Database = ExtractHealthOrError(results[0]),
            VectorSearch = ExtractHealthOrError(results[1]),
            MemoryCache = ExtractHealthOrError(results[2])
        };

        // Determine overall status
        var allHealthy = new[] { report.Database, report.VectorSearch, report.MemoryCache }
            .All(h => h.Status == "Healthy");

        report.OverallStatus = allHealthy ? "Healthy" : "Degraded";

        var statusCode = allHealthy ? 200 : 503;
        return StatusCode(statusCode, report);
    }

    // Helper method to extract health or create error health
    private ServiceHealth ExtractHealthOrError(GA.Business.Core.Microservices.Try<ServiceHealth> tryHealth)
    {
        return tryHealth.Match(
            onSuccess: health => health,
            onFailure: ex => new ServiceHealth
            {
                Status = "Unhealthy",
                Error = ex.Message
            }
        );
    }
}

/// <summary>
///     Detailed health report model
/// </summary>
public class DetailedHealthReport
{
    public DateTime Timestamp { get; set; }
    public string OverallStatus { get; set; } = string.Empty;
    public ServiceHealth Database { get; set; } = new();
    public ServiceHealth VectorSearch { get; set; } = new();
    public ServiceHealth MemoryCache { get; set; } = new();
}
