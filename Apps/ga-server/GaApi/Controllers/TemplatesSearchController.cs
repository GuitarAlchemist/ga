namespace GaApi.Controllers;

using Services;

[ApiController]
[Route("api/templates")]
public class TemplatesSearchController(
    VectorSearchService vectorSearch,
    ILogger<TemplatesSearchController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Semantic search: Find chord templates using natural language queries
    /// </summary>
    /// <param name="q">Natural language query (e.g., "open airy triads")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="numCandidates">Number of candidates to consider (higher = more accurate but slower)</param>
    /// <returns>List of chord templates ranked by semantic similarity</returns>
    [HttpGet("semantic")]
    public async Task<ActionResult<List<ChordTemplateSearchResult>>> SemanticTemplateSearch(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        [FromQuery] int numCandidates = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Query parameter 'q' is required");
            }

            var results = await vectorSearch.SemanticTemplateSearchAsync(q, limit, numCandidates);
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Vector search not configured");
            return StatusCode(503,
                "Vector search is not configured. Please set OpenAI API key or enable local embeddings.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing semantic template search");
            return StatusCode(500, "Error performing semantic template search");
        }
    }

    /// <summary>
    ///     Hybrid search: Combine semantic search with template attribute filters
    /// </summary>
    /// <param name="q">Natural language query</param>
    /// <param name="quality">Filter by quality (e.g., Major, Minor)</param>
    /// <param name="extension">Filter by extension (e.g., Seventh, Ninth)</param>
    /// <param name="stackingType">Filter by stacking type (e.g., Tertian, Quartal)</param>
    /// <param name="noteCount">Filter by number of notes</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="numCandidates">Number of candidates to consider</param>
    /// <returns>List of chord templates matching both semantic and keyword criteria</returns>
    [HttpGet("hybrid")]
    public async Task<ActionResult<List<ChordTemplateSearchResult>>> HybridTemplateSearch(
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

            var results = await vectorSearch.HybridTemplateSearchAsync(
                q, quality, extension, stackingType, noteCount, limit, numCandidates);
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Vector search not configured");
            return StatusCode(503,
                "Vector search is not configured. Please set OpenAI API key or enable local embeddings.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing hybrid template search");
            return StatusCode(500, "Error performing hybrid template search");
        }
    }
}
