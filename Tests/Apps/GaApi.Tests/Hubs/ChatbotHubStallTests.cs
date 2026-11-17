namespace GaApi.Tests.Hubs;

using System.Runtime.CompilerServices;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GaApi.Configuration;
using GaApi.Hubs;
using GaApi.Services;
using GaApi.Tests.Controllers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// The hub's budget bounds a stalled run, not a long answer. A run that produces no text
/// within the budget must reach the caller as <c>Error</c>: the hub used to cancel it after
/// 25 s and only log, so the page waited forever with no chunk, no MessageComplete, no Error.
/// </summary>
[TestFixture]
public class ChatbotHubStallTests
{
    private static readonly TimeSpan StallTimeout = TimeSpan.FromMilliseconds(300);

    [Test]
    [CancelAfter(10_000)]
    public async Task SendMessage_NoTextWithinTheBudget_SendsError(CancellationToken cancellationToken)
    {
        var caller = new ChatbotStreamingTests.RecordingCaller();
        var hub = CreateHub(caller, (runId, token) => Run(token, Routing(runId), Stall()));

        await hub.SendMessage("Why does a tritone substitution work?").WaitAsync(cancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(caller.Methods, Is.EqualTo(new[] { "MessageRoutingMetadata", "Error" }));
            Assert.That((string?)caller.Calls[^1].Args[0], Does.Contain("too long"));
        });
    }

    [Test]
    [CancelAfter(10_000)]
    public async Task SendMessage_AnswerLongerThanTheBudgetThatKeepsStreaming_Completes(CancellationToken cancellationToken)
    {
        var caller = new ChatbotStreamingTests.RecordingCaller();
        var pause = StallTimeout / 2;
        var hub = CreateHub(caller, (runId, token) => Run(token,
            Routing(runId),
            Text(runId, "A tritone ", pause), Text(runId, "substitute ", pause), Text(runId, "shares ", pause),
            Text(runId, "the guide tones.", pause),
            Done(runId, "A tritone substitute shares the guide tones.")));

        await hub.SendMessage("Why does a tritone substitution work?").WaitAsync(cancellationToken);

        Assert.That(caller.Methods, Is.EqualTo(new[]
        {
            "MessageRoutingMetadata", "ReceiveMessageChunk", "ReceiveMessageChunk", "ReceiveMessageChunk",
            "ReceiveMessageChunk", "MessageComplete",
        }));
    }

    [Test]
    [CancelAfter(10_000)]
    public async Task SendMessage_ClientDisconnects_SendsNoError(CancellationToken cancellationToken)
    {
        var caller = new ChatbotStreamingTests.RecordingCaller();
        using var connection = new CancellationTokenSource();
        var hub = CreateHub(caller, (runId, token) => Run(token, Routing(runId), Stall()), connection.Token);

        var send = hub.SendMessage("Why does a tritone substitution work?");
        await connection.CancelAsync();
        await send.WaitAsync(cancellationToken);

        Assert.That(caller.Methods, Does.Not.Contain("Error"));
    }

    private static Func<CancellationToken, Task<ChatEvent?>> Routing(string runId) =>
        _ => Task.FromResult<ChatEvent?>(new ChatEvent.Routing(runId, "theory", 0.9f, "semantic", null));

    private static Func<CancellationToken, Task<ChatEvent?>> Text(string runId, string text, TimeSpan delay) =>
        async token =>
        {
            await Task.Delay(delay, token);
            return new ChatEvent.TextDelta(runId, text);
        };

    private static Func<CancellationToken, Task<ChatEvent?>> Done(string runId, string answer) =>
        _ => Task.FromResult<ChatEvent?>(new ChatEvent.Completed(runId, new ChatResponse(answer, [])));

    /// <summary>A model that never answers: waits until the run is cancelled.</summary>
    private static Func<CancellationToken, Task<ChatEvent?>> Stall() =>
        async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        };

    private static async IAsyncEnumerable<ChatEvent> Run(
        [EnumeratorCancellation] CancellationToken cancellationToken,
        params Func<CancellationToken, Task<ChatEvent?>>[] steps)
    {
        foreach (var step in steps)
        {
            if (await step(cancellationToken) is { } chatEvent) yield return chatEvent;
        }
    }

    private static ChatbotHub CreateHub(
        ChatbotStreamingTests.RecordingCaller caller,
        Func<string, CancellationToken, IAsyncEnumerable<ChatEvent>> run,
        CancellationToken connectionAborted = default)
    {
        var intake = new Mock<IChatIntake>();
        intake.Setup(value => value.RunAsync(It.IsAny<ChatIntakeRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ChatIntakeRequest _, CancellationToken token) => run("hub-run", token));
        var sessionOptions = new Mock<IOptionsSnapshot<ChatbotOptions>>();
        sessionOptions.Setup(value => value.Value).Returns(new ChatbotOptions());

        return new ChatbotHub(
            NullLogger<ChatbotHub>.Instance,
            intake.Object,
            Mock.Of<IAgenticTraceCapture>(),
            new ChatbotSessionOrchestrator(sessionOptions.Object),
            Mock.Of<ISemanticKnowledgeSource>(),
            Options.Create(new ChatbotHubOptions { StallTimeout = StallTimeout }))
        {
            Context = ChatbotStreamingTests.HubContext($"hub-stall-{Guid.NewGuid():N}", connectionAborted),
            Clients = ChatbotStreamingTests.HubClients(caller),
        };
    }
}
