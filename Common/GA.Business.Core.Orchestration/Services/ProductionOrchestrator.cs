namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using System.Text;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Intents;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;

/// <summary>
/// Top-level orchestrator that unifies RAG, tab analysis, path optimization,
/// and semantic routing into a single production-grade chat entry point.
/// </summary>
// @ai:business-value top-level chat orchestrator — fan-out point for tab analysis, voicings, suggestions, modulation; behaviour change here ripples to every chatbot answer [T:manually-reviewed conf:0.9 src:product-owner@2026-05-24]
public class ProductionOrchestrator(
    TabAwareOrchestrator tabOrchestrator,
    SemanticRouter router,
    QueryUnderstandingService queryUnderstandingService,
    IAlgebraPromptClassifier algebraPromptClassifier,
    IIxAlgebraService ixAlgebraService,
    IEnumerable<IChatHook> chatHooks,
    ConversationHistoryStore historyStore,
    RoutingContextEnricher routingContextEnricher,
    SemanticIntentRouter intentRouter,
    TabAnalysisOrchestrationService tabAnalysisService,
    TabTokenizer tabTokenizer,
    IAgenticTraceCapture traceCapture,
    IServiceProvider services) : IHarmonicChatOrchestrator
{
    private readonly IReadOnlyList<IChatHook> _hooks = chatHooks.ToList();

    /// <summary>
    /// Cheap pre-check: does the message actually contain tab notation?
    /// The LLM-driven filter extractor (<see cref="QueryUnderstandingService"/>)
    /// occasionally mis-classifies plain music-theory prose as
    /// <c>Intent=AnalyzeTab</c>; without this guard we dispatch to
    /// <see cref="TabAnalysisOrchestrationService.AnalyzeTabAsync"/>, which
    /// then returns the useless "I detected tab but couldn't parse any
    /// chords" fallback. Run the same tokenizer the canonical
    /// <see cref="TabAwareOrchestrator"/> uses so the behaviour stays
    /// consistent across both routing paths.
    /// </summary>
    /// <remarks>
    /// Known limitation pinned by <c>TabTokenizerTests.Tokenize_BareDigitProseInputs_KnownLimitation</c>:
    /// pitch-class-set notation ("0146"), "12-bar blues", and hyphenated
    /// dates trip the bare-digit path in TabLineRegex. Those still
    /// false-positive here. Tightening the regex without breaking
    /// anonymous-row tab notation is a separate, larger change.
    /// </remarks>
    private bool HasTabContent(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var blocks = tabTokenizer.Tokenize(message);
        return blocks.Any(b => b.Slices.Any(s => s.Notes.Any()));
    }

    // A tab-handling intent (tab.optimize / tab.analyze) can only act on a tab in the
    // message — without one it just replies "paste a tab". The embedding router has no
    // tab-content signal, so a tabless phrasing whose words overlap a tab example (e.g.
    // "give me a ii-V-I progression in Bb" matching tab.optimize's example "make this
    // progression smoother to play") can wrongly win a tab intent. Suppress that pick so
    // the query falls through to a real handler instead of "I need a tab in the message".
    private bool IsTabIntentWithoutTab(string intentId, string? message) =>
        intentId.StartsWith("tab.", StringComparison.Ordinal) && !HasTabContent(message);

    // Routing-context enrichment lives in RoutingContextEnricher (2026-05-14
    // extraction) so it can be unit-tested without spinning up the full
    // orchestrator. Behaviour is unchanged from the inlined form.

    /// <summary>
    /// Emit a routing.candidates trace step with the top-3 intents the
    /// router considered, so the agentic trace shows the routing decision
    /// instead of hiding it inside the "orchestration.answer" black-box
    /// step. Surfaces base cosine score, boost (if any), and final score
    /// for each candidate — enough to debug routing misses without
    /// re-running the request.
    /// </summary>
    /// <remarks>
    /// Added 2026-05-13 in response to user critique on demos.guitaralchemist.com/chatbot/:
    /// "Agentic trace is not detailed at all, we seem to see only the end
    /// result not each intermediate steps". When intentMatch is null (no
    /// intent crossed threshold) we still emit the step so the trace shows
    /// WHY we fell through to the LLM agent path.
    /// </remarks>
    private void EmitRoutingTrace(IntentMatch? match)
    {
        if (match is { Ranking: { Count: > 0 } ranking })
        {
            var attrs = new Dictionary<string, object?>
            {
                ["routing.outcome"]      = "matched",
                ["routing.selected_id"]  = match.Value.Intent.Id,
                ["routing.confidence"]   = match.Value.Confidence,
                ["routing.matched_with"] = match.Value.MatchedExample,
                ["routing.top_count"]    = ranking.Count,
            };
            for (var i = 0; i < ranking.Count; i++)
            {
                var c = ranking[i];
                attrs[$"routing.top{i + 1}.id"]       = c.IntentId;
                attrs[$"routing.top{i + 1}.base"]     = c.BaseScore;
                attrs[$"routing.top{i + 1}.boost"]    = c.Boost;
                attrs[$"routing.top{i + 1}.final"]    = c.FinalScore;
                attrs[$"routing.top{i + 1}.matched"]  = c.MatchedSource;
            }
            traceCapture.AddStep("routing.candidates", "completed", 0, attrs);
            return;
        }

        // No match — fall-through to LLM path. Still emit the step so the
        // trace shows we DID try semantic routing and chose to give up.
        traceCapture.AddStep("routing.candidates", "completed", 0,
            new Dictionary<string, object?>
            {
                ["routing.outcome"] = "below_threshold_or_unavailable",
                ["routing.note"]    = "no intent crossed MinConfidence; falling through to LLM agent path",
            });
    }

    private void EmitDeclinedTrace(string intentId) =>
        traceCapture.AddStep("routing.declined", "completed", 0,
            new Dictionary<string, object?>
            {
                ["routing.declined_id"] = intentId,
                ["routing.note"]        = "intent found no input it handles; falling through to LLM agent path",
            });
    private static readonly string[] ExplicitVoicingKeywords =
    [
        "voicing",
        "voicings",
        "chord shape",
        "chord shapes",
        "fingering",
        "fingerings",
        "drop 2",
        "drop2",
        "drop 3",
        "drop3",
        "rootless",
        "shell",
        "grip",
        "fretting"
    ];

    /// <summary>
    /// Streams the answer through <paramref name="onToken"/>, then returns the full
    /// <see cref="ChatResponse"/> with routing and filter metadata. Runs the same routing
    /// cascade as <see cref="AnswerAsync"/>: the LLM agent branch streams its tokens as they
    /// arrive, every other branch emits its finished answer word by word.
    /// </summary>
    /// <param name="req">The chat request.</param>
    /// <param name="onToken">Callback invoked for each token of the answer.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete <see cref="ChatResponse"/> once the stream is finished.</returns>
    public Task<ChatResponse> AnswerStreamingAsync(
        ChatRequest req,
        Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(onToken);
        return RunCascadeAsync(req, onToken, ct);
    }

    /// <remarks>
    /// <c>virtual</c> so a host adapter test can substitute a stub orchestrator
    /// and assert on the <see cref="ChatRequest"/> it actually receives. Without
    /// that seam the session-identity invariant on
    /// <c>GaChatbot.Api.Services.ProductionChatOrchestratorClient.AnswerAsync</c>
    /// is only pinned on a helper nothing forces that method to call — a gap an
    /// independent review closed by re-inlining the original defect with the
    /// whole suite still green. See <c>ProductionChatOrchestratorClientTests</c>.
    /// </remarks>
    public virtual Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default) =>
        RunCascadeAsync(req, onToken: null, ct);

    /// <summary>
    /// One request's pass through the cascade: the hook-processed message, the identifiers
    /// every hook context carries, the agent history, and how the answer is emitted
    /// (<see cref="OnToken"/> is null on the non-streaming path).
    /// </summary>
    private sealed record CascadeTurn(
        ChatRequest Request,
        string Message,
        string SessionId,
        Guid CorrelationId,
        IReadOnlyList<ChatHistoryTurn> AgentHistory,
        Func<string, Task>? OnToken,
        Activity? Activity,
        Stopwatch Stopwatch);

    /// <summary>
    /// The single routing cascade behind both entry points. Order: request hooks →
    /// deterministic voicing guard → algebra fast path → semantic intent dispatch (with the
    /// skill hook lifecycle) → invalid-chord decline → LLM agent router (tab analysis, agent,
    /// or RAG). Each branch ends in <see cref="DeliverAsync"/>, so every answer is emitted,
    /// recorded in the session history, and passed to the response hooks the same way.
    /// </summary>
    private async Task<ChatResponse> RunCascadeAsync(
        ChatRequest req,
        Func<string, Task>? onToken,
        CancellationToken ct)
    {
        using var activity = ChatbotActivitySource.StartActivity(ChatbotActivitySource.OrchestratorAnswer, req.Message);
        var sw = Stopwatch.StartNew();

        // ── Session management ───────────────────────────────────────────────
        // Agents get the caller's turns, else the session's stored turns, read
        // before the current message is added.
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        var agentHistory = (req.History ?? historyStore.GetHistory(sessionId))
            .Select(t => new ChatHistoryTurn(t.Role, t.Content))
            .ToList();
        historyStore.AddTurn(sessionId, "user", req.Message);

        // One correlation ID for all ChatHookContext instances in this request chain.
        // Hooks (especially ObservabilityHook) key per-request state by this ID to avoid
        // collisions when the same skill runs concurrently for different requests.
        var correlationId = Guid.NewGuid();

        // ── OnRequestReceived hooks (sanitization, rate-limiting, auth) ───────
        var hookCtx = new ChatHookContext
        {
            OriginalMessage = req.Message,
            CurrentMessage  = req.Message,
            CorrelationId   = correlationId,
            SessionId       = sessionId,   // PR #157 Phase B — plumb session
            Services        = services,
        };

        foreach (var hook in _hooks)
        {
            var hookResult = await hook.OnRequestReceived(hookCtx, ct);
            if (hookResult.MutatedMessage is not null)
                hookCtx.CurrentMessage = hookResult.MutatedMessage;
            if (!hookResult.Cancel) continue;

            sw.Stop();
            var blocked = new ChatResponse(
                NaturalLanguageAnswer: hookResult.BlockedResponse?.Result ?? "Request blocked.",
                Candidates: [],
                Routing: new AgentRoutingMetadata("hook", 1f, "hook-blocked"));
            await EmitWordsAsync(onToken, blocked.NaturalLanguageAnswer);
            return blocked;
        }

        var turn = new CascadeTurn(
            req, hookCtx.CurrentMessage, sessionId, correlationId, agentHistory, onToken, activity, sw);

        // ── Deterministic voicing guard runs BEFORE semantic intent routing ──
        // The semantic embedding router can mis-rank an explicit voicing query
        // (e.g. "Show me Drop 2 voicings of Cmaj7") against the modes intent at
        // ~0.71 cosine similarity, stealing it from VoicingAgent. The guard
        // matches on high-precision surface tokens (chord literal + voicing
        // keyword), so when it fires there is no ambiguity worth routing
        // semantically. Diagnosed 2026-05-07 by codex CLI second-opinion;
        // roadmap P1 #6 (#81). The guard is a pure function of the message, so
        // it is not retried after semantic dispatch.
        if (TrySelectDeterministicAgent(turn.Message, out var deterministicAgent, out var deterministicRouting))
        {
            return await DispatchDeterministicAgentAsync(turn, deterministicAgent, deterministicRouting, ct);
        }

        // ── Deterministic algebra fast-path runs BEFORE semantic intent routing ──
        // Algebra prompts (prime form, ICV, Forte label, Z-relation, set class) are
        // pure finite math handled by IxAlgebraService — no LLM, no embeddings. The
        // unified semantic dispatch otherwise routes algebra via embeddings, which
        // means an environment without an embedding endpoint (CI runners without
        // Ollama) fails through to the LLM agent path and 500s when the LLM is
        // ALSO unreachable. The classifier is high-precision: keyword hit OR
        // bracketed/compact pitch-class set pattern. When it fires we already know
        // the prompt is algebra-shaped, so there is no value in paying the
        // embedding round-trip. Mirrors the deterministic voicing guard above.
        if (await TryAnswerWithAlgebraFastPathAsync(turn, ct) is { } algebraResponse)
        {
            return algebraResponse;
        }

        // ── Follow-up context enrichment for the routing pass ────────────────
        // A short prompt like "Show me a practical example on guitar" right
        // after a substantive turn ("How do I make this progression sound
        // darker?") used to mis-embed as a standalone request and route to
        // skill.practiceroutine (because "practice" / "practical" share a
        // centroid). For follow-up-shaped messages, prepend the most recent
        // prior user turn to the routing query so the embedding represents
        // the conversation thread, not just the snippet. The downstream
        // message stays clean (the LLM gets the real message plus full
        // history separately).
        //
        // The history lookup checks BOTH (a) the in-memory historyStore
        // (session-scoped, populated by the just-added user turn plus any
        // earlier turns from this session) AND (b) req.History (the
        // controller may forward client-supplied prior turns even when
        // sessionId is null — the React frontend posts conversationHistory
        // per request without a stable sessionId). Without (b), the very
        // first follow-up after a page reload would miss enrichment.
        //
        // Live found 2026-05-14 via demos.guitaralchemist.com/chatbot — see
        // task #168.
        var routingMessage = routingContextEnricher.EnrichIfFollowUp(turn.Message, sessionId, req.History);

        // ── Unified semantic intent dispatch with the hook lifecycle ─────────
        // One embedding similarity pass over IIntent registrations covers
        // algebra, deterministic skills, and tab-handling intents. See
        // docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
        // §"Routing classifiers".
        if (await TryDispatchViaIntentAsync(turn, routingMessage, ct) is { } intentResponse)
        {
            return intentResponse;
        }

        // Invalid chord names only (ga#745): decline before the LLM agent path.
        if (DeclineInvalidChordNames(turn.Message) is { } invalidChordResp)
        {
            activity?.SetTag("orchestration.branch", InvalidChordNames.RoutingMethod);
            return await DeliverAsync(
                turn,
                invalidChordResp,
                new AgentResponse
                {
                    AgentId    = invalidChordResp.Routing!.AgentId,
                    Result     = invalidChordResp.NaturalLanguageAnswer,
                    Confidence = InvalidChordNames.DeclineConfidence,
                },
                matchedSkillName: null,
                ct);
        }

        return await AnswerWithAgentRouterAsync(turn, ct);
    }

    /// <summary>
    /// Semantic intent dispatch with the skill hook lifecycle: <c>OnBeforeSkill</c> (which
    /// may veto), the intent, then <c>OnAfterSkill</c>. Returns null when no intent scored
    /// above threshold, when a tab intent was picked for a message without a tab, or when
    /// the intent declined — the caller then continues to the LLM agent path.
    /// </summary>
    /// <param name="routingMessage">Message used ONLY for the embedding-router
    /// score. May be enriched with prior conversation context. The intent's
    /// <c>ExecuteAsync</c> receives <see cref="CascadeTurn.Message"/> so its
    /// internal regex parsers see the raw text without the enrichment prefix.</param>
    private async Task<ChatResponse?> TryDispatchViaIntentAsync(
        CascadeTurn turn,
        string routingMessage,
        CancellationToken ct)
    {
        var match = await intentRouter.RouteAsync(routingMessage, services, ct);
        EmitRoutingTrace(match);
        if (match is not { } pick) return null;

        // A tab intent picked for a tabless message (e.g. "give me a ii-V-I progression
        // in Bb") cannot act — fall through to a real handler.
        if (IsTabIntentWithoutTab(pick.Intent.Id, turn.Message)) return null;

        // OnBeforeSkill hooks (intent.Id replaces the legacy MatchedSkillName).
        var beforeCtx = NewHookContext(turn, pick.Intent.Id, response: null);
        foreach (var hook in _hooks)
        {
            var r = await hook.OnBeforeSkill(beforeCtx, ct);
            if (!r.Cancel) continue;

            turn.Stopwatch.Stop();
            var blocked = new ChatResponse(
                NaturalLanguageAnswer: r.BlockedResponse?.Result ?? "Intent blocked.",
                Candidates: [],
                Routing: new AgentRoutingMetadata($"hook.{pick.Intent.Id}", 1f, "hook-blocked"));
            await EmitWordsAsync(turn.OnToken, blocked.NaturalLanguageAnswer);
            return blocked;
        }

        var intentResult = await pick.Intent.ExecuteAsync(turn.Message, ct);

        // Map IntentResult back to AgentResponse for hook compatibility.
        // PR #185 (2026-05-12): forward Data so structured payloads
        // (e.g. RememberThisSkill's MemoryWriteRequest) reach
        // OnResponseSent hooks. Without this line — and the matching
        // Data field on IntentResult + the forward in
        // OrchestratorSkillIntent — durable-memory writes from the
        // semantic-routing path silently fail because MemoryWriteHook
        // pattern-matches on ctx.Response?.Data.
        var skillRespForHooks = new AgentResponse
        {
            AgentId     = pick.Intent.Id,
            Result      = intentResult.Answer,
            Confidence  = intentResult.Confidence,
            Evidence    = intentResult.Evidence ?? [],
            Assumptions = [],
            Data        = intentResult.Data,
        };

        // OnAfterSkill pairs every OnBeforeSkill, a declined intent included, so hooks that
        // open per-request state on OnBeforeSkill (ObservabilityHook's span) close it.
        var afterCtx = NewHookContext(turn, pick.Intent.Id, skillRespForHooks);
        foreach (var hook in _hooks)
            await hook.OnAfterSkill(afterCtx, ct);

        // A declined intent saw no input it handles; continue to the agent path with history.
        if (intentResult.Declined)
        {
            EmitDeclinedTrace(pick.Intent.Id);
            return null;
        }

        turn.Activity?.SetTag("orchestration.branch", pick.Intent.Id);

        var chatResp = new ChatResponse(
            NaturalLanguageAnswer: intentResult.Answer,
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                pick.Intent.Id,
                Math.Min(intentResult.Confidence, pick.Confidence),
                intentResult.RoutingMethodOverride ?? "semantic-intent"),
            Grounding: BuildGrounding(intentResult));

        return await DeliverAsync(turn, chatResp, skillRespForHooks, pick.Intent.Id, ct);
    }

    /// <summary>
    /// The last stage of the cascade: the LLM agent router picks an agent while the filter
    /// extractor reads the message, then the request goes to tab analysis, the selected
    /// agent, or RAG. On the streaming path the agent streams its tokens as they arrive.
    /// </summary>
    private async Task<ChatResponse> AnswerWithAgentRouterAsync(CascadeTurn turn, CancellationToken ct)
    {
        var req = turn.Request;

        // Parallelise — both calls consume req.Message with no mutual dependency
        var filtersTask = queryUnderstandingService.ExtractFiltersAsync(req.Message, ct);
        var routingTask = router.RouteAsync(req.Message, ct);
        await Task.WhenAll(filtersTask, routingTask);
        var filters = filtersTask.Result;
        var routing = routingTask.Result;

        var routingMetadata = new AgentRoutingMetadata(
            routing.SelectedAgent.AgentId,
            routing.Confidence,
            routing.RoutingMethod);

        turn.Activity?.SetTag(ChatbotActivitySource.TagAgentId, routing.SelectedAgent.AgentId);
        turn.Activity?.SetTag(ChatbotActivitySource.TagAgentName, routing.SelectedAgent.Name);
        turn.Activity?.SetTag(ChatbotActivitySource.TagRoutingMethod, routing.RoutingMethod);
        turn.Activity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, routing.Confidence);

        ChatResponse result;
        var streamed = false;
        // LLM-extracted filter intent drives tab branch — no string keyword check.
        // (The semantic intent router upstream handles "make this smoother" /
        // "analyse this tab" via TabOptimizeIntent / TabAnalyzeIntent before we
        // reach this fallback, so this code only fires when the filter extractor
        // tagged the intent OR the agent router routed to AgentIds.Tab.)
        // Guarded by HasTabContent: the filter extractor sometimes mis-tags
        // theory prose ("Explain voice leading in jazz") as AnalyzeTab. Never
        // enter the tab branch when there is literally no tab notation in the
        // message — falls through to the agent path.
        var hasTabContent      = HasTabContent(req.Message);
        var shouldOptimizePath = filters?.Intent == "OptimizePath" && hasTabContent;
        var shouldAnalyzeTab   = (routing.SelectedAgent.AgentId == AgentIds.Tab ||
                                  filters?.Intent == "AnalyzeTab")
                                 && hasTabContent;

        if (shouldOptimizePath)
        {
            turn.Activity?.SetTag("orchestration.branch", "tab.path_optimization");
            var optimized = await tabAnalysisService.OptimizePathAsync(req.Message, ct);
            result = optimized with { Routing = routingMetadata, QueryFilters = filters };
        }
        else if (shouldAnalyzeTab)
        {
            turn.Activity?.SetTag("orchestration.branch", "tab.analysis");
            var analyzed = await tabAnalysisService.AnalyzeTabAsync(req.Message, ct);
            result = analyzed with { Routing = routingMetadata, QueryFilters = filters };
        }
        else if (routing.SelectedAgent is GuitarAlchemistAgentBase selectedAgent)
        {
            turn.Activity?.SetTag("orchestration.branch", $"agent.{selectedAgent.AgentId}");

            string answer;
            if (turn.OnToken is { } onToken)
            {
                var fullText = new StringBuilder();
                await foreach (var token in selectedAgent.ProcessStreamingAsync(
                    turn.Message, conversationHistory: turn.AgentHistory, cancellationToken: ct))
                {
                    await onToken(token);
                    fullText.Append(token);
                }

                answer = fullText.ToString();
                streamed = true;
            }
            else
            {
                var agentResponse = await selectedAgent.ProcessAsync(new AgentRequest
                {
                    Query = turn.Message,
                    ConversationHistory = turn.AgentHistory,
                }, ct);
                answer = agentResponse.Result;
            }

            result = new ChatResponse(
                NaturalLanguageAnswer: answer,
                Candidates: [],
                Routing: routingMetadata,
                QueryFilters: filters,
                DebugParams: new { Mode = "Agent", Agent = selectedAgent.AgentId });
        }
        else
        {
            turn.Activity?.SetTag("orchestration.branch", "rag");
            var response = await tabOrchestrator.AnswerAsync(req, ct);
            result = response with { Routing = routingMetadata, QueryFilters = filters };
        }

        return await DeliverAsync(
            turn,
            result,
            new AgentResponse
            {
                AgentId    = routing.SelectedAgent.AgentId,
                Result     = result.NaturalLanguageAnswer ?? string.Empty,
                Confidence = routing.Confidence,
            },
            matchedSkillName: null,
            ct,
            alreadyEmitted: streamed);
    }

    /// <summary>
    /// Shared dispatch path for deterministic agents (e.g. VoicingAgent for
    /// explicit voicing requests). The cascade calls it before semantic intent
    /// routing, so high-precision voicing prompts never reach
    /// SemanticIntentRouter, which can mis-rank them.
    /// </summary>
    private async Task<ChatResponse> DispatchDeterministicAgentAsync(
        CascadeTurn turn,
        GuitarAlchemistAgentBase agent,
        AgentRoutingMetadata routing,
        CancellationToken ct)
    {
        turn.Activity?.SetTag("orchestration.branch", $"deterministic.{agent.AgentId}");

        var agentResponse = await agent.ProcessAsync(new AgentRequest
        {
            Query = turn.Message,
            ConversationHistory = turn.AgentHistory,
        }, ct);

        var deterministicResponse = new ChatResponse(
            NaturalLanguageAnswer: agentResponse.Result,
            Candidates: [],
            Routing: routing with { Confidence = Math.Max(routing.Confidence, agentResponse.Confidence) },
            DebugParams: new { Mode = "DeterministicAgent", Agent = agent.AgentId });

        return await DeliverAsync(turn, deterministicResponse, agentResponse, matchedSkillName: null, ct);
    }

    /// <summary>
    /// Deterministic algebra fast-path. Detects set-class-algebra prompts via
    /// <see cref="IAlgebraPromptClassifier"/> (regex over keywords + bracketed/
    /// compact pitch-class sets) and dispatches to <see cref="IIxAlgebraService"/>
    /// directly. Bypasses <see cref="SemanticIntentRouter"/> so the dispatch
    /// works in environments without an embedding endpoint (CI without Ollama,
    /// offline dev). Returns <c>null</c> when the classifier rejects the prompt
    /// or when the service can't extract a usable pitch-class set — caller falls
    /// through to the unified semantic-intent dispatch as before.
    /// </summary>
    /// <remarks>
    /// Why this isn't the only algebra path: the semantic router's example-prompt
    /// matching still beats the classifier on edge cases where the user phrases an
    /// algebra question without keyword hits ("are these two collections related?").
    /// The fast-path is a STRICTLY HIGH-PRECISION subset — if it fires we short-
    /// circuit; if it doesn't, semantic dispatch still owns the prompt. Mirrors
    /// the deterministic voicing guard's relationship to VoicingIntent.
    ///
    /// The routing method <c>"ix-algebra"</c> matches <see cref="AlgebraIntent.ExecuteAsync"/>
    /// so downstream consumers (FallbackChatApplicationService deterministic-failure
    /// protection, trace dashboards) can't tell whether the fast-path or the
    /// semantic-routed AlgebraIntent answered.
    /// </remarks>
    private async Task<ChatResponse?> TryAnswerWithAlgebraFastPathAsync(CascadeTurn turn, CancellationToken ct)
    {
        if (!algebraPromptClassifier.IsAlgebraPrompt(turn.Message))
        {
            return null;
        }

        var answer = await ixAlgebraService.TryAnswerAsync(turn.Message, ct);
        if (answer is null)
        {
            // Classifier said algebra-shaped but service couldn't extract a set.
            // Fall through to semantic dispatch so AlgebraIntent can emit its
            // "I couldn't extract a pitch-class set" guidance with proper trace
            // attribution. Returning the same message inline would short-circuit
            // observability for the failure mode.
            return null;
        }

        turn.Activity?.SetTag("orchestration.branch", "algebra.fast_path");

        var algebraResponse = new ChatResponse(
            NaturalLanguageAnswer: answer.NaturalLanguageAnswer,
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                AgentId: "algebra",
                Confidence: 1.0f,
                RoutingMethod: "ix-algebra"),
            Grounding: answer.Grounding);

        // Wrap the facts dictionary as evidence strings so MemoryHook / analytics
        // see a shape consistent with skill responses.
        return await DeliverAsync(
            turn,
            algebraResponse,
            new AgentResponse
            {
                AgentId     = "algebra",
                Result      = answer.NaturalLanguageAnswer,
                Confidence  = 1.0f,
                Evidence    = answer.Facts.Select(kv => $"{kv.Key}: {kv.Value}").ToList(),
                Assumptions = [],
            },
            matchedSkillName: null,
            ct);
    }

    /// <summary>
    /// Ends every answering branch of the cascade: emits the answer (unless the branch
    /// already streamed it), records it in the session history, then runs the
    /// <c>OnResponseSent</c> hooks (memory writing, analytics). The answer enters the
    /// history only after it was emitted, so a stream the client never received is not
    /// recorded.
    /// </summary>
    private async Task<ChatResponse> DeliverAsync(
        CascadeTurn turn,
        ChatResponse response,
        AgentResponse hookResponse,
        string? matchedSkillName,
        CancellationToken ct,
        bool alreadyEmitted = false)
    {
        turn.Stopwatch.Stop();
        turn.Activity?.SetTag("orchestration.elapsed_ms", turn.Stopwatch.ElapsedMilliseconds);

        if (!alreadyEmitted)
            await EmitWordsAsync(turn.OnToken, response.NaturalLanguageAnswer);

        historyStore.AddTurn(turn.SessionId, "assistant", response.NaturalLanguageAnswer);

        var sentCtx = NewHookContext(turn, matchedSkillName, hookResponse);
        foreach (var hook in _hooks)
            await hook.OnResponseSent(sentCtx, ct);

        return response;
    }

    /// <summary>
    /// Emits a finished answer word by word on the streaming path; a no-op on the
    /// non-streaming path.
    /// </summary>
    private static async Task EmitWordsAsync(Func<string, Task>? onToken, string text)
    {
        if (onToken is null) return;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            await onToken(word + " ");
    }

    /// <summary>
    /// A hook context for a lifecycle point after <c>OnRequestReceived</c>. Every context
    /// carries the request's correlation and session ids (PR #157 Phase B: the session id is
    /// load-bearing for MemoryHook's session scope).
    /// </summary>
    private ChatHookContext NewHookContext(CascadeTurn turn, string? matchedSkillName, AgentResponse? response) =>
        new ChatHookContext
        {
            OriginalMessage  = turn.Request.Message,
            CurrentMessage   = turn.Message,
            MatchedSkillName = matchedSkillName,
            Response         = response,
            CorrelationId    = turn.CorrelationId,
            SessionId        = turn.SessionId,
            Services         = services,
        };

    /// <summary>
    /// Declines an improvisation request that names only invalid chords ("which
    /// arpeggio fits Hm Q7", ga#745). Such a request scores below every intent, and
    /// the LLM agent path, or the chat fallback behind it, then invents theory about
    /// the tokens. The routing confidence stays above the fallback threshold so the
    /// decline is the answer. Returns null when the message is not such a request.
    /// The caller records history and runs hooks, each at its path's usual point.
    /// </summary>
    private static ChatResponse? DeclineInvalidChordNames(string message)
    {
        var invalid = InvalidChordNames.Find(message);
        if (invalid.Count == 0) return null;

        return new ChatResponse(
            NaturalLanguageAnswer: InvalidChordNames.Decline(invalid),
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                "skill.improvisation",
                InvalidChordNames.DeclineConfidence,
                InvalidChordNames.RoutingMethod),
            DebugParams: new { Mode = "InvalidChordNames", Tokens = invalid });
    }

    private bool TrySelectDeterministicAgent(
        string message,
        out GuitarAlchemistAgentBase agent,
        out AgentRoutingMetadata routing)
    {
        // Remember-this requests take precedence over the voicing fast-path.
        // Otherwise a prompt like "remember that I prefer drop-2 voicings for
        // jazz comping" matches IsExplicitVoicingRequest (because "drop-2
        // voicings" is in the keyword list) and short-circuits to VoicingAgent,
        // returning voicings instead of writing to MemoryStore. Surfaced by
        // the live-orchestrator e2e test (PR after #190 harness).
        //
        // The remember-phrasing is high-precision — RememberThisParser
        // requires an explicit "remember"/"save"/"note"/"don't forget" lead
        // phrase. Any prompt that passes that gate is unambiguously a
        // memory-write request whose voicing-keyword content is incidental.
        if (GA.Business.ML.Agents.Skills.RememberThisParser.LooksLikeRememberRequest(message))
        {
            agent = null!;
            routing = null!;
            return false;
        }

        // Invalid chord names (ga#745) are left to DeclineInvalidChordNames: the voicing
        // agent's query extractor falls back to the LLM on chord symbols it cannot parse.
        if (InvalidChordNames.Find(message).Count > 0)
        {
            agent = null!;
            routing = null!;
            return false;
        }

        if (IsExplicitVoicingRequest(message)
            && router.Agents.FirstOrDefault(a => a.AgentId == AgentIds.Voicing) is { } voicingAgent)
        {
            agent = voicingAgent;
            routing = new AgentRoutingMetadata(AgentIds.Voicing, 0.92f, "deterministic-voicing");
            return true;
        }

        agent = null!;
        routing = null!;
        return false;
    }

    // A chord name followed by "shape" ("a G shape", "the A shape") names what the hand frets, as
    // capo and CAGED questions do, not a request for voicings. The guard used to match it and sent
    // "with a capo on fret 3, what key does a G shape sound in?" to the voicing agent (issue #888);
    // such questions now go to the semantic intent router. The chord-name form of the remaining
    // nouns ("Cmaj7 voicing", "Am fingering") is covered by the keywords.
    private static bool IsExplicitVoicingRequest(string message)
    {
        var lower = message.ToLowerInvariant();
        return ExplicitVoicingKeywords.Any(lower.Contains);
    }

    /// <summary>
    /// Maps the ML-layer <see cref="IntentGroundingEvidence"/> to the
    /// Orchestration-layer <see cref="GroundingMetadata"/> the public chat
    /// surface expects. Returns null when the intent didn't carry grounding
    /// data — only deterministic-compute intents (e.g. <c>algebra</c>,
    /// Path B SKILL.md skills) populate it.
    /// </summary>
    /// <remarks>
    /// The split exists to keep the ML-layer <see cref="IIntent"/> contract
    /// from depending on <see cref="GroundingMetadata"/>, which lives in the
    /// Orchestration layer. The orchestrator is the natural place to bridge
    /// the two representations.
    /// </remarks>
    private static GroundingMetadata? BuildGrounding(IntentResult result) =>
        result.Grounding is null
            ? null
            : new GroundingMetadata(
                result.Grounding.Source,
                result.Grounding.Revision,
                result.Grounding.QueryType,
                result.Grounding.Facts);
}
