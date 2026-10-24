namespace GaApi.Tests.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GaApi.Services;
using GaApi.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

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
        var provider = new Mock<IChatService>();
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        services.AddSingleton(provider.Object);
        using var container = services.BuildServiceProvider();
        var result = await container.GetRequiredService<IChatReadinessProbe>().CheckAsync();
        Assert.That(result.IsReady, Is.EqualTo(!providerCheck),
            "provider-free deterministic routes must not be blocked by the default registration");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Readiness_UsesConfiguredProviderAvailability(bool available)
    {
        var provider = new Mock<IChatService>();
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(available);
        var result = await new GaApiChatReadinessProbe(provider.Object).CheckAsync();
        Assert.That(result.IsReady, Is.EqualTo(available));
    }

    [Test]
    public async Task Readiness_ProviderFailure_FailsClosed()
    {
        var provider = new Mock<IChatService>();
        provider.Setup(value => value.IsAvailableAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("offline"));
        var result = await new GaApiChatReadinessProbe(provider.Object).CheckAsync();
        Assert.That(result.IsReady, Is.False);
    }

    [Test]
    public async Task Readiness_NonCooperativeProvider_IsBounded()
    {
        var provider = new Mock<IChatService>();
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
        Assert.ThrowsAsync<OperationCanceledException>(() => new GaApiChatReadinessProbe(Mock.Of<IChatService>()).CheckAsync(cancellation.Token));
    }

    [Test]
    public async Task Fallback_UsesSelectedProviderAndCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new Mock<IChatService>();
        provider.Setup(value => value.ChatAsync("Dm7", null, null, cancellation.Token)).ReturnsAsync("D F A C");
        var result = await new GaApiFallbackChatHandler(provider.Object).AnswerAsync("Dm7", null, cancellation.Token);
        Assert.That(result, Is.EqualTo("D F A C"));
    }

    [Test]
    public async Task Fallback_ForwardsHistoryToProviderInOrder()
    {
        List<ChatMessage>? received = null;
        var provider = new Mock<IChatService>();
        provider.Setup(value => value.ChatAsync("Which scale fits?", It.IsAny<List<ChatMessage>?>(), null, It.IsAny<CancellationToken>()))
            .Callback<string, List<ChatMessage>?, string?, CancellationToken>((_, history, _, _) => received = history)
            .ReturnsAsync("Try D Dorian.");
        ConversationTurn[] history =
        [
            new("user", "I am playing Dm7.", DateTimeOffset.UnixEpoch),
            new("assistant", "Its notes are D F A C.", DateTimeOffset.UnixEpoch),
        ];

        await new GaApiFallbackChatHandler(provider.Object).AnswerAsync("Which scale fits?", history);

        Assert.That(received, Is.Not.Null);
        Assert.That(received!.Select(message => (message.Role, message.Content)),
            Is.EqualTo(new[] { ("user", "I am playing Dm7."), ("assistant", "Its notes are D F A C.") }));
    }
}
