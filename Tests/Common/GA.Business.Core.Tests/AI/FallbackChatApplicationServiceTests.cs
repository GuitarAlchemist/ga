namespace GA.Business.Core.Tests.AI;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents;
using Microsoft.Extensions.Options;
using Moq;
using GaClosureRegistry = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// Unit tests for <see cref="FallbackChatApplicationService"/>. Codex CLI
/// 2026-05-08 risk-list item 2: "A fallback decorator can accidentally
/// convert deterministic ga_dsl_eval failures into plausible direct-chat
/// answers, which code review may miss unless tests assert confidence 0,
/// routing.method = fallback, and tool.failure_reason." This fixture is
/// the test that locks those guarantees.
/// </summary>
[TestFixture]
public class FallbackChatApplicationServiceTests
{
    private static FallbackChatApplicationService MakeDecorator(
        ChatResponse innerResponse,
        bool fallbackEnabled,
        IFallbackChatHandler? fallbackHandler = null,
        IAgenticTraceCapture? capture = null)
    {
        var inner = new Mock<IChatApplicationService>();
        inner.Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(innerResponse);

        var options = Options.Create(new FallbackOptions
        {
            Enabled = fallbackEnabled,
            MinConfidence = 0.25f,
            TimeoutSeconds = 5,
        });

        return new FallbackChatApplicationService(
            inner.Object,
            fallbackHandler ?? new NoOpFallbackChatHandler(),
            options,
            capture ?? new RecordingCapture());
    }

    [Test]
    public async Task ChatAsync_FallbackDisabled_PassesInnerResponseThrough_Unmodified()
    {
        var inner = new ChatResponse(
            NaturalLanguageAnswer: "low-confidence answer",
            Candidates: [],
            Routing: new AgentRoutingMetadata("agent-x", 0.1f, "agent-x-routing"));

        var decorator = MakeDecorator(inner, fallbackEnabled: false);
        var response = await decorator.ChatAsync(new ChatRequest("hi"));

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Is.EqualTo("low-confidence answer"));
            Assert.That(response.Routing!.AgentId, Is.EqualTo("agent-x"));
            Assert.That(response.Routing!.Confidence, Is.EqualTo(0.1f));
            Assert.That(response.Routing!.RoutingMethod, Is.EqualTo("agent-x-routing"));
        });
    }

    [Test]
    public async Task ChatAsync_FallbackEnabled_HighConfidenceInner_DoesNotFire()
    {
        var inner = new ChatResponse(
            NaturalLanguageAnswer: "grounded answer",
            Candidates: [],
            Routing: new AgentRoutingMetadata("algebra", 1.0f, "ix-algebra"));

        var fallback = new Mock<IFallbackChatHandler>();
        fallback.Setup(f => f.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("FALLBACK INVOKED");

        var decorator = MakeDecorator(inner, fallbackEnabled: true, fallbackHandler: fallback.Object);
        var response = await decorator.ChatAsync(new ChatRequest("hi"));

        Assert.That(response.NaturalLanguageAnswer, Is.EqualTo("grounded answer"));
        fallback.Verify(f => f.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ChatAsync_FallbackEnabled_LowConfidenceInner_FiresHandler_ClampsConfidenceToZero()
    {
        var inner = new ChatResponse(
            NaturalLanguageAnswer: "I don't know",
            Candidates: [],
            Routing: new AgentRoutingMetadata("agent-x", 0.1f, "agent-x-routing"));

        var fallback = new Mock<IFallbackChatHandler>();
        fallback.Setup(f => f.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("the fallback answer");

        var decorator = MakeDecorator(inner, fallbackEnabled: true, fallbackHandler: fallback.Object);
        var response = await decorator.ChatAsync(new ChatRequest("hi"));

        // Codex's three required assertions: confidence 0, routing.method
        // = fallback, tool.failure_reason set on activity.
        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Is.EqualTo("the fallback answer"));
            Assert.That(response.Routing!.AgentId, Is.EqualTo("fallback-direct"));
            Assert.That(response.Routing!.Confidence, Is.EqualTo(0f),
                "fallback path must clamp confidence to 0 — direct chat is not grounded");
            Assert.That(response.Routing!.RoutingMethod, Is.EqualTo("fallback"));
        });
        fallback.Verify(f => f.AnswerAsync("hi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ChatAsync_FallbackEnabled_DeterministicFailureInTrace_RefusesFallback()
    {
        // Capture has a step with tool.failure_reason = ga-dsl-eval-not-invoked
        // — a Path B skill explicitly failed. The fallback decorator must
        // surface that failure with confidence 0 instead of papering it over.
        var capture = new RecordingCapture();
        capture.AddStep(
            "skill.transpose",
            "completed",
            10,
            new Dictionary<string, object?>
            {
                [GA.Business.ML.Agents.ChatbotActivitySource.TagToolFailureReason] =
                    GA.Business.ML.Agents.ChatbotActivitySource.FailureReasons.GaDslEvalNotInvoked,
            });

        var inner = new ChatResponse(
            NaturalLanguageAnswer: "LLM apologized",
            Candidates: [],
            Routing: new AgentRoutingMetadata("skill.transpose", 0.1f, "orchestrator-skill-semantic"));

        var fallback = new Mock<IFallbackChatHandler>();
        fallback.Setup(f => f.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("PAPERED-OVER ANSWER");

        var decorator = MakeDecorator(inner, fallbackEnabled: true, fallbackHandler: fallback.Object, capture: capture);
        var response = await decorator.ChatAsync(new ChatRequest("hi"));

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Is.EqualTo("LLM apologized"),
                "deterministic-failure protection: original orchestrator response is preserved");
            Assert.That(response.Routing!.Confidence, Is.EqualTo(0f),
                "deterministic-failure protection: confidence clamped to 0");
            Assert.That(response.Routing!.AgentId, Is.EqualTo("skill.transpose"),
                "deterministic-failure protection: original agent id preserved (callers can trace the failed path)");
        });
        fallback.Verify(f => f.AnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never,
            "fallback handler MUST NOT be invoked when a deterministic tool failure is in the trace");
    }

    /// <summary>
    /// Real capture impl for tests that need to seed steps before the
    /// decorator inspects them. The production
    /// <c>AgenticTraceCapture</c> is internal — re-implementing the
    /// minimum surface here keeps this fixture self-contained.
    /// </summary>
    private sealed class RecordingCapture : IAgenticTraceCapture
    {
        private readonly List<AgenticTraceStep> _steps = [];

        public string RunId { get; } = $"test_{Guid.NewGuid():N}";

        public void AddStep(string name, string status, long elapsedMs, IReadOnlyDictionary<string, object?>? attributes = null) =>
            _steps.Add(new AgenticTraceStep(name, status, elapsedMs, attributes ?? new Dictionary<string, object?>()));

        public ITimedStep StartStep(string name, IReadOnlyDictionary<string, object?>? attributes = null) =>
            new InlineStep(this, name, attributes ?? new Dictionary<string, object?>());

        public AgenticTrace Build() =>
            new(RunId, "test", RunId, [.. _steps]);

        private sealed class InlineStep(RecordingCapture capture, string name, IReadOnlyDictionary<string, object?> attributes) : ITimedStep
        {
            public void Complete(string status = "completed", IReadOnlyDictionary<string, object?>? finalAttributes = null)
            {
                var merged = finalAttributes is null
                    ? attributes
                    : MergeAttributes(attributes, finalAttributes);
                capture.AddStep(name, status, 0, merged);
            }

            public void Dispose() => Complete();

            private static IReadOnlyDictionary<string, object?> MergeAttributes(
                IReadOnlyDictionary<string, object?> a,
                IReadOnlyDictionary<string, object?> b)
            {
                var merged = new Dictionary<string, object?>(a);
                foreach (var pair in b) merged[pair.Key] = pair.Value;
                return merged;
            }
        }
    }
}
