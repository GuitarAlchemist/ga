namespace GaApi.Controllers;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Models;
using Services;
using Path = System.IO.Path;

// using GA.Business.Core.Diagnostics // REMOVED - namespace does not exist;

/// <summary>
///     Health check and system information endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController(
    IHealthCheckService healthCheck,
    ILogger<HealthController> logger,
    TarsMcpClient? tarsMcpClient = null)
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

    /// <summary>
    ///     Get comprehensive system diagnostics using TARS MCP
    ///     Includes GPU info, system resources, service health, Git status, and network diagnostics
    /// </summary>
    /// <returns>Comprehensive system diagnostics</returns>
    /// <response code="200">Returns comprehensive diagnostics</response>
    /// <response code="503">TARS MCP service unavailable</response>
    [HttpGet("diagnostics/comprehensive")]
    [ProducesResponseType(typeof(GA.Business.Core.Diagnostics.ComprehensiveDiagnostics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GA.Business.Core.Diagnostics.ComprehensiveDiagnostics>> GetComprehensiveDiagnostics()
    {
        if (tarsMcpClient == null)
        {
            return StatusCode(503, new { error = "TARS MCP client not available" });
        }

        try
        {
            var repositoryPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..", ".."));

            var diagnostics = await tarsMcpClient.GetComprehensiveDiagnosticsAsync(
                repositoryPath);

            if (diagnostics == null)
            {
                return StatusCode(503, new { error = "Failed to get diagnostics from TARS MCP" });
            }

            return Ok(diagnostics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get comprehensive diagnostics");
            return StatusCode(500, new { error = "Failed to get comprehensive diagnostics", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get GPU information using TARS MCP
    /// </summary>
    /// <returns>GPU information including CUDA support and memory usage</returns>
    /// <response code="200">Returns GPU information</response>
    /// <response code="503">TARS MCP service unavailable</response>
    [HttpGet("diagnostics/gpu")]
    [ProducesResponseType(typeof(GA.Business.Core.Diagnostics.GpuInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GA.Business.Core.Diagnostics.GpuInfo>> GetGpuInfo()
    {
        if (tarsMcpClient == null)
        {
            return StatusCode(503, new { error = "TARS MCP client not available" });
        }

        try
        {
            var gpuInfo = await tarsMcpClient.GetGpuInfoAsync();

            if (gpuInfo == null)
            {
                return StatusCode(503, new { error = "Failed to get GPU info from TARS MCP" });
            }

            return Ok(gpuInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get GPU info");
            return StatusCode(500, new { error = "Failed to get GPU info", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get system resources using TARS MCP
    /// </summary>
    /// <returns>System resource metrics (CPU, memory, disk)</returns>
    /// <response code="200">Returns system resources</response>
    /// <response code="503">TARS MCP service unavailable</response>
    [HttpGet("diagnostics/system-resources")]
    [ProducesResponseType(typeof(GA.Business.Core.Diagnostics.SystemResources), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GA.Business.Core.Diagnostics.SystemResources>> GetSystemResources()
    {
        if (tarsMcpClient == null)
        {
            return StatusCode(503, new { error = "TARS MCP client not available" });
        }

        try
        {
            var resources = await tarsMcpClient.GetSystemResourcesAsync();

            if (resources == null)
            {
                return StatusCode(503, new { error = "Failed to get system resources from TARS MCP" });
            }

            return Ok(resources);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get system resources");
            return StatusCode(500, new { error = "Failed to get system resources", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get service health using TARS MCP
    /// </summary>
    /// <returns>Service health (environment variables, ports, services)</returns>
    /// <response code="200">Returns service health</response>
    /// <response code="503">TARS MCP service unavailable</response>
    [HttpGet("diagnostics/service-health")]
    [ProducesResponseType(typeof(GA.Business.Core.Diagnostics.ServiceHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GA.Business.Core.Diagnostics.ServiceHealth>> GetServiceHealth()
    {
        if (tarsMcpClient == null)
        {
            return StatusCode(503, new { error = "TARS MCP client not configured" });
        }

        try
        {
            var serviceHealth = await tarsMcpClient.GetServiceHealthAsync();

            if (serviceHealth == null)
            {
                return StatusCode(503, new { error = "Failed to get service health from TARS MCP" });
            }

            return Ok(serviceHealth);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get service health");
            return StatusCode(500, new { error = "Failed to get service health", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get Git repository health using TARS MCP
    /// </summary>
    /// <param name="repositoryPath">Optional repository path (defaults to current directory)</param>
    /// <returns>Git repository health (branch, changes, commits)</returns>
    /// <response code="200">Returns Git health</response>
    /// <response code="503">TARS MCP service unavailable</response>
    [HttpGet("diagnostics/git-health")]
    [ProducesResponseType(typeof(GA.Business.Core.Diagnostics.GitHealth), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GA.Business.Core.Diagnostics.GitHealth>> GetGitHealth(
        [FromQuery] string? repositoryPath = null)
    {
        if (tarsMcpClient == null)
        {
            return StatusCode(503, new { error = "TARS MCP client not configured" });
        }

        try
        {
            var gitHealth = await tarsMcpClient.GetGitHealthAsync(repositoryPath);

            if (gitHealth == null)
            {
                return StatusCode(503, new { error = "Failed to get Git health from TARS MCP" });
            }

            return Ok(gitHealth);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get Git health");
            return StatusCode(500, new { error = "Failed to get Git health", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get network diagnostics using TARS MCP
    /// </summary>
    /// <returns>Network diagnostics (connectivity, IP, DNS, connections)</returns>
    /// <response code="200">Returns network diagnostics</response>
    /// <response code="503">TARS MCP service unavailable</response>
    [HttpGet("diagnostics/network")]
    [ProducesResponseType(typeof(GA.Business.Core.Diagnostics.NetworkDiagnostics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GA.Business.Core.Diagnostics.NetworkDiagnostics>> GetNetworkDiagnostics()
    {
        if (tarsMcpClient == null)
        {
            return StatusCode(503, new { error = "TARS MCP client not configured" });
        }

        try
        {
            var networkDiagnostics = await tarsMcpClient.GetNetworkDiagnosticsAsync();

            if (networkDiagnostics == null)
            {
                return StatusCode(503, new { error = "Failed to get network diagnostics from TARS MCP" });
            }

            return Ok(networkDiagnostics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get network diagnostics");
            return StatusCode(500, new { error = "Failed to get network diagnostics", details = ex.Message });
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
