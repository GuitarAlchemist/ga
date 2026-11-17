namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Extensions;
using GA.Business.ML.Providers;
using GA.Providers.Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// <c>AI:ChatProvider=anthropic</c> (alias <c>claude</c>) and
/// <c>AI:CascadeProvider=anthropic</c> go through <see cref="AnthropicProvider"/>,
/// reading the key from <c>Anthropic:ApiKey</c> / <c>ANTHROPIC_API_KEY</c> and
/// the model from <c>Anthropic:Model</c>.
/// </summary>
[TestFixture]
public class AnthropicChatProviderRegistrationTests
{
    private const string AnthropicEnv = "ANTHROPIC_API_KEY";
    private string? _savedEnv;

    [SetUp]
    public void ClearAmbientKey()
    {
        _savedEnv = Environment.GetEnvironmentVariable(AnthropicEnv);
        Environment.SetEnvironmentVariable(AnthropicEnv, null);
    }

    [TearDown]
    public void RestoreAmbientKey() => Environment.SetEnvironmentVariable(AnthropicEnv, _savedEnv);

    private static IChatClient Resolve(string provider, IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddGuitarAlchemistChatClient(provider, configuration);
        return services.BuildServiceProvider().GetRequiredService<IChatClient>();
    }

    [Test]
    public void DefaultModel_IsCurrentSonnet() =>
        Assert.That(AnthropicProvider.DefaultModel, Is.EqualTo("claude-sonnet-5-5"));

    [TestCase("anthropic")]
    [TestCase("claude")]
    [TestCase("Anthropic")]
    public void Primary_UsesConfiguredModel(string provider)
    {
        var client = Resolve(provider, new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "test-key",
            ["Anthropic:Model"] = "claude-test-model",
        });

        var metadata = client.GetService<ChatClientMetadata>();
        Assert.That(metadata?.DefaultModelId, Is.EqualTo("claude-test-model"));
    }

    [Test]
    public void Primary_KeyFromEnvironment_DefaultsModel()
    {
        Environment.SetEnvironmentVariable(AnthropicEnv, "env-key");

        var client = Resolve("anthropic", new Dictionary<string, string?>());

        Assert.That(client.GetService<ChatClientMetadata>()?.DefaultModelId,
            Is.EqualTo(AnthropicProvider.DefaultModel));
    }

    [Test]
    public void Primary_WhitespaceModel_FallsBackToDefault()
    {
        var client = Resolve("anthropic", new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "test-key",
            ["Anthropic:Model"] = "  ",
        });

        Assert.That(client.GetService<ChatClientMetadata>()?.DefaultModelId,
            Is.EqualTo(AnthropicProvider.DefaultModel));
    }

    [Test]
    public void Primary_NoKey_ThrowsSafeMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Resolve("anthropic", new Dictionary<string, string?>()));

        Assert.That(ex!.Message, Does.Contain(AnthropicEnv));
    }

    [TestCase("anthropic")]
    [TestCase("claude")]
    public void Cascade_AnthropicSecondary_WrapsPrimaryInCascadingClient(string cascade)
    {
        var client = Resolve("ollama", new Dictionary<string, string?>
        {
            ["AI:CascadeProvider"] = cascade,
            ["Anthropic:ApiKey"] = "test-key",
        });

        Assert.That(client, Is.InstanceOf<CascadingChatClient>());
    }

    [Test]
    public void Cascade_AnthropicSecondary_NoKey_ThrowsSafeMessage()
    {
        // Proves the secondary is built through AnthropicProvider: without a key
        // it fails at first resolution, like the Mistral secondary does.
        var ex = Assert.Throws<InvalidOperationException>(() => Resolve("ollama", new Dictionary<string, string?>
        {
            ["AI:CascadeProvider"] = "anthropic",
        }));

        Assert.That(ex!.Message, Does.Contain(AnthropicEnv));
    }

    [Test]
    public void Cascade_UnknownProvider_ListsAnthropicAsValid()
    {
        var ex = Assert.Throws<ArgumentException>(() => Resolve("ollama", new Dictionary<string, string?>
        {
            ["AI:CascadeProvider"] = "not-a-provider",
        }));

        Assert.That(ex!.Message, Does.Contain("anthropic"));
    }
}
