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
    ChatTranscriptStore transcriptStore,
    IConfiguration configuration,
    ILogger<MemoryHook> logger) : IChatHook
{
    private readonly bool _enrichOnRetrieve =
        configuration.GetValue<bool?>("Memory:EnrichOnRetrieve") ?? false;

    // Rate-limit the "SessionId not plumbed" message — log once per
    // process, not per request. 0 = not yet logged; 1 = already logged.
    private int _loggedNullSessionId;

    // ── Audit counters (PR #174 follow-up: dropped-response visibility) ──
    // OnResponseSent silently skips writes when Confidence < 0.7 OR
    // Result.Length < 100. Without an audit channel operators can't
    // distinguish "MemoryHook is working but the chatbot rarely meets
    // threshold" from "MemoryHook is broken." These counters surface the
    // distribution via Information logs (once per reason on first hit, then
    // every <see cref="AuditSummaryInterval"/> drops) and Debug logs on
    // every drop. No behavior change — observability only.
    private long _droppedLowConfidence;
    private long _droppedShortContent;
    private int  _loggedFirstLowConfidence;
    private int  _loggedFirstShortContent;
    private const int AuditSummaryInterval = 100;

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
        // gets through the flag gate.
        //
        // Per PR #161 review F-1: reference Memory.MemoryMcpTools.McpOriginTag
        // (the source of truth) rather than a local literal — a rename
        // there must not silently regress this filter.
        var filtered = matches
            .Where(m => !m.Tags.Contains(Memory.MemoryMcpTools.McpOriginTag, StringComparer.Ordinal))
            .ToList();
        var filteredOutCount = matches.Count - filtered.Count;
        if (filteredOutCount > 0)
        {
            logger.LogInformation(
                "MemoryHook: filtered {Count} '{Tag}' entries from retrieval (SC-001 defense). " +
                "If this fires frequently, audit MemoryMcpTools.MemoryWrite usage and consider " +
                "setting Memory:AllowLlmGlobalWrite=false.",
                filteredOutCount, Memory.MemoryMcpTools.McpOriginTag);
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
        // PR #174 follow-up: each threshold drop records an audit entry so
        // operators can see why responses aren't reaching the transcript
        // store. The structure of the guard chain is preserved (still two
        // sequential early-returns) — only the side-effect is new.
        if (ctx.Response is not { Confidence: >= 0.7f } response)
        {
            // Distinguish "no response at all" (genuinely nothing to write)
            // from "response present but below confidence threshold" (a
            // real drop). Only the latter is audit-worthy.
            if (ctx.Response is not null)
                RecordDrop("low-confidence", $"confidence={ctx.Response.Confidence:F2}");
            return Task.FromResult(HookResult.Continue);
        }
        if (response.Result.Length < 100)
        {
            RecordDrop("short-content", $"length={response.Result.Length}");
            return Task.FromResult(HookResult.Continue);
        }

        // Per PR #161 review F-4 (preventative): refuse to write when
        // SessionId is null. ProductionOrchestrator currently always sets
        // it (req.SessionId ?? Guid.NewGuid().ToString("N")), so this
        // branch is dormant today — but a future orchestrator refactor
        // could pass null through. Without this guard, a null SessionId
        // would land an entry in the global partition WITHOUT the
        // origin:mcp-tool tag, defeating both layers of SC-001's defense.
        // Mirror of the OnRequestReceived safety belt at line 65.
        if (ctx.SessionId is null)
        {
            logger.LogWarning(
                "MemoryHook: refused to persist response — SessionId is null. " +
                "Writing here would land an entry in the global partition without the " +
                "origin:mcp-tool tag, defeating SC-001's two-layer defense. Check the " +
                "orchestrator's SessionId plumbing (PR #157 Phase B regression?).");
            return Task.FromResult(HookResult.Continue);
        }

        // Capture by value before fire-and-forget — ctx fields are not safe to
        // close over because the orchestrator may reuse / mutate the context
        // after this method returns.
        //
        // PR #174 review HIGH-1: use ctx.CurrentMessage (post-sanitization
        // via PromptSanitizationHook.HookResult.Mutate), NOT
        // ctx.OriginalMessage which is the raw user input. The role-
        // whitelist defense (Sec-M1 in PR #173) only blocks injection in
        // the Role field — content reaches the curator's prompt verbatim,
        // so writing raw user input was a second-order prompt-injection
        // vector. ctx.CurrentMessage falls back to OriginalMessage when no
        // sanitizing hook fired (e.g., in tests without the hook stack).
        var sessionId     = ctx.SessionId;
        var userMessage   = ctx.CurrentMessage ?? ctx.OriginalMessage ?? "";
        var correlationId = ctx.CorrelationId;
        var agentId       = response.AgentId;

        // PR #174 review HIGH-2 (Sec): symmetric truncation. The old code
        // capped the assistant snippet at 500 chars but let the user
        // message grow unbounded — a 1 MB pasted prompt × N turns would
        // produce a multi-GB transcripts.json that ChatTranscriptStore
        // re-serializes on every Save (O(n) per write). Cap both sides
        // at the same TurnContentCap so storage cost is bounded.
        var userSnippet      = userMessage[..Math.Min(TurnContentCap, userMessage.Length)];
        var assistantSnippet = response.Result[..Math.Min(TurnContentCap, response.Result.Length)];

        // PR #173 Phase 2 + PR #174 review: transient chat content goes to
        // ChatTranscriptStore, NOT MemoryStore. Two turns per response
        // (user, then assistant) give the curator's transcript input the
        // right shape. Sequence is assigned inside AppendTurn (PR #174
        // CR-H2) so user < assistant per Q+A is guaranteed regardless of
        // clock resolution.
        //
        // PR #174 review M-Cancellation: use CancellationToken.None so the
        // request pipeline's RequestAborted (fired when ASP.NET disposes
        // the scope after the response returns) cannot silently lose the
        // write. The work is detached anyway; tying it to the request CT
        // was a regression vector under high load / thread-pool starvation.
        _ = Task.Run(() =>
        {
            try
            {
                transcriptStore.AppendTurn(sessionId, "user",      userSnippet,      correlationId, agentId: null);
                transcriptStore.AppendTurn(sessionId, "assistant", assistantSnippet, correlationId, agentId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MemoryHook: failed to persist transcript turns");
            }
        }, CancellationToken.None);

        return Task.FromResult(HookResult.Continue);
    }

    /// <summary>
    /// Per-turn content cap (bytes-as-chars). Symmetric on both user and
    /// assistant turns to bound storage cost — PR #174 review HIGH-2.
    /// </summary>
    private const int TurnContentCap = 2000;

    /// <summary>
    /// Records that <see cref="OnResponseSent"/> dropped a response without
    /// writing it. Drives the audit-log channel: Information on first
    /// occurrence per reason (rate-limited via
    /// <see cref="Interlocked.CompareExchange(ref int, int, int)"/>), an
    /// Information summary every <see cref="AuditSummaryInterval"/> drops,
    /// and Debug on every individual drop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Counters are per-process (instance-scoped). They reset on restart
    /// — sufficient for "is this hook silently swallowing everything?"
    /// triage but not for long-running quality trend analysis. Exporting
    /// the counters to OpenTelemetry / Prometheus is a future iteration;
    /// for now the log channel is the operator-facing surface.
    /// </para>
    /// </remarks>
    private void RecordDrop(string reason, string detail)
    {
        long count;
        bool firstForReason;

        if (reason == "low-confidence")
        {
            count          = Interlocked.Increment(ref _droppedLowConfidence);
            firstForReason = Interlocked.CompareExchange(ref _loggedFirstLowConfidence, 1, 0) == 0;
        }
        else  // "short-content"
        {
            count          = Interlocked.Increment(ref _droppedShortContent);
            firstForReason = Interlocked.CompareExchange(ref _loggedFirstShortContent, 1, 0) == 0;
        }

        if (firstForReason)
        {
            logger.LogInformation(
                "MemoryHook audit: first response dropped for reason '{Reason}' ({Detail}). " +
                "Future drops for this reason will be summarized every {Interval} occurrences. " +
                "Per-drop detail is at Debug level. This first-hit message logs once per " +
                "reason per process — restart the process to see it again.",
                reason, detail, AuditSummaryInterval);
        }
        else if (count % AuditSummaryInterval == 0)
        {
            logger.LogInformation(
                "MemoryHook audit: {Reason} drops = {Count} since process start.",
                reason, count);
        }

        logger.LogDebug(
            "MemoryHook drop: reason={Reason} detail={Detail} count={Count}",
            reason, detail, count);
    }
}
