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
public sealed class FallbackChatApplicationService : IChatApplicationService
{
    private readonly IChatApplicationService _inner;
    private readonly IFallbackChatHandler _fallback;
    private readonly IOptions<FallbackOptions> _options;
    private readonly IAgenticTraceCapture _capture;

    public FallbackChatApplicationService(
        IChatApplicationService inner,
        IFallbackChatHandler fallback,
        IOptions<FallbackOptions> options,
        IAgenticTraceCapture capture)
    {
        // Validate options eagerly — codex CLI 2026-05-08 P1 QA: bad config
        // (negative timeout, out-of-range confidence) used to silently turn
        // every low-confidence request into a CancelAfter exception. Throw
        // at construction so misconfiguration fails loud at composition
        // time rather than at first failed request.
        var opts = options.Value;
        if (opts.MinConfidence is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                paramName: $"{FallbackOptions.SectionName}:{nameof(FallbackOptions.MinConfidence)}",
                actualValue: opts.MinConfidence,
                message: "FallbackOptions.MinConfidence must be in [0.0, 1.0]");
        }
        if (opts.TimeoutSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                paramName: $"{FallbackOptions.SectionName}:{nameof(FallbackOptions.TimeoutSeconds)}",
                actualValue: opts.TimeoutSeconds,
                message: "FallbackOptions.TimeoutSeconds must be >= 1");
        }

        _inner = inner;
        _fallback = fallback;
        _options = options;
        _capture = capture;
    }

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

    /// <summary>
    /// Routing-method prefixes that always indicate a deterministic-skill
    /// dispatch. When one fires AND confidence is below the fallback
    /// threshold, the orchestrator picked a deterministic skill and that
    /// skill returned a low-confidence answer — by P1 #5 contract, this is
    /// a deterministic failure that must NOT be papered over.
    /// </summary>
    /// <remarks>
    /// This is the explicit signal that replaced the ambient
    /// <c>Activity.Current</c> readback flagged in the codex CLI 2026-05-08
    /// QA. <c>ProductionOrchestrator.AnswerAsync</c> creates and disposes
    /// its own Activity, so any tags written inside that scope are gone by
    /// the time the fallback decorator inspects <c>Activity.Current</c>.
    /// Routing method, by contrast, lands on <see cref="ChatResponse.Routing"/>
    /// and survives the call boundary.
    /// </remarks>
    private static readonly string[] DeterministicRoutingMethods =
    [
        "ix-algebra",                       // AlgebraIntent
        "orchestrator-skill-semantic",      // SKILL.md skills (transpose / common-tones / diatonic-chords / etc.)
        "semantic-intent-voicing",          // VoicingIntent (semantic dispatch)
        "deterministic-voicing",            // VoicingAgent (regex-guard fast path)
    ];

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var opts = _options.Value;
        var response = await _inner.ChatAsync(request, cancellationToken);

        // Gate 1: master switch. Always emit a step so observability shows
        // the fallback decision even when nothing fires.
        if (!opts.Enabled)
        {
            _capture.AddStep(
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
            _capture.AddStep(
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

        // Gate 3: deterministic-failure exemption. If the orchestrator
        // routed to a deterministic skill (algebra / Path B SKILL.md /
        // voicing) AND confidence came back below threshold, that's a
        // deterministic failure that the P1 #5 contract says we surface
        // with confidence 0 intact — not paper over it with an LLM guess.
        // Two detection sources, both checked:
        //
        //   1. response.Routing.RoutingMethod — survives the call boundary
        //      and is the most reliable signal. Codex CLI 2026-05-08 QA
        //      flagged that the previous Activity.Current readback didn't
        //      work because ProductionOrchestrator disposes its Activity
        //      before the fallback decorator runs.
        //   2. AgenticTrace step attributes — written via the
        //      IAgenticTraceCapture seam by orchestration-layer code. Kept
        //      as belt-and-suspenders for any future direct trace tagging.
        var deterministicFailure = FindDeterministicFailure(response, _capture.Build());
        if (deterministicFailure is not null)
        {
            _capture.AddStep(
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
            var fallbackText = await _fallback.AnswerAsync(request.Message, fallbackCts.Token);
            sw.Stop();

            _capture.AddStep(
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
            _capture.AddStep(
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

    private static string? FindDeterministicFailure(ChatResponse response, AgenticTrace trace)
    {
        // Source 1: routing method on the response — the canonical signal.
        // Survives the orchestrator's Activity scope (which is what made
        // the original Activity.Current readback unreliable per the codex
        // CLI 2026-05-08 QA). When a deterministic skill ran and returned
        // a result, response.Routing.RoutingMethod tells us which one.
        var method = response.Routing?.RoutingMethod;
        if (!string.IsNullOrEmpty(method)
            && DeterministicRoutingMethods.Contains(method, StringComparer.Ordinal))
        {
            return $"deterministic-route:{method}";
        }

        // Source 2: AgenticTrace step attributes — written via the
        // IAgenticTraceCapture seam by orchestration-layer code. Kept for
        // any future direct trace tagging from inside Orchestration.
        foreach (var step in trace.Steps)
        {
            if (step.Attributes.TryGetValue(ChatbotActivitySource.TagToolFailureReason, out var reasonObj)
                && reasonObj is string reason
                && DeterministicFailureReasons.Contains(reason, StringComparer.Ordinal))
            {
                return reason;
            }
        }

        return null;
    }
}
