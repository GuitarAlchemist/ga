namespace GaApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using Services;


[ApiController]
[Route("api/[controller]")]
public class SearchController(VectorSearchService searchService, ILogger<SearchController> logger) : ControllerBase
{
    [HttpPost("hybrid")]
    public async Task<ActionResult<List<ChordSearchResult>>> HybridSearch([FromBody] HybridSearchRequest request)
    {
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
            return StatusCode(500, "Internal server error");
        }
    }
}

public record HybridSearchRequest(
    string Query,
    string? Quality = null,
    string? Extension = null,
    string? StackingType = null,
    int? NoteCount = null,
    int Limit = 10,
    int NumCandidates = 100,
    double[]? Vector = null
);
