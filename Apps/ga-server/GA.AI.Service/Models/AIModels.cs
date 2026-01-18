namespace GA.AI.Service.Models;

/// <summary>
/// Style learning system state
/// </summary>
public class StyleLearningSystemState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pattern recognition system state
/// </summary>
public class PatternRecognitionSystemState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Player style profile
/// </summary>
public class PlayerStyleProfile
{
    public string Id { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public Dictionary<string, double> StyleMetrics { get; set; } = new();
    public List<string> PreferredGenres { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Additional properties needed by controllers
    public double PreferredComplexity { get; set; } = 0.5;
    public double ExplorationRate { get; set; } = 0.3;
    public List<string> TopChordFamilies { get; set; } = new();
    public int FavoriteProgressionCount { get; set; }
    public int TotalProgressionsAnalyzed { get; set; }
}

/// <summary>
/// Adaptive learning path
/// </summary>
public class AdaptiveLearningPath
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<LearningStep> Steps { get; set; } = new();
    public double Progress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Learning step
/// </summary>
public class LearningStep
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>
/// Adaptive learning request
/// </summary>
public class AdaptiveLearningRequest
{
    public string UserId { get; set; } = string.Empty;
    public string LearningGoal { get; set; } = string.Empty;
    public Dictionary<string, object> UserPreferences { get; set; } = new();
    public List<string> CompletedTopics { get; set; } = new();
}

/// <summary>
/// Adaptation result
/// </summary>
public class AdaptationResult
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<string> Adaptations { get; set; } = new();
    public double ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance data
/// </summary>
public class PerformanceData
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, double> Metrics { get; set; } = new();
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Additional properties needed by controllers
    public List<double> Scores { get; set; } = new();
    public double EngagementScore { get; set; }
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
/// Practice session request
/// </summary>
public class PracticeSessionRequest
{
    public string UserId { get; set; } = string.Empty;
    public string SessionType { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public List<string> FocusAreas { get; set; } = new();
}

/// <summary>
/// Learning assistance
/// </summary>
public class LearningAssistance
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AssistanceType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Engagement analysis
/// </summary>
public class EngagementAnalysis
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public double EngagementScore { get; set; }
    public Dictionary<string, double> EngagementFactors { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Personalized achievement system
/// </summary>
public class PersonalizedAchievementSystem
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<Achievement> Achievements { get; set; } = new();
    public Dictionary<string, object> PersonalizationData { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Achievement
/// </summary>
public class Achievement
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
}

/// <summary>
/// Adapted recommendation
/// </summary>
public class AdaptedRecommendation
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RecommendationType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public Dictionary<string, object> AdaptationContext { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Update performance request model
/// </summary>
public class UpdatePerformance
{
    public UpdatePerformance() { }
    public UpdatePerformance(double accuracy, double speed, double consistency, string shapeId)
    {
        Accuracy = accuracy;
        Speed = speed;
        Consistency = consistency;
        ShapeId = shapeId;
    }

    public double Accuracy { get; set; }
    public double Speed { get; set; }
    public double Consistency { get; set; }
    public string ShapeId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Activity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Difficulty response model
/// </summary>
public class DifficultyResponse
{
    public double CurrentDifficulty { get; set; }
    public double RecommendedDifficulty { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Dictionary<string, object> Adjustments { get; set; } = new();
}

/// <summary>
/// Session state response model
/// </summary>
public class SessionStateResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Progress { get; set; }
    public int TotalExercises { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Get session state request model
/// </summary>
public class GetSessionState
{
    public string SessionId { get; set; } = string.Empty;
    public bool IncludeHistory { get; set; } = false;
}
