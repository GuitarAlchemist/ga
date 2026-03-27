namespace GaApi.Controllers;

using System.Diagnostics;
using Services;

/// <summary>
///     Request to generate tablature from a YouTube video
/// </summary>
public record YouTubeTabRequest(string Url, double? Fps = null);

/// <summary>
///     Response containing generated tablature and processing metadata
/// </summary>
public record YouTubeTabResponse(
    string Tab,
    int FrameCount,
    int NoteCount,
    long ProcessingTimeMs,
    double Confidence);

/// <summary>
///     REST API controller for the YouTube-to-tablature pipeline.
///     Orchestrates: video download -> frame extraction -> hand pose inference -> tab generation.
/// </summary>
[ApiController]
[Route("api/youtube-to-tab")]
public class YouTubeTabController(
    VideoFrameExtractor frameExtractor,
    HandPosePipeline handPosePipeline,
    PositionToTabGenerator tabGenerator,
    ILogger<YouTubeTabController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Generate ASCII tablature from a YouTube video URL.
    ///     Downloads the video, extracts frames, runs hand pose inference (mock),
    ///     and converts detected positions to standard guitar tab notation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(YouTubeTabResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<YouTubeTabResponse>> GenerateTab(
        [FromBody] YouTubeTabRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { error = "URL is required" });
        }

        if (!IsValidYouTubeUrl(request.Url))
        {
            return BadRequest(new { error = "Invalid YouTube URL" });
        }

        var fps = request.Fps ?? 2.0;
        if (fps is <= 0 or > 30)
        {
            return BadRequest(new { error = "FPS must be between 0 and 30" });
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // Step 1: Extract frames from video
            logger.LogInformation("Starting YouTube-to-tab pipeline for {Url} at {Fps} FPS", request.Url, fps);
            var frames = await frameExtractor.ExtractFramesAsync(request.Url, fps, cancellationToken);

            if (frames.Count == 0)
            {
                return Ok(new YouTubeTabResponse(
                    Tab: "No frames extracted from video",
                    FrameCount: 0,
                    NoteCount: 0,
                    ProcessingTimeMs: sw.ElapsedMilliseconds,
                    Confidence: 0.0));
            }

            // Step 2: Run hand pose inference on each frame
            var positions = await handPosePipeline.ProcessFramesAsync(frames, cancellationToken);

            // Step 3: Generate ASCII tab from positions
            var tab = tabGenerator.GenerateTab(positions, request.Url);

            sw.Stop();

            var noteCount = positions.Sum(p => p.Positions.Length);
            var avgConfidence = positions.Count > 0
                ? positions.SelectMany(p => p.Positions).Average(p => p.Confidence)
                : 0.0;

            logger.LogInformation(
                "YouTube-to-tab pipeline complete: {FrameCount} frames, {PositionCount} positions, {NoteCount} notes in {ElapsedMs}ms",
                frames.Count, positions.Count, noteCount, sw.ElapsedMilliseconds);

            return Ok(new YouTubeTabResponse(
                Tab: tab,
                FrameCount: frames.Count,
                NoteCount: noteCount,
                ProcessingTimeMs: sw.ElapsedMilliseconds,
                Confidence: Math.Round(avgConfidence, 2)));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not installed") || ex.Message.Contains("not on PATH"))
        {
            logger.LogError(ex, "Required tool missing for YouTube-to-tab pipeline");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("HandPoseService"))
        {
            logger.LogError(ex, "Hand pose service unavailable");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Hand pose service is not available. Please ensure it is running on port 8080." });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error in YouTube-to-tab pipeline");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "A required service is not available. Please try again later." });
        }
    }

    private static bool IsValidYouTubeUrl(string url) =>
        url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase);
}
