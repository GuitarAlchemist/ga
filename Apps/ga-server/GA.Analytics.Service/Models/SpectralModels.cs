namespace GA.Analytics.Service.Models;

/// <summary>
/// Agent spectral metrics
/// </summary>
public class AgentSpectralMetrics
{
    public string Id { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public Dictionary<string, double> SpectralData { get; set; } = new();
    public double DominantFrequency { get; set; }
    public double SpectralCentroid { get; set; }
    public double SpectralBandwidth { get; set; }
    public double SpectralRolloff { get; set; }
    public List<SpectralPeak> Peaks { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Spectral peak information
/// </summary>
public class SpectralPeak
{
    public double Frequency { get; set; }
    public double Magnitude { get; set; }
    public double Phase { get; set; }
    public double Bandwidth { get; set; }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public string Id { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double HitRatio => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public double TotalHitRate => HitRatio;
    public long TotalMemoryUsage { get; set; }
    public Dictionary<string, long> CategoryStats { get; set; } = new();
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Global validation result
/// </summary>
public class GlobalValidationResult
{
    public string Id { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public Dictionary<string, object> ValidationContext { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}



/// <summary>
/// Validation warning
/// </summary>
public class ValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Violation trend data
/// </summary>
public class ViolationTrend
{
    public DateTime Timestamp { get; set; }
    public long ViolationCount { get; set; }
    public string ViolationType { get; set; } = string.Empty;
}

/// <summary>
/// Invariant violation event
/// </summary>
public class InvariantViolationEvent
{
    public string Id { get; set; } = string.Empty;
    public string InvariantId { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}


