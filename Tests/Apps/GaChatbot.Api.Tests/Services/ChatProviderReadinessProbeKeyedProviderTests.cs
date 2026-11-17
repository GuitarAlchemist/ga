namespace GaChatbot.Api.Tests.Services;

using GA.Business.ML.Agents.Intents;
using GaChatbot.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Readiness for the hosted, key-based chat providers (Anthropic, Mistral).
/// Before these arms existed, <c>AI:ChatProvider=anthropic|mistral</c> reported
/// "Unsupported chat provider" and the readiness gate blocked every LLM answer.
/// </summary>
[TestFixture]
public class ChatProviderReadinessProbeKeyedProviderTests
{
    private const string AnthropicEnv = "ANTHROPIC_API_KEY";
    private const string MistralEnv = "MISTRAL_API_KEY";

    private string? _savedAnthropic;
    private string? _savedMistral;

    [SetUp]
    public void ClearAmbientKeys()
    {
        _savedAnthropic = Environment.GetEnvironmentVariable(AnthropicEnv);
        _savedMistral = Environment.GetEnvironmentVariable(MistralEnv);
        Environment.SetEnvironmentVariable(AnthropicEnv, null);
        Environment.SetEnvironmentVariable(MistralEnv, null);
    }

    [TearDown]
    public void RestoreAmbientKeys()
    {
        Environment.SetEnvironmentVariable(AnthropicEnv, _savedAnthropic);
        Environment.SetEnvironmentVariable(MistralEnv, _savedMistral);
    }

    [TestCase("anthropic")]
    [TestCase("claude")]
    [TestCase("Anthropic")]
    public async Task Anthropic_KeyInConfiguration_IsAvailable(string provider)
    {
        var probe = CreateProbe(new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = provider,
            ["Anthropic:ApiKey"] = "test-key",
            ["Anthropic:Model"] = "claude-test-model",
        });

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True, status.Message);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.Provider, Is.EqualTo(provider.ToLowerInvariant()));
            Assert.That(status.Message, Does.StartWith("Anthropic is configured."));
            Assert.That(status.ChatModel, Is.EqualTo("claude-test-model"));
            Assert.That(status.OrchestratorRoundTripOk, Is.True);
        });
    }

    [Test]
    public async Task Anthropic_KeyInEnvironment_IsAvailable_WithDefaultModel()
    {
        Environment.SetEnvironmentVariable(AnthropicEnv, "env-key");
        var probe = CreateProbe(new Dictionary<string, string?> { ["AI:ChatProvider"] = "anthropic" });

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True, status.Message);
            Assert.That(status.ChatModel, Is.EqualTo("claude-sonnet-5-5"));
        });
    }

    [Test]
    public async Task Anthropic_NoKey_IsUnavailable_AndNamesBothKeySources()
    {
        var probe = CreateProbe(new Dictionary<string, string?> { ["AI:ChatProvider"] = "anthropic" });

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.StartWith("Anthropic is not configured."));
            Assert.That(status.Message, Does.Contain(AnthropicEnv));
            Assert.That(status.Message, Does.Contain("Anthropic:ApiKey"));
            Assert.That(status.Message, Does.Not.Contain("Unsupported"));
        });
    }

    [TestCase("config")]
    [TestCase("env")]
    public async Task Mistral_KeyConfigured_IsAvailable(string source)
    {
        var values = new Dictionary<string, string?>
        {
            ["AI:ChatProvider"] = "mistral",
            ["Mistral:Model"] = "mistral-test-model",
        };
        if (source == "config")
        {
            values["Mistral:ApiKey"] = "test-key";
        }
        else
        {
            Environment.SetEnvironmentVariable(MistralEnv, "env-key");
        }

        var status = await CreateProbe(values).GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.True, status.Message);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.Provider, Is.EqualTo("mistral"));
            Assert.That(status.Message, Does.StartWith("Mistral is configured."));
            Assert.That(status.ChatModel, Is.EqualTo("mistral-test-model"));
        });
    }

    [Test]
    public async Task Mistral_NoKey_IsUnavailable()
    {
        var probe = CreateProbe(new Dictionary<string, string?> { ["AI:ChatProvider"] = "mistral" });

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.False);
            Assert.That(status.Message, Does.StartWith("Mistral is not configured."));
            Assert.That(status.Message, Does.Contain(MistralEnv));
        });
    }

    [Test]
    public async Task Anthropic_KeyConfigured_ButOrchestratorWedged_IsUnavailable()
    {
        // The key arm only answers "is the provider configured"; the round-trip
        // probe still gates IsAvailable exactly as for the other providers.
        var probe = CreateProbe(
            new Dictionary<string, string?>
            {
                ["AI:ChatProvider"] = "anthropic",
                ["Anthropic:ApiKey"] = "test-key",
            },
            intents: []);

        var status = await probe.GetStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.IsAvailable, Is.False);
            Assert.That(status.ProviderReachable, Is.True);
            Assert.That(status.OrchestratorRoundTripOk, Is.False);
            Assert.That(status.Message, Does.Contain("Round-trip:"));
        });
    }

    private static ChatProviderReadinessProbe CreateProbe(
        IReadOnlyDictionary<string, string?> values,
        IIntent[]? intents = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        foreach (var intent in intents ?? [new CatalogIntent()])
        {
            services.AddSingleton(intent);
        }

        return new ChatProviderReadinessProbe(
            configuration,
            new ThrowingHttpClientFactory(),
            services.BuildServiceProvider(),
            NullLogger<ChatProviderReadinessProbe>.Instance);
    }

    /// <summary>Key-based providers must not make an HTTP call to report readiness.</summary>
    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new AssertionException($"key-based readiness must not create HTTP client '{name}'");
    }

    private sealed class CatalogIntent : IIntent
    {
        public string Id => "skill.beginnerchords";
        public string Description => "catalog intent for tests";
        public IReadOnlyList<string> ExamplePrompts => [];
        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult("ok"));
    }
}
