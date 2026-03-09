namespace GA.Business.ML.Agents;

/// <summary>
/// A domain-grounded skill that runs at the orchestrator level — before routing
/// and before any agent is selected. Short-circuits the full LLM pipeline for
/// deterministic or near-deterministic queries.
/// </summary>
/// <remarks>
/// Mirrors the Claude Code skill pattern at the top of the processing chain:
/// <list type="bullet">
///   <item><see cref="CanHandle"/> declares when the skill applies (no side effects).</item>
///   <item><see cref="ExecuteAsync"/> computes the answer from domain logic, then optionally
///     calls the LLM only for explanation — never for computation.</item>
/// </list>
/// Skills are checked in registration order; first match wins.
/// </remarks>
public interface IOrchestratorSkill
{
    /// <summary>Human-readable name used for logging and trace tags.</summary>
    string Name { get; }

    /// <summary>Declares what this skill handles (surfaced in chatbot capability lists).</summary>
    string Description { get; }

    /// <summary>
    /// Fast, side-effect-free check — returns true when this skill can fully answer the message.
    /// </summary>
    bool CanHandle(string message);

    /// <summary>
    /// Executes the skill: compute with domain logic, optionally explain via LLM.
    /// </summary>
    Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default);
}
