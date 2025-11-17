namespace GA.Analytics.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using System.ComponentModel.DataAnnotations;
using GA.Analytics.Service.Models;
using GA.Analytics.Service.Services;

/// <summary>
///     Provides spectral analysis for external autonomous systems (e.g., TARS Tier2 governance).
/// </summary>
[ApiController]
[Route("api/spectral")]
public sealed class SpectralAnalyticsController(
    AgentSpectralAnalyzer analyzer,
    ILogger<SpectralAnalyticsController> logger) : ControllerBase
{
    /// <summary>
    ///     Compute spectral metrics for an agent interaction snapshot.
    /// </summary>
    [HttpPost("agent-loop")]
    [ProducesResponseType(typeof(AgentSpectralMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<AgentSpectralMetrics> AnalyzeAgentLoop([FromBody] AgentInteractionRequest request)
    {
        if (request.Agents.Count == 0)
        {
            return BadRequest("Agents collection must not be empty.");
        }

        try
        {
            var graph = new AgentInteractionGraph
            {
                Agents = request.Agents
                    .Select(a => new AgentNode
                    {
                        Id = a.Id,
                        DisplayName = a.DisplayName ?? a.Id,
                        Weight = a.Weight,
                        Signals = a.Signals ?? new Dictionary<string, double>()
                    })
                    .ToList(),
                Edges = request.Edges
                    .Select(e => new AgentInteractionEdge
                    {
                        Source = e.Source,
                        Target = e.Target,
                        Weight = e.Weight,
                        Features = e.Features ?? new Dictionary<string, double>()
                    })
                    .ToList(),
                IsUndirected = request.IsUndirected,
                Metadata = request.Metadata ?? new Dictionary<string, string>()
            };

            var metrics = analyzer.Analyze(graph);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to compute spectral metrics for agent loop");
            return StatusCode(StatusCodes.Status500InternalServerError, "Failed to compute spectral metrics");
        }
    }

    public sealed record AgentInteractionRequest
    {
        [Required] public required List<AgentNodeRequest> Agents { get; init; }

        public List<AgentInteractionEdgeRequest> Edges { get; init; } = new();

        public bool IsUndirected { get; init; } = true;

        public Dictionary<string, string>? Metadata { get; init; }
    }

    public sealed record AgentNodeRequest
    {
        [Required] public required string Id { get; init; }

        public string? DisplayName { get; init; }
        public double Weight { get; init; } = 1.0;
        public Dictionary<string, double>? Signals { get; init; }
    }

    public sealed record AgentInteractionEdgeRequest
    {
        [Required] public required string Source { get; init; }

        [Required] public required string Target { get; init; }

        public double Weight { get; init; } = 1.0;
        public Dictionary<string, double>? Features { get; init; }
    }
}
