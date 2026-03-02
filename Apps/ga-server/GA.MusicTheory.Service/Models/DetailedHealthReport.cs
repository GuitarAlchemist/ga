namespace GA.MusicTheory.Service.Models;

/// <summary>
///     Detailed health report including individual service status
/// </summary>
public class DetailedHealthReport
{
    /// <summary>
    ///     Overall system status
    /// </summary>
    public string OverallStatus { get; set; } = "Unknown";

    /// <summary>
    ///     Timestamp of the health report
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Database health status
    /// </summary>
    public ServiceHealth Database { get; set; } = new();

    /// <summary>
    ///     Memory cache health status
    /// </summary>
    public ServiceHealth MemoryCache { get; set; } = new();
}
