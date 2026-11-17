namespace GaApi.Tests.Services;

using GaApi.Services;
using Microsoft.Extensions.Configuration;

/// <summary>
///     Pins how GaApi reads <c>AI:ChatProvider</c>. An unrecognised value used to fall back to
///     Ollama without a word, so a deploy that set <c>anthropic</c> (the name the shared
///     GA.Business.ML registration and GaChatbot.Api accept) silently ran on Ollama.
/// </summary>
[TestFixture]
public class ChatProviderSelectionTests
{
    [TestCase("claude")]
    [TestCase("anthropic")]
    [TestCase("Anthropic")]
    public void ClaudeAndAnthropic_SelectTheAnthropicClient(string provider)
    {
        Assert.That(Select(provider).IsClaude, Is.True);
    }

    [TestCase("ollama")]
    [TestCase("docker")]
    public void LocalProviders_AreNotClaude(string provider)
    {
        Assert.That(Select(provider).IsClaude, Is.False);
    }

    [Test]
    public void MissingProvider_DefaultsToOllama()
    {
        Assert.That(Select(null).Provider, Is.EqualTo("ollama"));
    }

    [TestCase("mistral")]
    [TestCase("antropic")]
    public void UnsupportedProvider_FailsAtStartupNamingTheAcceptedValues(string provider)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Select(provider));

        Assert.That(error!.Message, Does.Contain(provider).And.Contain("anthropic").And.Contain("ollama"));
    }

    private static ChatProviderSelection Select(string? provider) =>
        ChatProviderSelection.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:ChatProvider"] = provider })
            .Build());
}
