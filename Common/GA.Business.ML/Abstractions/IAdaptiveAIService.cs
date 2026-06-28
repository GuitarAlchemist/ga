namespace GA.Business.ML.Abstractions;

using GA.Core.Functional;

/// <summary>
///     Adaptive-AI capability surface (layer 4).
///     Captures the core capability of the formerly parked <c>AdaptiveAIController</c>:
///     turning a player's current skill signal into a forward-looking difficulty curve
///     that downstream practice/challenge generators can ramp against.
/// </summary>
/// <remarks>
///     This is the proof slice for issue #48 (Option C). Only the difficulty-curve
///     capability is exposed here; the remaining AdvancedAI / VectorSearch /
///     SemanticSearch / EnhancedPersonalization surfaces stay parked until this lands.
/// </remarks>
public interface IAdaptiveAIService
{
    /// <summary>
    ///     Computes an adaptive difficulty curve for a player given their current
    ///     skill signal and the number of upcoming practice steps to project.
    /// </summary>
    /// <param name="request">The difficulty-curve request (player id, skill signal, step count).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     A <see cref="Result{TValue,TError}"/> carrying the projected
    ///     <see cref="DifficultyCurve"/> on success, or a human-readable validation
    ///     error string on failure.
    /// </returns>
    Result<DifficultyCurve, string> ComputeDifficultyCurve(
        DifficultyCurveRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Input for <see cref="IAdaptiveAIService.ComputeDifficultyCurve"/>.
/// </summary>
/// <param name="PlayerId">Identifier of the player the curve is computed for.</param>
/// <param name="SuccessRate">
///     Player's recent success rate in <c>[0, 1]</c> — the primary skill signal.
/// </param>
/// <param name="CurrentDifficulty">
///     Player's current difficulty level in <c>[0, 1]</c> (the starting point of the curve).
/// </param>
/// <param name="LearningRate">
///     Player's learning rate in <c>[0, 1]</c> — how aggressively the curve ramps.
/// </param>
/// <param name="Steps">Number of forward steps to project (1–64).</param>
public record DifficultyCurveRequest(
    string PlayerId,
    double SuccessRate,
    double CurrentDifficulty,
    double LearningRate,
    int Steps);

/// <summary>
///     A single point on a projected difficulty curve.
/// </summary>
/// <param name="Step">Zero-based step index.</param>
/// <param name="Difficulty">Projected difficulty at this step, in <c>[0, 1]</c>.</param>
public record DifficultyCurvePoint(int Step, double Difficulty);

/// <summary>
///     A projected adaptive difficulty curve for a player.
/// </summary>
/// <param name="PlayerId">Identifier of the player the curve was computed for.</param>
/// <param name="StartDifficulty">Difficulty the curve started from, in <c>[0, 1]</c>.</param>
/// <param name="TargetDifficulty">Difficulty the curve converges toward, in <c>[0, 1]</c>.</param>
/// <param name="Points">Ordered projected points, one per step.</param>
public record DifficultyCurve(
    string PlayerId,
    double StartDifficulty,
    double TargetDifficulty,
    IReadOnlyList<DifficultyCurvePoint> Points);
