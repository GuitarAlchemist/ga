namespace GaApi.Controllers;

using Microsoft.AspNetCore.RateLimiting;
using Services;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("semantic")]
public class VectorSearchController(
    VectorSearchService vectorSearch,
    ICachingService cache,
    PerformanceMetricsService metrics,
    ILogger<VectorSearchController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Semantic search: Find chords using natural language queries
    /// </summary>
    /// <param name="q">Natural language query (e.g., "dark moody jazz chords")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="numCandidates">Number of candidates to consider (higher = more accurate but slower)</param>
    /// <returns>List of chords ranked by semantic similarity</returns>
    [HttpGet("semantic")]
    public async Task<ActionResult<List<ChordSearchResult>>> SemanticSearch(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        [FromQuery] int numCandidates = 100)
    {
        using var _ = metrics.TrackSemanticRequest();

        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Query parameter 'q' is required");
            }

            // Use semantic caching with shorter TTL
            var cacheKey = $"semantic_{q}_{limit}_{numCandidates}";
            var results = await cache.GetOrCreateSemanticAsync(cacheKey,
                async () => { return await vectorSearch.SemanticSearchAsync(q, limit, numCandidates); });

            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            metrics.RecordSemanticError();
            logger.LogError(ex, "OpenAI API not configured");
            return StatusCode(503, "Vector search is not configured. Please set OpenAI API key.");
        }
        catch (Exception ex)
        {
            metrics.RecordSemanticError();
            logger.LogError(ex, "Error performing semantic search");
            return StatusCode(500, "Error performing semantic search");
        }
    }

    /// <summary>
    ///     Find chords similar to a given chord
    /// </summary>
    /// <param name="id">ID of the chord to find similar chords for</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="numCandidates">Number of candidates to consider</param>
    /// <returns>List of similar chords ranked by similarity</returns>
    [HttpGet("similar/{id}")]
    public async Task<ActionResult<List<ChordSearchResult>>> FindSimilar(
        int id,
        [FromQuery] int limit = 10,
        [FromQuery] int numCandidates = 100)
    {
        try
        {
            var results = await vectorSearch.FindSimilarChordsAsync(id, limit, numCandidates);
            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Chord not found: {ChordId}", id);
            return NotFound($"Chord with ID {id} not found");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Chord does not have embedding: {ChordId}", id);
            return BadRequest($"Chord {id} does not have an embedding. Please generate embeddings first.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding similar chords");
            return StatusCode(500, "Error finding similar chords");
        }
    }

    /// <summary>
    ///     Hybrid search: Combine semantic search with keyword filters
    /// </summary>
    /// <param name="q">Natural language query</param>
    /// <param name="quality">Filter by quality (e.g., Major, Minor)</param>
    /// <param name="extension">Filter by extension (e.g., Seventh, Ninth)</param>
    /// <param name="stackingType">Filter by stacking type (e.g., Tertian, Quartal)</param>
    /// <param name="noteCount">Filter by number of notes</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="numCandidates">Number of candidates to consider</param>
    /// <returns>List of chords matching both semantic and keyword criteria</returns>
    [HttpGet("hybrid")]
    public async Task<ActionResult<List<ChordSearchResult>>> HybridSearch(
        [FromQuery] string q,
        [FromQuery] string? quality = null,
        [FromQuery] string? extension = null,
        [FromQuery] string? stackingType = null,
        [FromQuery] int? noteCount = null,
        [FromQuery] int limit = 10,
        [FromQuery] int numCandidates = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Query parameter 'q' is required");
            }

            var results = await vectorSearch.HybridSearchAsync(
                q, quality, extension, stackingType, noteCount, limit, numCandidates);
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "OpenAI API not configured");
            return StatusCode(503, "Vector search is not configured. Please set OpenAI API key.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing hybrid search");
            return StatusCode(500, "Error performing hybrid search");
        }
    }

    /// <summary>
    ///     Generate embedding for a text (for testing/debugging)
    /// </summary>
    /// <param name="text">Text to generate embedding for</param>
    /// <returns>Vector embedding as array of doubles</returns>
    [HttpPost("embedding")]
    public async Task<ActionResult<double[]>> GenerateEmbedding([FromBody] string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest("Text is required");
            }

            var embedding = await vectorSearch.GenerateEmbeddingAsync(text);
            return Ok(embedding);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "OpenAI API not configured");
            return StatusCode(503, "Vector search is not configured. Please set OpenAI API key.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating embedding");
            return StatusCode(500, "Error generating embedding");
        }
    }
}
