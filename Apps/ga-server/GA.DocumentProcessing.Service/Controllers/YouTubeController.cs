namespace GA.DocumentProcessing.Service.Controllers;

using GA.DocumentProcessing.Service.Models;
using GA.DocumentProcessing.Service.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

[ApiController]
[Route("api/[controller]")]
public class YouTubeController : ControllerBase
{
    private readonly YouTubeTranscriptService _youtubeService;
    private readonly DocumentIngestionService _ingestionService;
    private readonly RetroactionLoopOrchestrator _retroactionOrchestrator;
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<YouTubeController> _logger;

    public YouTubeController(
        YouTubeTranscriptService youtubeService,
        DocumentIngestionService ingestionService,
        RetroactionLoopOrchestrator retroactionOrchestrator,
        MongoDbService mongoDbService,
        ILogger<YouTubeController> logger)
    {
        _youtubeService = youtubeService;
        _ingestionService = ingestionService;
        _retroactionOrchestrator = retroactionOrchestrator;
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Process a YouTube video: extract transcript and analyze
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessYouTubeVideo(
        [FromBody] ProcessYouTubeVideoRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing YouTube video: {Url}", request.YouTubeUrl);

            // Step 1: Extract transcript
            var transcript = await _youtubeService.ExtractTranscriptAsync(request.YouTubeUrl, cancellationToken);

            // Step 2: Process transcript as document
            ProcessedDocument? processedDoc = null;
            if (request.ExtractKnowledge)
            {
                processedDoc = await _ingestionService.ProcessTextAsync(
                    transcript.FullText,
                    $"YouTube: {transcript.VideoId}",
                    cancellationToken);
            }

            // Step 3: Save YouTube metadata
            var youtubeDoc = new YouTubeVideoDocument
            {
                VideoId = transcript.VideoId,
                VideoUrl = transcript.VideoUrl,
                Title = transcript.Title,
                Channel = transcript.Channel,
                Duration = transcript.Duration,
                FullTranscript = transcript.FullText,
                TranscriptSegments = transcript.Segments,
                TranscriptExtractedAt = transcript.ExtractedAt,
                ProcessedDocumentId = processedDoc?.Id,
                Tags = request.Tags ?? new List<string>(),
                Category = request.Category,
                ExtractedChordProgressions = processedDoc?.Knowledge?.ChordProgressions ?? new List<string>(),
                ExtractedScales = processedDoc?.Knowledge?.Scales ?? new List<string>(),
                ExtractedTechniques = processedDoc?.Knowledge?.Techniques ?? new List<string>()
            };

            var collection = _mongoDbService.Database.GetCollection<YouTubeVideoDocument>("youtube_videos");
            await collection.InsertOneAsync(youtubeDoc, cancellationToken: cancellationToken);

            var response = new ProcessYouTubeVideoResponse
            {
                VideoId = transcript.VideoId,
                VideoUrl = transcript.VideoUrl,
                Title = transcript.Title,
                Channel = transcript.Channel,
                TranscriptLength = transcript.FullText.Length,
                SegmentCount = transcript.Segments.Count,
                ProcessedDocumentId = processedDoc?.Id,
                ExtractedKnowledge = processedDoc?.Knowledge
            };

            return Ok(ApiResponse<ProcessYouTubeVideoResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing YouTube video");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Start retroaction loop with YouTube video
    /// </summary>
    [HttpPost("retroaction-loop")]
    public async Task<IActionResult> StartYouTubeRetroactionLoop(
        [FromBody] StartYouTubeRetroactionLoopRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting YouTube retroaction loop: {Url}", request.YouTubeUrl);

            // Step 1: Extract transcript
            var transcript = await _youtubeService.ExtractTranscriptAsync(request.YouTubeUrl, cancellationToken);

            // Step 2: Start retroaction loop with transcript as initial document
            var loopRequest = new RetroactionLoopRequest
            {
                InitialDocuments = new List<string> { transcript.FullText },
                Focus = request.Focus,
                MaxIterations = request.MaxIterations,
                ConvergenceThreshold = request.ConvergenceThreshold
            };

            var loopId = Guid.NewGuid().ToString();

            // Execute in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _retroactionOrchestrator.ExecuteLoopAsync(loopRequest, cancellationToken);

                    // Update YouTube document with retroaction loop results
                    var collection = _mongoDbService.Database.GetCollection<YouTubeVideoDocument>("youtube_videos");
                    var filter = Builders<YouTubeVideoDocument>.Filter.Eq(d => d.VideoId, transcript.VideoId);
                    var update = Builders<YouTubeVideoDocument>.Update
                        .Set(d => d.UpdatedAt, DateTime.UtcNow);
                    await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in YouTube retroaction loop background task");
                }
            }, cancellationToken);

            var response = new
            {
                LoopId = loopId,
                VideoId = transcript.VideoId,
                VideoUrl = transcript.VideoUrl,
                Status = "STARTED",
                Message = "Retroaction loop started in background"
            };

            return Ok(ApiResponse<object>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting YouTube retroaction loop");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get YouTube video by ID
    /// </summary>
    [HttpGet("{videoId}")]
    public async Task<IActionResult> GetYouTubeVideo(
        string videoId,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<YouTubeVideoDocument>("youtube_videos");
            var filter = Builders<YouTubeVideoDocument>.Filter.Eq(d => d.VideoId, videoId);
            var video = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (video == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Video {videoId} not found"));
            }

            return Ok(ApiResponse<YouTubeVideoDocument>.Ok(video));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YouTube video");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// List all processed YouTube videos
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListYouTubeVideos(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<YouTubeVideoDocument>("youtube_videos");
            var videos = await collection.Find(_ => true)
                .SortByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(ApiResponse<List<YouTubeVideoDocument>>.Ok(videos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing YouTube videos");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Search YouTube videos by tags or category
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchYouTubeVideos(
        [FromQuery] string? tag = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<YouTubeVideoDocument>("youtube_videos");
            var filterBuilder = Builders<YouTubeVideoDocument>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrWhiteSpace(tag))
            {
                filter &= filterBuilder.AnyEq(v => v.Tags, tag);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                filter &= filterBuilder.Eq(v => v.Category, category);
            }

            var videos = await collection.Find(filter)
                .SortByDescending(v => v.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(ApiResponse<List<YouTubeVideoDocument>>.Ok(videos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching YouTube videos");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }
}

