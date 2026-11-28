namespace GaChatbot.Api.Tests.Services;

using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;
using GaChatbot.Api.Controllers;
using GaChatbot.Api.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;
using ChatResponse = GA.Business.Core.Orchestration.Models.ChatResponse;

/// <summary>
/// Pins the ungrounded no-invented-frets rule together with its user-supplied exception on every
/// ungrounded path: direct, routed (Tab profile) and the full-mode fallback through the real
/// <see cref="OrchestratedChatApplicationService"/>. These tests check what is sent to the model
/// (system prompt, forwarded history, actual route). They do not prove model behavior.
/// </summary>
[TestFixture]
public sealed class UserSuppliedFretsPromptTests
{
    private const string Supplied = "x32010";

    private static List<ConversationTurn> HistoryWithShape() =>
    [
        new("user", $"Here is my tab: {Supplied}", DateTimeOffset.UtcNow),
        new("assistant", "Noted.", DateTimeOffset.UtcNow)
    ];

    [Test]
    public async Task Direct_AllowsUserSuppliedFrets_InCurrentMessage()
    {
        var client = new RecordingChatClient();
        var service = new DirectChatApplicationService(client, new ReadyProbe());

        await service.ChatAsync(new ChatExecutionRequest($"identify {Supplied}"));

        AssertRule(client.LastMessages);
        Assert.That(client.LastMessages![^1].Text, Is.EqualTo($"identify {Supplied}"));
    }

    [Test]
    public async Task Direct_AllowsUserSuppliedFrets_InHistory()
    {
        var client = new RecordingChatClient();
        var service = new DirectChatApplicationService(client, new ReadyProbe());

        await service.ChatAsync(new ChatExecutionRequest("Is it right?", HistoryWithShape()));

        AssertRule(client.LastMessages);
        Assert.Multiple(() =>
        {
            Assert.That(client.LastMessages, Has.Count.EqualTo(4));
            Assert.That(client.LastMessages![1].Text, Does.Contain(Supplied));
            Assert.That(client.LastMessages![3].Text, Is.EqualTo("Is it right?"));
        });
    }

    [Test]
    public async Task Routed_TabProfile_AllowsUserSuppliedFrets_InCurrentMessage()
    {
        var client = new RecordingChatClient();
        var service = CreateRouted(client);

        var result = await service.ChatAsync(
            new ChatExecutionRequest($"What notes are in this tab {Supplied}?"));

        Assert.That(result.Routing.AgentId, Is.EqualTo("tab-lite"), "the request must take the Tab route");
        AssertRule(client.LastMessages);
        Assert.That(client.LastMessages![0].Text, Does.Contain("Tab mode:"));
    }

    [Test]
    public async Task Routed_TabProfile_AllowsUserSuppliedFrets_InHistory()
    {
        var client = new RecordingChatClient();
        var service = CreateRouted(client);

        var result = await service.ChatAsync(new ChatExecutionRequest("Is it right?", HistoryWithShape()));

        Assert.That(result.Routing.AgentId, Is.EqualTo("tab-lite"), "history must carry the follow-up to the Tab route");
        AssertRule(client.LastMessages);
        Assert.That(client.LastMessages![1].Text, Does.Contain(Supplied));
    }

    [TestCase("error")]
    [TestCase("blank")]
    public async Task FullModeFallback_AllowsUserSuppliedFrets_InCurrentMessageAndHistory(string cause)
    {
        var client = new RecordingChatClient();
        var orchestrator = cause == "error"
            ? new StubOrchestrator(exception: new InvalidOperationException("boom"))
            : new StubOrchestrator(new ChatResponse("", [], Routing: new AgentRoutingMetadata("theory", 0.9f, "semantic")));
        var service = CreateOrchestrated(orchestrator, client);

        var result = await service.ChatAsync(
            new ChatExecutionRequest($"identify {Supplied}", HistoryWithShape()));

        Assert.That(result.Routing.AgentId, Is.EqualTo("fallback-direct"), "the answer must come from the direct fallback");
        AssertRule(client.LastMessages);
        Assert.Multiple(() =>
        {
            Assert.That(client.LastMessages![1].Text, Does.Contain(Supplied));
            Assert.That(client.LastMessages![^1].Text, Is.EqualTo($"identify {Supplied}"));
        });
    }

    /// <summary>
    /// Adversarial: the user asks the model to complete or fix a shape. The prompt must still forbid
    /// inventing, completing or changing frets on every ungrounded path.
    /// </summary>
    [TestCase("direct", "Complete this tab: x32")]
    [TestCase("direct", "Fix my tab x32010 so it is a G7")]
    [TestCase("routed", "Complete this tab: x32")]
    [TestCase("fallback", "Complete this tab: x32")]
    public async Task CompletionRequests_StillForbidInventingFrets(string path, string message)
    {
        var client = new RecordingChatClient();
        switch (path)
        {
            case "direct":
                await new DirectChatApplicationService(client, new ReadyProbe())
                    .ChatAsync(new ChatExecutionRequest(message));
                break;
            case "routed":
                await CreateRouted(client).ChatAsync(new ChatExecutionRequest(message));
                break;
            default:
                await CreateOrchestrated(new StubOrchestrator(exception: new InvalidOperationException("boom")), client)
                    .ChatAsync(new ChatExecutionRequest(message));
                break;
        }

        AssertRule(client.LastMessages);
        Assert.That(client.LastMessages![^1].Text, Is.EqualTo(message));
    }

    private static void AssertRule(List<AiChatMessage>? messages)
    {
        Assert.That(messages, Is.Not.Null);
        var system = messages![0];
        var text = Regex.Replace(system.Text, @"\s+", " "); // prompt lines wrap
        Assert.Multiple(() =>
        {
            Assert.That(system.Role, Is.EqualTo(ChatRole.System));
            // Prohibition kept, scoped to the model's own frets.
            Assert.That(text, Does.Contain("do not write fret numbers, tablature, chord diagrams"));
            Assert.That(text, Does.Contain("`vextab` blocks of your own"));
            // Exception for exactly what the user typed, current message or earlier.
            Assert.That(text, Does.Contain("The only frets you may write are ones the user typed, in this message or earlier ones"));
            Assert.That(text, Does.Contain("repeat them exactly as typed"));
            // Completion / correction stays forbidden.
            Assert.That(text, Does.Contain("Never add, complete, extend, correct or change fret numbers, even when asked"));
            // Never the grounded VexTab contract.
            Assert.That(text, Does.Not.Contain("fenced `vextab` block"));
        });
    }

    private static RoutedChatApplicationService CreateRouted(IChatClient client) =>
        new(client, new ReadyProbe(), new LightweightChatRouter(), new LightweightTheorySanityChecker());

    private static OrchestratedChatApplicationService CreateOrchestrated(StubOrchestrator orchestrator, IChatClient directClient)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Chatbot:StreamTimeoutSeconds"] = "5",
                ["Chatbot:FallbackMinConfidence"] = "0.25"
            })
            .Build();

        return new OrchestratedChatApplicationService(
            orchestrator,
            new ReadyProbe(),
            new DirectChatApplicationService(directClient, new ReadyProbe()),
            configuration,
            NullLogger<OrchestratedChatApplicationService>.Instance);
    }

    private sealed class StubOrchestrator(ChatResponse? response = null, Exception? exception = null)
        : IProductionChatOrchestratorClient
    {
        public Task<ChatResponse> AnswerAsync(ChatExecutionRequest request, CancellationToken cancellationToken = default) =>
            exception is not null ? throw exception : Task.FromResult(response!);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<AiChatMessage>? LastMessages { get; private set; }

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = [.. messages];
            return Task.FromResult(new AiChatResponse(new AiChatMessage(ChatRole.Assistant, "answer")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class ReadyProbe : IChatProviderReadinessProbe
    {
        public Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatbotStatus { IsAvailable = true, Message = "ready", Timestamp = DateTime.UtcNow });
    }
}
