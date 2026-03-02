namespace GA.Analytics.Service.Models;

using Domain.Services.Atonal.Grothendieck;
using Domain.Services.Validation;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
/// Deep relationship analysis result
/// </summary>
public class DeepRelationshipAnalysis
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public double RelationshipStrength { get; set; }
    public Dictionary<string, object> AnalysisData { get; set; } = [];
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
    public List<TrendDataPoint> DataPoints { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = [];
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trend data point
/// </summary>
public class TrendDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public Dictionary<string, object> Properties { get; set; } = [];
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
    public List<PracticeExercise> Exercises { get; set; } = [];
    public Dictionary<string, double> PerformanceMetrics { get; set; } = [];
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
    public List<CurriculumModule> Modules { get; set; } = [];
    public Dictionary<string, object> PersonalizationData { get; set; } = [];
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
    public List<string> Prerequisites { get; set; } = [];
    public List<string> LearningObjectives { get; set; } = [];
}

/// <summary>
/// Realtime recommendations
/// </summary>
public class RealtimeRecommendations
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<Recommendation> Recommendations { get; set; } = [];
    public Dictionary<string, double> ConfidenceScores { get; set; } = [];
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
    public Dictionary<string, object> Data { get; set; } = [];
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
    public Dictionary<string, double> ComponentComplexities { get; set; } = [];
    public Dictionary<string, object> AnalysisDetails { get; set; } = [];
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Concept cluster
/// </summary>
public class ConceptCluster
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> ConceptIds { get; set; } = [];
    public Dictionary<string, double> ConceptWeights { get; set; } = [];
    public double ClusterCoherence { get; set; }
    public Dictionary<string, object> Properties { get; set; } = [];
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
    public Dictionary<string, object> Context { get; set; } = [];
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
    public Dictionary<string, object> Parameters { get; set; } = [];
}

/// <summary>
/// Concept validation result
/// </summary>
public class ConceptValidationResult
{
    public string ConceptId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public double ConfidenceScore { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
    public Dictionary<string, object> ValidationData { get; set; } = [];

    // Additional properties needed by controllers
    public List<string> Successes { get; set; } = [];
    public List<object> Results { get; set; } = [];
}

/// <summary>
/// ICV computation result
/// </summary>
public class ICVResult
{
    public string Id { get; set; } = string.Empty;
    public double ICVValue { get; set; }
    public string ConceptId { get; set; } = string.Empty;
    public Dictionary<string, double> ComponentScores { get; set; } = [];
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Validation statistics
/// </summary>
public class ValidationStatistics
{
    public int TotalConcepts { get; set; }
    public int TotalViolations { get; set; }
    public double OverallSuccessRate { get; set; }
    public Dictionary<string, int> ConceptCounts { get; set; } = [];
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
    public Dictionary<string, int> ViolationsByType { get; set; } = [];
}

/// <summary>
/// Harmonic analysis options
/// </summary>
public class HarmonicAnalysisOptions
{
    public bool IncludeVoiceLeading { get; set; } = true;
    public bool AnalyzeProgressions { get; set; } = true;
    public int MaxDepth { get; set; } = 5;
    public Dictionary<string, object> Parameters { get; set; } = [];
}

/// <summary>
/// Progression constraints
/// </summary>
public class ProgressionConstraints
{
    public int MinLength { get; set; } = 4;
    public int MaxLength { get; set; } = 8;
    public List<string> AllowedChords { get; set; } = [];
    public List<string> ForbiddenProgressions { get; set; } = [];
    public Dictionary<string, object> Rules { get; set; } = [];
}

public class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string SkillLevel { get; set; } = "intermediate";
    public List<string> PreferredGenres { get; set; } = [];
    public List<string> Instruments { get; set; } = [];
    public Dictionary<string, object> Preferences { get; set; } = [];
}

public record ComputeIcvRequest(int[] PitchClasses);
public record ComputeDeltaRequest(IntervalClassVector Source, IntervalClassVector Target);
public record GrothendieckDeltaResponse(GrothendieckDelta Delta, double Cost, string Explanation);
public class FindNearbyRequest
{
    public PitchClassSet Source { get; set; } = default!;
    public int Limit { get; set; }
    public double MaxDistance { get; set; }
}
public record NearbySetResponse(PitchClassSet Set, GrothendieckDelta Delta, double Cost);

public class GlobalValidationResult
{
    public string Id { get; set; } = string.Empty;
    public int TotalInvariants { get; set; }
    public int ValidInvariants { get; set; }
    public int InvalidInvariants { get; set; }
    public List<ValidationResultDetail> ValidationResults { get; set; } = [];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ValidationSummary GetSummary() =>
        new()
        {
            TotalConcepts = TotalInvariants,
            ValidConcepts = ValidInvariants,
            OverallSuccessRate = TotalInvariants > 0 ? (double)ValidInvariants / TotalInvariants : 0,
            GeneratedAt = Timestamp
        };

    public Dictionary<string, CompositeInvariantValidationResult> IconicChordResults { get; set; } = [];
    public Dictionary<string, CompositeInvariantValidationResult> ChordProgressionResults { get; set; } = [];
    public Dictionary<string, CompositeInvariantValidationResult> GuitarTechniqueResults { get; set; } = [];
    public Dictionary<string, CompositeInvariantValidationResult> SpecializedTuningResults { get; set; } = [];
}

public class ValidationResultDetail
{
    public string InvariantId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public double Score { get; set; }
}

public class ValidationSummary
{
    public int TotalConcepts { get; set; }
    public int ValidConcepts { get; set; }
    public int TotalInvariants { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int ErrorViolations { get; set; }
    public int WarningViolations { get; set; }
    public int InfoViolations { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class CacheStatistics
{
    public string Id { get; set; } = string.Empty;
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public long TotalMemoryUsage { get; set; }
    public Dictionary<string, long> CategoryStats { get; set; } = [];

    public double TotalHitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
}

public class InvariantViolationEvent
{
    public string Id { get; set; } = string.Empty;
    public string InvariantId { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = [];
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
