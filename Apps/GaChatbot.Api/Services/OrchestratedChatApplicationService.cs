namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Notation;
using GaChatbot.Api.Controllers;

public sealed class OrchestratedChatApplicationService(
    IProductionChatOrchestratorClient orchestratorClient,
    IChatProviderReadinessProbe readinessProbe,
    DirectChatApplicationService directChat,
    IConfiguration configuration,
    ILogger<OrchestratedChatApplicationService> logger) : IChatApplicationService
{
    private readonly int _chatTimeoutSeconds = Math.Max(5, configuration.GetValue("Chatbot:StreamTimeoutSeconds", 60));
    private readonly float _fallbackMinConfidence = Math.Clamp(
        configuration.GetValue("Chatbot:FallbackMinConfidence", 0.25f),
        0f,
        1f);
    private readonly int _fallbackTimeoutSeconds = Math.Max(
        1,
        configuration.GetValue("Chatbot:FallbackTimeoutSeconds", 15));

    public async Task<ChatExecutionResult> ChatAsync(
        ChatExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var trace = new AgenticTraceBuilder($"run_{Guid.NewGuid():N}");
        trace.AddStep(
            "chat.request",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["gen_ai.operation.name"] = "chat",
                ["chat.mode"] = "full",
                ["history.turn_count"] = request.History?.Count ?? 0
            });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_chatTimeoutSeconds));

        try
        {
            ChatResponse response;
            using (var orchestrationStep = trace.StartStep(
                "orchestration.answer",
                new Dictionary<string, object?>
                {
                    ["timeout.seconds"] = _chatTimeoutSeconds
                }))
            {
                response = await orchestratorClient.AnswerAsync(request, timeoutCts.Token);
                orchestrationStep.Complete(
                    finalAttributes: new Dictionary<string, object?>
                    {
                        ["agent.id"] = response.Routing?.AgentId,
                        ["routing.method"] = response.Routing?.RoutingMethod,
                        ["routing.confidence"] = response.Routing?.Confidence,
                        ["grounding.source"] = response.Grounding?.Source,
                        ["candidate.count"] = response.Candidates.Count,
                        ["response.length"] = response.NaturalLanguageAnswer.Length
                    });
            }

            if (ShouldFallback(response, out var fallbackReason))
            {
                logger.LogWarning(
                    "Chat orchestration returned {FallbackReason}. Falling back to direct chat client.",
                    fallbackReason);

                trace.AddStep(
                    "orchestration.fallback",
                    "completed",
                    0,
                    new Dictionary<string, object?>
                    {
                        ["fallback.reason"] = fallbackReason,
                        ["fallback.target"] = "direct"
                    });

                return await BuildFallbackAsync(request, $"{fallbackReason}-fallback", trace, cancellationToken);
            }

            var notation = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(response.NaturalLanguageAnswer);
            var enrichedResponse = response with { NaturalLanguageAnswer = notation.Text };
            AddDetailedResponseTrace(trace, enrichedResponse, notation);

            return ToExecutionResult(enrichedResponse, trace.Build());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Chat orchestration exceeded {TimeoutSeconds}s. Falling back to direct chat client.",
                _chatTimeoutSeconds);

            trace.AddStep(
                "orchestration.timeout",
                "error",
                _chatTimeoutSeconds * 1000L,
                new Dictionary<string, object?>
                {
                    ["fallback.reason"] = "timeout",
                    ["fallback.target"] = "direct"
                });

            return await BuildFallbackAsync(request, "timeout-fallback", trace, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Chat orchestration failed. Falling back to direct chat client.");
            trace.AddStep(
                "orchestration.error",
                "error",
                0,
                new Dictionary<string, object?>
                {
                    ["exception.type"] = exception.GetType().Name,
                    ["fallback.reason"] = "error",
                    ["fallback.target"] = "direct"
                });

            return await BuildFallbackAsync(request, "error-fallback", trace, cancellationToken);
        }
    }

    public async IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(
        ChatExecutionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var result = await ChatAsync(request, cancellationToken);
        yield return new ChatStreamUpdate(Routing: result.Routing, Grounding: result.Grounding, Trace: result.Trace);

        foreach (var chunk in Helpers.SseChunker.SplitIntoChunks(result.NaturalLanguageAnswer))
        {
            yield return new ChatStreamUpdate(Chunk: chunk);
        }

        yield return new ChatStreamUpdate(IsCompleted: true);
    }

    public Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        readinessProbe.GetStatusAsync(cancellationToken);

    private bool ShouldFallback(ChatResponse response, out string reason)
    {
        if (string.IsNullOrWhiteSpace(response.NaturalLanguageAnswer))
        {
            reason = "empty";
            return true;
        }

        if (IsUngroundedLowConfidence(response))
        {
            reason = "low-confidence";
            return true;
        }

        if (IsUngroundedNoMatchAnswer(response))
        {
            reason = "no-match";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool IsUngroundedLowConfidence(ChatResponse response) =>
        response.Routing is { Confidence: var confidence }
        && confidence < _fallbackMinConfidence
        && response.Grounding is null
        && response.Candidates.Count == 0;

    private static bool IsUngroundedNoMatchAnswer(ChatResponse response) =>
        response.Grounding is null
        && response.Candidates.Count == 0
        && response.NaturalLanguageAnswer.Contains("no matches", StringComparison.OrdinalIgnoreCase);

    private async Task<ChatExecutionResult> BuildFallbackAsync(
        ChatExecutionRequest request,
        string routingMethod,
        AgenticTraceBuilder trace,
        CancellationToken cancellationToken)
    {
        using var fallbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fallbackCts.CancelAfter(TimeSpan.FromSeconds(_fallbackTimeoutSeconds));

        using (var fallbackStep = trace.StartStep(
            "gen_ai.chat.fallback",
            new Dictionary<string, object?>
            {
                ["gen_ai.system"] = "ollama",
                ["agent.id"] = "fallback-direct",
                ["routing.method"] = routingMethod,
                ["timeout.seconds"] = _fallbackTimeoutSeconds
            }))
        {
            try
            {
                var fallback = await directChat.ChatAsync(request, fallbackCts.Token);
                fallbackStep.Complete(
                    finalAttributes: new Dictionary<string, object?>
                    {
                        ["response.length"] = fallback.NaturalLanguageAnswer.Length
                    });

                return fallback with
                {
                    // Confidence clamped to 0 — direct chat is not grounded;
                    // calling code arbitrating routing must not treat a
                    // fallback answer as if it were a deterministic /
                    // grounded result. P1 #5 silent-degradation rule;
                    // codex CLI 2026-05-08 P1 #7 QA Q4. Mirrors the new
                    // FallbackChatApplicationService decorator's invariant.
                    Routing = new AgentRoutingMetadata("fallback-direct", 0f, routingMethod),
                    Grounding = null,
                    Trace = trace.Build()
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var answer =
                    "The request exceeded the chatbot fallback timeout before a grounded answer was ready. " +
                    "Try a narrower prompt or use the Stop control while we investigate this slow path.";

                fallbackStep.Complete(
                    "error",
                    new Dictionary<string, object?>
                    {
                        ["fallback.reason"] = "timeout",
                        ["response.length"] = answer.Length
                    });

                return new ChatExecutionResult(
                    answer,
                    new AgentRoutingMetadata("fallback-direct", 0f, $"{routingMethod}-timeout"),
                    null,
                    trace.Build());
            }
        }
    }

    private static ChatExecutionResult ToExecutionResult(ChatResponse response, AgenticTrace trace) =>
        new(
            response.NaturalLanguageAnswer,
            response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none"),
            response.Grounding,
            trace);

    private static void AddDetailedResponseTrace(
        AgenticTraceBuilder trace,
        ChatResponse response,
        NotationAugmentationResult notation)
    {
        trace.AddStep(
            "orchestration.route",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["agent.id"] = response.Routing?.AgentId,
                ["routing.method"] = response.Routing?.RoutingMethod,
                ["routing.confidence"] = response.Routing?.Confidence,
                ["query.intent"] = response.QueryFilters?.Intent,
                ["query.quality"] = response.QueryFilters?.Quality,
                ["query.extension"] = response.QueryFilters?.Extension,
                ["query.stacking_type"] = response.QueryFilters?.StackingType,
                ["query.note_count"] = response.QueryFilters?.NoteCount,
                ["query.key"] = response.QueryFilters?.Key
            });

        trace.AddStep(
            "agent.semantic_result",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["agent.id"] = response.Routing?.AgentId,
                ["grounding.source"] = response.Grounding?.Source,
                ["candidate.count"] = response.Candidates.Count,
                ["voicing.diagram.count"] = notation.DiagramCount,
                ["response.length"] = response.NaturalLanguageAnswer.Length,
                ["debug.mode"] = TryReadDebugValue(response.DebugParams, "Mode"),
                ["debug.agent"] = TryReadDebugValue(response.DebugParams, "Agent")
            });

        trace.AddStep(
            "notation.vextab",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["notation.diagram.count"] = notation.DiagramCount,
                ["notation.vextab.added_count"] = notation.AddedFenceCount,
                ["notation.format"] = "vextab",
                ["notation.renderer"] = "vexflow"
            });

        trace.AddStep(
            "response.emit",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["response.length"] = response.NaturalLanguageAnswer.Length,
                ["response.has_vextab"] = response.NaturalLanguageAnswer.Contains("```vextab", StringComparison.OrdinalIgnoreCase)
            });
    }

    private static object? TryReadDebugValue(object? debugParams, string propertyName)
    {
        if (debugParams is null)
        {
            return null;
        }

        var property = debugParams.GetType().GetProperty(propertyName);
        return property?.GetValue(debugParams);
    }
}
