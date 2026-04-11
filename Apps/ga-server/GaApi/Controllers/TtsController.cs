namespace GaApi.Controllers;

using Services;

/// <summary>
///     Backend proxy for text-to-speech synthesis.
///     Supports two providers:
///       - Voxtral (Mistral, cloud, high quality)
///       - Kokoro-82M (local, Apache 2.0, ~85x smaller)
///     Returns audio blobs to the frontend. When a provider is unavailable,
///     returns 503 with <c>{ "fallback": true }</c> so the client can fall back.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TtsController(
    IVoxtralTtsService voxtralService,
    IKokoroTtsService kokoroService,
    ILogger<TtsController> logger) : ControllerBase
{
    /// <summary>
    ///     Synthesize speech via Voxtral (default/legacy endpoint).
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

        var audioBytes = await voxtralService.SynthesizeAsync(request.Text, cancellationToken);
        if (audioBytes is null)
        {
            logger.LogDebug("Voxtral TTS unavailable — signaling fallback to client");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { fallback = true });
        }

        return File(audioBytes, "audio/mpeg");
    }

    /// <summary>
    ///     Synthesize speech via the local Kokoro-82M server.
    /// </summary>
    [HttpPost("kokoro")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SynthesizeKokoro(
        [FromBody] KokoroTtsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { error = "Text cannot be empty." });

        if (request.Text.Length > 5000)
            return BadRequest(new { error = "Text exceeds maximum length of 5000 characters." });

        var audioBytes = await kokoroService.SynthesizeAsync(request.Text, request.Voice, cancellationToken);
        if (audioBytes is null)
        {
            logger.LogDebug("Kokoro TTS unavailable — signaling fallback to client");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { fallback = true });
        }

        return File(audioBytes, "audio/mpeg");
    }

    /// <summary>
    ///     List available Kokoro voices.
    /// </summary>
    [HttpGet("kokoro/voices")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKokoroVoices(CancellationToken cancellationToken)
    {
        var voices = await kokoroService.GetVoicesAsync(cancellationToken);
        return Ok(new { voices });
    }

    /// <summary>
    ///     List available TTS providers with health status. Drives provider
    ///     selection in the frontend.
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken)
    {
        var kokoroHealthy = await kokoroService.CheckHealthAsync(cancellationToken);

        return Ok(new
        {
            providers = new object[]
            {
                new
                {
                    id = "voxtral",
                    name = "Voxtral (Mistral)",
                    local = false,
                    license = "commercial",
                    healthy = (bool?)null,
                    endpoint = "/api/tts"
                },
                new
                {
                    id = "kokoro",
                    name = "Kokoro-82M",
                    local = true,
                    license = "Apache-2.0",
                    healthy = kokoroHealthy,
                    endpoint = "/api/tts/kokoro"
                }
            }
        });
    }
}

/// <summary>
///     Request body for the default Voxtral TTS endpoint.
/// </summary>
public record TtsRequest(string Text);

/// <summary>
///     Request body for the Kokoro TTS endpoint. <paramref name="Voice"/> is optional.
/// </summary>
public record KokoroTtsRequest(string Text, string? Voice = null);
