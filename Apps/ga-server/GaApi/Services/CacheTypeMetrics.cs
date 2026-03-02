namespace GaApi.Services;

/// <summary>
///     Metrics for a specific cache type
/// </summary>
public class CacheTypeMetrics
{
    public string CacheType { get; set; } = string.Empty;
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalRequests => TotalHits + TotalMisses;
    public double HitRate => TotalRequests > 0 ? (double)TotalHits / TotalRequests : 0;
    public double MissRate => TotalRequests > 0 ? (double)TotalMisses / TotalRequests : 0;
    public Dictionary<string, OperationMetrics> Operations { get; set; } = [];
    public DateTime FirstRequestTime { get; set; }
    public DateTime LastRequestTime { get; set; }
}
