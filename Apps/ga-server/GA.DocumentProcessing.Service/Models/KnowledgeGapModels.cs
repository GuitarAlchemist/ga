namespace GA.DocumentProcessing.Service.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Analysis of knowledge gaps in the Guitar Alchemist knowledge base
/// </summary>
public class KnowledgeGapAnalysis
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public DateTime AnalysisDate { get; set; }
    public List<KnowledgeGap> Gaps { get; set; } = new();
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Get gaps by priority
    /// </summary>
    public List<KnowledgeGap> GetGapsByPriority(string priority)
    {
        return Gaps.Where(g => g.Priority == priority).ToList();
    }

    /// <summary>
    /// Get gaps by category
    /// </summary>
    public List<KnowledgeGap> GetGapsByCategory(string category)
    {
        return Gaps.Where(g => g.Category == category).ToList();
    }

    /// <summary>
    /// Get top N priority gaps
    /// </summary>
    public List<KnowledgeGap> GetTopPriorityGaps(int count)
    {
        var priorityOrder = new Dictionary<string, int>
        {
            ["Critical"] = 4,
            ["High"] = 3,
            ["Medium"] = 2,
            ["Low"] = 1
        };

        return Gaps
            .OrderByDescending(g => priorityOrder.GetValueOrDefault(g.Priority, 0))
            .Take(count)
            .ToList();
    }
}

/// <summary>
/// Represents a specific knowledge gap
/// </summary>
public class KnowledgeGap
{
    /// <summary>
    /// Category of knowledge (ChordProgression, Scale, Technique, Theory, etc.)
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific topic that's missing
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Description of the gap
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (Critical, High, Medium, Low)
    /// </summary>
    public string Priority { get; set; } = "Medium";

    /// <summary>
    /// Suggested YouTube search query to fill this gap
    /// </summary>
    public string SuggestedSearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Reason for the priority level
    /// </summary>
    public string? PriorityReason { get; set; }

    /// <summary>
    /// Related topics that depend on this knowledge
    /// </summary>
    public List<string> DependentTopics { get; set; } = new();

    /// <summary>
    /// Estimated learning time in minutes
    /// </summary>
    public int? EstimatedLearningTimeMinutes { get; set; }
}

/// <summary>
/// Request to start autonomous YouTube curation based on knowledge gaps
/// </summary>
public class StartAutonomousCurationRequest
{
    /// <summary>
    /// Maximum number of videos to search for per gap
    /// </summary>
    public int MaxVideosPerGap { get; set; } = 3;

    /// <summary>
    /// Maximum total videos to process
    /// </summary>
    public int MaxTotalVideos { get; set; } = 10;

    /// <summary>
    /// Minimum video quality score (0-1)
    /// </summary>
    public double MinQualityScore { get; set; } = 0.7;

    /// <summary>
    /// Focus on specific categories (empty = all categories)
    /// </summary>
    public List<string> FocusCategories { get; set; } = new();

    /// <summary>
    /// Focus on specific priority levels (empty = all priorities)
    /// </summary>
    public List<string> FocusPriorities { get; set; } = new();
}

/// <summary>
/// Result of autonomous curation process
/// </summary>
public class AutonomousCurationResult
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = "Running";
    public int GapsAnalyzed { get; set; }
    public int VideosFound { get; set; }
    public int VideosEvaluated { get; set; }
    public int VideosAccepted { get; set; }
    public int VideosRejected { get; set; }
    public List<CurationDecision> Decisions { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Decision made by the autonomous curation system
/// </summary>
public class CurationDecision
{
    public DateTime DecisionTime { get; set; }
    public string Action { get; set; } = string.Empty; // "Accept", "Reject", "NeedsReview"
    public string VideoId { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public KnowledgeGap RelatedGap { get; set; } = new();
    public double QualityScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> PositiveFactors { get; set; } = new();
    public List<string> NegativeFactors { get; set; } = new();
}

/// <summary>
/// YouTube video evaluation result
/// </summary>
public class VideoEvaluation
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Overall quality score (0-1)
    /// </summary>
    public double QualityScore { get; set; }

    /// <summary>
    /// Relevance to the knowledge gap (0-1)
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Educational value score (0-1)
    /// </summary>
    public double EducationalValueScore { get; set; }

    /// <summary>
    /// Engagement score based on views/likes ratio (0-1)
    /// </summary>
    public double EngagementScore { get; set; }

    /// <summary>
    /// Detailed evaluation reasoning from Ollama
    /// </summary>
    public string EvaluationReasoning { get; set; } = string.Empty;

    /// <summary>
    /// Recommended action (Accept, Reject, NeedsReview)
    /// </summary>
    public string RecommendedAction { get; set; } = "NeedsReview";
}

