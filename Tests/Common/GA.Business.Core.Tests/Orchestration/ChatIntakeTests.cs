namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using Moq;

/// <summary>
/// Unit tests for <see cref="ChatIntake"/> — the chat intake seam (campaign slice #1).
/// Locks the seam's contract: empty input rejects before the gate, a full gate yields
/// <see cref="ChatIntakeError.Busy"/> without dispatching, the happy path forwards the
/// trimmed message + opaque session id, and the gate is always released.
/// </summary>
[TestFixture]
public class ChatIntakeTests
{
    private static ChatResponse SampleResponse() =>
        new(NaturalLanguageAnswer: "ok",
            Candidates: [],
            Routing: new AgentRoutingMetadata("theory", 0.9f, "semantic"));

    private static (ChatIntake intake, Mock<IChatApplicationService> service, Mock<ILlmConcurrencyGate> gate)
        MakeIntake(bool gateOpen = true, ChatResponse? response = null)
    {
        var service = new Mock<IChatApplicationService>();
        service.Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response ?? SampleResponse());

        var gate = new Mock<ILlmConcurrencyGate>();
        gate.Setup(g => g.TryEnterAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateOpen);

        return (new ChatIntake(service.Object, gate.Object), service, gate);
    }

    [Test]
    public async Task IntakeAsync_EmptyMessage_RejectsBeforeGate()
    {
        var (intake, service, gate) = MakeIntake();

        var result = await intake.IntakeAsync(new ChatIntakeRequest("", "sess-1"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.GetErrorOrThrow(), Is.InstanceOf<ChatIntakeError.Validation>());
        });
        gate.Verify(g => g.TryEnterAsync(It.IsAny<CancellationToken>()), Times.Never,
            "validation must short-circuit before the gate is touched");
        service.Verify(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task IntakeAsync_WhitespaceMessage_Rejects()
    {
        var (intake, _, _) = MakeIntake();

        var result = await intake.IntakeAsync(new ChatIntakeRequest("   ", "sess-1"));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Is.InstanceOf<ChatIntakeError.Validation>());
    }

    [Test]
    public async Task IntakeAsync_GateFull_ReturnsBusy_WithoutDispatching()
    {
        var (intake, service, gate) = MakeIntake(gateOpen: false);

        var result = await intake.IntakeAsync(new ChatIntakeRequest("hi", "sess-1"));

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.GetErrorOrThrow(), Is.InstanceOf<ChatIntakeError.Busy>());
        service.Verify(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        gate.Verify(g => g.Release(), Times.Never, "a gate that was never entered must not be released");
    }

    [Test]
    public async Task IntakeAsync_HappyPath_ForwardsTrimmedMessageAndSessionId_AndReleasesGate()
    {
        var (intake, service, gate) = MakeIntake();
        ChatRequest? dispatched = null;
        service.Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((req, _) => dispatched = req)
            .ReturnsAsync(SampleResponse());

        var result = await intake.IntakeAsync(new ChatIntakeRequest("  what is a tritone?  ", "sess-42"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.GetValueOrThrow().NaturalLanguageAnswer, Is.EqualTo("ok"));
            Assert.That(dispatched, Is.Not.Null);
            Assert.That(dispatched!.Message, Is.EqualTo("what is a tritone?"), "message must be trimmed");
            Assert.That(dispatched.SessionId, Is.EqualTo("sess-42"), "opaque session id must be forwarded");
        });
        gate.Verify(g => g.Release(), Times.Once);
    }

    [Test]
    public async Task IntakeAsync_ForwardsConversationHistory()
    {
        var (intake, service, _) = MakeIntake();
        ChatRequest? dispatched = null;
        service.Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((req, _) => dispatched = req)
            .ReturnsAsync(SampleResponse());
        var history = new List<ConversationTurn>
        {
            new("user", "earlier question", DateTimeOffset.UnixEpoch),
            new("assistant", "earlier answer", DateTimeOffset.UnixEpoch),
        };

        await intake.IntakeAsync(new ChatIntakeRequest("follow-up", "sess-7", history));

        Assert.That(dispatched, Is.Not.Null);
        Assert.That(dispatched!.History, Is.EqualTo(history), "conversation history must reach the orchestrator");
    }

    [Test]
    public void IntakeAsync_DispatchThrows_StillReleasesGate()
    {
        var (intake, service, gate) = MakeIntake();
        service.Setup(s => s.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => intake.IntakeAsync(new ChatIntakeRequest("hi", "sess-1")));

        gate.Verify(g => g.Release(), Times.Once, "gate must be released even when dispatch throws");
    }
}
