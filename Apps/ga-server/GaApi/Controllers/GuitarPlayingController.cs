namespace GaApi.Controllers;

using GA.Business.AI.AI.HandPose;
using GA.Business.AI.AI.SoundBank;

/// <summary>
///     Controller for guitar playing simulation using hand pose detection and AI sound generation
///     Orchestrates HandPoseService and SoundBankService
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GuitarPlayingController : ControllerBase
{
    private readonly HandPoseClient _handPoseClient;
    private readonly ILogger<GuitarPlayingController> _logger;
    private readonly SoundBankClient _soundBankClient;

    public GuitarPlayingController(
        HandPoseClient handPoseClient,
        SoundBankClient soundBankClient,
        ILogger<GuitarPlayingController> logger)
    {
        _handPoseClient = handPoseClient;
        _soundBankClient = soundBankClient;
        _logger = logger;
    }

    /// <summary>
    ///     Detect hand pose from uploaded image
    /// </summary>
    [HttpPost("detect-hands")]
    [ProducesResponseType(typeof(HandPoseResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DetectHands(IFormFile image, CancellationToken cancellationToken)
    {
        try
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest("No image provided");
            }

            _logger.LogInformation("Detecting hands in uploaded image: {FileName} ({Size} bytes)",
                image.FileName, image.Length);

            using var stream = image.OpenReadStream();
            var result = await _handPoseClient.InferAsync(stream, image.FileName, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting hands");
            return StatusCode(500, new { error = "Failed to detect hands", details = ex.Message });
        }
    }

    /// <summary>
    ///     Map hand pose to guitar positions
    /// </summary>
    [HttpPost("map-to-guitar")]
    [ProducesResponseType(typeof(GuitarMappingResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> MapToGuitar(
        [FromBody] GuitarMappingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Mapping hand pose to guitar positions");

            var result = await _handPoseClient.MapToGuitarAsync(
                request.HandPose,
                request.NeckConfig,
                request.HandToMap,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mapping to guitar");
            return StatusCode(500, new { error = "Failed to map to guitar", details = ex.Message });
        }
    }

    /// <summary>
    ///     Detect hands and map to guitar in one call
    /// </summary>
    [HttpPost("detect-and-map")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DetectAndMap(
        IFormFile image,
        [FromQuery] string handToMap = "left",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest("No image provided");
            }

            _logger.LogInformation("Detecting hands and mapping to guitar: {FileName}", image.FileName);

            // Step 1: Detect hands
            using var stream = image.OpenReadStream();
            var handPose = await _handPoseClient.InferAsync(stream, image.FileName, cancellationToken);

            // Step 2: Map to guitar
            var guitarMapping = await _handPoseClient.MapToGuitarAsync(
                handPose,
                new NeckConfig(),
                handToMap,
                cancellationToken);

            return Ok(new
            {
                handPose,
                guitarMapping
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in detect-and-map");
            return StatusCode(500, new { error = "Failed to detect and map", details = ex.Message });
        }
    }

    /// <summary>
    ///     Generate sound for a guitar position
    /// </summary>
    [HttpPost("generate-sound")]
    [ProducesResponseType(typeof(JobResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GenerateSound(
        [FromBody] SoundGenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating sound: {Instrument} string={String} fret={Fret}",
                request.Instrument, request.String, request.Fret);

            var result = await _soundBankClient.GenerateSoundAsync(request, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sound");
            return StatusCode(500, new { error = "Failed to generate sound", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get sound generation job status
    /// </summary>
    [HttpGet("sound-jobs/{jobId}")]
    [ProducesResponseType(typeof(JobStatusResponse), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetSoundJobStatus(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _soundBankClient.GetJobStatusAsync(jobId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to get job status", details = ex.Message });
        }
    }

    /// <summary>
    ///     Download generated sound sample
    /// </summary>
    [HttpGet("sounds/{sampleId}/download")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DownloadSound(string sampleId, CancellationToken cancellationToken)
    {
        try
        {
            var audioData = await _soundBankClient.DownloadSampleAsync(sampleId, cancellationToken);
            return File(audioData, "audio/wav", $"{sampleId}.wav");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading sample {SampleId}", sampleId);
            return StatusCode(500, new { error = "Failed to download sample", details = ex.Message });
        }
    }

    /// <summary>
    ///     Full pipeline: detect hands, map to guitar, generate sounds
    /// </summary>
    [HttpPost("play-from-image")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> PlayFromImage(
        IFormFile image,
        [FromQuery] string handToMap = "left",
        [FromQuery] bool waitForGeneration = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest("No image provided");
            }

            _logger.LogInformation("Full pipeline: play from image {FileName}", image.FileName);

            // Step 1: Detect hands
            using var stream = image.OpenReadStream();
            var handPose = await _handPoseClient.InferAsync(stream, image.FileName, cancellationToken);

            // Step 2: Map to guitar
            var guitarMapping = await _handPoseClient.MapToGuitarAsync(
                handPose,
                new NeckConfig(),
                handToMap,
                cancellationToken);

            // Step 3: Generate sounds for each position
            var soundJobs = new List<JobResponse>();
            foreach (var position in guitarMapping.Positions.Take(5)) // Limit to 5 positions
            {
                var soundRequest = new SoundGenerationRequest(
                    "electric_guitar",
                    position.String,
                    position.Fret,
                    0.7,
                    new List<string> { "pluck" },
                    null,
                    1.0
                );

                var job = await _soundBankClient.GenerateSoundAsync(soundRequest, cancellationToken);
                soundJobs.Add(job);
            }

            // Optionally wait for all jobs to complete
            List<SoundSample>? completedSamples = null;
            if (waitForGeneration)
            {
                completedSamples = new List<SoundSample>();
                foreach (var job in soundJobs)
                {
                    var completed = await _soundBankClient.WaitForJobCompletionAsync(
                        job.JobId,
                        TimeSpan.FromSeconds(30),
                        cancellationToken);

                    if (completed.Sample != null)
                    {
                        completedSamples.Add(completed.Sample);
                    }
                }
            }

            return Ok(new
            {
                handPose,
                guitarMapping,
                soundJobs,
                completedSamples
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in play-from-image pipeline");
            return StatusCode(500, new { error = "Failed to process image", details = ex.Message });
        }
    }

    /// <summary>
    ///     Search for existing sound samples
    /// </summary>
    [HttpPost("search-sounds")]
    [ProducesResponseType(typeof(SearchResponse), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SearchSounds(
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _soundBankClient.SearchSamplesAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching sounds");
            return StatusCode(500, new { error = "Failed to search sounds", details = ex.Message });
        }
    }

    /// <summary>
    ///     Health check for both services
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> HealthCheck(CancellationToken cancellationToken)
    {
        var handPoseHealthy = await _handPoseClient.HealthCheckAsync(cancellationToken);
        var soundBankHealthy = await _soundBankClient.HealthCheckAsync(cancellationToken);

        return Ok(new
        {
            handPoseService = handPoseHealthy ? "healthy" : "unhealthy",
            soundBankService = soundBankHealthy ? "healthy" : "unhealthy",
            overall = handPoseHealthy && soundBankHealthy ? "healthy" : "degraded"
        });
    }
}
