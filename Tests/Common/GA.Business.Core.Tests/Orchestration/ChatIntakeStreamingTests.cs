namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.Core.Orchestration.Trace;
using Microsoft.Extensions.Options;
using Moq;

[TestFixture]
public class ChatIntakeStreamingTests
{
    private static readonly ChatResponse Answer = new("D Dorian", [], Routing: new("theory", 0.9f, "semantic"));

    private static (ChatIntake Intake, Mock<IHarmonicChatOrchestrator> Orchestrator,
        Mock<ILlmConcurrencyGate> Gate, Mock<IFallbackChatHandler> Fallback, Mock<IAgenticTraceCapture> Trace)
        Pipeline(bool ready = true, bool gateOpen = true)
    {
        var orchestrator = new Mock<IHarmonicChatOrchestrator>();
        var gate = new Mock<ILlmConcurrencyGate>();
        gate.Setup(value => value.TryEnterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(gateOpen);
        var fallback = new Mock<IFallbackChatHandler>();
        fallback.Setup(value => value.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationTurn>?>(), It.IsAny<CancellationToken>())).ReturnsAsync("fallback answer");
        var trace = new Mock<IAgenticTraceCapture>();
        trace.Setup(value => value.Build()).Returns(new AgenticTrace("trace", "test", "run", []));
        trace.Setup(value => value.StartStep(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
            .Returns(Mock.Of<ITimedStep>());
        var probe = new Mock<IChatReadinessProbe>();
        probe.Setup(value => value.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ChatReadinessResult(ready, "test provider"));
        IChatApplicationService service = new HarmonicChatApplicationService(orchestrator.Object);
        service = new TraceableChatApplicationService(service, trace.Object);
        service = new FallbackChatApplicationService(service, fallback.Object,
            Options.Create(new FallbackOptions { Enabled = true, TimeoutSeconds = 1 }), trace.Object);
        service = new ReadinessGatedChatApplicationService(service, probe.Object, trace.Object);
        return (new ChatIntake(service, gate.Object), orchestrator, gate, fallback, trace);
    }

    [Test]
    public async Task Streaming_ForwardsHistoryAndStreamsBeforeCompletion()
    {
        var (intake, orchestrator, gate, _, trace) = Pipeline();
        List<string> chunks = [];
        ConversationTurn[] history = [new("user", "Dm7", DateTimeOffset.UnixEpoch)];
        orchestrator.Setup(value => value.AnswerStreamingAsync(It.IsAny<ChatRequest>(), It.IsAny<Func<string, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChatRequest request, Func<string, Task> emit, CancellationToken _) =>
            {
                Assert.That(request.History, Is.EqualTo(history));
                Assert.That(request.SessionId, Is.EqualTo("server-session"));
                Assert.That(request.Message, Is.EqualTo("which scale?"));
                await emit("D ");
                Assert.That(chunks, Is.EqualTo(new[] { "D " }), "tokens must arrive before dispatch completes");
                await emit("Dorian");
                return Answer;
            });
        var result = await intake.IntakeStreamingAsync(new("  which scale?  ", "server-session", history),
            token => { chunks.Add(token); return Task.CompletedTask; });
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(string.Concat(chunks), Is.EqualTo("D Dorian"));
        gate.Verify(value => value.Release(), Times.Once);
        trace.Verify(value => value.AddStep("readiness.check", "completed", It.IsAny<long>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
        trace.Verify(value => value.StartStep("orchestration.answer", It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
    }

    [TestCase("")]
    [TestCase("valid request")]
    public async Task Streaming_RejectedRequest_DoesNotEmitOrReleaseUnownedGate(string message)
    {
        var (intake, orchestrator, gate, _, _) = Pipeline(gateOpen: false);
        var result = await intake.IntakeStreamingAsync(new(message), _ => throw new AssertionException("rejection emitted text"));
        Assert.That(result.IsFailure, Is.True);
        gate.Verify(value => value.Release(), Times.Never);
        orchestrator.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Streaming_NotReady_EmitsBlockedAnswerWithoutDispatch()
    {
        var (intake, orchestrator, gate, _, _) = Pipeline(ready: false);
        List<string> chunks = [];
        var result = await intake.IntakeStreamingAsync(new("hi"), token => { chunks.Add(token); return Task.CompletedTask; });
        Assert.That(result.GetValueOrThrow().Routing!.RoutingMethod, Is.EqualTo("readiness-blocked"));
        Assert.That(result.GetValueOrThrow().Routing!.Confidence, Is.Zero);
        Assert.That(chunks, Has.Count.EqualTo(1));
        Assert.That(chunks[0], Is.EqualTo(result.GetValueOrThrow().NaturalLanguageAnswer));
        orchestrator.VerifyNoOtherCalls();
        gate.Verify(value => value.Release(), Times.Once);
    }

    [TestCase(false, "semantic", "fallback")]
    [TestCase(true, "semantic", "semantic")]
    [TestCase(false, "ix-algebra", "ix-algebra")]
    public async Task Streaming_FallbackNeverReplacesSentTextOrDeterministicFailures(bool emitText, string route, string expectedRoute)
    {
        var (intake, orchestrator, _, fallback, _) = Pipeline();
        orchestrator.Setup(value => value.AnswerStreamingAsync(It.IsAny<ChatRequest>(), It.IsAny<Func<string, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChatRequest _, Func<string, Task> emit, CancellationToken _) =>
            {
                if (emitText) await emit("original answer");
                return new ChatResponse("original answer", [], Routing: new("agent", 0.1f, route));
            });
        List<string> chunks = [];
        var result = await intake.IntakeStreamingAsync(new("hi"), token => { chunks.Add(token); return Task.CompletedTask; });
        Assert.That(result.GetValueOrThrow().Routing!.RoutingMethod, Is.EqualTo(expectedRoute));
        Assert.That(string.Concat(chunks), Is.EqualTo(expectedRoute == "fallback" ? "fallback answer" : "original answer"));
        fallback.Verify(value => value.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationTurn>?>(), It.IsAny<CancellationToken>()),
            expectedRoute == "fallback" ? Times.Once() : Times.Never());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Streaming_CancellationOrWriterFailure_ReleasesGate(bool cancel)
    {
        var (intake, orchestrator, gate, fallback, _) = Pipeline();
        using var cancellation = new CancellationTokenSource();
        orchestrator.Setup(value => value.AnswerStreamingAsync(It.IsAny<ChatRequest>(), It.IsAny<Func<string, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ChatRequest _, Func<string, Task> emit, CancellationToken token) =>
            {
                await emit("partial");
                token.ThrowIfCancellationRequested();
                return Answer;
            });
        Task Emit(string _)
        {
            if (!cancel) throw new IOException("client disconnected");
            cancellation.Cancel();
            return Task.CompletedTask;
        }
        if (cancel)
            Assert.ThrowsAsync<OperationCanceledException>(() => intake.IntakeStreamingAsync(new("hi"), Emit, cancellation.Token));
        else
            Assert.ThrowsAsync<IOException>(() => intake.IntakeStreamingAsync(new("hi"), Emit));
        gate.Verify(value => value.Release(), Times.Once);
        fallback.VerifyNoOtherCalls();
    }
}
