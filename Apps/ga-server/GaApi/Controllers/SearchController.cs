namespace GaApi.Controllers;

using Services;

/// <summary>
///     Semantic and hybrid chord voicing search over the embedded voicing index.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SearchController(VectorSearchService searchService, ILogger<SearchController> logger) : ControllerBase
{
    /// <summary>
    ///     Execute a hybrid search combining a text query with optional chord-property filters.
    ///     Returns ranked chord results from the in-memory voicing index.
    /// </summary>
    /// <param name="request">Search query and optional filter parameters.</param>
    /// <returns>A ranked list of matching chord search results.</returns>
    [HttpPost("hybrid")]
    [ProducesResponseType(typeof(List<ChordSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<ChordSearchResult>>> HybridSearch([FromBody] HybridSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query must not be empty.");

        try
        {
            var results = await searchService.HybridSearchAsync(
                request.Query,
                request.Quality,
                request.Extension,
                request.StackingType,
                request.NoteCount,
                request.Limit,
                request.NumCandidates
            );

            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing hybrid search");
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }
}
