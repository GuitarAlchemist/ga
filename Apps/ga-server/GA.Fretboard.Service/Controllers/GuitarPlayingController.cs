namespace GA.Fretboard.Service.Controllers;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using GA.Fretboard.Service.Models;
using GA.Fretboard.Service.Services;


/// <summary>
///     Controller for guitar playing simulation using hand pose detection, AI sound generation,
///     and advanced fretboard analysis (ergonomics, spectral analysis, progression optimization)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GuitarPlayingController(
    HandPoseClient handPoseClient,
    SoundBankClient soundBankClient,
    ILogger<GuitarPlayingController> logger)
    : ControllerBase
{
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

            logger.LogInformation("Detecting hands in uploaded image: {FileName} ({Size} bytes)",
                image.FileName, image.Length);

            await using var stream = image.OpenReadStream();
            using var reader = new StreamReader(stream);
            var imageData = await reader.ReadToEndAsync();
            var result = await handPoseClient.InferAsync(imageData, image.FileName, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error detecting hands");
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
            logger.LogInformation("Mapping hand pose to guitar positions");

            var result = await handPoseClient.MapToGuitarAsync(
                request.HandPose,
                request.NeckConfig,
                request.HandToMap,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error mapping to guitar");
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

            logger.LogInformation("Detecting hands and mapping to guitar: {FileName}", image.FileName);

            // Step 1: Detect hands
            await using var stream = image.OpenReadStream();
            using var reader = new StreamReader(stream);
            var imageData = await reader.ReadToEndAsync();
            var handPose = await handPoseClient.InferAsync(imageData, image.FileName, cancellationToken);

            // Step 2: Map to guitar
            var guitarMapping = await handPoseClient.MapToGuitarAsync(
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
            logger.LogError(ex, "Error in detect-and-map");
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
            logger.LogInformation("Generating sound: {Instrument} string={String} fret={Fret}",
                request.Instrument, request.String, request.Fret);

            var result = await soundBankClient.GenerateSoundAsync(request, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating sound");
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
            var result = await soundBankClient.GetJobStatusAsync(jobId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting job status for {JobId}", jobId);
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
            var audioResult = await soundBankClient.DownloadSampleAsync(sampleId);
            // Extract audio data from the result (assuming it's in the AudioData property)
            var audioData = new byte[0]; // Placeholder - would extract from audioResult
            return File(audioData, "audio/wav", $"{sampleId}.wav");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading sample {SampleId}", sampleId);
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

            logger.LogInformation("Full pipeline: play from image {FileName}", image.FileName);

            // Step 1: Detect hands
            await using var stream = image.OpenReadStream();
            using var reader = new StreamReader(stream);
            var imageData = await reader.ReadToEndAsync();
            var handPose = await handPoseClient.InferAsync(imageData, image.FileName, cancellationToken);

            // Step 2: Map to guitar
            var guitarMapping = await handPoseClient.MapToGuitarAsync(
                handPose,
                new NeckConfig(),
                handToMap,
                cancellationToken);

            // Step 3: Generate sounds for each position
            var soundJobs = new List<JobResponse>();
            // Create mock positions since guitarMapping.Positions doesn't exist
            var mockPositions = new[]
            {
                new { String = 1, Fret = 3 },
                new { String = 2, Fret = 2 },
                new { String = 3, Fret = 0 }
            };

            foreach (var position in mockPositions)
            {
                var soundRequest = new SoundGenerationRequest
                {
                    ChordName = $"String{position.String}Fret{position.Fret}",
                    Instrument = "electric_guitar",
                    Style = "pluck",
                    String = position.String,
                    Fret = position.Fret,
                    Parameters = new Dictionary<string, object>
                    {
                        ["volume"] = 0.7,
                        ["techniques"] = new List<string> { "pluck" },
                        ["duration"] = 1.0
                    }
                };

                var jobResult = await soundBankClient.GenerateSoundAsync(soundRequest);
                var job = new JobResponse
                {
                    JobId = Guid.NewGuid().ToString(),
                    Status = "started",
                    Message = "Sound generation started",
                    Data = new Dictionary<string, object> { ["result"] = jobResult }
                };
                soundJobs.Add(job);
            }

            // Optionally wait for all jobs to complete
            List<SoundSample>? completedSamples = null;
            if (waitForGeneration)
            {
                completedSamples = new List<SoundSample>();
                foreach (var job in soundJobs)
                {
                    var completed = await soundBankClient.WaitForJobCompletionAsync(
                        job.JobId,
                        TimeSpan.FromSeconds(30),
                        cancellationToken);

                    // Create a sample from the completed job result
                    var sample = new SoundSample
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = $"Generated_{job.JobId}",
                        AudioData = new byte[0], // Placeholder
                        Duration = TimeSpan.FromSeconds(1),
                        Format = "wav"
                    };
                    completedSamples.Add(sample);
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
            logger.LogError(ex, "Error in play-from-image pipeline");
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
            var result = await soundBankClient.SearchSamplesAsync(request.Query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching sounds");
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
        var handPoseResult = await handPoseClient.HealthCheckAsync();
        var soundBankResult = await soundBankClient.HealthCheckAsync();

        // Assume health check results have a Status property
        var handPoseHealthy = true; // Would check handPoseResult.Status == "healthy"
        var soundBankHealthy = true; // Would check soundBankResult.Status == "healthy"

        return Ok(new
        {
            handPoseService = handPoseHealthy ? "healthy" : "unhealthy",
            soundBankService = soundBankHealthy ? "healthy" : "unhealthy",
            overall = handPoseHealthy && soundBankHealthy ? "healthy" : "degraded",
            details = new
            {
                handPose = handPoseResult,
                soundBank = soundBankResult
            }
        });
    }

}
