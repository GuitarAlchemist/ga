namespace GA.DocumentProcessing.Service.Controllers;

using GA.DocumentProcessing.Service.Models;
using GA.DocumentProcessing.Service.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

/// <summary>
/// API controller for NotebookLM + Ollama retroaction loop
/// </summary>
[ApiController]
[Route("api/retroaction")]
public class RetroactionLoopController : ControllerBase
{
    private readonly RetroactionLoopOrchestrator _orchestrator;
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<RetroactionLoopController> _logger;

    public RetroactionLoopController(
        RetroactionLoopOrchestrator orchestrator,
        MongoDbService mongoDbService,
        ILogger<RetroactionLoopController> logger)
    {
        _orchestrator = orchestrator;
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new retroaction loop
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ApiResponse<RetroactionLoopStatusDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> StartRetroactionLoop(
        [FromBody] StartRetroactionLoopDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting retroaction loop with {DocCount} documents",
                request.Documents.Count);

            if (request.Documents.Count == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("At least one document is required"));
            }

            if (string.IsNullOrWhiteSpace(request.Focus))
            {
                return BadRequest(ApiResponse<object>.Fail("Focus is required"));
            }

            // Start the loop (async - runs in background)
            var loopRequest = new RetroactionLoopRequest
            {
                InitialDocuments = request.Documents,
                Focus = request.Focus,
                MaxIterations = request.MaxIterations,
                ConvergenceThreshold = request.ConvergenceThreshold
            };

            // Execute in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _orchestrator.ExecuteLoopAsync(loopRequest, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background retroaction loop failed");
                }
            }, cancellationToken);

            // Return immediately with loop ID
            var response = new RetroactionLoopStatusDto
            {
                LoopId = Guid.NewGuid().ToString(),
                Status = "STARTED",
                CurrentIteration = 0,
                TotalIterations = request.MaxIterations
            };

            return Ok(ApiResponse<RetroactionLoopStatusDto>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting retroaction loop");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get the status of a retroaction loop
    /// </summary>
    [HttpGet("{loopId}/status")]
    [ProducesResponseType(typeof(ApiResponse<RetroactionLoopStatusDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetLoopStatus(
        string loopId,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<RetroactionLoopResult>("retroaction_loops");
            var filter = Builders<RetroactionLoopResult>.Filter.Eq(r => r.LoopId, loopId);
            var result = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Loop {loopId} not found"));
            }

            var status = new RetroactionLoopStatusDto
            {
                LoopId = result.LoopId,
                CurrentIteration = result.Iterations.Count,
                TotalIterations = result.TotalIterations,
                ConvergenceScore = result.ConvergenceScore,
                Converged = result.Converged,
                Status = result.Converged ? "CONVERGED" : "IN_PROGRESS",
                Iterations = result.Iterations.Select(i => new IterationSummaryDto
                {
                    IterationNumber = i.IterationNumber,
                    Duration = i.Duration,
                    PodcastSize = i.NotebookLMPodcastSize,
                    KnowledgeGapsCount = i.KnowledgeGaps.Count,
                    TopGaps = i.KnowledgeGaps.Take(3).ToList()
                }).ToList()
            };

            return Ok(ApiResponse<RetroactionLoopStatusDto>.Ok(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loop status");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get detailed results of a retroaction loop
    /// </summary>
    [HttpGet("{loopId}/results")]
    [ProducesResponseType(typeof(ApiResponse<RetroactionLoopResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetLoopResults(
        string loopId,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<RetroactionLoopResult>("retroaction_loops");
            var filter = Builders<RetroactionLoopResult>.Filter.Eq(r => r.LoopId, loopId);
            var result = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Loop {loopId} not found"));
            }

            return Ok(ApiResponse<RetroactionLoopResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loop results");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// List all retroaction loops
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(ApiResponse<List<RetroactionLoopStatusDto>>), 200)]
    public async Task<IActionResult> ListLoops(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<RetroactionLoopResult>("retroaction_loops");
            var results = await collection.Find(_ => true)
                .SortByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync(cancellationToken);

            var statusList = results.Select(r => new RetroactionLoopStatusDto
            {
                LoopId = r.LoopId,
                CurrentIteration = r.Iterations.Count,
                TotalIterations = r.TotalIterations,
                ConvergenceScore = r.ConvergenceScore,
                Converged = r.Converged,
                Status = r.Converged ? "CONVERGED" : "IN_PROGRESS"
            }).ToList();

            return Ok(ApiResponse<List<RetroactionLoopStatusDto>>.Ok(statusList));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing loops");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get convergence metrics for a loop
    /// </summary>
    [HttpGet("{loopId}/convergence")]
    [ProducesResponseType(typeof(ApiResponse<ConvergenceMetricsDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetConvergenceMetrics(
        string loopId,
        CancellationToken cancellationToken)
    {
        try
        {
            var collection = _mongoDbService.Database.GetCollection<RetroactionLoopResult>("retroaction_loops");
            var filter = Builders<RetroactionLoopResult>.Filter.Eq(r => r.LoopId, loopId);
            var result = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);

            if (result == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Loop {loopId} not found"));
            }

            var metrics = new ConvergenceMetricsDto
            {
                LoopId = result.LoopId,
                FinalConvergenceScore = result.ConvergenceScore,
                Converged = result.Converged,
                IterationScores = result.Iterations.Select((iter, index) =>
                {
                    // Calculate score for this iteration
                    var gapCount = iter.KnowledgeGaps.Count;
                    var prevGapCount = index > 0 ? result.Iterations[index - 1].KnowledgeGaps.Count : 10;
                    var score = Math.Max(0, 1.0 - (double)gapCount / Math.Max(prevGapCount, 1));
                    return score;
                }).ToList(),
                TotalDuration = TimeSpan.FromSeconds(result.Iterations.Sum(i => i.Duration.TotalSeconds)),
                AveragePodcastSize = result.Iterations.Any()
                    ? (int)result.Iterations.Average(i => i.NotebookLMPodcastSize)
                    : 0
            };

            return Ok(ApiResponse<ConvergenceMetricsDto>.Ok(metrics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting convergence metrics");
            return StatusCode(500, ApiResponse<object>.Fail($"Internal server error: {ex.Message}"));
        }
    }
}

/// <summary>
/// Convergence metrics for visualization
/// </summary>
public class ConvergenceMetricsDto
{
    public string LoopId { get; set; } = string.Empty;
    public double FinalConvergenceScore { get; set; }
    public bool Converged { get; set; }
    public List<double> IterationScores { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public int AveragePodcastSize { get; set; }
}

