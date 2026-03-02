namespace GaApi.Controllers;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Models;
using Services;
using Path = System.IO.Path;

// using GA.Domain.Core.Diagnostics // REMOVED - namespace does not exist;

/// <summary>
///     Health check and system information endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController(
    IHealthCheckService healthCheck,
    ILogger<HealthController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Get comprehensive health status of all services
    /// </summary>
    /// <returns>Health status of the API and all dependencies</returns>
    /// <response code="200">Returns health status (may include degraded services)</response>
    /// <response code="503">Service unavailable - critical services are down</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth()
    {
        try
        {
            var health = await healthCheck.GetHealthAsync();

            // Return 503 if any critical services are unhealthy
            var hasCriticalFailures = health.Services.Values
                .Any(s => s.Status == "Unhealthy");

            if (hasCriticalFailures)
            {
                return StatusCode(503, health);
            }

            return Ok(health);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");

            var errorResponse = new HealthCheckResponse
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
            };

            return StatusCode(503, errorResponse);
        }
    }

    /// <summary>
    ///     Simple health check endpoint for load balancers
    /// </summary>
    /// <returns>Simple OK response if service is healthy</returns>
    /// <response code="200">Service is healthy</response>
    /// <response code="503">Service is unhealthy</response>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Ping()
    {
        try
        {
            var health = await healthCheck.GetHealthAsync();

            if (health.Status == "Healthy")
            {
                return Ok(new { status = "OK", timestamp = DateTime.UtcNow });
            }

            return StatusCode(503, new { status = "Unhealthy", timestamp = DateTime.UtcNow });
        }
        catch
        {
            return StatusCode(503, new { status = "Error", timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>
    ///     Get detailed database health information
    /// </summary>
    /// <returns>Database connectivity and performance metrics</returns>
    /// <response code="200">Returns database health status</response>
    /// <response code="500">Database health check failed</response>
    [HttpGet("database")]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServiceHealth>> GetDatabaseHealth()
    {
        try
        {
            var health = await healthCheck.CheckDatabaseAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            return StatusCode(500, new { error = "Database health check failed", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get vector search service health information
    /// </summary>
    /// <returns>Vector search service status and performance</returns>
    /// <response code="200">Returns vector search health status</response>
    /// <response code="500">Vector search health check failed</response>
    [HttpGet("vector-search")]
    [ProducesResponseType(typeof(ServiceHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ServiceHealth>> GetVectorSearchHealth()
    {
        try
        {
            var health = await healthCheck.CheckVectorSearchAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vector search health check failed");
            return StatusCode(500, new { error = "Vector search health check failed", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get system information and metrics
    /// </summary>
    /// <returns>System performance and resource usage information</returns>
    /// <response code="200">Returns system information</response>
    [HttpGet("system")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetSystemInfo()
    {
        try
        {
            var process = Process.GetCurrentProcess();

            var systemInfo = new
            {
                timestamp = DateTime.UtcNow,
                server = new
                {
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                    is64BitProcess = Environment.Is64BitProcess
                },
                process = new
                {
                    processId = process.Id,
                    processName = process.ProcessName,
                    startTime = process.StartTime,
                    workingSet = process.WorkingSet64,
                    privateMemorySize = process.PrivateMemorySize64,
                    virtualMemorySize = process.VirtualMemorySize64,
                    totalProcessorTime = process.TotalProcessorTime,
                    threadCount = process.Threads.Count
                },
                memory = new
                {
                    workingSetMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2),
                    privateMemoryMB = Math.Round(process.PrivateMemorySize64 / 1024.0 / 1024.0, 2),
                    virtualMemoryMB = Math.Round(process.VirtualMemorySize64 / 1024.0 / 1024.0, 2)
                },
                runtime = new
                {
                    version = Environment.Version.ToString(),
                    frameworkDescription = RuntimeInformation.FrameworkDescription,
                    runtimeIdentifier = RuntimeInformation.RuntimeIdentifier
                }
            };

            return Ok(systemInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get system information");
            return StatusCode(500, new { error = "Failed to get system information", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get API version and build information
    /// </summary>
    /// <returns>API version, build date, and configuration information</returns>
    /// <response code="200">Returns version information</response>
    [HttpGet("version")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            var buildDate = GetBuildDate(assembly);

            var versionInfo = new
            {
                version = version?.ToString() ?? "Unknown",
                buildDate,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                timestamp = DateTime.UtcNow,
                api = new
                {
                    name = "Guitar Alchemist API",
                    description = "RESTful API for guitar chord and music theory data",
                    documentation = "/swagger"
                }
            };

            return Ok(versionInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get version information");
            return StatusCode(500, new { error = "Failed to get version information", details = ex.Message });
        }
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        try
        {
            var fileInfo = new FileInfo(assembly.Location);
            return fileInfo.CreationTime;
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }
}
