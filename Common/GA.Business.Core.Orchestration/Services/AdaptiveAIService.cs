namespace GA.Business.Core.Orchestration.Services;

using GA.Business.ML.Abstractions;
using GA.Core.Functional;
using Microsoft.Extensions.Logging;

/// <summary>
///     Layer-5 implementation of <see cref="IAdaptiveAIService"/>.
///     Projects an adaptive difficulty curve from a player's current skill signal.
/// </summary>
/// <remarks>
///     <para>
///         Proof slice for issue #48 (Option C). The curve model is intentionally
///         simple and deterministic: it converges the current difficulty toward a
///         target derived from the player's recent success rate, ramping at a pace
///         driven by the learning rate. This mirrors the comfort-zone "attractor"
///         concept surfaced by the React <c>AdaptiveAIDashboard</c>
///         (success rate / current difficulty / learning rate).
///     </para>
///     <para>
///         Railway-oriented: all validation failures return
///         <see cref="Result{TValue,TError}.Failure"/> with a human-readable message
///         rather than throwing.
///     </para>
/// </remarks>
public sealed class AdaptiveAIService(ILogger<AdaptiveAIService> logger) : IAdaptiveAIService
{
    private const int MaxSteps = 64;

    /// <inheritdoc />
    public Result<DifficultyCurve, string> ComputeDifficultyCurve(
        DifficultyCurveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Result<DifficultyCurve, string>.Failure("Request must not be null.");
        }

        if (string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return Result<DifficultyCurve, string>.Failure("PlayerId must not be empty.");
        }

        if (!IsUnitInterval(request.SuccessRate))
        {
            return Result<DifficultyCurve, string>.Failure("SuccessRate must be in [0, 1].");
        }

        if (!IsUnitInterval(request.CurrentDifficulty))
        {
            return Result<DifficultyCurve, string>.Failure("CurrentDifficulty must be in [0, 1].");
        }

        if (!IsUnitInterval(request.LearningRate))
        {
            return Result<DifficultyCurve, string>.Failure("LearningRate must be in [0, 1].");
        }

        if (request.Steps is < 1 or > MaxSteps)
        {
            return Result<DifficultyCurve, string>.Failure(
                $"Steps must be between 1 and {MaxSteps}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var start = request.CurrentDifficulty;

        // Target difficulty: a high success rate means the player has out-grown the
        // current difficulty, so we aim higher; a low success rate eases off. We bias
        // toward a "comfort zone" success target of ~0.75 (the dashboard's green band).
        var target = Clamp01(start + (request.SuccessRate - 0.75) * 0.5);

        // Ramp factor blends the learning rate with a floor so the curve always moves
        // a little; a learning rate of 0 still converges, just slowly.
        var ramp = 0.1 + 0.4 * request.LearningRate;

        var points = new List<DifficultyCurvePoint>(request.Steps);
        var current = start;
        for (var step = 0; step < request.Steps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Exponential approach toward the target (geometric convergence).
            current += (target - current) * ramp;
            points.Add(new DifficultyCurvePoint(step, Math.Round(Clamp01(current), 4)));
        }

        logger.LogDebug(
            "Computed difficulty curve for player {PlayerId}: start={Start}, target={Target}, steps={Steps}",
            request.PlayerId,
            start,
            target,
            request.Steps);

        return Result<DifficultyCurve, string>.Success(
            new DifficultyCurve(request.PlayerId, start, target, points));
    }

    private static bool IsUnitInterval(double value) =>
        !double.IsNaN(value) && value is >= 0.0 and <= 1.0;

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
