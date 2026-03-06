namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Rag.Models;
using GA.Core.Functional;

#pragma warning disable SKEXP0001

/// <summary>
///     API controller for advanced semantic and spectral search
/// </summary>
[ApiController]
[Route("api/search")]
[Produces("application/json")]
public class SearchController(
    ISpectralRetrievalService retrievalService,
    GA.Business.ML.Abstractions.IEmbeddingGenerator embeddingGenerator,
    ILogger<SearchController> logger) : ControllerBase
{
    /// <summary>
    ///     Perform a weighted similarity search across vector partitions
    /// </summary>
    [HttpPost("spectral")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SpectralSearch([FromBody] SpectralSearchRequest request)
    {
        try
        {
            float[] queryEmbedding;

            if (request.Embedding is { Length: > 0 })
            {
                queryEmbedding = request.Embedding;
            }
            else if (!string.IsNullOrEmpty(request.Query))
            {
                // We need to generate embedding for the query text
                // Since this uses OPTIC-K schema, we might need a specific text-to-musical-concept conversion
                // For now, let's assume we search by a reference concept
                return BadRequest("Query text to embedding conversion not implemented for pure spectral search. Use a reference chord or provide embedding.");
            }
            else
            {
                return BadRequest("Query or Embedding is required.");
            }

            var results = retrievalService.Search(
                queryEmbedding,
                request.Limit,
                request.Preset,
                request.Quality,
                request.Extension,
                request.StackingType,
                request.NoteCount
            );

            return Ok(results.Select(r => new
            {
                r.Doc.Id,
                r.Doc.ChordName,
                r.Doc.SemanticTags,
                r.Doc.SearchableText,
                r.Score
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing spectral search");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    ///     Find voice-leading neighbors using pure spectral geometry
    /// </summary>
    [HttpPost("neighbors")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindNeighbors([FromBody] NeighborSearchRequest request)
    {
        try
        {
            var results = retrievalService.FindSpectralNeighbors(request.Embedding, request.Limit);

            return Ok(results.Select(r => new
            {
                r.Doc.Id,
                r.Doc.ChordName,
                r.Doc.MidiNotes,
                r.Score
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding spectral neighbors");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class SpectralSearchRequest
{
    public string? Query { get; set; }
    public float[]? Embedding { get; set; }
    public int Limit { get; set; } = 10;
    public SpectralRetrievalService.SearchPreset Preset { get; set; } = SpectralRetrievalService.SearchPreset.Tonal;
    public string? Quality { get; set; }
    public string? Extension { get; set; }
    public string? StackingType { get; set; }
    public int? NoteCount { get; set; }
}

public class NeighborSearchRequest
{
    public float[] Embedding { get; set; } = [];
    public int Limit { get; set; } = 10;
}

#pragma warning restore SKEXP0001
