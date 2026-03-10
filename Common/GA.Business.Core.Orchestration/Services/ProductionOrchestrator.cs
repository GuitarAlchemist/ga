namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
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
    IEnumerable<IOrchestratorSkill> orchestratorSkills,
    IEnumerable<IChatHook> chatHooks) : IHarmonicChatOrchestrator
{
    private readonly IReadOnlyList<IOrchestratorSkill> _skills = orchestratorSkills.ToList();
    private readonly IReadOnlyList<IChatHook>          _hooks  = chatHooks.ToList();

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
        // ── OnRequestReceived hooks (sanitization, rate-limiting, auth) ───────
        var hookCtx = new ChatHookContext
        {
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

        // ── Fast-path: domain-grounded skills bypass routing + LLM pipeline ──
        foreach (var skill in _skills)
        {
            if (!skill.CanHandle(message)) continue;

            var skillResp = await skill.ExecuteAsync(message, ct);

            // Emit skill answer as word-level simulated tokens
            foreach (var word in skillResp.Result.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                await onToken(word + " ");

            return new ChatResponse(
                NaturalLanguageAnswer: skillResp.Result,
                Candidates: [],
                Routing: new AgentRoutingMetadata($"skill.{skill.Name}", skillResp.Confidence, "orchestrator-skill"));
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

        // For tab/path-optimization intents fall back to non-streaming path
        if (routing.SelectedAgent.AgentId == AgentIds.Tab ||
            (filters?.Intent is "OptimizePath" or "AnalyzeTab"))
        {
            ChatResponse fallback;
            if (filters?.Intent == "OptimizePath" || IsAskingForOptimization(req.Message))
                fallback = await HandlePathOptimizationAsync(req.Message, ct);
            else
                fallback = await HandleTabAnalysisAsync(req.Message, ct);

            fallback = fallback with { Routing = routingMetadata, QueryFilters = filters };

            // Emit the full answer as word-level simulated tokens
            foreach (var word in fallback.NaturalLanguageAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                await onToken(word + " ");

            return fallback;
        }

        // For all other agents: stream token-by-token if the selected agent supports it
        if (routing.SelectedAgent is GuitarAlchemistAgentBase streamingAgent)
        {
            var fullText = new StringBuilder();
            await foreach (var token in streamingAgent.ProcessStreamingAsync(req.Message, cancellationToken: ct))
            {
                await onToken(token);
                fullText.Append(token);
            }

            // Build a minimal ChatResponse with the streamed text
            return new ChatResponse(
                fullText.ToString(),
                [],
                Routing: routingMetadata,
                QueryFilters: filters);
        }

        // Fallback: non-streaming path with word-level simulation
        var response = await tabOrchestrator.AnswerAsync(req, ct);
        response = response with { Routing = routingMetadata, QueryFilters = filters };
        foreach (var word in response.NaturalLanguageAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            await onToken(word + " ");
        return response;
    }

    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        using var activity = ChatbotActivitySource.StartActivity(ChatbotActivitySource.OrchestratorAnswer, req.Message);
        var sw = Stopwatch.StartNew();

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

        // ── Fast-path: domain-grounded skills bypass routing + LLM pipeline ──
        foreach (var skill in _skills)
        {
            if (!skill.CanHandle(message)) continue;

            // OnBeforeSkill hooks
            var beforeCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = skill.Name,
                CorrelationId    = correlationId,
            };
            foreach (var hook in _hooks)
            {
                var r = await hook.OnBeforeSkill(beforeCtx, ct);
                if (r.Cancel)
                {
                    sw.Stop();
                    return new ChatResponse(
                        NaturalLanguageAnswer: r.BlockedResponse?.Result ?? "Skill blocked.",
                        Candidates: [],
                        Routing: new AgentRoutingMetadata($"hook.{skill.Name}", 1f, "hook-blocked"));
                }
            }

            var skillResp = await skill.ExecuteAsync(message, ct);

            // OnAfterSkill hooks
            var afterCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = skill.Name,
                Response         = skillResp,
                CorrelationId    = correlationId,
            };
            foreach (var hook in _hooks)
                await hook.OnAfterSkill(afterCtx, ct);

            sw.Stop();
            activity?.SetTag("orchestration.branch", $"skill.{skill.Name}");
            activity?.SetTag("orchestration.elapsed_ms", sw.ElapsedMilliseconds);

            var chatResp = new ChatResponse(
                NaturalLanguageAnswer: skillResp.Result,
                Candidates: [],
                Routing: new AgentRoutingMetadata($"skill.{skill.Name}", skillResp.Confidence, "orchestrator-skill"));

            // OnResponseSent hooks (memory writing, analytics)
            var sentCtx = new ChatHookContext
            {
                OriginalMessage  = req.Message,
                CurrentMessage   = message,
                MatchedSkillName = skill.Name,
                Response         = skillResp,
                CorrelationId    = correlationId,
            };
            foreach (var hook in _hooks)
                await hook.OnResponseSent(sentCtx, ct);

            return chatResp;
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
        if (routing.SelectedAgent.AgentId == AgentIds.Tab ||
            (filters?.Intent is "OptimizePath" or "AnalyzeTab"))
        {
            if (filters?.Intent == "OptimizePath" || IsAskingForOptimization(req.Message))
            {
                activity?.SetTag("orchestration.branch", "tab.path_optimization");
                var optimized = await HandlePathOptimizationAsync(req.Message, ct);
                result = optimized with { Routing = routingMetadata, QueryFilters = filters };
            }
            else
            {
                activity?.SetTag("orchestration.branch", "tab.analysis");
                var analyzed = await HandleTabAnalysisAsync(req.Message, ct);
                result = analyzed with { Routing = routingMetadata, QueryFilters = filters };
            }
        }
        else
        {
            activity?.SetTag("orchestration.branch", "rag");
            var response = await tabOrchestrator.AnswerAsync(req, ct);
            result = response with { Routing = routingMetadata, QueryFilters = filters };
        }

        sw.Stop();
        activity?.SetTag("orchestration.elapsed_ms", sw.ElapsedMilliseconds);

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

    private async Task<ChatResponse> HandlePathOptimizationAsync(string query, CancellationToken ct)
    {
        // Extract raw tab from query
        var diagramMatch = Regex.Match(query, @"([x\d]{1,2}-){5}[x\d]{1,2}");
        string tabText;

        if (diagramMatch.Success && !query.Contains('|'))
        {
            var parts = diagramMatch.Value.Split('-');
            if (parts.Length == 6)
            {
                var sb = new StringBuilder();
                for (int i = 5; i >= 0; i--)
                    sb.AppendLine($"|--{parts[i]}--|");
                tabText = sb.ToString();
            }
            else
            {
                tabText = ExtractTabLines(query);
            }
        }
        else
        {
            tabText = ExtractTabLines(query);
        }

        var analysis = await tabAnalyzer.AnalyzeAsync(tabText);
        if (analysis.Events.Count == 0)
            return new ChatResponse("I couldn't find a valid tab to optimize.", []);

        var solution = await tabSolver.SolveOptimalPathAsync(analysis.Events.Select(e => e.Document));
        var alternatives = await altService.GetAlternativesAsync(analysis.Events.Select(e => e.Document));

        var narrative = new StringBuilder();
        narrative.AppendLine("I've re-calculated the optimal path for that progression to minimize hand movement and transitions.");
        narrative.AppendLine();
        narrative.AppendLine("**Optimized Tab:**");
        narrative.AppendLine("```");
        narrative.AppendLine(solution.TabContent);
        narrative.AppendLine("```");
        narrative.AppendLine($"*Optimization Score: {solution.TotalPhysicalCost:F1} physical cost units.*");

        if (alternatives.Any())
        {
            narrative.AppendLine();
            narrative.AppendLine("### Alternative Styles");
            string[] stringNames = ["e", "B", "G", "D", "A", "E"];

            foreach (var alt in alternatives)
            {
                narrative.AppendLine($"**{alt.Label}** ({alt.Description})");
                narrative.AppendLine("```");
                var sb = new StringBuilder();
                for (int s = 0; s < 6; s++)
                {
                    sb.Append(stringNames[s] + "|");
                    var stringIdx = s + 1;
                    foreach (var chord in alt.Tab)
                    {
                        var pos = chord.FirstOrDefault(n => n.StringIndex.Value == stringIdx);
                        if (pos != null) sb.Append($"-{pos.Fret}-");
                        else sb.Append("-x-");
                    }
                    sb.AppendLine("|");
                }
                narrative.AppendLine(sb.ToString());
                narrative.AppendLine("```");
            }
        }

        return new ChatResponse(
            narrative.ToString(),
            [],
            DebugParams: new { Mode = "PathOptimization", Cost = solution.TotalPhysicalCost });
    }

    private async Task<ChatResponse> HandleTabAnalysisAsync(string tab, CancellationToken ct)
    {
        var result = await tabAnalyzer.AnalyzeAsync(tab);
        if (result.Events.Count == 0)
            return new ChatResponse("I detected tab but couldn't parse any chords.", [], DebugParams: new { Status = "Empty" });

        var events = result.Events.ToList();
        var embeddingTasks = events.Select(async e =>
        {
            try
            {
                var emb = await embeddingGenerator.GenerateEmbeddingAsync(e.Document);
                return e with { Document = e.Document with { Embedding = emb } };
            }
            catch (Exception)
            {
                // Degrade gracefully — proceed without embedding for this event
                return e;
            }
        }).ToList();

        events = [.. await Task.WhenAll(embeddingTasks)];
        var progression = events.Select(e => e.Document).ToList();

        var targets = modulationAnalyzer.IdentifyTargets(progression);
        var suggestions = await suggestionService.SuggestNextAsync(progression.Last(), topK: 3);

        return presenter.FormatAnalysis(result, suggestions, targets);
    }

    private static bool IsAskingForOptimization(string query)
    {
        var q = query.ToLowerInvariant();
        return q.Contains("smooth") || q.Contains("easy") || q.Contains("optimize") ||
               q.Contains("ergonomic") || q.Contains("better path");
    }

    private static string ExtractTabLines(string query) =>
        string.Join("\n", query.Split('\n').Where(l => l.Contains('|') || l.Contains("--")));
}
