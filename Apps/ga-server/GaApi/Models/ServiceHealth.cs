namespace GaApi.Models;

/// <summary>
///     Individual service health information
/// </summary>
public class ServiceHealth
{
    /// <summary>
    ///     Service health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    ///     Response time in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    ///     Additional service details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }

    /// <summary>
    ///     Error message if unhealthy
    /// </summary>
    public string? Error { get; set; }
}