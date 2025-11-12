namespace GA.Business.AI;

/// <summary>
///     AI-generated recommendation
/// </summary>
public class AiRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = [];
}

/// <summary>
///     Data quality analysis result
/// </summary>
public class DataQualityAnalysis
{
    public string ConceptType { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public Dictionary<string, double> CategoryScores { get; set; } = [];
}

/// <summary>
///     AI-suggested invariant
/// </summary>
public class InvariantSuggestion
{
    public string Name { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string TargetProperty { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string EstimatedImpact { get; set; } = string.Empty;
    public Dictionary<string, object> SuggestedConfiguration { get; set; } = [];
    public DateTime SuggestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Validation failure prediction
/// </summary>
public class ValidationPrediction
{
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public double RiskScore { get; set; }
    public double PredictedFailureRate { get; set; }
    public double Confidence { get; set; }
    public List<string> RecommendedActions { get; set; } = [];
    public DateTime PredictedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Invariant optimization result
/// </summary>
public class InvariantOptimization
{
    public DateTime OptimizedAt { get; set; } = DateTime.UtcNow;
    public object? CurrentPerformance { get; set; }
    public List<OptimizationRecommendation> Recommendations { get; set; } = [];
    public double EstimatedImprovement { get; set; }
}

/// <summary>
///     Optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public OptimizationType Type { get; set; }
    public string InvariantName { get; set; } = string.Empty;
    public string ConceptType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExpectedImprovement { get; set; } = string.Empty;
    public OptimizationPriority Priority { get; set; }
}

public enum OptimizationType
{
    Performance,
    Accuracy,
    Configuration,
    DataQuality
}

public enum OptimizationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
///     AI configuration
/// </summary>
public class AiConfiguration
{
    public bool EnableAi { get; set; } = false;
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
    public bool EnableCaching { get; set; } = true;
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);
}
