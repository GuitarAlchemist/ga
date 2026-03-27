namespace GaApi.Controllers;

using Services;

/// <summary>
///     Backend proxy for text-to-speech synthesis via Voxtral (Mistral AI).
///     Returns audio blobs to the frontend. When not configured, returns 503
///     with a fallback signal so the client can use browser speech synthesis.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TtsController(
    IVoxtralTtsService ttsService,
    ILogger<TtsController> logger) : ControllerBase
{
    /// <summary>
    ///     Synthesize speech from text. Returns audio/mpeg on success,
    ///     or 503 with <c>{ "fallback": true }</c> when the service is unavailable.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Synthesize(
        [FromBody] TtsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text cannot be empty." });

        if (request.Text.Length > 5000)
            return BadRequest(new { error = "Text exceeds maximum length of 5000 characters." });

        var audioBytes = await ttsService.SynthesizeAsync(request.Text, cancellationToken);
        if (audioBytes is null)
        {
            logger.LogDebug("TTS unavailable — signaling fallback to client");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { fallback = true });
        }

        return File(audioBytes, "audio/mpeg");
    }
}

/// <summary>
///     Request body for the TTS endpoint.
/// </summary>
public record TtsRequest(string Text);
