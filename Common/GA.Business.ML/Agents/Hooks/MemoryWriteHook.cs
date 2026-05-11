namespace GA.Business.ML.Agents.Hooks;

using Memory;
using Microsoft.Extensions.Logging;

/// <summary>
/// Picks up a <see cref="MemoryWriteRequest"/> emitted on
/// <see cref="AgentResponse.Data"/> by <c>RememberThisSkill</c> (or any
/// future skill that wants to persist durable knowledge) and writes it
/// to <see cref="MemoryStore"/> under the caller's
/// <see cref="ChatHookContext.SessionId"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a hook rather than skill-side writes:</b>
/// <see cref="IOrchestratorSkill.ExecuteAsync"/> is session-agnostic — the
/// skill cannot see <see cref="ChatHookContext.SessionId"/>. Routing every
/// durable-write through this hook keeps session-scoping enforced
/// centrally: the storage write happens in exactly one place, with one
/// known invariant (SessionId from the live chat context).
/// </para>
/// <para>
/// <b>Refuses on null SessionId.</b> Same posture as
/// <see cref="MemoryHook.OnResponseSent"/>'s SC-001 guard: writing to the
/// global partition without the operator-flagged
/// <c>Memory:AllowLlmGlobalWrite</c> path would defeat the session-scoping
/// defense. When SessionId is null, log a warning and let the user's
/// "I've saved..." confirmation hang — operationally that's a clearer
/// signal than silently writing to global and breaking the security
/// posture downstream.
/// </para>
/// <para>
/// <b>Refuses on disallowed types.</b> Only <c>fact</c> / <c>preference</c> /
/// <c>focus</c> writes are accepted. <c>response</c> is the transient
/// transcript-store type post-PR #174 — a remember-skill emitting that
/// type would mean a bug, and we refuse rather than re-introduce the
/// architectural conflation that PR #173/#174 closed.
/// </para>
/// </remarks>
public sealed class MemoryWriteHook(
    MemoryStore memoryStore,
    ILogger<MemoryWriteHook> logger) : IChatHook
{
    /// <summary>
    /// Durable-memory types this hook will persist. <c>response</c> is
    /// deliberately excluded — that's transcript-log content, which lives
    /// in <see cref="ChatTranscriptStore"/> per PR #173/#174.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fact",
            "preference",
            "focus",
        };

    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default) =>
        Task.FromResult(HookResult.Continue);

    public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (ctx.Response?.Data is not MemoryWriteRequest req)
            return Task.FromResult(HookResult.Continue);

        if (ctx.SessionId is null)
        {
            logger.LogWarning(
                "MemoryWriteHook: refused to persist '{Type}/{Key}' — SessionId is null. " +
                "Writing here would land an entry in the global partition without an " +
                "operator-flagged path (SC-001 defense). The user's confirmation message " +
                "has already been sent — there's now a soft inconsistency. Check the " +
                "orchestrator's SessionId plumbing (PR #157 Phase B regression?).",
                req.Type, req.Key);
            return Task.FromResult(HookResult.Continue);
        }

        if (!AllowedTypes.Contains(req.Type))
        {
            logger.LogWarning(
                "MemoryWriteHook: refused to persist '{Type}/{Key}' — type is not in the " +
                "durable-memory whitelist ({Allowed}). 'response' belongs in the transcript " +
                "store (PR #173/#174); arbitrary types would re-introduce the conflation.",
                req.Type, req.Key, string.Join(", ", AllowedTypes));
            return Task.FromResult(HookResult.Continue);
        }

        try
        {
            memoryStore.Write(
                sessionId: ctx.SessionId,
                key:       req.Key,
                type:      req.Type,
                content:   req.Content,
                tags:      req.Tags.ToArray());

            logger.LogInformation(
                "MemoryWriteHook: persisted {Type} entry '{Key}' for session {SessionId} " +
                "(content length {Len}).",
                req.Type, req.Key, ctx.SessionId, req.Content.Length);
        }
        catch (Exception ex)
        {
            // Mirror MemoryHook.OnResponseSent's posture: log loudly,
            // don't rethrow — the user has already seen the confirmation
            // and there's nothing we can do mid-pipeline. The soft
            // inconsistency is preferable to a thrown exception that
            // surfaces as a 500 to the chat client.
            logger.LogError(ex,
                "MemoryWriteHook: failed to persist {Type} entry '{Key}' for session " +
                "{SessionId}. The user has already received the 'I've saved' confirmation; " +
                "they will likely re-ask later and hit the same failure.",
                req.Type, req.Key, ctx.SessionId);
        }

        return Task.FromResult(HookResult.Continue);
    }
}
