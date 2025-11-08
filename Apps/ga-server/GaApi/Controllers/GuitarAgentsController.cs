namespace GaApi.Controllers;

using Models;
using Services;

/// <summary>
///     Endpoints that expose Microsoft Agent Framework powered guitar helpers.
/// </summary>
[ApiController]
[Route("api/agents/guitar")]
[Produces("application/json")]
public sealed class GuitarAgentsController(
    IGuitarAgentOrchestrator orchestrator,
    ILogger<GuitarAgentsController> logger) : ControllerBase
{
    private readonly ILogger<GuitarAgentsController> _logger = logger;
    private readonly IGuitarAgentOrchestrator _orchestrator = orchestrator;

    /// <summary>
    ///     Adds colour and tasteful substitutions to an existing chord progression.
    /// </summary>
    [HttpPost("progressions/spice-up")]
    [ProducesResponseType(typeof(GuitarAgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuitarAgentResponse>> SpiceUpProgression(
        [FromBody] SpiceUpProgressionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _orchestrator.SpiceUpProgressionAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return Problem(
                "Request was cancelled.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spice up progression.");
            return Problem("The agent was unable to process the request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    ///     Reharmonizes a progression while keeping it playable for guitarists.
    /// </summary>
    [HttpPost("progressions/reharmonize")]
    [ProducesResponseType(typeof(GuitarAgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuitarAgentResponse>> ReharmonizeProgression(
        [FromBody] ReharmonizeProgressionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _orchestrator.ReharmonizeProgressionAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return Problem(
                "Request was cancelled.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reharmonize progression.");
            return Problem("The agent was unable to process the request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    ///     Generates a brand new progression tailored to the supplied brief.
    /// </summary>
    [HttpPost("progressions/create")]
    [ProducesResponseType(typeof(GuitarAgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuitarAgentResponse>> CreateProgression(
        [FromBody] CreateProgressionRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _orchestrator.CreateProgressionAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            return Problem(
                "Request was cancelled.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compose progression.");
            return Problem("The agent was unable to process the request.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
