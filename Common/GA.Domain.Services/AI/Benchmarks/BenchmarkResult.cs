namespace GA.Domain.Services.AI.Benchmarks;

public class BenchmarkResult
{
    public string BenchmarkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<BenchmarkStep> Steps { get; set; } = [];
    public string RawOutput { get; set; } = string.Empty;
    public bool Passed => Score >= 0.8; // Standard threshold
}

public class BenchmarkStep
{
    public string Name { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public double Score { get; set; }
    public bool Passed { get; set; }
    public string? Notes { get; set; }
}
