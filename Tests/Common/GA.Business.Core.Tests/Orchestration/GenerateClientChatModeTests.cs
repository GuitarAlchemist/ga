namespace GA.Business.Core.Tests.Orchestration;

using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// The default <see cref="ITextGenerator"/> runs its prompts through the application's chat client
/// (Anthropic in production and CI) instead of Ollama's <c>/api/generate</c>, so the chatbot needs no
/// local model server. Ollama is selected only with <c>AI:Generate:Provider=ollama</c>, or when no
/// chat client is registered.
/// </summary>
[TestFixture]
public class GenerateClientChatModeTests
{
    private sealed record Filters(string? Key, string? Chord);

    private static readonly object FiltersSchema = new { type = "object", required = new[] { "chord" } };

    [Test]
    public async Task ChatMode_SendsThePromptToTheChatClient_WithoutTemperature()
    {
        var (generator, chat, sent) = Generator("Dorian is the second mode.");

        var text = await generator.GenerateAsync("Explain Dorian.", temperature: 0.7f, numPredict: 512);

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
        var (generator, _, sent) = Generator("Here you go:\n```json\n{\"key\":\"D minor\",\"chord\":\"Dm7\"}\n```");

        var filters = await generator.GenerateStructuredAsync<Filters>("Extract filters.", FiltersSchema);

        Assert.Multiple(() =>
        {
            Assert.That(filters, Is.EqualTo(new Filters("D minor", "Dm7")));
            Assert.That(sent[0].Messages.Single().Text, Does.Contain("JSON"));
        });
    }

    [Test]
    public async Task ChatMode_Schema_IsWrittenIntoThePrompt()
    {
        var (generator, _, sent) = Generator("{\"key\":null,\"chord\":\"G7\"}");

        var filters = await generator.GenerateStructuredAsync<Filters>("Extract filters.", FiltersSchema);

        Assert.Multiple(() =>
        {
            Assert.That(filters?.Chord, Is.EqualTo("G7"));
            Assert.That(
                sent[0].Messages.Single().Text,
                Is.EqualTo("Extract filters.\n\nRespond with a single JSON object matching this JSON Schema, and nothing else:\n"
                           + "{\"type\":\"object\",\"required\":[\"chord\"]}"));
        });
    }

    [Test]
    public async Task ChatMode_ReplyWithoutJson_ReturnsNull()
    {
        var (generator, _, _) = Generator("I cannot help with that.");

        Assert.That(await generator.GenerateStructuredAsync<Filters>("Extract filters.", FiltersSchema), Is.Null);
    }

    [Test]
    public async Task WithoutProviderSetting_TheChatClientIsUsed()
    {
        // Ollama is being phased out: it is used only when AI:Generate:Provider=ollama.
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "From the chat client.")));

        var generator = Resolve(provider: null, chat.Object);
        var text = await generator.GenerateAsync("Explain Dorian.");

        Assert.Multiple(() =>
        {
            Assert.That(generator, Is.InstanceOf<ChatClientTextGenerator>());
            Assert.That(text, Is.EqualTo("From the chat client."));
        });
    }

    [TestCase(null)]
    [TestCase("chat")]
    [TestCase("anthropic")]
    public void WithAChatClient_OnlyProviderOllamaSelectsTheOllamaAdapter(string? provider) =>
        Assert.That(Resolve(provider, new Mock<IChatClient>().Object), Is.InstanceOf<ChatClientTextGenerator>());

    [TestCase("ollama")]
    [TestCase("Ollama")]
    public void ProviderOllama_SelectsTheOllamaAdapter_EvenWithAChatClient(string provider) =>
        Assert.That(Resolve(provider, new Mock<IChatClient>().Object), Is.InstanceOf<OllamaGenerateClient>());

    [Test]
    public void WithoutAChatClient_FallsBackToTheOllamaAdapter() =>
        Assert.That(Resolve(provider: null, chatClient: null), Is.InstanceOf<OllamaGenerateClient>());

    /// <summary>Resolves <see cref="ITextGenerator"/> through the production composition root.</summary>
    private static ITextGenerator Resolve(string? provider, IChatClient? chatClient)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Generate:Provider"] = provider })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        if (chatClient is not null) services.AddSingleton(chatClient);
        services.AddChatbotOrchestration();
        return services.BuildServiceProvider().GetRequiredService<ITextGenerator>();
    }

    private static (ITextGenerator Generator, Mock<IChatClient> Chat, List<(List<ChatMessage> Messages, ChatOptions? Options)> Sent) Generator(string reply)
    {
        List<(List<ChatMessage>, ChatOptions?)> sent = [];
        var chat = new Mock<IChatClient>();
        chat.Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((m, o, _) => sent.Add(([.. m], o)))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        return (new ChatClientTextGenerator(chat.Object), chat, sent);
    }
}
