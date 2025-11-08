namespace GuitarAlchemistChatbot.Services;

using System.Text.Json;

/// <summary>
///     HTTP client for communicating with Graphiti knowledge graph service
/// </summary>
public class GraphitiClient(HttpClient httpClient, ILogger<GraphitiClient> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Add a learning episode to the knowledge graph
    /// </summary>
    public async Task<GraphitiResponse?> AddEpisodeAsync(
        string userId,
        string episodeType,
        Dictionary<string, object> content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Adding episode for user {UserId}: {Type}", userId, episodeType);

            var request = new
            {
                user_id = userId,
                episode_type = episodeType,
                content,
                timestamp = DateTime.UtcNow
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/graphiti/episodes",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GraphitiResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding episode to Graphiti");
            return null;
        }
    }

    /// <summary>
    ///     Search the knowledge graph
    /// </summary>
    public async Task<GraphitiSearchResponse?> SearchAsync(
        string query,
        string? userId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Searching Graphiti: {Query}", query);

            var request = new
            {
                query,
                search_type = "hybrid",
                limit,
                user_id = userId
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/graphiti/search",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GraphitiSearchResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching Graphiti");
            return null;
        }
    }

    /// <summary>
    ///     Get personalized recommendations
    /// </summary>
    public async Task<GraphitiRecommendationResponse?> GetRecommendationsAsync(
        string userId,
        string recommendationType,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Getting recommendations for user {UserId}: {Type}",
                userId,
                recommendationType);

            var request = new
            {
                user_id = userId,
                recommendation_type = recommendationType,
                context
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/graphiti/recommendations",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GraphitiRecommendationResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recommendations from Graphiti");
            return null;
        }
    }

    /// <summary>
    ///     Get user's learning progress
    /// </summary>
    public async Task<GraphitiUserProgressResponse?> GetUserProgressAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting progress for user {UserId}", userId);

            var response = await httpClient.GetAsync(
                $"/api/graphiti/users/{userId}/progress",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GraphitiUserProgressResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user progress from Graphiti");
            return null;
        }
    }

    /// <summary>
    ///     Check if Graphiti service is healthy
    /// </summary>
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("/api/graphiti/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// Response DTOs (matching Graphiti API)
public record GraphitiResponse(
    string Status,
    string? Message,
    object? Data);

public record GraphitiSearchResponse(
    string Status,
    string Query,
    List<GraphitiSearchResult> Results,
    int Count);

public record GraphitiSearchResult(
    string Content,
    double Score,
    string? Type);

public record GraphitiRecommendationResponse(
    string Status,
    string UserId,
    string RecommendationType,
    List<GraphitiRecommendation> Recommendations);

public record GraphitiRecommendation(
    string Type,
    string Content,
    double Confidence,
    string? Reasoning);

public record GraphitiUserProgressResponse(
    string Status,
    string UserId,
    GraphitiUserProgress? Progress);

public record GraphitiUserProgress(
    double SkillLevel,
    int SessionsCompleted,
    string? RecentActivity,
    string? ImprovementTrend,
    string? NextMilestone);
