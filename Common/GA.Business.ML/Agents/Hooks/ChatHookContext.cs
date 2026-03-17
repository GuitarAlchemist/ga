namespace GA.Business.ML.Agents.Hooks;

using GA.Business.Core.Context;

/// <summary>
/// Mutable context object passed to every <see cref="IChatHook"/> invocation.
/// Hooks may read and mutate <see cref="CurrentMessage"/> and <see cref="Response"/>.
/// </summary>
public sealed class ChatHookContext
{
    /// <summary>The original unmodified message from the user.</summary>
    public required string OriginalMessage { get; init; }

    /// <summary>
    /// The message as seen by the current stage of the pipeline.
    /// Hooks may replace this value by returning <see cref="HookResult.Mutate"/>;
    /// the updated value propagates to subsequent hooks and the skill.
    /// </summary>
    public string CurrentMessage { get; set; } = string.Empty;

    /// <summary>
    /// The skill that matched <see cref="CurrentMessage"/>; null before routing.
    /// Set at <see cref="IChatHook.OnBeforeSkill"/> and later lifecycle points.
    /// </summary>
    public string? MatchedSkillName { get; init; }

    /// <summary>
    /// The skill's response; null before <see cref="IChatHook.OnAfterSkill"/>.
    /// </summary>
    public AgentResponse? Response { get; set; }

    /// <summary>
    /// Per-request correlation ID set by the orchestrator at the start of each
    /// <c>AnswerAsync</c> call. Hooks use this to correlate <c>OnBeforeSkill</c>
    /// and <c>OnAfterSkill</c> without collisions when the same skill runs concurrently
    /// for different requests.
    /// </summary>
    public Guid CorrelationId { get; init; }

    /// <summary>Optional session or user identifier for per-user policies.</summary>
    public string? UserId { get; init; }

    /// <summary>DI service locator — available to stateful hooks that need services.</summary>
    public IServiceProvider? Services { get; init; }

    /// <summary>
    /// The musical session context for the current request, populated by the orchestrator
    /// when an <see cref="GA.Business.Core.Session.ISessionContextProvider"/> is available.
    /// Null when no session context provider is registered.
    /// </summary>
    public MusicalSessionContext? SessionContext { get; init; }
}
