namespace GA.Analytics.Service.Models;

/// <summary>
/// Deep relationship analysis result
/// </summary>
public class DeepRelationshipAnalysis
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public double RelationshipStrength { get; set; }
    public Dictionary<string, object> AnalysisData { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Musical trend analysis result
/// </summary>
public class MusicalTrendAnalysis
{
    public string Id { get; set; } = string.Empty;
    public string TrendType { get; set; } = string.Empty;
    public double TrendStrength { get; set; }
    public List<TrendDataPoint> DataPoints { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trend data point
/// </summary>
public class TrendDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Intelligent practice session
/// </summary>
public class IntelligentPracticeSession
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public List<PracticeExercise> Exercises { get; set; } = new();
    public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Practice exercise
/// </summary>
public class PracticeExercise
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public TimeSpan Duration { get; set; }
    public double CompletionScore { get; set; }
}

/// <summary>
/// Personalized curriculum
/// </summary>
public class PersonalizedCurriculum
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CurriculumName { get; set; } = string.Empty;
    public List<CurriculumModule> Modules { get; set; } = new();
    public Dictionary<string, object> PersonalizationData { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Curriculum module
/// </summary>
public class CurriculumModule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public List<string> LearningObjectives { get; set; } = new();
}

/// <summary>
/// Realtime recommendations
/// </summary>
public class RealtimeRecommendations
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<Recommendation> Recommendations { get; set; } = new();
    public Dictionary<string, double> ConfidenceScores { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Individual recommendation
/// </summary>
public class Recommendation
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Priority { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Complexity metrics
/// </summary>
public class ComplexityMetrics
{
    public string Id { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public double OverallComplexity { get; set; }
    public Dictionary<string, double> ComponentComplexities { get; set; } = new();
    public Dictionary<string, object> AnalysisDetails { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Concept cluster
/// </summary>
public class ConceptCluster
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> ConceptIds { get; set; } = new();
    public Dictionary<string, double> ConceptWeights { get; set; } = new();
    public double ClusterCoherence { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Learning recommendation
/// </summary>
public class LearningRecommendation
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RecommendationType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Shape graph build options
/// </summary>
public class ShapeGraphBuildOptions
{
    public bool IncludeConnections { get; set; } = true;
    public int MaxDepth { get; set; } = 5;
    public int MaxFret { get; set; } = 12;
    public int MaxSpan { get; set; } = 4;
    public int MaxShapesPerSet { get; set; } = 10;
    public string Algorithm { get; set; } = "default";
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Agent interaction graph
/// </summary>
public class AgentInteractionGraph
{
    public string Id { get; set; } = string.Empty;
    public List<AgentNode> Nodes { get; set; } = new();
    public List<AgentNode> Agents { get; set; } = new();
    public List<AgentEdge> Edges { get; set; } = new();
    public bool IsUndirected { get; set; } = false;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Agent node
/// </summary>
public class AgentNode
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> Signals { get; set; } = new();
}

/// <summary>
/// Agent edge
/// </summary>
public class AgentEdge
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Concept validation result
/// </summary>
public class ConceptValidationResult
{
    public string ConceptId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public double ConfidenceScore { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public Dictionary<string, object> ValidationData { get; set; } = new();

    // Additional properties needed by controllers
    public List<string> Successes { get; set; } = new();
    public List<object> Results { get; set; } = new();
}

/// <summary>
/// ICV computation result
/// </summary>
public class ICVResult
{
    public string Id { get; set; } = string.Empty;
    public double ICVValue { get; set; }
    public string ConceptId { get; set; } = string.Empty;
    public Dictionary<string, double> ComponentScores { get; set; } = new();
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Agent interaction edge
/// </summary>
public class AgentInteractionEdge
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public string InteractionType { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, object> Features { get; set; } = new();
}

/// <summary>
/// Validation statistics
/// </summary>
public class ValidationStatistics
{
    public int TotalConcepts { get; set; }
    public int TotalViolations { get; set; }
    public double OverallSuccessRate { get; set; }
    public Dictionary<string, int> ConceptCounts { get; set; } = new();
}

/// <summary>
/// Violation statistics
/// </summary>
public class ViolationStatistics
{
    public int CriticalViolations { get; set; }
    public int ErrorViolations { get; set; }
    public int WarningViolations { get; set; }
    public double OverallHealthScore { get; set; }
    public Dictionary<string, int> ViolationsByType { get; set; } = new();
}

/// <summary>
/// Harmonic analysis options
/// </summary>
public class HarmonicAnalysisOptions
{
    public bool IncludeVoiceLeading { get; set; } = true;
    public bool AnalyzeProgressions { get; set; } = true;
    public int MaxDepth { get; set; } = 5;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Progression constraints
/// </summary>
public class ProgressionConstraints
{
    public int MinLength { get; set; } = 4;
    public int MaxLength { get; set; } = 8;
    public List<string> AllowedChords { get; set; } = new();
    public List<string> ForbiddenProgressions { get; set; } = new();
    public Dictionary<string, object> Rules { get; set; } = new();
}
