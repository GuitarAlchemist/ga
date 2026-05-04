namespace GaApi.Controllers;

using System.Net.Http;
using GA.Business.AI.HandPose;

/// <summary>
///     Hand pose detection endpoints used by the Hand Pose demo (/demos/hand-pose).
///     Wraps <see cref="HandPoseClient"/> so the demo works with only GaApi + the
///     Python hand-pose-service running (no full Aspire microservice mesh required).
/// </summary>
[ApiController]
[Route("api/guitarplaying")]
[Produces("application/json")]
public class GuitarPlayingController(
    HandPoseClient handPoseClient,
    ILogger<GuitarPlayingController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Detect hand pose keypoints in an uploaded image.
    /// </summary>
    [HttpPost("detect-hands")]
    [ProducesResponseType(typeof(HandPoseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DetectHands(IFormFile image, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { error = "No image provided. Upload a file under the 'image' form field." });
        }

        try
        {
            await using var stream = image.OpenReadStream();
            var result = await handPoseClient.InferAsync(stream, image.FileName, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is HttpRequestException)
        {
            // HandPoseClient wraps transport failures this way when the Python service is unreachable.
            logger.LogWarning(ex, "hand-pose-service unreachable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Hand pose service is not running",
                details = "Start the Python service under Apps/hand-pose-service (uvicorn on :8080), " +
                          "or set HandPoseService:BaseUrl to a reachable endpoint."
            });
        }
    }

    /// <summary>
    ///     Lightweight health probe for the downstream Python hand-pose service.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var up = await handPoseClient.HealthCheckAsync(ct);
        return Ok(new
        {
            handPoseService = up ? "healthy" : "unavailable",
            overall = up ? "healthy" : "degraded"
        });
    }
}
