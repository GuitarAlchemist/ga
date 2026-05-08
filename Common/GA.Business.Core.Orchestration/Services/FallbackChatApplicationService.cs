namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents;
using Microsoft.Extensions.Options;

/// <summary>
/// <see cref="IChatApplicationService"/> decorator that catches low-confidence
/// orchestrator responses and routes them through an <see cref="IFallbackChatHandler"/>.
/// Stacks BETWEEN <see cref="ReadinessGatedChatApplicationService"/> (outer)
/// and <see cref="TraceableChatApplicationService"/> (inner) so the trace
/// step records the orchestrator's actual result before the fallback
/// decision is taken.
/// </summary>
/// <remarks>
/// <para>
/// Default behavior: <see cref="FallbackOptions.Enabled"/> is false. With
/// the gate closed, the decorator is a transparent passthrough that emits a
/// <c>fallback.skipped</c> trace step so dashboards can distinguish
/// "fallback disabled" from "fallback enabled but didn't fire". With the
/// gate open and the inner response below
/// <see cref="FallbackOptions.MinConfidence"/>, fallback fires.
/// </para>
/// <para>
/// Hard safety guarantees from the codex CLI 2026-05-08 design review:
/// </para>
/// <list type="bullet">
///   <item>Final confidence is clamped to 0 even when the fallback handler
///   succeeds — direct chat is not grounded; never claim it is.</item>
///   <item>Routing method changes to <c>fallback</c>; AgentId becomes
///   <c>fallback-direct</c>; the original failed routing metadata is
///   preserved in the trace step's attributes
///   (<c>fallback.original_agent_id</c>, <c>fallback.original_routing_method</c>,
///   <c>fallback.original_confidence</c>) so dashboards can trace the
///   failed path.</item>
///   <item><see cref="ChatbotActivitySource.TagToolFailureReason"/> is set
///   to <see cref="ChatbotActivitySource.FailureReasons.OrchestratorLowConfidenceFallback"/>
///   on the current activity.</item>
///   <item>NEVER fires when a deterministic tool failure is in the trace
///   (FailureReasons.GaDslEvalNotInvoked, .SkillMdException,
///   .EmptyModelResponse) — those are P1 #5 silent-degradation surfaces
///   and must be surfaced as confidence 0 with the original failure
///   reason intact, not papered over with an LLM guess.</item>
///   <item>Bounded by <see cref="FallbackOptions.TimeoutSeconds"/>; on
///   timeout the orchestrator's original (low-confidence) response is
///   returned as-is with a <c>fallback.timeout</c> trace step rather than
///   crashing.</item>
/// </list>
/// </remarks>
public sealed class FallbackChatApplicationService(
    IChatApplicationService inner,
    IFallbackChatHandler fallback,
    IOptions<FallbackOptions> options,
    IAgenticTraceCapture capture) : IChatApplicationService
{
    /// <summary>
    /// Trace-attribute keys on which the fallback decorator REFUSES to fire.
    /// Mirror the deterministic-failure values in
    /// <see cref="ChatbotActivitySource.FailureReasons"/>.
    /// </summary>
    private static readonly string[] DeterministicFailureReasons =
    [
        ChatbotActivitySource.FailureReasons.GaDslEvalNotInvoked,
        ChatbotActivitySource.FailureReasons.SkillMdException,
        ChatbotActivitySource.FailureReasons.EmptyModelResponse,
        ChatbotActivitySource.FailureReasons.ClosureNotFound,
        ChatbotActivitySource.FailureReasons.ClosureNotExposed,
        ChatbotActivitySource.FailureReasons.ClosureRuntimeError,
        ChatbotActivitySource.FailureReasons.ClosureTimeout,
        ChatbotActivitySource.FailureReasons.ClosureException,
        ChatbotActivitySource.FailureReasons.MissingRequiredArg,
        ChatbotActivitySource.FailureReasons.ArgCoerceFailed,
    ];

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var response = await inner.ChatAsync(request, cancellationToken);

        // Gate 1: master switch. Always emit a step so observability shows
        // the fallback decision even when nothing fires.
        if (!opts.Enabled)
        {
            capture.AddStep(
                "fallback.skipped",
                "completed",
                0,
                new Dictionary<string, object?>
                {
                    ["fallback.gated"] = "disabled",
                });
            return response;
        }

        // Gate 2: confidence threshold.
        var confidence = response.Routing?.Confidence ?? 0f;
        if (confidence >= opts.MinConfidence)
        {
            capture.AddStep(
                "fallback.skipped",
                "completed",
                0,
                new Dictionary<string, object?>
                {
                    ["fallback.gated"] = "above-threshold",
                    ["fallback.threshold"] = opts.MinConfidence,
                    ["fallback.observed_confidence"] = confidence,
                });
            return response;
        }

        // Gate 3: deterministic-failure exemption. If anything inside the
        // orchestrator already declared a deterministic-tool failure, the
        // P1 #5 contract says we surface that failure with confidence 0
        // intact — not paper over it with an LLM guess. Inspect the
        // already-captured trace for any deterministic failure reason.
        var deterministicFailure = FindDeterministicFailure(capture.Build());
        if (deterministicFailure is not null)
        {
            capture.AddStep(
                "fallback.skipped",
                "completed",
                0,
                new Dictionary<string, object?>
                {
                    ["fallback.gated"] = "deterministic-failure-protected",
                    ["fallback.protected_reason"] = deterministicFailure,
                });
            // Preserve the orchestrator's response unchanged but clamp
            // confidence to 0 so callers can't mistake a deterministic
            // failure for a real low-confidence answer.
            return response with
            {
                Routing = response.Routing is null
                    ? new AgentRoutingMetadata("orchestrator", 0f, "deterministic-failure-protected")
                    : response.Routing with { Confidence = 0f },
            };
        }

        // All gates passed — fire the fallback handler.
        var sw = Stopwatch.StartNew();
        using var fallbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fallbackCts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

        Activity.Current?.SetTag(ChatbotActivitySource.TagToolName, "orchestrator");
        Activity.Current?.SetTag(
            ChatbotActivitySource.TagToolFailureReason,
            ChatbotActivitySource.FailureReasons.OrchestratorLowConfidenceFallback);

        try
        {
            var fallbackText = await fallback.AnswerAsync(request.Message, fallbackCts.Token);
            sw.Stop();

            capture.AddStep(
                "orchestration.fallback",
                "completed",
                sw.ElapsedMilliseconds,
                new Dictionary<string, object?>
                {
                    ["fallback.original_agent_id"] = response.Routing?.AgentId,
                    ["fallback.original_routing_method"] = response.Routing?.RoutingMethod,
                    ["fallback.original_confidence"] = confidence,
                    ["fallback.threshold"] = opts.MinConfidence,
                    ["fallback.response_length"] = fallbackText?.Length ?? 0,
                });

            // Confidence stays at 0 (per codex's safety guarantee) so callers
            // arbitrating routing don't treat a direct-chat answer as if it
            // were grounded. AgentId names the surface explicitly so
            // dashboards can split fallback traffic out.
            return new ChatResponse(
                NaturalLanguageAnswer: fallbackText ?? string.Empty,
                Candidates: [],
                Routing: new AgentRoutingMetadata(
                    AgentId: "fallback-direct",
                    Confidence: 0f,
                    RoutingMethod: "fallback"),
                Grounding: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            capture.AddStep(
                "orchestration.fallback",
                "error",
                sw.ElapsedMilliseconds,
                new Dictionary<string, object?>
                {
                    ["fallback.failure_reason"] = "timeout",
                    ["fallback.timeout_seconds"] = opts.TimeoutSeconds,
                });
            // Surface the orchestrator's original response with confidence 0
            // — both the orchestrator's low-confidence answer AND the
            // fallback timeout are visible in the trace.
            return response with
            {
                Routing = response.Routing is null
                    ? new AgentRoutingMetadata("orchestrator", 0f, "fallback-timeout")
                    : response.Routing with { Confidence = 0f, RoutingMethod = "fallback-timeout" },
            };
        }
    }

    private static string? FindDeterministicFailure(AgenticTrace trace)
    {
        // Source 1: AgenticTrace step attributes — written via the
        // IAgenticTraceCapture seam by orchestration-layer code.
        foreach (var step in trace.Steps)
        {
            if (step.Attributes.TryGetValue(ChatbotActivitySource.TagToolFailureReason, out var reasonObj)
                && reasonObj is string reason
                && DeterministicFailureReasons.Contains(reason, StringComparer.Ordinal))
            {
                return reason;
            }
        }

        // Source 2: Activity.Current tag — written by ML-layer code that
        // can't reach the orchestration-scoped capture (the layer rule
        // forbids GA.Business.ML referencing GA.Business.Core.Orchestration).
        // SkillMdDrivenSkill and SkillMdDrivenWrapperBase tag here when
        // ga_dsl_eval is skipped or a SKILL.md path throws. Belt-and-
        // suspenders so the deterministic-failure protection covers both
        // surfaces — codex CLI 2026-05-08 risk-list item 2 ("fallback
        // hiding deterministic failures").
        var current = Activity.Current;
        if (current is not null)
        {
            foreach (var tag in current.TagObjects)
            {
                if (tag.Key == ChatbotActivitySource.TagToolFailureReason
                    && tag.Value is string activityReason
                    && DeterministicFailureReasons.Contains(activityReason, StringComparer.Ordinal))
                {
                    return activityReason;
                }
            }
        }

        return null;
    }
}
