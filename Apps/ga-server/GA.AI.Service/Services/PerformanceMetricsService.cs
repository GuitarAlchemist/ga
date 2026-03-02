namespace GA.AI.Service.Services;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
///     Performance metrics service to track regular vs semantic operation performance
///     Helps identify when microservices split might be needed
/// </summary>
public class PerformanceMetricsService : IDisposable
{
    private readonly Meter _meter = new("GuitarAlchemist.API", "1.0.0");
    private readonly Histogram<double> _regularDurationHistogram;
    private readonly Counter<long> _regularErrorCounter;
    private readonly Histogram<long> _regularMemoryHistogram;
    private readonly Counter<long> _regularRequestCounter;
    private readonly Histogram<double> _semanticDurationHistogram;
    private readonly Counter<long> _semanticErrorCounter;
    private readonly Histogram<long> _semanticMemoryHistogram;
    private readonly Counter<long> _semanticRequestCounter;
    private long _regularErrors;

    // In-memory statistics for quick access
    private long _regularRequests;
    private double _regularTotalDuration;
    private long _semanticErrors;
    private long _semanticRequests;
    private double _semanticTotalDuration;
    private readonly Lock _statsLock = new();

    // ... (rest of constructor)
    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Track a regular request
    /// </summary>
    public IDisposable TrackRegularRequest()
    {
        Interlocked.Increment(ref _regularRequests);
        _regularRequestCounter.Add(1);
        return new RequestTracker(this, false);
    }

    /// <summary>
    ///     Track a semantic request
    /// </summary>
    public IDisposable TrackSemanticRequest()
    {
        Interlocked.Increment(ref _semanticRequests);
        _semanticRequestCounter.Add(1);
        return new RequestTracker(this, true);
    }

    /// <summary>
    ///     Record a regular request memory usage
    /// </summary>
    public void RecordRegularMemory(long bytes) => _regularMemoryHistogram.Record(bytes);

    /// <summary>
    ///     Record a semantic request memory usage
    /// </summary>
    public void RecordSemanticMemory(long bytes) => _semanticMemoryHistogram.Record(bytes);

    /// <summary>
    ///     Record a regular request error
    /// </summary>
    public void RecordRegularError()
    {
        Interlocked.Increment(ref _regularErrors);
        _regularErrorCounter.Add(1);
    }

    /// <summary>
    ///     Record a semantic request error
    /// </summary>
    public void RecordSemanticError()
    {
        Interlocked.Increment(ref _semanticErrors);
        _semanticErrorCounter.Add(1);
    }

    /// <summary>
    ///     Record a regular request duration
    /// </summary>
    public void RecordRegularDuration(double durationMs)
    {
        _regularDurationHistogram.Record(durationMs);

        // Update running average
        lock (_statsLock)
        {
            _regularTotalDuration += durationMs;
        }
    }

    /// <summary>
    ///     Record a semantic request duration
    /// </summary>
    public void RecordSemanticDuration(double durationMs)
    {
        _semanticDurationHistogram.Record(durationMs);

        // Update running average
        lock (_statsLock)
        {
            _semanticTotalDuration += durationMs;
        }
    }

    // ... (RecordMemory/Error methods)

    /// <summary>
    ///     Get performance statistics
    /// </summary>
    public PerformanceStatistics GetStatistics()
    {
        // Snapshot values to ensure consistency
        long regReq, semReq, regErr, semErr;
        double regDur, semDur;

        lock (_statsLock)
        {
            regReq = Interlocked.Read(ref _regularRequests);
            semReq = Interlocked.Read(ref _semanticRequests);
            regErr = Interlocked.Read(ref _regularErrors);
            semErr = Interlocked.Read(ref _semanticErrors);
            regDur = _regularTotalDuration;
            semDur = _semanticTotalDuration;
        }

        var stats = new PerformanceStatistics
        {
            RegularRequests = regReq,
            SemanticRequests = semReq,
            RegularAverageDuration = regReq > 0 ? regDur / regReq : 0,
            SemanticAverageDuration = semReq > 0 ? semDur / semReq : 0,
            RegularErrors = regErr,
            SemanticErrors = semErr,
            RegularErrorRate = regReq > 0 ? (double)regErr / regReq : 0,
            SemanticErrorRate = semReq > 0 ? (double)semErr / semReq : 0,
            PerformanceRatio = regReq > 0 && semReq > 0 && regDur > 0
                ? (semDur / semReq) / (regDur / regReq)
                : 0
        };

        stats.SplitRecommendation = GetSplitRecommendation(stats);
        return stats;
    }

    private static string GetSplitRecommendation(PerformanceStatistics stats)
    {
        // Recommend split if:
        // 1. Semantic requests are significantly slower (>10x)
        // 2. High volume of both types (>1000 each)
        // 3. Different error rates

        if (stats.RegularRequests < 100 || stats.SemanticRequests < 100)
        {
            return "Not enough data to make recommendation";
        }

        if (stats.PerformanceRatio > 10)
        {
            return "RECOMMEND SPLIT: Semantic operations are significantly slower (>10x)";
        }

        if (stats.PerformanceRatio > 5 && (stats.RegularRequests > 1000 || stats.SemanticRequests > 1000))
        {
            return "CONSIDER SPLIT: High volume with significant performance difference";
        }

        if (Math.Abs(stats.RegularErrorRate - stats.SemanticErrorRate) > 0.1)
        {
            return "CONSIDER SPLIT: Different error rates suggest different reliability characteristics";
        }

        return "KEEP TOGETHER: Current architecture is appropriate";
    }

    private class RequestTracker(PerformanceMetricsService service, bool isSemanticRequest) : IDisposable
    {
        private readonly long _startMemory = GC.GetTotalMemory(false);
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public void Dispose()
        {
            _stopwatch.Stop();
            var duration = _stopwatch.Elapsed.TotalMilliseconds;
            var memoryUsed = GC.GetTotalMemory(false) - _startMemory;

            if (isSemanticRequest)
            {
                service.RecordSemanticDuration(duration);
                service.RecordSemanticMemory(memoryUsed);
            }
            else
            {
                service.RecordRegularDuration(duration);
                service.RecordRegularMemory(memoryUsed);
            }
        }
    }
}

public class PerformanceStatistics
{
    public long RegularRequests { get; set; }
    public long SemanticRequests { get; set; }
    public double RegularAverageDuration { get; set; }
    public double SemanticAverageDuration { get; set; }
    public long RegularErrors { get; set; }
    public long SemanticErrors { get; set; }
    public double RegularErrorRate { get; set; }
    public double SemanticErrorRate { get; set; }
    public double PerformanceRatio { get; set; }
    public string SplitRecommendation { get; set; } = string.Empty;
}
