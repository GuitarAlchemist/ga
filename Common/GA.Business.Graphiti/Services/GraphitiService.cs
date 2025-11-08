namespace GA.Business.Graphiti.Services;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;

/// <summary>
///     Configuration options for Graphiti service
/// </summary>
public class GraphitiOptions
{
    public const string SectionName = "Graphiti";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
///     Service for communicating with the Graphiti knowledge graph API
/// </summary>
public class GraphitiService : IGraphitiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<GraphitiService> _logger;
    private readonly GraphitiOptions _options;

    public GraphitiService(
        HttpClient httpClient,
        IOptions<GraphitiOptions> options,
        ILogger<GraphitiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = _options.Timeout;
    }

    public async Task<GraphitiResponse<object>> AddEpisodeAsync(
        EpisodeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding episode for user {UserId}", request.UserId);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/episodes", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GraphitiResponse<object>>(responseJson, _jsonOptions);

            _logger.LogInformation("Successfully added episode for user {UserId}", request.UserId);
            return result ?? new GraphitiResponse<object> { Status = "error", Message = "Invalid response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add episode for user {UserId}", request.UserId);
            return new GraphitiResponse<object>
            {
                Status = "error",
                Message = ex.Message
            };
        }
    }

    public async Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching knowledge graph: {Query}", request.Query);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/search", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<SearchResponse>(responseJson, _jsonOptions);

            _logger.LogInformation("Search completed: {ResultCount} results", result?.Count ?? 0);
            return result ?? new SearchResponse { Status = "error", Query = request.Query };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search knowledge graph: {Query}", request.Query);
            return new SearchResponse
            {
                Status = "error",
                Query = request.Query
            };
        }
    }

    public async Task<RecommendationResponse> GetRecommendationsAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting recommendations for user {UserId}", request.UserId);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/recommendations", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<RecommendationResponse>(responseJson, _jsonOptions);

            _logger.LogInformation("Got {Count} recommendations for user {UserId}",
                result?.Recommendations.Count ?? 0, request.UserId);
            return result ?? new RecommendationResponse
            {
                Status = "error",
                UserId = request.UserId,
                RecommendationType = request.RecommendationType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendations for user {UserId}", request.UserId);
            return new RecommendationResponse
            {
                Status = "error",
                UserId = request.UserId,
                RecommendationType = request.RecommendationType
            };
        }
    }

    public async Task<UserProgressResponse> GetUserProgressAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting progress for user {UserId}", userId);

            var response = await _httpClient.GetAsync($"/users/{userId}/progress", cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<UserProgressResponse>(responseJson, _jsonOptions);

            _logger.LogInformation("Retrieved progress for user {UserId}", userId);
            return result ?? new UserProgressResponse { Status = "error", UserId = userId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get progress for user {UserId}", userId);
            return new UserProgressResponse { Status = "error", UserId = userId };
        }
    }

    public async Task<GraphStatsResponse> GetGraphStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting graph statistics");

            var response = await _httpClient.GetAsync("/graph/stats", cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GraphStatsResponse>(responseJson, _jsonOptions);

            _logger.LogInformation("Retrieved graph statistics");
            return result ?? new GraphStatsResponse { Status = "error" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get graph statistics");
            return new GraphStatsResponse { Status = "error" };
        }
    }

    public async Task<GraphitiResponse<object>> SyncFromMongoDbAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting MongoDB sync");

            var response = await _httpClient.PostAsync("/graph/sync", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<GraphitiResponse<object>>(responseJson, _jsonOptions);

            _logger.LogInformation("MongoDB sync completed");
            return result ?? new GraphitiResponse<object> { Status = "error", Message = "Invalid response" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync from MongoDB");
            return new GraphitiResponse<object> { Status = "error", Message = ex.Message };
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return false;
        }
    }
}
