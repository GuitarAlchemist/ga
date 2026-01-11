namespace GA.Testing.Semantic;

/// <summary>
/// Defines a service for qualitative reasoning evaluation (Level 1 Testing).
/// Typically backed by a local or remote LLM.
/// </summary>
public interface IJudgeService
{
    /// <summary>
    /// Evaluates text against a specific rubric.
    /// </summary>
    /// <param name="text">The output to judge.</param>
    /// <param name="prompt">Contextual instructions for the judge.</param>
    /// <param name="rubric">Specific criteria (passing/failing) for the evaluation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing whether it passed and the reasoning profile.</returns>
    Task<JudgeResult> EvaluateAsync(string text, string prompt, string rubric, CancellationToken cancellationToken = default);
}

public record JudgeResult(bool IsPassing, string Rationale, double Confidence);
