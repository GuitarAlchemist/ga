namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Intents;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;

/// <summary>
/// Top-level orchestrator that unifies RAG, tab analysis, path optimization,
/// and semantic routing into a single production-grade chat entry point.
/// </summary>
public class ProductionOrchestrator(
    TabAwareOrchestrator tabOrchestrator,
    TabAnalysisService tabAnalyzer,
    NextChordSuggestionService suggestionService,
    ModulationAnalyzer modulationAnalyzer,
    TabPresentationService presenter,
    MusicalEmbeddingGenerator embeddingGenerator,
    AdvancedTabSolver tabSolver,
    AlternativeFingeringService altService,
    SemanticRouter router,
    QueryUnderstandingService queryUnderstandingService,
    IAlgebraPromptClassifier algebraPromptClassifier,
    IIxAlgebraService ixAlgebraService,
    IEnumerable<IOrchestratorSkill> orchestratorSkills,
    IEnumerable<IChatHook> chatHooks,
    ConversationHistoryStore historyStore,
    RoutingContextEnricher routingContextEnricher,
    SemanticIntentRouter intentRouter,
    TabAnalysisOrchestrationService tabAnalysisService,
    TabTokenizer tabTokenizer,
    IAgenticTraceCapture traceCapture,
    IServiceProvider services) : IHarmonicChatOrchestrator
{
    private readonly IReadOnlyList<IOrchestratorSkill> _skills = orchestratorSkills.ToList();
    private readonly IReadOnlyList<IChatHook>          _hooks  = chatHooks.ToList();

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
    /// Streams the LLM response token-by-token, calling <paramref name="onToken"/> for each token,
    /// then returns the full <see cref="ChatResponse"/> with routing and filter metadata when done.
    /// </summary>
    /// <param name="req">The chat request.</param>
    /// <param name="onToken">Callback invoked for each token as it arrives from the LLM.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete <see cref="ChatResponse"/> once the stream is finished.</returns>
    public async Task<ChatResponse> AnswerStreamingAsync(
        ChatRequest req,
        Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        // ── Session management ───────────────────────────────────────────────
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
        var history = req.History ?? historyStore.GetHistory(sessionId);
        var agentHistory = history
            .Select(t => new ChatHistoryTurn(t.Role, t.Content))
            .ToList();
        historyStore.AddTurn(sessionId, "user", req.Message);

        // ── OnRequestReceived hooks (sanitization, rate-limiting, auth) ───────
        var correlationId = Guid.NewGuid();
        var hookCtx = new ChatHookContext
        {
            CorrelationId   = correlationId,
            SessionId       = sessionId,   // PR #157 Phase B — plumb session
            OriginalMessage = req.Message,
            CurrentMessage  = req.Message,
            Services        = services,
        };

        foreach (var hook in _hooks)
        {
            var hookResult = await hook.OnRequestReceived(hookCtx, ct);
            if (hookResult.MutatedMessage is not null)
                hookCtx.CurrentMessage = hookResult.MutatedMessage;
            if (!hookResult.Cancel) continue;

            return new ChatResponse(
                NaturalLanguageAnswer: hookResult.BlockedResponse?.Result ?? "Request blocked.",
                Candidates: [],
                Routing: new AgentRoutingMetadata("hook", 1f, "hook-blocked"));
        }

        var message = hookCtx.CurrentMessage;

        // ── Follow-up context enrichment for the routing pass ────────────────
        // Same enrichment as the non-streaming AnswerAsync path (see task #168).
        // The streaming path is the one the React demo uses, so without this
        // line the headline "Show me a practical example" follow-up fix would
        // not actually fire on the live surface — caught by code-review 2026-05-14.
        var routingMessage = routingContextEnricher.EnrichIfFollowUp(message, sessionId, req.History);

        // ── Unified semantic intent dispatch (replaces the legacy algebra check
        //    and the per-skill CanHandle foreach). One embedding similarity pass
        //    over IIntent registrations covers algebra, deterministic skills,
        //    and tab-handling intents. See
        //    docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
        //    §"Routing classifiers".
        var semanticResp = await TryDispatchViaIntentAsync(routingMessage, message, ct);
        if (semanticResp is not null)
        {
            foreach (var word in semanticResp.NaturalLanguageAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                await onToken(word + " ");

            historyStore.AddTurn(sessionId, "assistant", semanticResp.NaturalLanguageAnswer);
            return semanticResp;
        }

        if (TrySelectDeterministicAgent(message, out var deterministicAgent, out var deterministicRouting))
        {
            var agentResponse = await deterministicAgent.ProcessAsync(new AgentRequest
            {
                Query = message,
                ConversationHistory = agentHistory
            }, ct);

            foreach (var word in agentResponse.Result.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                await onToken(word + " ");

            historyStore.AddTurn(sessionId, "assistant", agentResponse.Result);
            return new ChatResponse(
                NaturalLanguageAnswer: agentResponse.Result,
                Candidates: [],
                Routing: deterministicRouting with { Confidence = Math.Max(deterministicRouting.Confidence, agentResponse.Confidence) },
                DebugParams: new { Mode = "DeterministicAgent", Agent = deterministicAgent.AgentId });
        }

        // Route the request and extract filters in parallel (same as AnswerAsync)
        var filtersTask = queryUnderstandingService.ExtractFiltersAsync(req.Message, ct);
        var routingTask = router.RouteAsync(req.Message, ct);
        await Task.WhenAll(filtersTask, routingTask);
        var filters = filtersTask.Result;
        var routing = routingTask.Result;

        var routingMetadata = new AgentRoutingMetadata(
            routing.SelectedAgent.AgentId,
            routing.Confidence,
            routing.RoutingMethod);

        // For tab/path-optimization intents fall back to non-streaming path.
        // Dispatch by the LLM-extracted filter intent (no string-match keywords).
        // Guarded by HasTabContent: filter extractor sometimes mis-tags
        // theory prose ("Explain voice leading in jazz") as AnalyzeTab.
        // Never enter the tab branch when there is literally no tab
        // notation in the message — falls through to the agent path.
        if ((routing.SelectedAgent.AgentId == AgentIds.Tab ||
             filters?.Intent is "OptimizePath" or "AnalyzeTab")
            && HasTabContent(req.Message))
        {
            ChatResponse fallback;
            if (filters?.Intent == "OptimizePath")
                fallback = await tabAnalysisService.OptimizePathAsync(req.Message, ct);
            else
                fallback = await tabAnalysisService.AnalyzeTabAsync(req.Message, ct);

            fallback = fallback with { Routing = routingMetadata, QueryFilters = filters };

            // Emit the full answer as word-level simulated tokens
            foreach (var word in fallback.NaturalLanguageAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                await onToken(word + " ");

            historyStore.AddTurn(sessionId, "assistant", fallback.NaturalLanguageAnswer);
            return fallback;
        }

        // For all other agents: stream token-by-token if the selected agent supports it
        if (routing.SelectedAgent is GuitarAlchemistAgentBase streamingAgent)
        {
            var fullText = new StringBuilder();
            await foreach (var token in streamingAgent.ProcessStreamingAsync(
                req.Message, conversationHistory: agentHistory, cancellationToken: ct))
            {
                await onToken(token);
                fullText.Append(token);
            }

            var answer = fullText.ToString();
            historyStore.AddTurn(sessionId, "assistant", answer);

            // Build a minimal ChatResponse with the streamed text
            return new ChatResponse(
                answer,
                [],
                Routing: routingMetadata,
                QueryFilters: filters);
        }

        // Fallback: non-streaming path with word-level simulation
        var response = await tabOrchestrator.AnswerAsync(req, ct);
        response = response with { Routing = routingMetadata, QueryFilters = filters };
        foreach (var word in response.NaturalLanguageAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            await onToken(word + " ");
        historyStore.AddTurn(sessionId, "assistant", response.NaturalLanguageAnswer);
        return response;
    }

    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        using var activity = ChatbotActivitySource.StartActivity(ChatbotActivitySource.OrchestratorAnswer, req.Message);
        var sw = Stopwatch.StartNew();

        // ── Session management ───────────────────────────────────────────────
        var sessionId = req.SessionId ?? Guid.NewGuid().ToString("N");
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
        };

        foreach (var hook in _hooks)
        {
            var hookResult = await hook.OnRequestReceived(hookCtx, ct);
            if (hookResult.MutatedMessage is not null)
                hookCtx.CurrentMessage = hookResult.MutatedMessage;
            if (!hookResult.Cancel) continue;

            sw.Stop();
            return new ChatResponse(
                NaturalLanguageAnswer: hookResult.BlockedResponse?.Result ?? "Request blocked.",
                Candidates: [],
                Routing: new AgentRoutingMetadata("hook", 1f, "hook-blocked"));
        }

        var message = hookCtx.CurrentMessage;

        // ── Deterministic voicing guard runs BEFORE semantic intent routing ──
        // The semantic embedding router can mis-rank an explicit voicing query
        // (e.g. "Show me Drop 2 voicings of Cmaj7") against the modes intent at
        // ~0.71 cosine similarity, stealing it from VoicingAgent. The guard
        // matches on high-precision surface tokens (chord literal + voicing
        // keyword), so when it fires there is no ambiguity worth routing
        // semantically. Diagnosed 2026-05-07 by codex CLI second-opinion;
        // roadmap P1 #6 (#81). The "formalize VoicingIntent" follow-up is
        // separate — this just stops the misroute.
        if (TrySelectDeterministicAgent(message, out var preIntentAgent, out var preIntentRouting))
        {
            return await DispatchDeterministicAgentAsync(
                req, message, sessionId, correlationId, preIntentAgent, preIntentRouting,
                activity, sw, ct);
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
        var algebraFastPath = await TryAnswerWithAlgebraFastPathAsync(req, message, sessionId, correlationId, activity, sw, ct);
        if (algebraFastPath is not null)
        {
            return algebraFastPath;
        }

        // ── Follow-up context enrichment for the routing pass ────────────────
        // A short prompt like "Show me a practical example on guitar" right
        // after a substantive turn ("How do I make this progression sound
        // darker?") used to mis-embed as a standalone request and route to
        // skill.practiceroutine (because "practice" / "practical" share a
        // centroid). For follow-up-shaped messages, prepend the most recent
        // prior user turn to the routing query so the embedding represents
        // the conversation thread, not just the snippet. The downstream
        // `message` variable stays clean (the LLM gets the real message
        // plus full history separately).
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
        var routingMessage = routingContextEnricher.EnrichIfFollowUp(message, sessionId, req.History);

        // ── Unified semantic intent dispatch with the hook lifecycle ─────────
        // One embedding similarity pass over IIntent registrations covers
        // algebra, deterministic skills, and tab-handling intents. Replaces
        // the legacy TryAnswerWithAlgebraAsync branch and the per-skill
        // CanHandle foreach. See migration recommendation §"Routing classifiers".
        var intentMatch = await intentRouter.RouteAsync(routingMessage, services, ct);
        EmitRoutingTrace(intentMatch);
        if (intentMatch is { } pick)
        {
            // OnBeforeSkill hooks (intent.Id replaces the legacy MatchedSkillName).
            var beforeCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = pick.Intent.Id,
                CorrelationId    = correlationId,
                SessionId        = sessionId,   // PR #157 Phase B
            };
            foreach (var hook in _hooks)
            {
                var r = await hook.OnBeforeSkill(beforeCtx, ct);
                if (r.Cancel)
                {
                    sw.Stop();
                    return new ChatResponse(
                        NaturalLanguageAnswer: r.BlockedResponse?.Result ?? "Intent blocked.",
                        Candidates: [],
                        Routing: new AgentRoutingMetadata($"hook.{pick.Intent.Id}", 1f, "hook-blocked"));
                }
            }

            var intentResult = await pick.Intent.ExecuteAsync(message, ct);

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
                AgentId    = pick.Intent.Id,
                Result     = intentResult.Answer,
                Confidence = intentResult.Confidence,
                Evidence   = intentResult.Evidence ?? [],
                Assumptions = [],
                Data       = intentResult.Data,
            };

            // OnAfterSkill hooks
            var afterCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = pick.Intent.Id,
                Response         = skillRespForHooks,
                CorrelationId    = correlationId,
                SessionId        = sessionId,   // PR #157 Phase B
            };
            foreach (var hook in _hooks)
                await hook.OnAfterSkill(afterCtx, ct);

            sw.Stop();
            activity?.SetTag("orchestration.branch", pick.Intent.Id);
            activity?.SetTag("orchestration.elapsed_ms", sw.ElapsedMilliseconds);

            var chatResp = new ChatResponse(
                NaturalLanguageAnswer: intentResult.Answer,
                Candidates: [],
                Routing: new AgentRoutingMetadata(
                    pick.Intent.Id,
                    Math.Min(intentResult.Confidence, pick.Confidence),
                    intentResult.RoutingMethodOverride ?? "semantic-intent"),
                Grounding: BuildGrounding(intentResult));

            // OnResponseSent hooks (memory writing, analytics)
            var sentCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = pick.Intent.Id,
                Response         = skillRespForHooks,
                CorrelationId    = correlationId,
                SessionId        = sessionId,   // PR #157 Phase B — load-bearing for MemoryHook session scope
            };
            foreach (var hook in _hooks)
                await hook.OnResponseSent(sentCtx, ct);

            historyStore.AddTurn(sessionId, "assistant", chatResp.NaturalLanguageAnswer);
            return chatResp;
        }

        if (TrySelectDeterministicAgent(message, out var deterministicAgent, out var deterministicRouting))
        {
            return await DispatchDeterministicAgentAsync(
                req, message, sessionId, correlationId, deterministicAgent, deterministicRouting,
                activity, sw, ct);
        }

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

        activity?.SetTag(ChatbotActivitySource.TagAgentId, routing.SelectedAgent.AgentId);
        activity?.SetTag(ChatbotActivitySource.TagAgentName, routing.SelectedAgent.Name);
        activity?.SetTag(ChatbotActivitySource.TagRoutingMethod, routing.RoutingMethod);
        activity?.SetTag(ChatbotActivitySource.TagRoutingConfidence, routing.Confidence);

        ChatResponse result;
        // LLM-extracted filter intent drives tab branch — no string keyword check.
        // (The semantic intent router upstream handles "make this smoother" /
        // "analyse this tab" via TabOptimizeIntent / TabAnalyzeIntent before we
        // reach this fallback, so this code only fires when the filter extractor
        // tagged the intent OR the agent router routed to AgentIds.Tab.)
        // Guarded by HasTabContent: prevents mis-classification dispatching
        // pure theory prose into the empty-tab fallback path. See comment
        // on the streaming dispatch above for rationale.
        var hasTabContent      = HasTabContent(req.Message);
        var shouldOptimizePath = filters?.Intent == "OptimizePath" && hasTabContent;
        var shouldAnalyzeTab   = (routing.SelectedAgent.AgentId == AgentIds.Tab ||
                                  filters?.Intent == "AnalyzeTab")
                                 && hasTabContent;

        if (shouldAnalyzeTab || shouldOptimizePath)
        {
            if (shouldOptimizePath)
            {
                activity?.SetTag("orchestration.branch", "tab.path_optimization");
                var optimized = await tabAnalysisService.OptimizePathAsync(req.Message, ct);
                result = optimized with { Routing = routingMetadata, QueryFilters = filters };
            }
            else
            {
                activity?.SetTag("orchestration.branch", "tab.analysis");
                var analyzed = await tabAnalysisService.AnalyzeTabAsync(req.Message, ct);
                result = analyzed with { Routing = routingMetadata, QueryFilters = filters };
            }
        }
        else
        {
            if (routing.SelectedAgent is GuitarAlchemistAgentBase selectedAgent)
            {
                activity?.SetTag("orchestration.branch", $"agent.{selectedAgent.AgentId}");

                var agentResponse = await selectedAgent.ProcessAsync(new AgentRequest
                {
                    Query = message,
                    ConversationHistory = req.History?
                        .Select(t => new ChatHistoryTurn(t.Role, t.Content))
                        .ToList()
                }, ct);

                result = new ChatResponse(
                    NaturalLanguageAnswer: agentResponse.Result,
                    Candidates: [],
                    Routing: routingMetadata,
                    QueryFilters: filters,
                    DebugParams: new { Mode = "Agent", Agent = selectedAgent.AgentId });
            }
            else
            {
                activity?.SetTag("orchestration.branch", "rag");
                var response = await tabOrchestrator.AnswerAsync(req, ct);
                result = response with { Routing = routingMetadata, QueryFilters = filters };
            }
        }

        sw.Stop();
        activity?.SetTag("orchestration.elapsed_ms", sw.ElapsedMilliseconds);

        historyStore.AddTurn(sessionId, "assistant", result.NaturalLanguageAnswer);

        // OnResponseSent fires for ALL paths — skill path above, and here for RAG/tab.
        var ragSentCtx = new ChatHookContext
        {
            OriginalMessage = req.Message,
            CurrentMessage  = message,
            CorrelationId   = correlationId,
            SessionId       = sessionId,   // PR #157 Phase B
            Response        = new AgentResponse
            {
                AgentId    = routing.SelectedAgent.AgentId,
                Result     = result.NaturalLanguageAnswer ?? string.Empty,
                Confidence = routing.Confidence,
            },
        };
        foreach (var hook in _hooks)
            await hook.OnResponseSent(ragSentCtx, ct);

        return result;
    }

    /// <summary>
    /// Shared dispatch path for deterministic agents (e.g. VoicingAgent for
    /// explicit voicing requests). Called from two positions in
    /// <see cref="AnswerAsync"/>: a pre-semantic-routing guard that catches
    /// high-precision voicing prompts before SemanticIntentRouter can mis-rank
    /// them, and a post-semantic-routing fallback that catches any
    /// deterministic case the intent router didn't.
    /// </summary>
    private async Task<ChatResponse> DispatchDeterministicAgentAsync(
        ChatRequest req,
        string message,
        string sessionId,
        Guid correlationId,
        GuitarAlchemistAgentBase agent,
        AgentRoutingMetadata routing,
        Activity? activity,
        Stopwatch sw,
        CancellationToken ct)
    {
        activity?.SetTag("orchestration.branch", $"deterministic.{agent.AgentId}");

        var agentResponse = await agent.ProcessAsync(new AgentRequest
        {
            Query = message,
            ConversationHistory = req.History?
                .Select(t => new ChatHistoryTurn(t.Role, t.Content))
                .ToList()
        }, ct);

        sw.Stop();
        activity?.SetTag("orchestration.elapsed_ms", sw.ElapsedMilliseconds);

        var deterministicResponse = new ChatResponse(
            NaturalLanguageAnswer: agentResponse.Result,
            Candidates: [],
            Routing: routing with { Confidence = Math.Max(routing.Confidence, agentResponse.Confidence) },
            DebugParams: new { Mode = "DeterministicAgent", Agent = agent.AgentId });

        historyStore.AddTurn(sessionId, "assistant", deterministicResponse.NaturalLanguageAnswer);

        var sentCtx = new ChatHookContext
        {
            OriginalMessage = req.Message,
            CurrentMessage  = message,
            CorrelationId   = correlationId,
            SessionId       = sessionId,   // PR #157 Phase B — load-bearing for MemoryHook
            Response        = agentResponse,
        };
        foreach (var hook in _hooks)
            await hook.OnResponseSent(sentCtx, ct);

        return deterministicResponse;
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
    private async Task<ChatResponse?> TryAnswerWithAlgebraFastPathAsync(
        ChatRequest req,
        string message,
        string sessionId,
        Guid correlationId,
        Activity? activity,
        Stopwatch sw,
        CancellationToken ct)
    {
        if (!algebraPromptClassifier.IsAlgebraPrompt(message))
        {
            return null;
        }

        var answer = await ixAlgebraService.TryAnswerAsync(message, ct);
        if (answer is null)
        {
            // Classifier said algebra-shaped but service couldn't extract a set.
            // Fall through to semantic dispatch so AlgebraIntent can emit its
            // "I couldn't extract a pitch-class set" guidance with proper trace
            // attribution. Returning the same message inline would short-circuit
            // observability for the failure mode.
            return null;
        }

        sw.Stop();
        activity?.SetTag("orchestration.branch", "algebra.fast_path");
        activity?.SetTag("orchestration.elapsed_ms", sw.ElapsedMilliseconds);

        var algebraResponse = new ChatResponse(
            NaturalLanguageAnswer: answer.NaturalLanguageAnswer,
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                AgentId: "algebra",
                Confidence: 1.0f,
                RoutingMethod: "ix-algebra"),
            Grounding: answer.Grounding);

        historyStore.AddTurn(sessionId, "assistant", algebraResponse.NaturalLanguageAnswer);

        // OnResponseSent for parity with every other dispatch path. Wrap the
        // facts dictionary as evidence strings so MemoryHook / analytics see a
        // shape consistent with skill responses.
        var sentCtx = new ChatHookContext
        {
            OriginalMessage = req.Message,
            CurrentMessage  = message,
            CorrelationId   = correlationId,
            SessionId       = sessionId,
            Response        = new AgentResponse
            {
                AgentId    = "algebra",
                Result     = answer.NaturalLanguageAnswer,
                Confidence = 1.0f,
                Evidence   = answer.Facts.Select(kv => $"{kv.Key}: {kv.Value}").ToList(),
                Assumptions = [],
            },
        };
        foreach (var hook in _hooks)
            await hook.OnResponseSent(sentCtx, ct);

        return algebraResponse;
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

    private static bool IsExplicitVoicingRequest(string message)
    {
        var lower = message.ToLowerInvariant();
        return ExplicitVoicingKeywords.Any(lower.Contains)
               || Regex.IsMatch(
                   message,
                   @"\b[A-G](?:#|b)?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[#b]\d+)?(?:/[A-G](?:#|b)?)?\s+(?:voicing|voicings|shape|shapes|fingering|fingerings)\b",
                   RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Streaming-path entry to the unified semantic intent dispatch — used by
    /// <see cref="AnswerStreamingAsync"/>. Returns the intent's response or
    /// null when no intent scored above threshold (caller falls through to
    /// the LLM agent path).
    /// </summary>
    /// <param name="routingMessage">Message used ONLY for the embedding-router
    /// score. May be enriched with prior conversation context.</param>
    /// <param name="executeMessage">Original user message — the intent's
    /// <c>ExecuteAsync</c> receives this so its internal regex parsers see
    /// the raw text without the enrichment prefix.</param>
    private async Task<ChatResponse?> TryDispatchViaIntentAsync(
        string routingMessage,
        string executeMessage,
        CancellationToken ct)
    {
        var match = await intentRouter.RouteAsync(routingMessage, services, ct);
        EmitRoutingTrace(match);
        if (match is not { } pick) return null;

        var result = await pick.Intent.ExecuteAsync(executeMessage, ct);
        return new ChatResponse(
            NaturalLanguageAnswer: result.Answer,
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                pick.Intent.Id,
                Math.Min(result.Confidence, pick.Confidence),
                result.RoutingMethodOverride ?? "semantic-intent"),
            Grounding: BuildGrounding(result));
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
