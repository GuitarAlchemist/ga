namespace GA.BSP.Service.Services;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
///     Performance metrics service to track regular vs semantic operation performance
///     Helps identify when microservices split might be needed
/// </summary>
public class PerformanceMetricsService : IDisposable
{
    private readonly Meter _meter;
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

    public PerformanceMetricsService()
    {
        _meter = new Meter("GuitarAlchemist.API", "1.0.0");

        // Request counters
        _regularRequestCounter = _meter.CreateCounter<long>(
            "api.requests.regular",
            description: "Number of regular API requests");

        _semanticRequestCounter = _meter.CreateCounter<long>(
            "api.requests.semantic",
            description: "Number of semantic/vector search requests");

        // Duration histograms
        _regularDurationHistogram = _meter.CreateHistogram<double>(
            "api.duration.regular",
            "ms",
            "Duration of regular API requests in milliseconds");

        _semanticDurationHistogram = _meter.CreateHistogram<double>(
            "api.duration.semantic",
            "ms",
            "Duration of semantic/vector search requests in milliseconds");

        // Memory histograms
        _regularMemoryHistogram = _meter.CreateHistogram<long>(
            "api.memory.regular",
            "bytes",
            "Memory usage for regular API requests");

        _semanticMemoryHistogram = _meter.CreateHistogram<long>(
            "api.memory.semantic",
            "bytes",
            "Memory usage for semantic/vector search requests");

        // Error counters
        _regularErrorCounter = _meter.CreateCounter<long>(
            "api.errors.regular",
            description: "Number of errors in regular API requests");

        _semanticErrorCounter = _meter.CreateCounter<long>(
            "api.errors.semantic",
            description: "Number of errors in semantic/vector search requests");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    /// <summary>
    ///     Track a regular API request
    /// </summary>
    public IDisposable TrackRegularRequest()
    {
        _regularRequestCounter.Add(1);
        Interlocked.Increment(ref _regularRequests);
        return new RequestTracker(this, false);
    }

    /// <summary>
    ///     Track a semantic API request
    /// </summary>
    public IDisposable TrackSemanticRequest()
    {
        _semanticRequestCounter.Add(1);
        Interlocked.Increment(ref _semanticRequests);
        return new RequestTracker(this, true);
    }

    /// <summary>
    ///     Record a regular request duration
    /// </summary>
    public void RecordRegularDuration(double durationMs)
    {
        _regularDurationHistogram.Record(durationMs);

        // Update running average
        lock (this)
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
        lock (this)
        {
            _semanticTotalDuration += durationMs;
        }
    }

    /// <summary>
    ///     Record memory usage for a regular request
    /// </summary>
    public void RecordRegularMemory(long bytes)
    {
        _regularMemoryHistogram.Record(bytes);
    }

    /// <summary>
    ///     Record memory usage for a semantic request
    /// </summary>
    public void RecordSemanticMemory(long bytes)
    {
        _semanticMemoryHistogram.Record(bytes);
    }

    /// <summary>
    ///     Record a regular request error
    /// </summary>
    public void RecordRegularError()
    {
        _regularErrorCounter.Add(1);
        Interlocked.Increment(ref _regularErrors);
    }

    /// <summary>
    ///     Record a semantic request error
    /// </summary>
    public void RecordSemanticError()
    {
        _semanticErrorCounter.Add(1);
        Interlocked.Increment(ref _semanticErrors);
    }

    /// <summary>
    ///     Get performance statistics
    /// </summary>
    public PerformanceStatistics GetStatistics()
    {
        return new PerformanceStatistics
        {
            RegularRequests = _regularRequests,
            SemanticRequests = _semanticRequests,
            RegularAverageDuration = _regularRequests > 0 ? _regularTotalDuration / _regularRequests : 0,
            SemanticAverageDuration = _semanticRequests > 0 ? _semanticTotalDuration / _semanticRequests : 0,
            RegularErrors = _regularErrors,
            SemanticErrors = _semanticErrors,
            RegularErrorRate = _regularRequests > 0 ? (double)_regularErrors / _regularRequests : 0,
            SemanticErrorRate = _semanticRequests > 0 ? (double)_semanticErrors / _semanticRequests : 0,
            PerformanceRatio = _regularRequests > 0 && _semanticRequests > 0
                ? _semanticTotalDuration / _semanticRequests / (_regularTotalDuration / _regularRequests)
                : 0,
            SplitRecommendation = GetSplitRecommendation()
        };
    }

    private string GetSplitRecommendation()
    {
        var stats = GetStatistics();

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
