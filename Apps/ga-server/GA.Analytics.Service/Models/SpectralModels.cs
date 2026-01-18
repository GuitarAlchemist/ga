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


