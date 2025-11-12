namespace GA.DocumentProcessing.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

/// <summary>
/// API controller for autonomous YouTube video curation
/// Uses Graphiti knowledge graph to guide intelligent content discovery
/// </summary>
[ApiController]
[Route("api/autonomous-curation")]
public class AutonomousCurationController : ControllerBase
{
    private readonly ILogger<AutonomousCurationController> _logger;
    private readonly KnowledgeGapAnalyzer _gapAnalyzer;
    private readonly AutonomousCurationOrchestrator _curationOrchestrator;

    public AutonomousCurationController(
        ILogger<AutonomousCurationController> logger,
        KnowledgeGapAnalyzer gapAnalyzer,
        AutonomousCurationOrchestrator curationOrchestrator)
    {
        _logger = logger;
        _gapAnalyzer = gapAnalyzer;
        _curationOrchestrator = curationOrchestrator;
    }

    /// <summary>
    /// Analyze knowledge gaps in the current knowledge base
    /// </summary>
    [HttpGet("analyze-gaps")]
    [ProducesResponseType(typeof(KnowledgeGapAnalysis), 200)]
    public async Task<IActionResult> AnalyzeGaps(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Analyzing knowledge gaps");

            var analysis = await _gapAnalyzer.AnalyzeGapsAsync(cancellationToken);

            return Ok(new
            {
                Status = "success",
                Message = $"Found {analysis.Gaps.Count} knowledge gaps",
                Data = analysis
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing knowledge gaps");
            return StatusCode(500, new
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get knowledge gaps by priority level
    /// </summary>
    [HttpGet("gaps/by-priority/{priority}")]
    [ProducesResponseType(typeof(List<KnowledgeGap>), 200)]
    public async Task<IActionResult> GetGapsByPriority(string priority, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await _gapAnalyzer.AnalyzeGapsAsync(cancellationToken);
            var gaps = analysis.GetGapsByPriority(priority);

            return Ok(new
            {
                Status = "success",
                Priority = priority,
                Count = gaps.Count,
                Gaps = gaps
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gaps by priority");
            return StatusCode(500, new
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get knowledge gaps by category
    /// </summary>
    [HttpGet("gaps/by-category/{category}")]
    [ProducesResponseType(typeof(List<KnowledgeGap>), 200)]
    public async Task<IActionResult> GetGapsByCategory(string category, CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await _gapAnalyzer.AnalyzeGapsAsync(cancellationToken);
            var gaps = analysis.GetGapsByCategory(category);

            return Ok(new
            {
                Status = "success",
                Category = category,
                Count = gaps.Count,
                Gaps = gaps
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting gaps by category");
            return StatusCode(500, new
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Start autonomous curation process
    /// The system will:
    /// 1. Analyze knowledge gaps
    /// 2. Search YouTube for relevant videos
    /// 3. Evaluate video quality
    /// 4. Automatically process high-quality videos
    /// 5. Update Graphiti knowledge graph
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(AutonomousCurationResult), 200)]
    public async Task<IActionResult> StartCuration(
        [FromBody] StartAutonomousCurationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting autonomous curation");

            var result = await _curationOrchestrator.StartCurationAsync(request, cancellationToken);

            return Ok(new
            {
                Status = "success",
                Message = $"Curation complete: {result.VideosAccepted} videos accepted, {result.VideosRejected} rejected",
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during autonomous curation");
            return StatusCode(500, new
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Start autonomous curation with default settings
    /// Focuses on high-priority gaps
    /// </summary>
    [HttpPost("start/quick")]
    [ProducesResponseType(typeof(AutonomousCurationResult), 200)]
    public async Task<IActionResult> StartQuickCuration(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting quick autonomous curation");

            var request = new StartAutonomousCurationRequest
            {
                MaxVideosPerGap = 2,
                MaxTotalVideos = 5,
                MinQualityScore = 0.75,
                FocusPriorities = new List<string> { "Critical", "High" }
            };

            var result = await _curationOrchestrator.StartCurationAsync(request, cancellationToken);

            return Ok(new
            {
                Status = "success",
                Message = $"Quick curation complete: {result.VideosAccepted} videos accepted",
                Data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quick curation");
            return StatusCode(500, new
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Get curation statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await _gapAnalyzer.AnalyzeGapsAsync(cancellationToken);

            var stats = new
            {
                TotalGaps = analysis.Gaps.Count,
                GapsByPriority = new
                {
                    Critical = analysis.GetGapsByPriority("Critical").Count,
                    High = analysis.GetGapsByPriority("High").Count,
                    Medium = analysis.GetGapsByPriority("Medium").Count,
                    Low = analysis.GetGapsByPriority("Low").Count
                },
                GapsByCategory = analysis.Gaps
                    .GroupBy(g => g.Category)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopPriorityGaps = analysis.GetTopPriorityGaps(5)
            };

            return Ok(new
            {
                Status = "success",
                Data = stats
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting curation stats");
            return StatusCode(500, new
            {
                Status = "error",
                Message = ex.Message
            });
        }
    }
}

