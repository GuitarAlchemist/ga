namespace GaApi.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Services;

/// <summary>
///     REST surface for voicing retrieval. Wraps <see cref="ISemanticKnowledgeSource"/>
///     so external callers (MCP tools, test harnesses, observability) can query the
///     OPTIC-K voicing index without going through the full chatbot pipeline.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VoicingsController(
    ILogger<VoicingsController> logger,
    ISemanticKnowledgeSource semanticKnowledge)
    : ControllerBase
{
    /// <summary>
    ///     Retrieve the top-<c>limit</c> voicings most relevant to a free-text query.
    ///     The query is embedded via Ollama and matched against the 313k-voicing OPTIC-K index.
    /// </summary>
    [HttpPost("retrieve")]
    [ProducesResponseType(typeof(VoicingRetrieveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<VoicingRetrieveResponse>> Retrieve(
        [FromBody] VoicingRetrieveRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "query is required" });
        }

        var limit = Math.Clamp(request.Limit ?? 10, 1, 50);
        var started = Stopwatch.GetTimestamp();

        var results = await semanticKnowledge.SearchAsync(request.Query, limit, cancellationToken);

        var latencyMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        logger.LogInformation(
            "voicing-retrieve query='{Query}' limit={Limit} returned={Count} latency_ms={LatencyMs:F1}",
            request.Query, limit, results.Count, latencyMs);

        return Ok(new VoicingRetrieveResponse(
            SchemaVersion: "v1",
            Query: request.Query,
            TopK: limit,
            EmbeddingModel: "ollama",
            LatencyMs: latencyMs,
            Results: [.. results.Select((r, i) => new VoicingRetrieveResult(
                Rank: i,
                Score: r.Score,
                Snippet: r.Content))]));
    }
}

/// <summary>Request body for <c>POST /api/voicings/retrieve</c>.</summary>
public sealed record VoicingRetrieveRequest(
    [Required] string Query,
    int? Limit);

/// <summary>Response envelope with schema version for forward compatibility.</summary>
public sealed record VoicingRetrieveResponse(
    string SchemaVersion,
    string Query,
    int TopK,
    string EmbeddingModel,
    double LatencyMs,
    IReadOnlyList<VoicingRetrieveResult> Results);

/// <summary>A single retrieved voicing with its score and LLM-friendly snippet.</summary>
public sealed record VoicingRetrieveResult(
    int Rank,
    double Score,
    string Snippet);
