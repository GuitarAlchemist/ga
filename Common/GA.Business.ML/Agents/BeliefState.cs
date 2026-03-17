namespace GA.Business.ML.Agents;

using System.Linq;

/// <summary>
/// Tetravalent belief values — extends classical True/False with Unknown and Contradictory.
/// Based on Demerzel governance tetravalent logic.
/// </summary>
public enum BeliefValue { True, False, Unknown, Contradictory }

/// <summary>
/// A tetravalent belief state inferred from an <see cref="AgentResponse"/>.
/// Captures not just confidence but the epistemic quality of the response.
/// </summary>
public sealed record BeliefState(
    BeliefValue Value,
    float Confidence,
    IReadOnlyList<string> SupportingEvidence,
    IReadOnlyList<string> ContradictingEvidence)
{
    /// <summary>
    /// Infers a tetravalent belief state from an agent response.
    /// </summary>
    public static BeliefState FromAgentResponse(AgentResponse response)
    {
        var supporting = response.Evidence.ToList();
        var contradicting = response.Assumptions
            .Where(a => a.Contains("contradict", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var value = contradicting.Count > 0
            ? BeliefValue.Contradictory
            : response.Confidence < 0.01f
                ? BeliefValue.False
                : response.Confidence < 0.5f
                    ? BeliefValue.Unknown
                    : BeliefValue.True;

        return new BeliefState(value, response.Confidence, supporting, contradicting);
    }
}
