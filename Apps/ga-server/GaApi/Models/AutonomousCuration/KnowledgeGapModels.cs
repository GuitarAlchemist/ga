namespace GaApi.Models.AutonomousCuration;

/// <summary>
/// Result of knowledge gap analysis
/// </summary>
public class KnowledgeGapAnalysis
{
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    public List<KnowledgeGap> Gaps { get; set; } = new();
    public int TotalGaps => Gaps.Count;

    /// <summary>
    /// Get gaps by priority level
    /// </summary>
    public List<KnowledgeGap> GetGapsByPriority(string priority)
    {
        return [.. Gaps.Where(g => g.Priority.Equals(priority, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Get gaps by category
    /// </summary>
    public List<KnowledgeGap> GetGapsByCategory(string category)
    {
        return [.. Gaps.Where(g => g.Category.Equals(category, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Get top priority gaps
    /// </summary>
    public List<KnowledgeGap> GetTopPriorityGaps(int count)
    {
        var priorityOrder = new Dictionary<string, int>
        {
            { "Critical", 0 },
            { "High", 1 },
            { "Medium", 2 },
            { "Low", 3 }
        };

        return [.. Gaps
            .OrderBy(g => priorityOrder.GetValueOrDefault(g.Priority, 999))
            .Take(count)];
    }
}

/// <summary>
/// Represents a gap in the knowledge base
/// </summary>
public class KnowledgeGap
{
    public string Category { get; set; } = string.Empty; // ChordProgression, Scale, Technique, Theory
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium"; // Critical, High, Medium, Low
    public string SuggestedSearchQuery { get; set; } = string.Empty;
    public string PriorityReason { get; set; } = string.Empty;
    public List<string> DependentTopics { get; set; } = new();
    public int EstimatedLearningTimeMinutes { get; set; }
}

/// <summary>
/// Request to start autonomous curation
/// </summary>
public class StartAutonomousCurationRequest
{
    public int MaxVideosPerGap { get; set; } = 3;
    public int MaxTotalVideos { get; set; } = 10;
    public double MinQualityScore { get; set; } = 0.7;
    public List<string> FocusCategories { get; set; } = new();
    public List<string> FocusPriorities { get; set; } = new();
}

/// <summary>
/// Result of autonomous curation process
/// </summary>
public class AutonomousCurationResult
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int GapsAnalyzed { get; set; }
    public int VideosFound { get; set; }
    public int VideosEvaluated { get; set; }
    public int VideosAccepted { get; set; }
    public int VideosRejected { get; set; }
    public List<CurationDecision> Decisions { get; set; } = new();
}

/// <summary>
/// Decision made about a video
/// </summary>
public class CurationDecision
{
    public DateTime DecisionTime { get; set; }
    public string Action { get; set; } = string.Empty; // Accept, Reject, NeedsReview
    public string VideoId { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public KnowledgeGap? RelatedGap { get; set; }
    public double QualityScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> PositiveFactors { get; set; } = new();
    public List<string> NegativeFactors { get; set; } = new();
}

/// <summary>
/// Video quality evaluation result
/// </summary>
public class VideoEvaluation
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime PublishedAt { get; set; }
    
    public double QualityScore { get; set; }
    public double RelevanceScore { get; set; }
    public double EducationalValueScore { get; set; }
    public double EngagementScore { get; set; }
    
    public string EvaluationReasoning { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty; // Accept, Reject, NeedsReview
    public List<string> PositiveFactors { get; set; } = new();
    public List<string> NegativeFactors { get; set; } = new();
}

/// <summary>
/// YouTube search result
/// </summary>
public class YouTubeSearchResult
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime PublishedAt { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
}

