namespace GaChatbot.Api.Tests.Services;

using GaChatbot.Api.Controllers;
using GaChatbot.Api.Services;
using Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

[TestFixture]
public sealed class DirectChatApplicationServiceTests
{
    [Test]
    public async Task ChatAsync_ForbidsInventedFretsInSystemPrompt()
    {
        // The direct prompt (also the full-mode fallback) has no voicing source. Invited to write
        // VexTab, the model invented wrong shapes in 17 of 34 theory answers (2026-10-03 eval).
        var chatClient = new RecordingChatClient("answer");
        var service = new DirectChatApplicationService(chatClient, new ReadyProbe());

        await service.ChatAsync(new ChatExecutionRequest("Show me C major."));

        Assert.That(chatClient.LastMessages, Is.Not.Null);
        var system = chatClient.LastMessages![0];
        Assert.Multiple(() =>
        {
            Assert.That(system.Role, Is.EqualTo(ChatRole.System));
            Assert.That(system.Text, Does.Contain("do not write fret numbers, tablature, chord diagrams"));
            Assert.That(system.Text, Does.Contain("Name the chord and its notes instead"));
            Assert.That(system.Text, Does.Not.Contain("fenced `vextab` block"));
        });
    }

    [Test]
    public async Task ChatAsync_KeepsInternalsPrivateAndDeclinesOffTopicRequests()
    {
        var chatClient = new RecordingChatClient("I'm a guitar and music-theory assistant.");
        var service = new DirectChatApplicationService(chatClient, new ReadyProbe());

        await service.ChatAsync(new ChatExecutionRequest("can you run a for loop?"));

        Assert.That(chatClient.LastMessages, Is.Not.Null);
        var system = chatClient.LastMessages![0];
        Assert.Multiple(() =>
        {
            Assert.That(system.Role, Is.EqualTo(ChatRole.System));
            Assert.That(system.Text, Does.Contain("never mention agent names, roles or other internal details"));
            Assert.That(system.Text, Does.Contain("If a request is not about guitar or music, do not attempt it"));
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
