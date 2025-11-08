namespace GA.Business.Core.Diagnostics;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP client for TARS MCP Server diagnostics
/// Provides GPU info, system resources, service health, Git health, and network diagnostics
/// </summary>
public class TarsMcpClient(HttpClient httpClient, ILogger<TarsMcpClient> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Get GPU information including CUDA support, memory usage, and temperature
    /// </summary>
    public async Task<GpuInfo?> GetGpuInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Requesting GPU info from TARS MCP");

            var response = await httpClient.PostAsJsonAsync(
                "/mcp/tools/get_gpu_info",
                new { },
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TARS MCP GPU info request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            // TARS MCP returns an array of GPU info objects
            var results = await response.Content.ReadFromJsonAsync<List<GpuInfo>>(_jsonOptions, cancellationToken);

            if (results != null && results.Count > 0)
            {
                var result = results[0]; // Return first GPU
                logger.LogDebug("GPU info retrieved: {Name}, CUDA: {CudaSupported}",
                    result.Name, result.CudaSupported);
                return result;
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting GPU info from TARS MCP");
            return null;
        }
    }

    /// <summary>
    /// Get system resource metrics (CPU, memory, disk usage)
    /// </summary>
    public async Task<SystemResources?> GetSystemResourcesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Requesting system resources from TARS MCP");

            var response = await httpClient.PostAsJsonAsync(
                "/mcp/tools/get_system_resources",
                new { },
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TARS MCP system resources request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SystemResources>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting system resources from TARS MCP");
            return null;
        }
    }

    /// <summary>
    /// Get service health including environment variables, listening ports, and running services
    /// </summary>
    public async Task<ServiceHealth?> GetServiceHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Requesting service health from TARS MCP");

            var response = await httpClient.PostAsJsonAsync(
                "/mcp/tools/get_service_health",
                new { },
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TARS MCP service health request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ServiceHealth>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting service health from TARS MCP");
            return null;
        }
    }

    /// <summary>
    /// Get Git repository health
    /// </summary>
    public async Task<GitHealth?> GetGitHealthAsync(string? repositoryPath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Requesting Git health from TARS MCP for path: {Path}", repositoryPath ?? "current directory");

            object request = repositoryPath != null
                ? new { repository_path = repositoryPath }
                : new { };

            var response = await httpClient.PostAsJsonAsync(
                "/mcp/tools/get_git_health",
                request,
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TARS MCP Git health request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GitHealth>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Git health from TARS MCP");
            return null;
        }
    }

    /// <summary>
    /// Get network diagnostics
    /// </summary>
    public async Task<NetworkDiagnostics?> GetNetworkDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Requesting network diagnostics from TARS MCP");

            var response = await httpClient.PostAsJsonAsync(
                "/mcp/tools/get_network_diagnostics",
                new { },
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TARS MCP network diagnostics request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<NetworkDiagnostics>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting network diagnostics from TARS MCP");
            return null;
        }
    }

    /// <summary>
    /// Get comprehensive diagnostics (all of the above in one call)
    /// </summary>
    public async Task<ComprehensiveDiagnostics?> GetComprehensiveDiagnosticsAsync(
        string? repositoryPath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Requesting comprehensive diagnostics from TARS MCP");

            object request = repositoryPath != null
                ? new { repository_path = repositoryPath }
                : new { };

            var response = await httpClient.PostAsJsonAsync(
                "/mcp/tools/get_comprehensive_diagnostics",
                request,
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("TARS MCP comprehensive diagnostics request failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ComprehensiveDiagnostics>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting comprehensive diagnostics from TARS MCP");
            return null;
        }
    }

    /// <summary>
    /// Check if TARS MCP service is available
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// DTOs matching TARS MCP response format

public record GpuInfo(
    string Name,
    long MemoryTotal,
    long MemoryUsed,
    long MemoryFree,
    bool CudaSupported,
    string? DriverVersion,
    double? Temperature);

public record SystemResources(
    double CpuUsagePercent,
    int CpuCoreCount,
    double CpuFrequency,
    long MemoryTotalBytes,
    long MemoryUsedBytes,
    long MemoryAvailableBytes,
    long DiskTotalBytes,
    long DiskUsedBytes,
    long DiskFreeBytes,
    int ProcessCount,
    int ThreadCount,
    double Uptime);

public record ServiceHealth(
    Dictionary<string, string> EnvironmentVariables,
    List<int> PortsListening,
    List<string> ServicesRunning);

public record GitHealth(
    bool IsRepository,
    string CurrentBranch,
    bool IsClean,
    int UnstagedChanges,
    int StagedChanges,
    int Commits,
    string? RemoteUrl,
    string? LastCommitHash,
    DateTime? LastCommitDate,
    int AheadBy,
    int BehindBy);

public record NetworkDiagnostics(
    bool IsConnected,
    string? PublicIpAddress,
    double DnsResolutionTime,
    int ActiveConnections,
    List<string> NetworkInterfaces);

public record ComprehensiveDiagnostics(
    List<GpuInfo>? GpuInfo,
    SystemResources? SystemResources,
    ServiceHealth? ServiceHealth,
    NetworkDiagnostics? NetworkDiagnostics,
    GitHealth? GitHealth,
    DateTime Timestamp,
    double OverallHealthScore);

