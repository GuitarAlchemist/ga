namespace GA.Business.Core.AI.Benchmarks;

using System;
using System.Collections.Generic;

public class BenchmarkResult
{
    public string BenchmarkId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Score { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<BenchmarkStep> Steps { get; set; } = new();
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
