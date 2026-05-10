namespace GA.Business.ML.Agents.Hooks;

using Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hook that enriches requests with relevant memory context and persists
/// high-confidence responses as new memories.
/// </summary>
/// <remarks>
/// <para>
/// Retrieval (request-time injection) defaults to OFF — opt in via
/// <c>Memory:EnrichOnRetrieve=true</c>.
/// </para>
/// <para>
/// <b>Session scoping (2026-05-10):</b> Search and Write now pass the
/// caller's <see cref="ChatHookContext.SessionId"/> through to
/// <see cref="MemoryStore"/>, which filters entries to the matching
/// session plus global ones. The leak that motivated the OFF default
/// — "injecting accumulated history into every anonymous request leaks
/// chord references between unrelated conversations" — is fixed at the
/// storage layer.
/// </para>
/// <para>
/// However, the flag still defaults OFF until the transport layer
/// (ChatbotHub for SignalR, controllers for HTTP) actually populates
/// <see cref="ChatHookContext.SessionId"/>. When SessionId is null, this
/// hook conservatively skips retrieval to preserve the existing safety
/// posture — flipping the flag on without plumbing SessionId would
/// regress to the leaky behaviour.
/// </para>
/// </remarks>
public sealed class MemoryHook(
    MemoryStore memoryStore,
    IConfiguration configuration,
    ILogger<MemoryHook> logger) : IChatHook
{
    private readonly bool _enrichOnRetrieve =
        configuration.GetValue<bool?>("Memory:EnrichOnRetrieve") ?? false;

    /// <summary>
    /// Searches memory for context relevant to the incoming message and prepends it.
    /// Off by default; enable with <c>Memory:EnrichOnRetrieve=true</c>.
    /// </summary>
    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (!_enrichOnRetrieve) return Task.FromResult(HookResult.Continue);

        // Safety belt: when SessionId isn't plumbed, refuse to retrieve. The
        // alternative (treat null as a global session) replays the leak that
        // the OFF default was protecting against. Operators see this gap by
        // enabling EnrichOnRetrieve=true and observing "no memory injected"
        // — that's the signal to wire SessionId into ChatHookContext from
        // their transport layer.
        if (ctx.SessionId is null)
        {
            logger.LogDebug(
                "MemoryHook: skip retrieval — ChatHookContext.SessionId not set " +
                "(transport layer hasn't plumbed it yet; see ChatHookContext.SessionId remarks).");
            return Task.FromResult(HookResult.Continue);
        }

        var matches = memoryStore.Search(ctx.SessionId, ctx.CurrentMessage);
        if (matches.Count == 0) return Task.FromResult(HookResult.Continue);

        var contextBlock = string.Join("\n", matches.Take(3)
            .Select(m => $"[memory:{m.Key}] {m.Content}"));

        var enriched = $"[Relevant context from memory]\n{contextBlock}\n\n{ctx.CurrentMessage}";
        logger.LogDebug(
            "MemoryHook: injecting {Count} memory entries for session {SessionId}",
            matches.Count, ctx.SessionId);
        return Task.FromResult(HookResult.Mutate(enriched));
    }

    /// <summary>
    /// After a response is sent, if confidence is high enough and the response is non-trivial,
    /// persist it as a memory entry for future context enrichment.
    /// </summary>
    public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (ctx.Response is not { Confidence: >= 0.7f } response) return Task.FromResult(HookResult.Continue);
        if (response.Result.Length < 100) return Task.FromResult(HookResult.Continue);

        // Capture by value before fire-and-forget — ctx fields are not safe to
        // close over because the orchestrator may reuse / mutate the context
        // after this method returns.
        var sessionId       = ctx.SessionId;
        var correlationId   = ctx.CorrelationId;
        var originalMessage = ctx.OriginalMessage;
        var agentId         = response.AgentId;
        var resultSnippet   = response.Result[..Math.Min(500, response.Result.Length)];

        // Fire-and-forget write — don't block the response pipeline
        _ = Task.Run(() =>
        {
            try
            {
                var key = $"response_{correlationId:N}";
                memoryStore.Write(
                    sessionId: sessionId,
                    key: key,
                    type: "response",
                    content: $"Q: {originalMessage}\nA: {resultSnippet}",
                    tags: [agentId]);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MemoryHook: failed to persist response memory");
            }
        }, ct);

        return Task.FromResult(HookResult.Continue);
    }
}
