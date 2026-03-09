namespace GA.Business.ML.Agents.Hooks;

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
    /// Hooks may replace this value via <see cref="HookResult.Mutate"/>;
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

    /// <summary>Optional session or user identifier for per-user policies.</summary>
    public string? UserId { get; init; }

    /// <summary>DI service locator — available to stateful hooks that need services.</summary>
    public required IServiceProvider Services { get; init; }
}
