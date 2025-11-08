namespace GA.Business.Graphiti.Models;

using System.Text.Json.Serialization;

/// <summary>
///     Request model for adding learning episodes to Graphiti
/// </summary>
public class EpisodeRequest
{
    [JsonPropertyName("user_id")] public required string UserId { get; set; }

    [JsonPropertyName("episode_type")] public required string EpisodeType { get; set; }

    [JsonPropertyName("content")] public required Dictionary<string, object> Content { get; set; }

    [JsonPropertyName("timestamp")] public DateTime? Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Request model for searching the knowledge graph
/// </summary>
public class SearchRequest
{
    [JsonPropertyName("query")] public required string Query { get; set; }

    [JsonPropertyName("search_type")] public string SearchType { get; set; } = "hybrid";

    [JsonPropertyName("limit")] public int Limit { get; set; } = 10;

    [JsonPropertyName("user_id")] public string? UserId { get; set; }
}

/// <summary>
///     Request model for getting personalized recommendations
/// </summary>
public class RecommendationRequest
{
    [JsonPropertyName("user_id")] public required string UserId { get; set; }

    [JsonPropertyName("recommendation_type")]
    public required string RecommendationType { get; set; }

    [JsonPropertyName("context")] public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
///     Response model for API operations
/// </summary>
public class GraphitiResponse<T>
{
    [JsonPropertyName("status")] public required string Status { get; set; }

    [JsonPropertyName("message")] public string? Message { get; set; }

    [JsonPropertyName("data")] public T? Data { get; set; }
}

/// <summary>
///     Search result model
/// </summary>
public class SearchResponse
{
    [JsonPropertyName("status")] public required string Status { get; set; }

    [JsonPropertyName("query")] public required string Query { get; set; }

    [JsonPropertyName("results")] public List<SearchResult> Results { get; set; } = [];

    [JsonPropertyName("count")] public int Count { get; set; }
}

/// <summary>
///     Individual search result
/// </summary>
public class SearchResult
{
    [JsonPropertyName("content")] public required string Content { get; set; }

    [JsonPropertyName("score")] public double Score { get; set; }

    [JsonPropertyName("type")] public string? Type { get; set; }
}

/// <summary>
///     Recommendation response model
/// </summary>
public class RecommendationResponse
{
    [JsonPropertyName("status")] public required string Status { get; set; }

    [JsonPropertyName("user_id")] public required string UserId { get; set; }

    [JsonPropertyName("recommendation_type")]
    public required string RecommendationType { get; set; }

    [JsonPropertyName("recommendations")] public List<Recommendation> Recommendations { get; set; } = [];
}

/// <summary>
///     Individual recommendation
/// </summary>
public class Recommendation
{
    [JsonPropertyName("type")] public required string Type { get; set; }

    [JsonPropertyName("content")] public required string Content { get; set; }

    [JsonPropertyName("confidence")] public double Confidence { get; set; }

    [JsonPropertyName("reasoning")] public string? Reasoning { get; set; }
}

/// <summary>
///     User progress response model
/// </summary>
public class UserProgressResponse
{
    [JsonPropertyName("status")] public required string Status { get; set; }

    [JsonPropertyName("user_id")] public required string UserId { get; set; }

    [JsonPropertyName("progress")] public UserProgress? Progress { get; set; }
}

/// <summary>
///     User progress data
/// </summary>
public class UserProgress
{
    [JsonPropertyName("skill_level")] public double SkillLevel { get; set; }

    [JsonPropertyName("sessions_completed")]
    public int SessionsCompleted { get; set; }

    [JsonPropertyName("recent_activity")] public string? RecentActivity { get; set; }

    [JsonPropertyName("improvement_trend")]
    public string? ImprovementTrend { get; set; }

    [JsonPropertyName("next_milestone")] public string? NextMilestone { get; set; }
}

/// <summary>
///     Graph statistics response
/// </summary>
public class GraphStatsResponse
{
    [JsonPropertyName("status")] public required string Status { get; set; }

    [JsonPropertyName("stats")] public GraphStats? Stats { get; set; }
}

/// <summary>
///     Graph statistics data
/// </summary>
public class GraphStats
{
    [JsonPropertyName("total_nodes")] public int TotalNodes { get; set; }

    [JsonPropertyName("total_edges")] public int TotalEdges { get; set; }

    [JsonPropertyName("user_count")] public int UserCount { get; set; }

    [JsonPropertyName("chord_count")] public int ChordCount { get; set; }

    [JsonPropertyName("scale_count")] public int ScaleCount { get; set; }
}
