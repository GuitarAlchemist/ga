namespace GA.MusicTheory.Service.Models;

/// <summary>
///     Overall health check response
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    ///     Overall health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    ///     Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Individual service health checks
    /// </summary>
    public Dictionary<string, ServiceHealth> Services { get; set; } = [];

    /// <summary>
    ///     API version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    ///     Environment name
    /// </summary>
    public string Environment { get; set; } = "Development";
}
