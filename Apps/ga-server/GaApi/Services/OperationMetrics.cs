namespace GaApi.Services;

/// <summary>
///     Metrics for a specific cache operation
/// </summary>
public class OperationMetrics
{
    public string Operation { get; set; } = string.Empty;
    public long Count { get; set; }
    public double AverageDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
}
