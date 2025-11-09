namespace GaApi.Controllers;

using GA.Business.Graphiti.Models;
using GA.Business.Graphiti.Services;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
///     API controller for Graphiti knowledge graph operations
///     Provides temporal knowledge graph functionality for music learning
/// </summary>
[ApiController]
[Route("api/graphiti")]
[EnableRateLimiting("fixed")]
public class GraphitiController(
    IGraphitiService graphitiService,
    ILogger<GraphitiController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Add a learning episode to the knowledge graph
    /// </summary>
    /// <param name="request">Episode data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    [HttpPost("episodes")]
    [ProducesResponseType(typeof(GraphitiResponse<object>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<GraphitiResponse<object>>> AddEpisode(
        [FromBody] EpisodeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Adding episode for user {UserId}", request.UserId);

            var result = await graphitiService.AddEpisodeAsync(request, cancellationToken);

            if (result.Status == "error")
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding episode for user {UserId}", request.UserId);
            return StatusCode(500, new GraphitiResponse<object>
            {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Search the knowledge graph
    /// </summary>
    /// <param name="request">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Searching knowledge graph: {Query}", request.Query);

            var result = await graphitiService.SearchAsync(request, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching knowledge graph: {Query}", request.Query);
            return StatusCode(500, new { error = "An error occurred during search", query = request.Query });
        }
    }

    /// <summary>
    ///     Get personalized recommendations for a user
    /// </summary>
    /// <param name="request">Recommendation parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Personalized recommendations</returns>
    [HttpPost("recommendations")]
    [ProducesResponseType(typeof(RecommendationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<RecommendationResponse>> GetRecommendations(
        [FromBody] RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting recommendations for user {UserId}", request.UserId);

            var result = await graphitiService.GetRecommendationsAsync(request, cancellationToken);

            if (result.Status == "error")
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recommendations for user {UserId}", request.UserId);
            return StatusCode(500, new RecommendationResponse
            {
                Status = "error",
                UserId = request.UserId,
                RecommendationType = request.RecommendationType
            });
        }
    }

    /// <summary>
    ///     Get user's learning progress over time
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User progress data</returns>
    [HttpGet("users/{userId}/progress")]
    [ProducesResponseType(typeof(UserProgressResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<UserProgressResponse>> GetUserProgress(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting progress for user {UserId}", userId);

            var result = await graphitiService.GetUserProgressAsync(userId, cancellationToken);

            if (result.Status == "error")
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting progress for user {UserId}", userId);
            return StatusCode(500, new UserProgressResponse
            {
                Status = "error",
                UserId = userId
            });
        }
    }

    /// <summary>
    ///     Get knowledge graph statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Graph statistics</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(GraphStatsResponse), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<GraphStatsResponse>> GetGraphStats(
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting graph statistics");

            var result = await graphitiService.GetGraphStatsAsync(cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting graph statistics");
            return StatusCode(500, new GraphStatsResponse
            {
                Status = "error"
            });
        }
    }

    /// <summary>
    ///     Sync data from MongoDB to knowledge graph
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync operation result</returns>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(GraphitiResponse<object>), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<GraphitiResponse<object>>> SyncFromMongoDB(
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting MongoDB sync");

            var result = await graphitiService.SyncFromMongoDbAsync(cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing from MongoDB");
            return StatusCode(500, new GraphitiResponse<object>
            {
                Status = "error",
                Message = "Internal server error"
            });
        }
    }

    /// <summary>
    ///     Check Graphiti service health
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<ActionResult> CheckHealth(CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await graphitiService.IsHealthyAsync(cancellationToken);

            if (isHealthy)
            {
                return Ok(new { status = "healthy", service = "graphiti" });
            }

            return StatusCode(503, new { status = "unhealthy", service = "graphiti" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking Graphiti health");
            return StatusCode(503, new { status = "error", service = "graphiti" });
        }
    }
}
