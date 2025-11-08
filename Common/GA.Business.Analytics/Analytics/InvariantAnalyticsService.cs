namespace GA.Business.Core.Analytics;

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
///     Service for collecting and analyzing invariant validation metrics
/// </summary>
public class InvariantAnalyticsService(ILogger<InvariantAnalyticsService> logger, IMemoryCache cache)
{
    private readonly IMemoryCache _cache = cache;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, InvariantMetrics> _metrics = new();
    private readonly ConcurrentQueue<ViolationEvent> _violationEvents = new();

    /// <summary>
    ///     Record a validation event
    /// </summary>
    public void RecordValidation(string invariantName, string conceptType, bool isValid, TimeSpan executionTime,
        string? errorMessage = null)
    {
        var key = $"{conceptType}_{invariantName}";

        _metrics.AddOrUpdate(key,
            new InvariantMetrics
            {
                InvariantName = invariantName,
                ConceptType = conceptType,
                TotalValidations = 1,
                SuccessfulValidations = isValid ? 1 : 0,
                FailedValidations = isValid ? 0 : 1,
                TotalExecutionTime = executionTime,
                MinExecutionTime = executionTime,
                MaxExecutionTime = executionTime,
                LastValidated = DateTime.UtcNow
            },
            (k, existing) =>
            {
                existing.TotalValidations++;
                if (isValid)
                {
                    existing.SuccessfulValidations++;
                }
                else
                {
                    existing.FailedValidations++;
                }

                existing.TotalExecutionTime += executionTime;
                existing.MinExecutionTime =
                    TimeSpan.FromTicks(Math.Min(existing.MinExecutionTime.Ticks, executionTime.Ticks));
                existing.MaxExecutionTime =
                    TimeSpan.FromTicks(Math.Max(existing.MaxExecutionTime.Ticks, executionTime.Ticks));
                existing.LastValidated = DateTime.UtcNow;

                return existing;
            });

        // Record violation event if validation failed
        if (!isValid)
        {
            _violationEvents.Enqueue(new ViolationEvent
            {
                InvariantName = invariantName,
                ConceptType = conceptType,
                ErrorMessage = errorMessage ?? "Unknown error",
                Timestamp = DateTime.UtcNow
            });

            // Keep only last 10,000 violation events
            while (_violationEvents.Count > 10000)
            {
                _violationEvents.TryDequeue(out _);
            }
        }

        logger.LogTrace(
            "Recorded validation: {InvariantName} for {ConceptType} - Valid: {IsValid}, Time: {ExecutionTime}ms",
            invariantName, conceptType, isValid, executionTime.TotalMilliseconds);
    }

    /// <summary>
    ///     Get analytics for a specific invariant
    /// </summary>
    public InvariantAnalytics? GetInvariantAnalytics(string invariantName, string conceptType)
    {
        var key = $"{conceptType}_{invariantName}";
        if (!_metrics.TryGetValue(key, out var metrics))
        {
            return null;
        }

        return CreateAnalytics(metrics);
    }

    /// <summary>
    ///     Get analytics for all invariants
    /// </summary>
    public virtual List<InvariantAnalytics> GetAllAnalytics()
    {
        return _metrics.Values.Select(CreateAnalytics).OrderByDescending(a => a.FailureRate).ToList();
    }

    /// <summary>
    ///     Get analytics for a specific concept type
    /// </summary>
    public virtual List<InvariantAnalytics> GetAnalyticsByConceptType(string conceptType)
    {
        return _metrics.Values
            .Where(m => m.ConceptType.Equals(conceptType, StringComparison.OrdinalIgnoreCase))
            .Select(CreateAnalytics)
            .OrderByDescending(a => a.FailureRate)
            .ToList();
    }

    /// <summary>
    ///     Get top failing invariants
    /// </summary>
    public List<InvariantAnalytics> GetTopFailingInvariants(int count = 10)
    {
        return _metrics.Values
            .Select(CreateAnalytics)
            .Where(a => a.FailedValidations > 0)
            .OrderByDescending(a => a.FailureRate)
            .ThenByDescending(a => a.FailedValidations)
            .Take(count)
            .ToList();
    }

    /// <summary>
    ///     Get slowest invariants
    /// </summary>
    public List<InvariantAnalytics> GetSlowestInvariants(int count = 10)
    {
        return _metrics.Values
            .Select(CreateAnalytics)
            .OrderByDescending(a => a.AverageExecutionTime)
            .Take(count)
            .ToList();
    }

    /// <summary>
    ///     Get recent violation events
    /// </summary>
    public virtual List<ViolationEvent> GetRecentViolations(int count = 100)
    {
        return _violationEvents.TakeLast(count).OrderByDescending(v => v.Timestamp).ToList();
    }

    /// <summary>
    ///     Get violation trends over time
    /// </summary>
    public virtual ViolationTrends GetViolationTrends(TimeSpan period)
    {
        var cutoff = DateTime.UtcNow - period;
        var recentViolations = _violationEvents.Where(v => v.Timestamp >= cutoff).ToList();

        var trends = new ViolationTrends
        {
            Period = period,
            TotalViolations = recentViolations.Count,
            ViolationsByInvariant = recentViolations
                .GroupBy(v => v.InvariantName)
                .ToDictionary(g => g.Key, g => g.Count()),
            ViolationsByConceptType = recentViolations
                .GroupBy(v => v.ConceptType)
                .ToDictionary(g => g.Key, g => g.Count()),
            ViolationsByHour = recentViolations
                .GroupBy(v => v.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return trends;
    }

    /// <summary>
    ///     Get performance insights
    /// </summary>
    public virtual PerformanceInsights GetPerformanceInsights()
    {
        var allMetrics = _metrics.Values.ToList();

        if (!allMetrics.Any())
        {
            return new PerformanceInsights();
        }

        var insights = new PerformanceInsights
        {
            TotalValidations = allMetrics.Sum(m => m.TotalValidations),
            TotalFailures = allMetrics.Sum(m => m.FailedValidations),
            AverageExecutionTime =
                TimeSpan.FromTicks((long)allMetrics.Average(m =>
                    m.TotalExecutionTime.Ticks / Math.Max(m.TotalValidations, 1))),
            SlowestInvariant = allMetrics
                .OrderByDescending(m => m.TotalExecutionTime.Ticks / Math.Max(m.TotalValidations, 1)).FirstOrDefault()
                ?.InvariantName,
            MostFailedInvariant =
                allMetrics.OrderByDescending(m => m.FailedValidations).FirstOrDefault()?.InvariantName,
            OverallSuccessRate = allMetrics.Sum(m => m.SuccessfulValidations) /
                                 (double)Math.Max(allMetrics.Sum(m => m.TotalValidations), 1)
        };

        return insights;
    }

    /// <summary>
    ///     Generate recommendations based on analytics
    /// </summary>
    public List<AnalyticsRecommendation> GetRecommendations()
    {
        var recommendations = new List<AnalyticsRecommendation>();
        var allAnalytics = GetAllAnalytics();

        // High failure rate recommendations
        var highFailureInvariants = allAnalytics.Where(a => a.FailureRate > 0.1 && a.TotalValidations > 10).ToList();
        foreach (var invariant in highFailureInvariants)
        {
            recommendations.Add(new AnalyticsRecommendation
            {
                Type = RecommendationType.HighFailureRate,
                Priority = invariant.FailureRate > 0.5 ? RecommendationPriority.High : RecommendationPriority.Medium,
                Title = $"High failure rate for {invariant.InvariantName}",
                Description =
                    $"Invariant '{invariant.InvariantName}' has a {invariant.FailureRate:P} failure rate. Consider reviewing the validation logic or data quality.",
                InvariantName = invariant.InvariantName,
                ConceptType = invariant.ConceptType,
                Metrics = new Dictionary<string, object>
                {
                    ["FailureRate"] = invariant.FailureRate,
                    ["TotalValidations"] = invariant.TotalValidations,
                    ["FailedValidations"] = invariant.FailedValidations
                }
            });
        }

        // Slow execution recommendations
        var slowInvariants = allAnalytics.Where(a => a.AverageExecutionTime > TimeSpan.FromMilliseconds(50)).ToList();
        foreach (var invariant in slowInvariants)
        {
            recommendations.Add(new AnalyticsRecommendation
            {
                Type = RecommendationType.SlowExecution,
                Priority = invariant.AverageExecutionTime > TimeSpan.FromMilliseconds(200)
                    ? RecommendationPriority.High
                    : RecommendationPriority.Low,
                Title = $"Slow execution for {invariant.InvariantName}",
                Description =
                    $"Invariant '{invariant.InvariantName}' takes an average of {invariant.AverageExecutionTime.TotalMilliseconds:F1}ms to execute. Consider optimization.",
                InvariantName = invariant.InvariantName,
                ConceptType = invariant.ConceptType,
                Metrics = new Dictionary<string, object>
                {
                    ["AverageExecutionTime"] = invariant.AverageExecutionTime.TotalMilliseconds,
                    ["MaxExecutionTime"] = invariant.MaxExecutionTime.TotalMilliseconds
                }
            });
        }

        // Unused invariants
        var recentCutoff = DateTime.UtcNow - TimeSpan.FromDays(7);
        var unusedInvariants = allAnalytics.Where(a => a.LastValidated < recentCutoff).ToList();
        foreach (var invariant in unusedInvariants)
        {
            recommendations.Add(new AnalyticsRecommendation
            {
                Type = RecommendationType.UnusedInvariant,
                Priority = RecommendationPriority.Low,
                Title = $"Unused invariant: {invariant.InvariantName}",
                Description =
                    $"Invariant '{invariant.InvariantName}' hasn't been used in the last 7 days. Consider if it's still needed.",
                InvariantName = invariant.InvariantName,
                ConceptType = invariant.ConceptType,
                Metrics = new Dictionary<string, object>
                {
                    ["LastValidated"] = invariant.LastValidated,
                    ["DaysSinceLastUse"] = (DateTime.UtcNow - invariant.LastValidated).TotalDays
                }
            });
        }

        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }

    /// <summary>
    ///     Clear analytics data
    /// </summary>
    public void ClearAnalytics()
    {
        lock (_lock)
        {
            _metrics.Clear();
            while (_violationEvents.TryDequeue(out _))
            {
            }
        }

        logger.LogInformation("Analytics data cleared");
    }

    /// <summary>
    ///     Export analytics data
    /// </summary>
    public AnalyticsExport ExportAnalytics()
    {
        return new AnalyticsExport
        {
            ExportedAt = DateTime.UtcNow,
            Metrics = GetAllAnalytics(),
            ViolationEvents = GetRecentViolations(1000),
            PerformanceInsights = GetPerformanceInsights(),
            Recommendations = GetRecommendations()
        };
    }

    private static InvariantAnalytics CreateAnalytics(InvariantMetrics metrics)
    {
        return new InvariantAnalytics
        {
            InvariantName = metrics.InvariantName,
            ConceptType = metrics.ConceptType,
            TotalValidations = metrics.TotalValidations,
            SuccessfulValidations = metrics.SuccessfulValidations,
            FailedValidations = metrics.FailedValidations,
            SuccessRate = metrics.TotalValidations > 0
                ? (double)metrics.SuccessfulValidations / metrics.TotalValidations
                : 0,
            FailureRate = metrics.TotalValidations > 0
                ? (double)metrics.FailedValidations / metrics.TotalValidations
                : 0,
            AverageExecutionTime = metrics.TotalValidations > 0
                ? TimeSpan.FromTicks(metrics.TotalExecutionTime.Ticks / metrics.TotalValidations)
                : TimeSpan.Zero,
            MinExecutionTime = metrics.MinExecutionTime,
            MaxExecutionTime = metrics.MaxExecutionTime,
            LastValidated = metrics.LastValidated
        };
    }
}

/// <summary>
///     Internal metrics storage
/// </summary>
internal class InvariantMetrics
{
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public long TotalValidations { get; set; }
    public long SuccessfulValidations { get; set; }
    public long FailedValidations { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    public DateTime LastValidated { get; set; }
}
