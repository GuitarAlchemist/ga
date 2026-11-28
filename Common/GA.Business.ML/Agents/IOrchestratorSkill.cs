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
///   <item><see cref="ExecuteAsync(string, CancellationToken)"/> computes the answer from domain logic, then optionally
///     calls the LLM only for explanation — never for computation.</item>
/// </list>
/// Skills are checked in registration order; first match wins.
/// </remarks>
public interface IOrchestratorSkill
{
    /// <summary>Human-readable name used for logging and trace tags.</summary>
    string Name { get; }

    /// <summary>Declares what this skill handles (surfaced in chatbot capability lists
    /// and embedded by <c>SemanticIntentRouter</c> for routing).</summary>
    string Description { get; }

    /// <summary>
    /// Canonical phrasings that should route to this skill — used by the unified
    /// embedding-based <c>SemanticIntentRouter</c> (per
    /// <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>
    /// §"Routing classifiers"). Empty list means the skill participates in
    /// routing only via <see cref="CanHandle"/> (legacy / fallback path).
    /// Three to six representative examples is the sweet spot.
    /// </summary>
    IReadOnlyList<string> ExamplePrompts => [];

    /// <summary>
    /// Legacy fast path — string-matching predicate retained for the foreach
    /// fallback in <c>ProductionOrchestrator</c>. New skills should prefer
    /// <see cref="ExamplePrompts"/> and stub this to <c>false</c>.
    /// </summary>
    bool CanHandle(string message);

    /// <summary>
    /// True when the history-aware <c>ExecuteAsync</c> resolves an elliptical follow-up ("And a
    /// C shape?") against the previous turns and declines one it cannot resolve. Default: false.
    /// </summary>
    bool ResolvesFollowUps => false;

    /// <summary>
    /// Executes the skill: compute with domain logic, optionally explain via LLM.
    /// </summary>
    Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the skill with the conversation turns that precede <paramref name="message"/>,
    /// oldest first. LLM-backed skills pass them to the model so a follow-up such as "Now
    /// transpose it to F." resolves "it"; deterministic skills ignore them (the default).
    /// </summary>
    Task<AgentResponse> ExecuteAsync(
        string message,
        IReadOnlyList<ChatHistoryTurn> history,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(message, cancellationToken);
}
