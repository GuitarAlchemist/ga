namespace GaApi.Controllers;

using System.ComponentModel.DataAnnotations;
using GA.Business.ML.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
///     Adaptive-AI endpoints (issue #48 proof slice).
///     Thin controller: all logic lives behind <see cref="IAdaptiveAIService"/>
///     (layer 4 abstraction, implemented in layer 5 orchestration). This controller
///     only validates the wire shape, delegates, and frames the result.
/// </summary>
/// <remarks>
///     Unparked from <c>Controllers/_Parked/</c> as the first of the five parked
///     AI controllers. AdvancedAI / VectorSearch / SemanticSearch /
///     EnhancedPersonalization remain parked until this pattern lands.
/// </remarks>
[ApiController]
[Route("api/adaptive-ai")]
[Produces("application/json")]
public sealed class AdaptiveAIController(IAdaptiveAIService adaptiveAi) : ControllerBase
{
    /// <summary>
    ///     Computes an adaptive difficulty curve for a player.
    /// </summary>
    /// <param name="request">Player skill signal and projection step count.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>The projected difficulty curve, or a 400 with the validation error.</returns>
    [HttpPost("difficulty-curve")]
    [ProducesResponseType(typeof(DifficultyCurve), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ComputeDifficultyCurve(
        [FromBody] DifficultyCurveApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = adaptiveAi.ComputeDifficultyCurve(
            new DifficultyCurveRequest(
                request.PlayerId,
                request.SuccessRate,
                request.CurrentDifficulty,
                request.LearningRate,
                request.Steps),
            cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: Ok,
            onFailure: error => Problem(detail: error, statusCode: StatusCodes.Status400BadRequest));
    }
}

/// <summary>
///     Wire shape for <c>POST /api/adaptive-ai/difficulty-curve</c>.
/// </summary>
public sealed class DifficultyCurveApiRequest
{
    /// <summary>Identifier of the player the curve is computed for.</summary>
    [Required]
    [MaxLength(128)]
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Recent success rate in [0, 1].</summary>
    [Range(0.0, 1.0)]
    public double SuccessRate { get; set; }

    /// <summary>Current difficulty level in [0, 1].</summary>
    [Range(0.0, 1.0)]
    public double CurrentDifficulty { get; set; }

    /// <summary>Learning rate in [0, 1].</summary>
    [Range(0.0, 1.0)]
    public double LearningRate { get; set; }

    /// <summary>Number of forward steps to project (1–64).</summary>
    [Range(1, 64)]
    public int Steps { get; set; } = 16;
}
