namespace GaApi.Tests.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GaApi.Services;
using GaApi.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;

[TestFixture]
public class ChatProviderAdapterTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Registration_ProviderGateIsExplicitlyOptIn(bool providerCheck)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "ollama",
            ["Chatbot:Readiness:ProviderCheck"] = providerCheck.ToString()
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAiServices(configuration);
        var provider = new Mock<IChatProviderAvailability>();
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        services.AddSingleton(provider.Object);
        using var container = services.BuildServiceProvider();
        var result = await container.GetRequiredService<IChatReadinessProbe>().CheckAsync();
        Assert.That(result.IsReady, Is.EqualTo(!providerCheck),
            "provider-free deterministic routes must not be blocked by the default registration");
    }

    [TestCase("ollama")]
    [TestCase("docker")]
    public void Registration_ChatClient_HasFunctionInvocation(string chatProvider)
    {
        using var container = BuildAiContainer(chatProvider);

        var client = container.GetRequiredService<IChatClient>();

        Assert.That(client.GetService<FunctionInvokingChatClient>(), Is.Not.Null,
            "tool-calling skills need the same function-invocation pipeline as the Anthropic client");
    }

    [Test]
    public void Registration_OllamaChatClient_UsesChatbotModel()
    {
        using var container = BuildAiContainer("ollama", chatbotModel: "llama3.2:1b");

        var client = container.GetRequiredService<IChatClient>();

        Assert.That(client.GetService<ChatClientMetadata>()?.DefaultModelId, Is.EqualTo("llama3.2:1b"),
            "Chatbot:Model selects the Ollama chat model, as OllamaChatService did");
    }

    private static ServiceProvider BuildAiContainer(string chatProvider, string chatbotModel = "llama3.2:3b")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = chatProvider,
            ["Chatbot:Model"] = chatbotModel,
            ["Ollama:ChatModel"] = "ignored-when-chatbot-model-is-set"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAiServices(configuration);
        return services.BuildServiceProvider();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Readiness_UsesConfiguredProviderAvailability(bool available)
    {
        var provider = new Mock<IChatProviderAvailability>();
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(available);
        var result = await new GaApiChatReadinessProbe(provider.Object).CheckAsync();
        Assert.That(result.IsReady, Is.EqualTo(available));
    }

    [Test]
    public async Task Readiness_ProviderFailure_FailsClosed()
    {
        var provider = new Mock<IChatProviderAvailability>();
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("offline"));
        var result = await new GaApiChatReadinessProbe(provider.Object).CheckAsync();
        Assert.That(result.IsReady, Is.False);
    }

    [Test]
    public async Task Readiness_NonCooperativeProvider_IsBounded()
    {
        var provider = new Mock<IChatProviderAvailability>();
        var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).Returns(pending.Task);
        var result = await new GaApiChatReadinessProbe(provider.Object).CheckAsync().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.That(result.IsReady, Is.False);
        Assert.That(result.Reason, Does.Contain("timed out"));
        pending.SetResult(true);
    }

    [Test]
    public void Readiness_CallerCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => new GaApiChatReadinessProbe(Mock.Of<IChatProviderAvailability>()).CheckAsync(cancellation.Token));
    }

    [Test]
    public async Task Fallback_UsesSelectedProviderAndCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<IChatClient>();
        client.Setup(value => value.GetResponseAsync(
                It.Is<IEnumerable<AiChatMessage>>(messages => messages.Single().Role == ChatRole.User && messages.Single().Text == "Dm7"),
                It.IsAny<ChatOptions?>(),
                cancellation.Token))
            .ReturnsAsync(new AiChatResponse(new AiChatMessage(ChatRole.Assistant, "D F A C")));
        var result = await new GaApiFallbackChatHandler(client.Object).AnswerAsync("Dm7", null, cancellation.Token);
        Assert.That(result, Is.EqualTo("D F A C"));
    }

    [Test]
    public async Task Fallback_ForwardsHistoryToProviderInOrder()
    {
        List<AiChatMessage>? received = null;
        var client = new Mock<IChatClient>();
        client.Setup(value => value.GetResponseAsync(It.IsAny<IEnumerable<AiChatMessage>>(), It.IsAny<ChatOptions?>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<AiChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) => received = [.. messages])
            .ReturnsAsync(new AiChatResponse(new AiChatMessage(ChatRole.Assistant, "Try D Dorian.")));
        ConversationTurn[] history =
        [
            new("user", "I am playing Dm7.", DateTimeOffset.UnixEpoch),
            new("assistant", "Its notes are D F A C.", DateTimeOffset.UnixEpoch),
        ];

        await new GaApiFallbackChatHandler(client.Object).AnswerAsync("Which scale fits?", history);

        Assert.That(received, Is.Not.Null);
        Assert.That(received!.Select(message => (message.Role, message.Text)),
            Is.EqualTo(new[]
            {
                (ChatRole.User, "I am playing Dm7."),
                (ChatRole.Assistant, "Its notes are D F A C."),
                (ChatRole.User, "Which scale fits?")
            }));
    }
}
