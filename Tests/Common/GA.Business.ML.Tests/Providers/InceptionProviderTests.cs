// Namespace deliberately NOT `GA.Business.ML.Tests.Providers` — that name shadows
// the production `GA.Business.ML.Providers` namespace for sibling test files that
// use `using Providers;` (OllamaProviderIntegrationTests.cs and
// HybridEmbeddingServiceIntegrationTests.cs). C# resolves the shorter name first,
// hiding OllamaProvider / HybridEmbeddingService from those tests and breaking
// `dotnet test` on the whole project with CS0103 / CS0246. Using a
// `Tests.Unit.ProviderTests` suffix keeps the file grouped under Providers/ on
// disk without colliding in the namespace tree.
namespace GA.Business.ML.Tests.Unit.ProviderTests;

using GA.Business.ML.Providers;
using Microsoft.Extensions.Configuration;

/// <summary>
///     Tests for the Inception Labs (Mercury 2) subagent provider — the config-gating
///     behavior, NOT live HTTP calls. The actual chat-completion path is exercised by
///     manual benchmarking (PowerShell script in the 2026-05-13 telemetry session) and
///     by the wider integration tests once <c>Inception:EnableForQueryExtraction</c>
///     is flipped to <c>true</c> in a CI run with a key.
/// </summary>
[TestFixture]
[Category("Unit")]
public class InceptionProviderTests
{
    private const string FakeApiKey = "sk_unit-test-not-a-real-key";

    private static IConfiguration BuildConfig(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void IsConfigured_WhenKeyMissing_ReturnsFalse()
    {
        var prev = Environment.GetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar, null);
            var cfg = BuildConfig(new Dictionary<string, string?>());
            Assert.That(InceptionProvider.IsConfigured(cfg), Is.False);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar, prev);
        }
    }

    [Test]
    public void IsConfigured_WhenKeyInConfig_ReturnsTrue()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Inception:ApiKey"] = FakeApiKey,
        });

        Assert.That(InceptionProvider.IsConfigured(cfg), Is.True);
    }

    [Test]
    public void IsConfigured_WhenKeyInEnvVar_ReturnsTrue()
    {
        var prev = Environment.GetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar, FakeApiKey);
            var cfg = BuildConfig(new Dictionary<string, string?>());
            Assert.That(InceptionProvider.IsConfigured(cfg), Is.True);
        }
        finally
        {
            Environment.SetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar, prev);
        }
    }

    [Test]
    public void IsEnabledForQueryExtraction_KeyAloneIsNotEnough()
    {
        // The two-flag gate prevents a stray INCEPTION_API_KEY env var (or an
        // operator pasting the key into config without realizing the implications)
        // from silently re-routing the chatbot's query extractor through Mercury.
        // Both `Inception:ApiKey` AND `Inception:EnableForQueryExtraction=true` must
        // be set — same opt-in shape as `AI:ChatProvider=claude`.
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Inception:ApiKey"] = FakeApiKey,
            // EnableForQueryExtraction deliberately omitted
        });

        Assert.That(InceptionProvider.IsEnabledForQueryExtraction(cfg), Is.False,
            "Key alone must NOT enable query-extraction routing — flag is the opt-in.");
    }

    [Test]
    public void IsEnabledForQueryExtraction_FlagAloneIsNotEnough()
    {
        var prev = Environment.GetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar, null);
            var cfg = BuildConfig(new Dictionary<string, string?>
            {
                ["Inception:EnableForQueryExtraction"] = "true",
                // No ApiKey
            });

            Assert.That(InceptionProvider.IsEnabledForQueryExtraction(cfg), Is.False,
                "Flag without a key must NOT enable — the chat client would throw at construction.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(InceptionProvider.ApiKeyEnvVar, prev);
        }
    }

    [Test]
    public void IsEnabledForQueryExtraction_BothKeyAndFlag_ReturnsTrue()
    {
        var cfg = BuildConfig(new Dictionary<string, string?>
        {
            ["Inception:ApiKey"] = FakeApiKey,
            ["Inception:EnableForQueryExtraction"] = "true",
        });

        Assert.That(InceptionProvider.IsEnabledForQueryExtraction(cfg), Is.True);
    }

    [Test]
    public void CreateChatClient_MissingKey_ThrowsAtConstruction()
    {
        // Eager-throw at construction surfaces a missing key as a host-startup error,
        // not as an opaque 500 on the first user query. Same lesson as PR #151 for
        // Anthropic. The DI registration uses `IsConfigured` to gate the keyed
        // singleton so production never hits this exception path unless someone
        // bypasses the helper.
        Assert.Throws<InvalidOperationException>(
            () => InceptionProvider.CreateChatClient(apiKey: string.Empty));
    }
}
