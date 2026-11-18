namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.Core.Orchestration.Trace;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// <see cref="IChatIntake.RunAsync"/> through the real decorator chain (readiness, fallback,
/// trace): the event contract every streaming transport maps to its wire format.
/// </summary>
[TestFixture]
public class ChatIntakeStreamingTests
{
    private static readonly ChatResponse Answer = new("D Dorian", [], Routing: new("theory", 0.9f, "semantic"));

    private delegate Task<ChatResponse> StreamingAnswer(
        ChatRequest request,
        Func<string, Task> emit,
        CancellationToken cancellationToken,
        Func<AgentRoutingMetadata, GroundingMetadata?, Task>? onRouting);

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

    private static void Answers(Mock<IHarmonicChatOrchestrator> orchestrator, StreamingAnswer answer) =>
        orchestrator.Setup(value => value.AnswerStreamingAsync(
                It.IsAny<ChatRequest>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<AgentRoutingMetadata, GroundingMetadata?, Task>?>()))
            .Returns((ChatRequest request, Func<string, Task> emit, CancellationToken token,
                    Func<AgentRoutingMetadata, GroundingMetadata?, Task>? onRouting) =>
                answer(request, emit, token, onRouting));

    private static async Task<List<ChatEvent>> CollectAsync(
        IAsyncEnumerable<ChatEvent> events, CancellationToken cancellationToken = default)
    {
        List<ChatEvent> collected = [];
        await foreach (var chatEvent in events.WithCancellation(cancellationToken)) collected.Add(chatEvent);
        return collected;
    }

    private static string Text(IEnumerable<ChatEvent> events) =>
        string.Concat(events.OfType<ChatEvent.TextDelta>().Select(delta => delta.Text));

    [Test]
    [CancelAfter(10_000)]
    public async Task RunAsync_YieldsRoutingThenTextAsItIsProducedThenCompleted(CancellationToken cancellationToken)
    {
        var (intake, orchestrator, gate, _, trace) = Pipeline();
        var restReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;
        ConversationTurn[] history = [new("user", "Dm7", DateTimeOffset.UnixEpoch)];
        Answers(orchestrator, async (request, emit, _, onRouting) =>
        {
            Assert.That(request.History, Is.EqualTo(history));
            Assert.That(request.SessionId, Is.EqualTo("server-session"));
            Assert.That(request.Message, Is.EqualTo("which scale?"));
            await onRouting!(Answer.Routing!, null);
            await emit("D ");
            await restReleased.Task;
            await emit("Dorian");
            finished = true;
            return Answer;
        });

        await using var events = intake.RunAsync(new("  which scale?  ", "server-session", history), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        List<ChatEvent> seen = [];
        while (!seen.OfType<ChatEvent.TextDelta>().Any() && await events.MoveNextAsync()) seen.Add(events.Current);
        Assert.That(finished, Is.False, "the first text must reach the transport before the answer is finished");
        restReleased.SetResult();
        while (await events.MoveNextAsync()) seen.Add(events.Current);

        Assert.Multiple(() =>
        {
            Assert.That(seen.Select(chatEvent => chatEvent.GetType()), Is.EqualTo(new[]
            {
                typeof(ChatEvent.Routing), typeof(ChatEvent.TextDelta), typeof(ChatEvent.TextDelta), typeof(ChatEvent.Completed),
            }));
            Assert.That(seen.Select(chatEvent => chatEvent.RunId).Distinct().Count(), Is.EqualTo(1), "one run id per run");
            Assert.That(((ChatEvent.Routing)seen[0]).AgentId, Is.EqualTo("theory"));
            Assert.That(Text(seen), Is.EqualTo("D Dorian"));
            Assert.That(((ChatEvent.Completed)seen[^1]).Response, Is.EqualTo(Answer));
        });
        gate.Verify(value => value.Release(), Times.Once);
        trace.Verify(value => value.AddStep("readiness.check", "completed", It.IsAny<long>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
        trace.Verify(value => value.StartStep("orchestration.answer", It.IsAny<IReadOnlyDictionary<string, object?>?>()), Times.Once);
    }

    [Test]
    public async Task RunAsync_TextBeforeAnyRoutingReport_StillStartsWithRouting()
    {
        var (intake, orchestrator, _, _, _) = Pipeline();
        Answers(orchestrator, async (_, emit, _, _) =>
        {
            await emit("D Dorian");
            return Answer;
        });

        var events = await CollectAsync(intake.RunAsync(new("hi")));

        Assert.That(events.Select(chatEvent => chatEvent.GetType()), Is.EqualTo(new[]
        {
            typeof(ChatEvent.Routing), typeof(ChatEvent.TextDelta), typeof(ChatEvent.Completed),
        }));
    }

    [TestCase("")]
    [TestCase("valid request")]
    public async Task RunAsync_RejectedRequest_YieldsLoneFailedWithoutReleasingUnownedGate(string message)
    {
        var (intake, orchestrator, gate, _, _) = Pipeline(gateOpen: false);

        var events = await CollectAsync(intake.RunAsync(new(message)));

        Assert.That(events, Has.Count.EqualTo(1));
        var failed = events[0] as ChatEvent.Failed;
        Assert.That(failed?.Rejection, message.Length == 0
            ? Is.InstanceOf<ChatIntakeError.Validation>()
            : Is.InstanceOf<ChatIntakeError.Busy>());
        gate.Verify(value => value.Release(), Times.Never);
        orchestrator.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RunAsync_NotReady_YieldsBlockedAnswerWithoutDispatch()
    {
        var (intake, orchestrator, gate, _, _) = Pipeline(ready: false);

        var events = await CollectAsync(intake.RunAsync(new("hi")));

        var routing = (ChatEvent.Routing)events[0];
        var completed = (ChatEvent.Completed)events[^1];
        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(3));
            Assert.That(routing.Method, Is.EqualTo("readiness-blocked"));
            Assert.That(routing.Confidence, Is.Zero);
            Assert.That(Text(events), Is.EqualTo(completed.Response.NaturalLanguageAnswer));
        });
        orchestrator.VerifyNoOtherCalls();
        gate.Verify(value => value.Release(), Times.Once);
    }

    [TestCase(false, "semantic", "fallback")]
    [TestCase(true, "semantic", "semantic")]
    [TestCase(false, "ix-algebra", "ix-algebra")]
    public async Task RunAsync_FallbackNeverReplacesSentTextOrDeterministicFailures(bool emitText, string route, string expectedRoute)
    {
        var (intake, orchestrator, _, fallback, _) = Pipeline();
        Answers(orchestrator, async (_, emit, _, onRouting) =>
        {
            var original = new ChatResponse("original answer", [], Routing: new("agent", 0.1f, route));
            await onRouting!(original.Routing!, null);
            if (emitText) await emit("original answer");
            return original;
        });

        var events = await CollectAsync(intake.RunAsync(new("hi")));

        Assert.Multiple(() =>
        {
            Assert.That(((ChatEvent.Routing)events[0]).Method, Is.EqualTo(expectedRoute), "routing matches the answer sent");
            Assert.That(((ChatEvent.Completed)events[^1]).Response.Routing!.RoutingMethod, Is.EqualTo(expectedRoute));
            Assert.That(Text(events), Is.EqualTo(expectedRoute == "fallback" ? "fallback answer" : "original answer"));
        });
        fallback.Verify(value => value.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ConversationTurn>?>(), It.IsAny<CancellationToken>()),
            expectedRoute == "fallback" ? Times.Once() : Times.Never());
    }

    [Test]
    public async Task RunAsync_PipelineFailsAfterText_EndsWithFailedAndReleasesGate()
    {
        var (intake, orchestrator, gate, fallback, _) = Pipeline();
        Answers(orchestrator, async (_, emit, _, onRouting) =>
        {
            await onRouting!(Answer.Routing!, null);
            await emit("partial");
            throw new HttpRequestException("provider dropped the stream");
        });

        var events = await CollectAsync(intake.RunAsync(new("hi")));

        Assert.Multiple(() =>
        {
            Assert.That(events.Select(chatEvent => chatEvent.GetType()), Is.EqualTo(new[]
            {
                typeof(ChatEvent.Routing), typeof(ChatEvent.TextDelta), typeof(ChatEvent.Failed),
            }));
            Assert.That(((ChatEvent.Failed)events[^1]).Rejection, Is.Null);
        });
        gate.Verify(value => value.Release(), Times.Once);
        fallback.VerifyNoOtherCalls();
    }

    [Test]
    [CancelAfter(10_000)]
    public void RunAsync_CallerCancels_ThrowsAndReleasesGateOnceThePipelineStops(CancellationToken testToken)
    {
        var (intake, orchestrator, gate, fallback, _) = Pipeline();
        var pipelineStopped = false;
        Answers(orchestrator, async (_, emit, token, _) =>
        {
            try
            {
                await emit("partial");
                await Task.Delay(Timeout.Infinite, token);
                return Answer;
            }
            finally
            {
                pipelineStopped = true;
            }
        });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(testToken);

        Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () =>
        {
            await foreach (var chatEvent in intake.RunAsync(new("hi"), cancellation.Token))
            {
                if (chatEvent is ChatEvent.TextDelta) await cancellation.CancelAsync();
            }
        });

        Assert.That(pipelineStopped, Is.True, "the gate is released only after the pipeline has stopped");
        gate.Verify(value => value.Release(), Times.Once);
        fallback.VerifyNoOtherCalls();
    }

    [Test]
    [CancelAfter(10_000)]
    public async Task RunAsync_TransportStopsReading_CancelsThePipelineAndReleasesGate(CancellationToken testToken)
    {
        var (intake, orchestrator, gate, _, _) = Pipeline();
        var pipelineCancelled = false;
        Answers(orchestrator, async (_, emit, token, onRouting) =>
        {
            await onRouting!(Answer.Routing!, null);
            await emit("partial");
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                pipelineCancelled = true;
                throw;
            }

            return Answer;
        });

        await foreach (var chatEvent in intake.RunAsync(new("hi"), testToken))
        {
            if (chatEvent is ChatEvent.TextDelta) break;
        }

        Assert.That(pipelineCancelled, Is.True);
        gate.Verify(value => value.Release(), Times.Once);
    }
}
