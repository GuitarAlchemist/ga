namespace GA.Business.Core.Analytics;

/// <summary>
///     Analytics data for a specific invariant
/// </summary>
public class InvariantAnalytics
{
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public long TotalValidations { get; set; }
    public long SuccessfulValidations { get; set; }
    public long FailedValidations { get; set; }
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    public DateTime LastValidated { get; set; }
}

/// <summary>
///     Violation event record
/// </summary>
public class ViolationEvent
{
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
///     Violation trends over time
/// </summary>
public class ViolationTrends
{
    public TimeSpan Period { get; set; }
    public int TotalViolations { get; set; }
    public Dictionary<string, int> ViolationsByInvariant { get; set; } = [];
    public Dictionary<string, int> ViolationsByConceptType { get; set; } = [];
    public Dictionary<int, int> ViolationsByHour { get; set; } = [];
}

/// <summary>
///     Performance insights
/// </summary>
public class PerformanceInsights
{
    public long TotalValidations { get; set; }
    public long TotalFailures { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public string? SlowestInvariant { get; set; }
    public string? MostFailedInvariant { get; set; }
    public double OverallSuccessRate { get; set; }
}

/// <summary>
///     Analytics recommendation
/// </summary>
public class AnalyticsRecommendation
{
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public Dictionary<string, object> Metrics { get; set; } = [];
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Types of recommendations
/// </summary>
public enum RecommendationType
{
    HighFailureRate,
    SlowExecution,
    UnusedInvariant,
    DataQualityIssue,
    PerformanceOptimization,
    ConfigurationIssue
}

/// <summary>
///     Recommendation priority levels
/// </summary>
public enum RecommendationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
///     Analytics export data
/// </summary>
public class AnalyticsExport
{
    public DateTime ExportedAt { get; set; }
    public List<InvariantAnalytics> Metrics { get; set; } = [];
    public List<ViolationEvent> ViolationEvents { get; set; } = [];
    public PerformanceInsights PerformanceInsights { get; set; } = new();
    public List<AnalyticsRecommendation> Recommendations { get; set; } = [];
}

/// <summary>
///     Analytics dashboard data
/// </summary>
public class AnalyticsDashboard
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public OverallMetrics Overall { get; set; } = new();
    public List<InvariantAnalytics> TopFailingInvariants { get; set; } = [];
    public List<InvariantAnalytics> SlowestInvariants { get; set; } = [];
    public List<AnalyticsRecommendation> TopRecommendations { get; set; } = [];
    public ViolationTrends RecentTrends { get; set; } = new();
    public Dictionary<string, ConceptTypeMetrics> ConceptTypeMetrics { get; set; } = [];
}

/// <summary>
///     Overall system metrics
/// </summary>
public class OverallMetrics
{
    public long TotalValidations { get; set; }
    public long TotalFailures { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public int ActiveInvariants { get; set; }
    public int ConceptTypes { get; set; }
    public DateTime LastValidation { get; set; }
}

/// <summary>
///     Metrics for a specific concept type
/// </summary>
public class ConceptTypeMetrics
{
    public string ConceptType { get; set; } = string.Empty;
    public long TotalValidations { get; set; }
    public long TotalFailures { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public int ActiveInvariants { get; set; }
    public string? MostFailedInvariant { get; set; }
    public string? SlowestInvariant { get; set; }
}

/// <summary>
///     Real-time analytics update
/// </summary>
public class AnalyticsUpdate
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string? ErrorMessage { get; set; }
    public long TotalValidationsToday { get; set; }
    public long TotalFailuresToday { get; set; }
}

/// <summary>
///     Analytics configuration
/// </summary>
public class AnalyticsConfiguration
{
    public bool EnableAnalytics { get; set; } = true;
    public bool EnableRealTimeUpdates { get; set; } = true;
    public int MaxViolationEvents { get; set; } = 10000;
    public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan TrendAnalysisPeriod { get; set; } = TimeSpan.FromDays(7);
    public List<string> ExcludedInvariants { get; set; } = [];

    public Dictionary<RecommendationType, bool> EnabledRecommendations { get; set; } = new()
    {
        [RecommendationType.HighFailureRate] = true,
        [RecommendationType.SlowExecution] = true,
        [RecommendationType.UnusedInvariant] = true,
        [RecommendationType.DataQualityIssue] = true,
        [RecommendationType.PerformanceOptimization] = true,
        [RecommendationType.ConfigurationIssue] = true
    };
}

/// <summary>
///     Analytics query parameters
/// </summary>
public class AnalyticsQuery
{
    public string? ConceptType { get; set; }
    public string? InvariantName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Limit { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public List<RecommendationType>? RecommendationTypes { get; set; }
    public RecommendationPriority? MinPriority { get; set; }
}

/// <summary>
///     Analytics aggregation result
/// </summary>
public class AnalyticsAggregation
{
    public string GroupBy { get; set; } = string.Empty;
    public Dictionary<string, AggregationMetrics> Groups { get; set; } = [];
    public AggregationMetrics Overall { get; set; } = new();
}

/// <summary>
///     Aggregation metrics
/// </summary>
public class AggregationMetrics
{
    public long Count { get; set; }
    public long Successes { get; set; }
    public long Failures { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
}

/// <summary>
///     Analytics alert
/// </summary>
public class AnalyticsAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

/// <summary>
///     Alert types
/// </summary>
public enum AlertType
{
    HighFailureRate,
    PerformanceDegradation,
    UnusualActivity,
    SystemError,
    ConfigurationChange
}

/// <summary>
///     Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

/// <summary>
///     Analytics health status
/// </summary>
public class AnalyticsHealth
{
    public bool IsHealthy { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public int ActiveAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
    public List<string> Issues { get; set; } = [];
    public Dictionary<string, object> Metrics { get; set; } = [];
}
