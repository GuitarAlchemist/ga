namespace GaChatbot.Api.Tests.Services;

using GaChatbot.Api.Controllers;
using GaChatbot.Api.Services;
using Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;
using ConversationTurn = GA.Business.Core.Orchestration.Models.ConversationTurn;

[TestFixture]
public sealed class RoutedChatApplicationServiceTests
{
    [Test]
    public async Task ChatAsync_RoutesShortFollowUpFromRecentHistory()
    {
        var chatClient = new RecordingChatClient("Minor means the third is lowered.");
        var service = new RoutedChatApplicationService(
            chatClient,
            new ReadyProbe(),
            new LightweightChatRouter(),
            new LightweightTheorySanityChecker());
        var history = new List<ConversationTurn>
        {
            new("user", "Explain the C major chord.", DateTimeOffset.UtcNow),
            new("assistant", "C major contains C, E, and G.", DateTimeOffset.UtcNow)
        };

        var result = await service.ChatAsync(new ChatExecutionRequest("What about minor?", history));

        Assert.Multiple(() =>
        {
            Assert.That(result.Routing.AgentId, Is.EqualTo("theory-lite"));
            Assert.That(result.Routing.RoutingMethod, Is.EqualTo("lightweight-router-context"));
            Assert.That(result.NaturalLanguageAnswer, Is.EqualTo("Minor means the third is lowered."));
        });
    }

    [Test]
    public async Task ChatAsync_SendsHistoryOnceBeforeCurrentUserMessage()
    {
        var chatClient = new RecordingChatClient("answer");
        var service = new RoutedChatApplicationService(
            chatClient,
            new ReadyProbe(),
            new LightweightChatRouter(),
            new LightweightTheorySanityChecker());
        var history = new List<ConversationTurn>
        {
            new("user", "Explain C major.", DateTimeOffset.UtcNow),
            new("assistant", "C major contains C, E, and G.", DateTimeOffset.UtcNow)
        };

        await service.ChatAsync(new ChatExecutionRequest("What about minor?", history));

        Assert.That(chatClient.LastMessages, Is.Not.Null);
        var messages = chatClient.LastMessages!;
        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Count.EqualTo(4));
            Assert.That(messages[0].Role, Is.EqualTo(ChatRole.System));
            Assert.That(messages[0].Text, Does.Contain("fenced `vextab` block"));
            Assert.That(messages[0].Text, Does.Contain("string 6 = low E"));
            Assert.That(messages[1].Role, Is.EqualTo(ChatRole.User));
            Assert.That(messages[1].Text, Is.EqualTo("Explain C major."));
            Assert.That(messages[2].Role, Is.EqualTo(ChatRole.Assistant));
            Assert.That(messages[2].Text, Is.EqualTo("C major contains C, E, and G."));
            Assert.That(messages[3].Role, Is.EqualTo(ChatRole.User));
            Assert.That(messages[3].Text, Is.EqualTo("What about minor?"));
            Assert.That(messages.Count(message => message.Text == "What about minor?"), Is.EqualTo(1));
        });
    }

    private sealed class RecordingChatClient(string responseText) : IChatClient
    {
        public List<AiChatMessage>? LastMessages { get; private set; }

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<AiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastMessages = [.. messages];
            return Task.FromResult(new AiChatResponse(new AiChatMessage(ChatRole.Assistant, responseText)));
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
            Task.FromResult(new ChatbotStatus
            {
                IsAvailable = true,
                Message = "ready",
                Timestamp = DateTime.UtcNow
            });
    }
}
