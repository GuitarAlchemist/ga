namespace GA.Business.ML.Agents.Skills;

using Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Explicit "remember this" skill — the first durable-memory writer in the
/// chatbot. Parses "remember that I prefer drop-2 voicings" / "save this:
/// jazz comping focus" / "note: my favorite key is Bb" into a
/// <see cref="MemoryWriteRequest"/> and emits it on
/// <see cref="AgentResponse.Data"/>. The owning <c>MemoryWriteHook</c>
/// then persists it under the caller's
/// <see cref="Hooks.ChatHookContext.SessionId"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> after PR #173/#174 split transcript content from
/// durable memory, <see cref="MemoryStore"/> is empty of <c>fact</c> /
/// <c>preference</c> / <c>focus</c> entries — there's nothing for
/// <c>Memory:EnrichOnRetrieve=true</c> to retrieve. This skill is the
/// first writer; without it, the memory retrieval surface stays unfilled
/// regardless of how good the retrieval mechanics get.
/// </para>
/// <para>
/// <b>Explicit-only by design:</b> this skill fires ONLY when the user
/// directly says "remember"/"save"/"note"/"store"/"don't forget" — never
/// auto-extracts implicit preferences from arbitrary chat. Auto-extraction
/// is option C from the PR #172 audit and was deferred for safety: the
/// chatbot inferring "the user prefers X" and silently writing it is a
/// trust-leak vector that bypasses both SC-001's MCP-write gate and the
/// session-scoping defense.
/// </para>
/// <para>
/// <b>Session attribution:</b> the skill does not (and cannot) know the
/// caller's SessionId — <see cref="IOrchestratorSkill.ExecuteAsync"/> is
/// session-agnostic by interface design. The skill emits the request,
/// the hook owns the write with the correct session. If <c>MemoryWriteHook</c>
/// is not registered in DI, the request is harmlessly ignored and the
/// user sees only the confirmation message.
/// </para>
/// </remarks>
public sealed class RememberThisSkill(ILogger<RememberThisSkill> logger) : IOrchestratorSkill
{
    public string Name        => "RememberThis";
    public string Description =>
        "Persists a user-stated fact, preference, or focus to durable memory " +
        "for retrieval in future conversations. Fires on explicit phrasings " +
        "like 'remember that...', 'save this:', 'note:', 'don't forget...'. " +
        "Never auto-extracts — the user must explicitly ask.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "remember that I prefer drop-2 voicings for jazz comping",
        "save this: my favorite key is Bb",
        "note: I'm working on fingerstyle technique this month",
        "don't forget that I'm an intermediate guitarist",
        "remember I always tune to drop D for these songs",
        "store this fact: my main guitar is a Telecaster",
        "please remember my focus this week is jazz comping",
    ];

    public bool CanHandle(string message) =>
        RememberThisParser.LooksLikeRememberRequest(message);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = RememberThisParser.TryParse(message);
        if (request is null)
        {
            // Defensive — the router shouldn't reach here without a
            // remember-phrasing, but if it did, refuse rather than write
            // an empty entry. Returns a low-confidence response so any
            // downstream confidence-gated logic ignores it.
            logger.LogDebug("RememberThisSkill: no remember-phrasing detected in: {Msg}", message);
            return Task.FromResult(AgentResponse.CannotHelp(
                agentId: "remember-this",
                reason:  "I didn't find anything to remember in that message — try " +
                         "phrasing it as 'remember that ...' or 'save this: ...'."));
        }

        logger.LogInformation(
            "RememberThisSkill: parsed remember-request type={Type} key={Key} contentLen={Len}",
            request.Type, request.Key, request.Content.Length);

        // The confirmation message uses present-perfect ("I've noted...")
        // to signal commitment to the user. The actual write hasn't
        // happened yet — MemoryWriteHook does it after this method
        // returns and OnResponseSent fires — but by then the response
        // text is fixed, so we have to write the message optimistically.
        // If the hook can't persist (e.g., no session), it logs a warning
        // and the user has a soft inconsistency. Acceptable for v0.1
        // since the alternative (delaying the response until after the
        // write completes) requires a deeper orchestrator refactor.
        var confirmation = request.Type switch
        {
            "preference" => $"Got it — I'll remember your preference: \"{request.Content}\".",
            "focus"      => $"Noted — current focus: \"{request.Content}\".",
            _            => $"Saved — I'll remember: \"{request.Content}\".",
        };

        return Task.FromResult(new AgentResponse
        {
            AgentId    = "remember-this",
            Result     = confirmation,
            Confidence = 1.0f,
            Evidence   = [$"Parsed as type={request.Type}", $"Key: {request.Key}"],
            Data       = request,
        });
    }
}
