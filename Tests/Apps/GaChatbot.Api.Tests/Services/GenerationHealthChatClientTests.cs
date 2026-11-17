namespace GaChatbot.Api.Tests.Services;

using GA.Business.ML.Providers;
using GaChatbot.Api.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The host wraps its registered chat client so every LLM generation feeds the
/// readiness probe. The wrapper itself is tested in GA.Business.ML.Tests.
/// </summary>
[TestFixture]
public sealed class GenerationHealthChatClientTests
{
    [Test]
    public void AddMinimalChatbotApi_WrapsTheRegisteredChatClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Chatbot:Mode"] = "direct",
                ["AI:ChatProvider"] = "ollama",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMinimalChatbotApi(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetRequiredService<IChatClient>(), Is.InstanceOf<GenerationHealthChatClient>());
    }
}
