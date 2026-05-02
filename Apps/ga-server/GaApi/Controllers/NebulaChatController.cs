namespace GaApi.Controllers;

using Services;

/// <summary>
///     Chat endpoint for the Harmonic Nebula Sidekick — Claude Haiku 4.5
///     with tool-use over the OPTIC-K voicing corpus.
/// </summary>
[ApiController]
[Route("api/nebula")]
public class NebulaChatController(
    ILogger<NebulaChatController> logger,
    NebulaSidekickService sidekick)
    : ControllerBase
{
    [HttpPost("chat")]
    [ProducesResponseType(typeof(NebulaChatReply), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NebulaChatReply>> Chat(
        [FromBody] NebulaChatRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "message is required" });
        }

        var reply = await sidekick.ChatAsync(request, ct);
        logger.LogInformation(
            "nebula chat: tools={Tools} error={Error} voicing={Voicing}",
            reply.ToolCalls.Count,
            reply.Error ?? "-",
            request.Context?.SelectedVoicing?.GlobalId ?? "-");
        return Ok(reply);
    }
}
