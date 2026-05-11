namespace GA.Business.ML.Agents.Memory;

/// <summary>
/// A pending durable-memory write that a skill emits via
/// <see cref="AgentResponse.Data"/>. The owning <c>MemoryWriteHook</c>
/// (on <c>OnResponseSent</c>) picks it up and persists it with the
/// caller's <see cref="Hooks.ChatHookContext.SessionId"/>.
/// </summary>
/// <remarks>
/// <para>
/// This indirection exists because <see cref="IOrchestratorSkill.ExecuteAsync"/>
/// is intentionally session-agnostic (skills are pure-functional units
/// that don't know which user is calling). The skill parses intent and
/// emits a request; the hook owns the actual <see cref="MemoryStore"/>
/// write with the correct session scope. This keeps the SC-001 defense
/// intact — no skill can write into a session it doesn't own, and no
/// skill can write globally without an operator-flagged path.
/// </para>
/// <para>
/// <b>Why a record on AgentResponse.Data rather than a side-channel:</b>
/// <list type="bullet">
///   <item>The hook fires after the response is produced; passing the
///         request through Data keeps the flow strictly synchronous.</item>
///   <item>The hook can pattern-match <c>ctx.Response.Data is MemoryWriteRequest req</c>
///         without inspecting any other context — no shared state.</item>
///   <item>If the skill is invoked from a test that doesn't wire the
///         hook, the request is harmlessly ignored.</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Key">Stable key used for <see cref="MemoryStore"/> dedup
/// within a session. Skills generate this from the content (typically
/// a slug + short hash) so the same write request is idempotent.</param>
/// <param name="Type">Memory entry type — <c>fact</c>, <c>preference</c>,
/// <c>focus</c>, or another future durable type. Never <c>response</c>
/// (that's the transient transcript-store type post-PR #174).</param>
/// <param name="Content">The user-supplied content to persist verbatim
/// (after sanitization upstream via <c>PromptSanitizationHook</c>).</param>
/// <param name="Tags">Optional structured tags for filtering and search.</param>
public sealed record MemoryWriteRequest(
    string Key,
    string Type,
    string Content,
    IReadOnlyList<string> Tags);
