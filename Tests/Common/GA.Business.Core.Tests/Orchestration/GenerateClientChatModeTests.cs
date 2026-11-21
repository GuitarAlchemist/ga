namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Clients;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Moq;

/// <summary>
/// With <c>AI:Generate:Provider=chat</c> the generate client runs its prompts through the
/// application's chat client (Anthropic in production and CI) instead of Ollama's
/// <c>/api/generate</c>, so the chatbot needs no local model server.
/// </summary>
[TestFixture]
public class GenerateClientChatModeTests
{
    private sealed record Filters(string? Key, string? Chord);

    [Test]
    public async Task ChatMode_SendsThePromptToTheChatClient_WithoutTemperature()
    {
        var (client, chat, sent) = Client("Dorian is the second mode.");

        var text = await client.GenerateAsync("Explain Dorian.", temperature: 0.7f, numPredict: 512);

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo("Dorian is the second mode."));
            Assert.That(sent[0].Messages.Single().Text, Is.EqualTo("Explain Dorian."));
            // Haiku 5.5 rejects non-default sampling parameters.
            Assert.That(sent[0].Options?.Temperature, Is.Null);
            Assert.That(sent[0].Options?.MaxOutputTokens, Is.EqualTo(512));
        });
        chat.VerifyAll();
    }

    [Test]
    public async Task ChatMode_Structured_ExtractsTheJsonObjectFromTheReply()
    {
        var (client, _, sent) = Client("Here you go:\n```json\n{\"key\":\"D minor\",\"chord\":\"Dm7\"}\n```");

        var filters = await client.GenerateStructuredAsync<Filters>("Extract filters.");

        Assert.Multiple(() =>
        {
            Assert.That(filters, Is.EqualTo(new Filters("D minor", "Dm7")));
            Assert.That(sent[0].Messages.Single().Text, Does.Contain("JSON"));
        });
    }

    [Test]
    public async Task ChatMode_Schema_IsWrittenIntoThePrompt()
    {
        var (client, _, sent) = Client("{\"key\":null,\"chord\":\"G7\"}");

        var filters = await client.GenerateStructuredAsync<Filters>("Extract filters.", new { type = "object", required = new[] { "chord" } });

        Assert.Multiple(() =>
        {
            Assert.That(filters?.Chord, Is.EqualTo("G7"));
            Assert.That(sent[0].Messages.Single().Text, Does.Contain("\"required\":[\"chord\"]"));
        });
    }

    [Test]
    public async Task WithoutProviderSetting_TheChatClientIsUsed()
    {
        // Ollama is being phased out: it is used only when AI:Generate:Provider=ollama.
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "From the chat client.")));
        var client = new OllamaGenerateClient(
            new Mock<IHttpClientFactory>(MockBehavior.Strict).Object, new ConfigurationBuilder().Build(), chat.Object);

        Assert.That(await client.GenerateAsync("Explain Dorian."), Is.EqualTo("From the chat client."));
    }

    [Test]
    public async Task ChatMode_ReplyWithoutJson_ReturnsNull()
    {
        var (client, _, _) = Client("I cannot help with that.");

        Assert.That(await client.GenerateStructuredAsync<Filters>("Extract filters."), Is.Null);
    }

    private static (OllamaGenerateClient Client, Mock<IChatClient> Chat, List<(List<ChatMessage> Messages, ChatOptions? Options)> Sent) Client(string reply)
    {
        List<(List<ChatMessage>, ChatOptions?)> sent = [];
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, o, _) => sent.Add(([.. m], o)))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Generate:Provider"] = "chat" })
            .Build();
        var client = new OllamaGenerateClient(new Mock<IHttpClientFactory>(MockBehavior.Strict).Object, configuration, chat.Object);
        return (client, chat, sent);
    }
}
