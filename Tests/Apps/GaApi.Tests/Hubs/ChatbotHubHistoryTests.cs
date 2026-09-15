namespace GaApi.Tests.Hubs;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Core.Functional;
using GaApi.Configuration;
using GaApi.Hubs;
using GaApi.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
///     The SignalR hub keeps per-connection history; these tests pin that it forwards
///     that history through <see cref="IChatIntake" />, matching the REST and AG-UI transports.
/// </summary>
[TestFixture]
public class ChatbotHubHistoryTests
{
    [Test]
    public async Task SendMessage_ForwardsPriorTurnsInOrder()
    {
        var received = new List<ChatIntakeRequest>();
        var hub = CreateHub(received, $"hub-history-{Guid.NewGuid():N}");

        await hub.SendMessage("  What is Dm7?  ");
        await hub.SendMessage("Now transpose it up a tone");

        Assert.That(received, Has.Count.EqualTo(2));
        Assert.That(received[0].History, Is.Null, "first message has no prior turns");
        Assert.That(received[1].SessionId, Is.EqualTo(received[0].SessionId));
        Assert.That(received[1].Message, Is.EqualTo("Now transpose it up a tone"));
        Assert.That(received[1].History, Is.Not.Null, "second message must carry prior turns");
        Assert.That(
            received[1].History!.Select(turn => (turn.Role, turn.Content)),
            Is.EqualTo(new[] { ("user", "What is Dm7?"), ("assistant", "Answer 1") }));
    }

    [Test]
    public async Task SendMessage_AfterClearHistory_ForwardsNoPriorTurns()
    {
        var received = new List<ChatIntakeRequest>();
        var hub = CreateHub(received, $"hub-history-{Guid.NewGuid():N}");

        await hub.SendMessage("What is Dm7?");
        await hub.ClearHistory();
        await hub.SendMessage("And G7?");

        Assert.That(received, Has.Count.EqualTo(2));
        Assert.That(received[1].History, Is.Null);
    }

    [Test]
    public async Task SendMessage_RejectedIntake_DoesNotRecordTurn()
    {
        var received = new List<ChatIntakeRequest>();
        var hub = CreateHub(received, $"hub-history-{Guid.NewGuid():N}", busyOnCall: 1);

        await hub.SendMessage("What is Dm7?");
        await hub.SendMessage("And G7?");

        Assert.That(received, Has.Count.EqualTo(2));
        Assert.That(received[1].History, Is.Null);
    }

    private static ChatbotHub CreateHub(List<ChatIntakeRequest> received, string connectionId, int? busyOnCall = null)
    {
        var intake = new Mock<IChatIntake>();
        intake.Setup(x => x.IntakeAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatIntakeRequest request, CancellationToken _) =>
            {
                received.Add(request);
                return received.Count == busyOnCall
                    ? Result<ChatResponse, ChatIntakeError>.Failure(new ChatIntakeError.Busy())
                    : Result<ChatResponse, ChatIntakeError>.Success(
                        new ChatResponse($"Answer {received.Count}", [], Routing: new("theory", 0.9f, "deterministic")));
            });

        var options = new Mock<IOptionsSnapshot<ChatbotOptions>>();
        options.Setup(x => x.Value).Returns(new ChatbotOptions());

        var context = new Mock<HubCallerContext>();
        context.Setup(x => x.ConnectionId).Returns(connectionId);
        context.Setup(x => x.ConnectionAborted).Returns(CancellationToken.None);

        var clients = new Mock<IHubCallerClients>();
        clients.Setup(x => x.Caller).Returns(Mock.Of<ISingleClientProxy>());

        return new ChatbotHub(
            NullLogger<ChatbotHub>.Instance,
            intake.Object,
            Mock.Of<IAgenticTraceCapture>(),
            new ChatbotSessionOrchestrator(options.Object),
            Mock.Of<ISemanticKnowledgeSource>())
        {
            Context = context.Object,
            Clients = clients.Object
        };
    }
}
