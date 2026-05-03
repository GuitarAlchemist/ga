namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Intents;
using GA.Business.Core.Orchestration.Models;
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
    SemanticIntentRouter intentRouter,
    TabAnalysisOrchestrationService tabAnalysisService,
    IServiceProvider services) : IHarmonicChatOrchestrator
{
    private readonly IReadOnlyList<IOrchestratorSkill> _skills = orchestratorSkills.ToList();
    private readonly IReadOnlyList<IChatHook>          _hooks  = chatHooks.ToList();
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

        // ── Unified semantic intent dispatch (replaces the legacy algebra check
        //    and the per-skill CanHandle foreach). One embedding similarity pass
        //    over IIntent registrations covers algebra, deterministic skills,
        //    and tab-handling intents. See
        //    docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
        //    §"Routing classifiers".
        var semanticResp = await TryDispatchViaIntentAsync(message, ct);
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
        if (routing.SelectedAgent.AgentId == AgentIds.Tab ||
            (filters?.Intent is "OptimizePath" or "AnalyzeTab"))
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

        // ── Unified semantic intent dispatch with the hook lifecycle ─────────
        // One embedding similarity pass over IIntent registrations covers
        // algebra, deterministic skills, and tab-handling intents. Replaces
        // the legacy TryAnswerWithAlgebraAsync branch and the per-skill
        // CanHandle foreach. See migration recommendation §"Routing classifiers".
        var intentMatch = await intentRouter.RouteAsync(message, services, ct);
        if (intentMatch is { } pick)
        {
            // OnBeforeSkill hooks (intent.Id replaces the legacy MatchedSkillName).
            var beforeCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = pick.Intent.Id,
                CorrelationId    = correlationId,
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
            var skillRespForHooks = new AgentResponse
            {
                AgentId    = pick.Intent.Id,
                Result     = intentResult.Answer,
                Confidence = intentResult.Confidence,
                Evidence   = intentResult.Evidence ?? [],
                Assumptions = [],
            };

            // OnAfterSkill hooks
            var afterCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = pick.Intent.Id,
                Response         = skillRespForHooks,
                CorrelationId    = correlationId,
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
                    intentResult.RoutingMethodOverride ?? "semantic-intent"));

            // OnResponseSent hooks (memory writing, analytics)
            var sentCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = pick.Intent.Id,
                Response         = skillRespForHooks,
                CorrelationId    = correlationId,
            };
            foreach (var hook in _hooks)
                await hook.OnResponseSent(sentCtx, ct);

            historyStore.AddTurn(sessionId, "assistant", chatResp.NaturalLanguageAnswer);
            return chatResp;
        }

        if (TrySelectDeterministicAgent(message, out var deterministicAgent, out var deterministicRouting))
        {
            activity?.SetTag("orchestration.branch", $"deterministic.{deterministicAgent.AgentId}");

            var agentResponse = await deterministicAgent.ProcessAsync(new AgentRequest
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
                Routing: deterministicRouting with { Confidence = Math.Max(deterministicRouting.Confidence, agentResponse.Confidence) },
                DebugParams: new { Mode = "DeterministicAgent", Agent = deterministicAgent.AgentId });

            historyStore.AddTurn(sessionId, "assistant", deterministicResponse.NaturalLanguageAnswer);

            var sentCtx = new ChatHookContext
            {
                OriginalMessage = req.Message,
                CurrentMessage  = message,
                CorrelationId   = correlationId,
                Response        = agentResponse,
            };
            foreach (var hook in _hooks)
                await hook.OnResponseSent(sentCtx, ct);

            return deterministicResponse;
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
        var shouldOptimizePath = filters?.Intent == "OptimizePath";
        var shouldAnalyzeTab   = routing.SelectedAgent.AgentId == AgentIds.Tab ||
                                 filters?.Intent == "AnalyzeTab";

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

    private bool TrySelectDeterministicAgent(
        string message,
        out GuitarAlchemistAgentBase agent,
        out AgentRoutingMetadata routing)
    {
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
    private async Task<ChatResponse?> TryDispatchViaIntentAsync(string message, CancellationToken ct)
    {
        var match = await intentRouter.RouteAsync(message, services, ct);
        if (match is not { } pick) return null;

        var result = await pick.Intent.ExecuteAsync(message, ct);
        return new ChatResponse(
            NaturalLanguageAnswer: result.Answer,
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                pick.Intent.Id,
                Math.Min(result.Confidence, pick.Confidence),
                result.RoutingMethodOverride ?? "semantic-intent"));
    }
}
