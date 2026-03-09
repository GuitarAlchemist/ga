namespace GA.Business.ML.Agents.Hooks;

/// <summary>
/// A lifecycle interceptor for the chatbot orchestration pipeline.
/// Mirrors Claude Code's hook model: hooks fire at named lifecycle points and
/// can cancel execution, mutate the message, or short-circuit with a blocked response.
/// </summary>
/// <remarks>
/// All methods have default implementations (no-op Continue) — implement only
/// the lifecycle points you care about. Hooks execute sequentially in registration
/// order; the first hook that returns <see cref="HookResult.Cancel"/> stops the chain.
/// </remarks>
public interface IChatHook
{
    /// <summary>
    /// Fires before any skill routing — ideal for sanitization, rate-limit checks, auth.
    /// Equivalent to Claude Code's <c>UserPromptSubmit</c> hook.
    /// </summary>
    Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
        => Task.FromResult(HookResult.Continue);

    /// <summary>
    /// Fires after a skill is matched, before it executes.
    /// Equivalent to Claude Code's <c>PreToolUse</c> hook. Can veto the skill.
    /// </summary>
    Task<HookResult> OnBeforeSkill(ChatHookContext ctx, CancellationToken ct = default)
        => Task.FromResult(HookResult.Continue);

    /// <summary>
    /// Fires after a skill returns its response. Can mutate or observe the result.
    /// Equivalent to Claude Code's <c>PostToolUse</c> hook.
    /// </summary>
    Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct = default)
        => Task.FromResult(HookResult.Continue);

    /// <summary>
    /// Fires once the final response is assembled and about to be returned.
    /// Equivalent to Claude Code's <c>Stop</c> hook — ideal for memory writing, analytics.
    /// </summary>
    Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
        => Task.FromResult(HookResult.Continue);
}

/// <summary>
/// The outcome of a hook invocation. Controls whether the pipeline continues,
/// is cancelled, or processes a mutated message.
/// </summary>
/// <param name="Cancel">When true the pipeline stops immediately.</param>
/// <param name="MutatedMessage">Replacement message for downstream processing (null = unchanged).</param>
/// <param name="BlockedResponse">Response to return when <paramref name="Cancel"/> is true.</param>
public sealed record HookResult(
    bool Cancel = false,
    string? MutatedMessage = null,
    AgentResponse? BlockedResponse = null)
{
    /// <summary>Pipeline continues unchanged.</summary>
    public static HookResult Continue => new();

    /// <summary>Block the request and return <paramref name="reason"/> to the caller.</summary>
    public static HookResult Block(string reason) => new(
        Cancel: true,
        BlockedResponse: new AgentResponse
        {
            AgentId    = "hook",
            Result     = reason,
            Confidence = 0f,
        });

    /// <summary>Replace the current message before downstream processing.</summary>
    public static HookResult Mutate(string message) => new(MutatedMessage: message);
}
