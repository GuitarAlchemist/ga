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

    // Rate-limit the "SessionId not plumbed" message — log once per
    // process, not per request. 0 = not yet logged; 1 = already logged.
    private int _loggedNullSessionId;

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
        //
        // LogInformation (not Debug) on purpose: this branch represents a
        // config-vs-plumbing mismatch (EnrichOnRetrieve=true but transport
        // layer hasn't shipped SessionId yet — Phase B of PR #157). At
        // default log levels Debug is filtered out, hiding the only
        // breadcrumb explaining why retrieval is silently disabled. Rate-
        // limited via the once-per-process flag below so we don't flood
        // logs on a hot path.
        if (ctx.SessionId is null)
        {
            if (Interlocked.CompareExchange(ref _loggedNullSessionId, 1, 0) == 0)
            {
                logger.LogInformation(
                    "MemoryHook: Memory:EnrichOnRetrieve=true but ChatHookContext.SessionId " +
                    "is null. Retrieval is being skipped for safety until the transport layer " +
                    "(ChatbotHub for SignalR, controllers for HTTP) plumbs a session identifier " +
                    "into ChatHookContext.SessionId. See PR #157 Phase B. This message logs once " +
                    "per process; subsequent skips are silent.");
            }
            return Task.FromResult(HookResult.Continue);
        }

        var matches = memoryStore.Search(ctx.SessionId, ctx.CurrentMessage);

        // SC-001 defense in depth: filter out entries that originated from
        // MemoryMcpTools.MemoryWrite (LLM-callable). Even if the
        // Memory:AllowLlmGlobalWrite flag is enabled (intentionally or by
        // mistake), and even if a prompt-injected write landed before this
        // filter shipped, retrieval injection refuses to surface those
        // entries into a future session's prompt. The McpOriginTag is
        // applied automatically on every MemoryMcpTools.MemoryWrite that
        // gets through the flag gate. See
        // docs/solutions/security/2026-05-11-sc-001-mcp-write-injection.md.
        const string McpOriginTag = "origin:mcp-tool";
        var filtered = matches
            .Where(m => !m.Tags.Contains(McpOriginTag, StringComparer.Ordinal))
            .ToList();
        var filteredOutCount = matches.Count - filtered.Count;
        if (filteredOutCount > 0)
        {
            logger.LogInformation(
                "MemoryHook: filtered {Count} '{Tag}' entries from retrieval (SC-001 defense). " +
                "If this fires frequently, audit MemoryMcpTools.MemoryWrite usage and consider " +
                "setting Memory:AllowLlmGlobalWrite=false.",
                filteredOutCount, McpOriginTag);
        }
        if (filtered.Count == 0) return Task.FromResult(HookResult.Continue);

        var contextBlock = string.Join("\n", filtered.Take(3)
            .Select(m => $"[memory:{m.Key}] {m.Content}"));

        var enriched = $"[Relevant context from memory]\n{contextBlock}\n\n{ctx.CurrentMessage}";
        logger.LogDebug(
            "MemoryHook: injecting {Count} memory entries for session {SessionId}",
            filtered.Count, ctx.SessionId);
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
