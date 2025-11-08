namespace GA.Business.Graphiti.Services;

using Models;

/// <summary>
///     Interface for Graphiti knowledge graph operations
/// </summary>
public interface IGraphitiService
{
    /// <summary>
    ///     Add a learning episode to the knowledge graph
    /// </summary>
    Task<GraphitiResponse<object>> AddEpisodeAsync(EpisodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Search the knowledge graph
    /// </summary>
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get personalized recommendations for a user
    /// </summary>
    Task<RecommendationResponse> GetRecommendationsAsync(RecommendationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get user's learning progress
    /// </summary>
    Task<UserProgressResponse> GetUserProgressAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get knowledge graph statistics
    /// </summary>
    Task<GraphStatsResponse> GetGraphStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sync data from MongoDB to the knowledge graph
    /// </summary>
    Task<GraphitiResponse<object>> SyncFromMongoDbAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if the Graphiti service is healthy
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
