namespace GaApi.Controllers;

using GA.Business.AI.AI.HuggingFace;

/// <summary>
///     Controller for AI-powered music and audio generation using Hugging Face models
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MusicGenerationController : ControllerBase
{
    private readonly HuggingFaceClient _huggingFaceClient;
    private readonly ILogger<MusicGenerationController> _logger;
    private readonly MusicGenService _musicGenService;

    public MusicGenerationController(
        MusicGenService musicGenService,
        HuggingFaceClient huggingFaceClient,
        ILogger<MusicGenerationController> logger)
    {
        _musicGenService = musicGenService;
        _huggingFaceClient = huggingFaceClient;
        _logger = logger;
    }

    /// <summary>
    ///     Generate music from a text description
    /// </summary>
    /// <param name="request">Music generation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated audio file</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateMusic(
        [FromBody] MusicGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating music: {Description}", request.Description);

            var response = await _musicGenService.GenerateMusicAsync(request, cancellationToken);

            return File(
                response.AudioData,
                $"audio/{response.Format}",
                $"music_{DateTime.UtcNow:yyyyMMddHHmmss}.{response.Format}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating music");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    ///     Generate a backing track for practice
    /// </summary>
    /// <param name="key">Musical key (e.g., "C major", "A minor")</param>
    /// <param name="style">Musical style (e.g., "blues", "jazz", "rock")</param>
    /// <param name="duration">Duration in seconds (default: 30)</param>
    /// <param name="tempo">Tempo in BPM (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated backing track audio file</returns>
    [HttpPost("backing-track")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateBackingTrack(
        [FromQuery] string key,
        [FromQuery] string style,
        [FromQuery] double duration = 30.0,
        [FromQuery] int? tempo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating backing track: {Key} {Style}", key, style);

            var response = await _musicGenService.GenerateBackingTrackAsync(
                key, style, duration, tempo, cancellationToken);

            return File(
                response.AudioData,
                $"audio/{response.Format}",
                $"backing_{key}_{style}_{DateTime.UtcNow:yyyyMMddHHmmss}.{response.Format}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating backing track");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    ///     Generate audio for a chord progression
    /// </summary>
    /// <param name="progression">Chord progression (e.g., "I-IV-V-I", "ii-V-I")</param>
    /// <param name="key">Musical key</param>
    /// <param name="style">Musical style (default: "acoustic guitar")</param>
    /// <param name="duration">Duration in seconds (default: 15)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated chord progression audio file</returns>
    [HttpPost("chord-progression")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateChordProgression(
        [FromQuery] string progression,
        [FromQuery] string key,
        [FromQuery] string style = "acoustic guitar",
        [FromQuery] double duration = 15.0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating chord progression: {Progression} in {Key}", progression, key);

            var response = await _musicGenService.GenerateChordProgressionAsync(
                progression, key, style, duration, cancellationToken);

            return File(
                response.AudioData,
                $"audio/{response.Format}",
                $"progression_{progression}_{key}_{DateTime.UtcNow:yyyyMMddHHmmss}.{response.Format}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chord progression");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    ///     Generate guitar-specific audio
    /// </summary>
    /// <param name="request">Guitar synthesis request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated guitar audio file</returns>
    [HttpPost("guitar")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateGuitarAudio(
        [FromBody] GuitarSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating guitar audio: {Style}", request.Style);

            var response = await _musicGenService.GenerateGuitarAudioAsync(request, cancellationToken);

            return File(
                response.AudioData,
                $"audio/{response.Format}",
                $"guitar_{request.Style}_{DateTime.UtcNow:yyyyMMddHHmmss}.{response.Format}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating guitar audio");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    ///     Check if Hugging Face API is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> HealthCheck(CancellationToken cancellationToken)
    {
        try
        {
            var isHealthy = await _huggingFaceClient.HealthCheckAsync(cancellationToken);

            return Ok(new
            {
                status = isHealthy ? "healthy" : "unhealthy",
                service = "Hugging Face Inference API",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(500, new { status = "unhealthy", error = ex.Message });
        }
    }

    /// <summary>
    ///     Get information about available models
    /// </summary>
    /// <returns>List of available models</returns>
    [HttpGet("models")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetAvailableModels()
    {
        var models = new[]
        {
            new
            {
                id = HuggingFaceModels.MusicGenSmall,
                name = "MusicGen Small",
                description = "Fast music generation from text descriptions",
                task = "text-to-audio",
                recommended = true
            },
            new
            {
                id = HuggingFaceModels.MusicGenLarge,
                name = "MusicGen Large",
                description = "High quality music generation (slower)",
                task = "text-to-audio",
                recommended = false
            },
            new
            {
                id = HuggingFaceModels.StableAudioOpen,
                name = "Stable Audio Open",
                description = "Open source audio generation",
                task = "text-to-audio",
                recommended = true
            },
            new
            {
                id = HuggingFaceModels.Riffusion,
                name = "Riffusion",
                description = "Music generation from text",
                task = "text-to-audio",
                recommended = false
            }
        };

        return Ok(models);
    }
}
