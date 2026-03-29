namespace GaApi.Controllers;

using GaApi.Services;

/// <summary>
///     Pipeline execution API — runs brainstorm/plan/build/review/compound stages.
///     Admin-only: only accessible from localhost or with admin token.
///     Progress broadcast via SignalR PipelineHub.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PipelineController(
    ILogger<PipelineController> logger,
    PipelineExecutionService pipelineService)
    : ControllerBase
{
    /// <summary>
    ///     Run a single pipeline stage.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<PipelineStageResult>> RunStage(
        [FromBody] PipelineRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin())
            return Unauthorized(new { error = "Pipeline actions require admin access" });

        if (!Enum.TryParse<PipelineStage>(request.Stage, true, out var stage))
            return BadRequest(new { error = $"Unknown stage: {request.Stage}. Valid: brainstorm, plan, implement, review, compound" });

        logger.LogInformation("Pipeline stage {Stage} requested for: {Title}", stage, request.Title);

        var result = await pipelineService.RunStageAsync(request.Title, request.Source, stage, ct);
        return Ok(result);
    }

    /// <summary>
    ///     Run the full pipeline (all 5 stages) for an item.
    /// </summary>
    [HttpPost("run-all")]
    public async Task<ActionResult<List<PipelineStageResult>>> RunFullPipeline(
        [FromBody] PipelineRequest request,
        CancellationToken ct)
    {
        if (!IsAdmin())
            return Unauthorized(new { error = "Pipeline actions require admin access" });

        logger.LogInformation("Full pipeline requested for: {Title}", request.Title);

        var results = await pipelineService.RunFullPipelineAsync(request.Title, request.Source, ct);
        return Ok(results);
    }

    /// <summary>
    ///     Get active pipeline runs.
    /// </summary>
    [HttpGet("active")]
    public ActionResult GetActiveRuns() =>
        Ok(pipelineService.GetActiveRuns().Select(r => new
        {
            r.Id,
            r.Title,
            r.Source,
            CurrentStage = r.CurrentStage.ToString().ToLowerInvariant(),
            r.Log,
            r.StartedAt,
            r.Autopilot,
        }));

    /// <summary>
    ///     Admin check: localhost or admin token header.
    /// </summary>
    private bool IsAdmin()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (remoteIp is "127.0.0.1" or "::1" or "localhost") return true;

        var token = Request.Headers["X-Admin-Token"].FirstOrDefault();
        return token == "ga-owner-2026";
    }
}
