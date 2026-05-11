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

    /// <summary>
    /// Optional per-conversation identifier. Set by the transport layer
    /// (e.g. SignalR <c>Context.ConnectionId</c> in ChatbotHub, HTTP
    /// session cookie in controllers) so downstream hooks can scope state
    /// to one conversation rather than the whole process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When null, hooks that depend on session scope (e.g.
    /// <see cref="MemoryHook"/>) MUST default to a conservative global
    /// behaviour rather than treating "no session" as a single shared
    /// session — that's the leak documented in PR #151 review
    /// (<c>Memory:EnrichOnRetrieve=false</c> default).
    /// </para>
    /// <para>
    /// Distinct from <see cref="CorrelationId"/>: CorrelationId is one
    /// Guid per request and changes between turns; SessionId is stable
    /// across the turns of one conversation.
    /// </para>
    /// <para>
    /// <b>SignalR rotation caveat (PR #159 review ux-001):</b> when the
    /// transport is SignalR (<c>ChatbotHub</c>), SessionId =
    /// <c>Context.ConnectionId</c>, which is a *per-physical-connection*
    /// identifier. With <c>withAutomaticReconnect()</c> on the client,
    /// a transient WebSocket drop opens a fresh negotiation and a fresh
    /// ConnectionId. Treat SessionId as <i>connection-scoped, not
    /// user-scoped</i>: a coffee-shop wifi blip rotates the session and
    /// the user's prior turns become unreachable from MemoryHook.
    /// Acceptable for the anonymous demo (where the alternative — a
    /// stable client-supplied ID — would expand the trust boundary).
    /// </para>
    /// <para>
    /// <b>HTTP controllers (PR #159 review plumb-001):</b> the HTTP chat
    /// surfaces at <c>ChatbotController</c> (both GaApi and
    /// GaChatbot.Api) do NOT plumb SessionId today — Phase B is
    /// SignalR-only. The orchestrator falls back to a fresh Guid per
    /// request so HTTP callers get throwaway per-request sessions whose
    /// writes are unreachable from future retrieval. Tracked as Phase C
    /// (task #103); do NOT flip <c>Memory:EnrichOnRetrieve=true</c>
    /// expecting transport-uniform behaviour until Phase C ships.
    /// </para>
    /// </remarks>
    public string? SessionId { get; init; }

    /// <summary>DI service locator — available to stateful hooks that need services.</summary>
    public IServiceProvider? Services { get; init; }
}
