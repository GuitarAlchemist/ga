namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Providers.Mistral;
using Microsoft.Extensions.Configuration;

[TestFixture]
public class MistralProviderTests
{
    private string? _savedEnv;

    [SetUp]
    public void SaveEnv()
    {
        _savedEnv = Environment.GetEnvironmentVariable(MistralProvider.ApiKeyEnvVar);
        Environment.SetEnvironmentVariable(MistralProvider.ApiKeyEnvVar, null);
    }

    [TearDown]
    public void RestoreEnv() =>
        Environment.SetEnvironmentVariable(MistralProvider.ApiKeyEnvVar, _savedEnv);

    private static IConfiguration Config(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public void ResolveApiKey_PrefersConfigOverEnv()
    {
        Environment.SetEnvironmentVariable(MistralProvider.ApiKeyEnvVar, "env-value");

        var key = MistralProvider.ResolveApiKey(Config(new Dictionary<string, string?>
        {
            ["Mistral:ApiKey"] = "config-value",
        }));

        Assert.That(key, Is.EqualTo("config-value"));
    }

    [Test]
    public void ResolveApiKey_FallsBackToEnv_WhenConfigMissing()
    {
        Environment.SetEnvironmentVariable(MistralProvider.ApiKeyEnvVar, "env-value");

        var key = MistralProvider.ResolveApiKey(Config(new Dictionary<string, string?>()));

        Assert.That(key, Is.EqualTo("env-value"));
    }

    [Test]
    public void ResolveApiKey_ReturnsNull_WhenNeitherSet()
    {
        var key = MistralProvider.ResolveApiKey(Config(new Dictionary<string, string?>()));
        Assert.That(key, Is.Null);
    }

    [Test]
    public void ResolveApiKey_TreatsWhitespaceConfigAsAbsent()
    {
        Environment.SetEnvironmentVariable(MistralProvider.ApiKeyEnvVar, "env-value");

        var key = MistralProvider.ResolveApiKey(Config(new Dictionary<string, string?>
        {
            ["Mistral:ApiKey"] = "   ",
        }));

        Assert.That(key, Is.EqualTo("env-value"));
    }

    [Test]
    public void IsAvailable_ReturnsFalse_WhenNeitherKeySource()
    {
        Assert.That(MistralProvider.IsAvailable(Config(new Dictionary<string, string?>())), Is.False);
    }

    [Test]
    public void IsAvailable_ReturnsTrue_WhenConfigPresent()
    {
        var available = MistralProvider.IsAvailable(Config(new Dictionary<string, string?>
        {
            ["Mistral:ApiKey"] = "config-value",
        }));

        Assert.That(available, Is.True);
    }

    [Test]
    public void CreateChatClientFromConfig_NoKey_ThrowsSafeMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => MistralProvider.CreateChatClientFromConfig(Config(new Dictionary<string, string?>())));

        Assert.That(ex!.Message, Does.Contain("MISTRAL_API_KEY"));
        Assert.That(ex.Message, Does.Not.Contain("="),
            "error message must not echo env-var values that could leak secrets");
    }

    [Test]
    public void CreateChatClientFromConfig_WithKey_Succeeds()
    {
        var client = MistralProvider.CreateChatClientFromConfig(Config(new Dictionary<string, string?>
        {
            ["Mistral:ApiKey"] = "sk-mistral-fake-test-key",
            ["Mistral:Model"]  = "mistral-medium-latest",
        }));

        Assert.That(client, Is.Not.Null);
    }
}
