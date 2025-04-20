using Microsoft.AspNetCore.Mvc;

namespace GA.Business.Core.AI.LmStudio;

/// <summary>
/// Controller for LM Studio integration
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LmStudioController : ControllerBase
{
    private readonly LmStudioIntegrationService _integrationService;
    private readonly ILogger<LmStudioController> _logger;

    public LmStudioController(
        LmStudioIntegrationService integrationService,
        ILogger<LmStudioController> logger)
    {
        _integrationService = integrationService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves context for a query using vector search
    /// </summary>
    [HttpPost("context")]
    public async Task<IActionResult> GetContext([FromBody] QueryRequest request)
    {
        try
        {
            var context = await _integrationService.RetrieveContextAsync(request.Query, request.Limit);
            return Ok(new { context });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving context for query: {Query}", request.Query);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request model for context retrieval
    /// </summary>
    public class QueryRequest
    {
        /// <summary>
        /// The user's query
        /// </summary>
        public string Query { get; set; } = string.Empty;
        
        /// <summary>
        /// Maximum number of results to return
        /// </summary>
        public int Limit { get; set; } = 5;
    }
}
