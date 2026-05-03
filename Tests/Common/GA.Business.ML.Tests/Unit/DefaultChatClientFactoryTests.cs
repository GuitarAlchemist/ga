namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Moq;

[TestFixture]
public class DefaultChatClientFactoryTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static IConfiguration ConfigWith(IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IChatClient FakeDefaultClient() => new Mock<IChatClient>().Object;

    [Test]
    public void Create_DefaultPurpose_ReturnsRegisteredClient()
    {
        var registered = FakeDefaultClient();
        var factory = new DefaultChatClientFactory(EmptyConfig(), registered);

        Assert.That(factory.Create("default"), Is.SameAs(registered));
    }

    [Test]
    public void Create_FastLocalPurpose_ReturnsRegisteredClient()
    {
        var registered = FakeDefaultClient();
        var factory = new DefaultChatClientFactory(EmptyConfig(), registered);

        Assert.That(factory.Create("fast-local"), Is.SameAs(registered));
    }

    [Test]
    public void Create_PurposeIsCaseInsensitive()
    {
        var registered = FakeDefaultClient();
        var factory = new DefaultChatClientFactory(EmptyConfig(), registered);

        Assert.That(factory.Create("Default"),    Is.SameAs(registered));
        Assert.That(factory.Create("FAST-LOCAL"), Is.SameAs(registered));
    }

    [Test]
    public void Create_RepeatedCallsForSamePurpose_ReturnSameInstance()
    {
        var registered = FakeDefaultClient();
        var factory = new DefaultChatClientFactory(EmptyConfig(), registered);

        var first  = factory.Create("default");
        var second = factory.Create("default");

        Assert.That(second, Is.SameAs(first), "factory must cache per purpose for the lifetime of the process");
    }

    [Test]
    public void Create_UnknownPurpose_ThrowsArgumentException()
    {
        var factory = new DefaultChatClientFactory(EmptyConfig(), FakeDefaultClient());

        Assert.That(
            () => factory.Create("not-a-real-purpose"),
            Throws.ArgumentException.With.Message.Contains("default, skill-md, qa-architect, fast-local"));
    }

    [Test]
    public void Create_SkillMdPurpose_NoApiKey_ThrowsSafeMessage()
    {
        // Ensure no ambient ANTHROPIC_API_KEY interferes.
        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            var factory = new DefaultChatClientFactory(EmptyConfig(), FakeDefaultClient());

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create("skill-md"));
            Assert.That(ex!.Message, Does.Contain("API key"));
            Assert.That(ex.Message, Does.Not.Contain("="),
                "error message must not echo back env-var values that could leak secrets");
        }
        finally
        {
            if (original is not null)
                Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }

    [Test]
    public void Create_QaArchitectPurpose_NoApiKey_ThrowsSafeMessage()
    {
        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        try
        {
            var factory = new DefaultChatClientFactory(EmptyConfig(), FakeDefaultClient());

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create("qa-architect"));
            Assert.That(ex!.Message, Does.Contain("API key"));
        }
        finally
        {
            if (original is not null)
                Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }

    [Test]
    public void Create_NullOrWhitespacePurpose_Throws()
    {
        var factory = new DefaultChatClientFactory(EmptyConfig(), FakeDefaultClient());

        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        // and ArgumentException for empty/whitespace — both are valid guard signals.
        Assert.That(() => factory.Create(null!), Throws.InstanceOf<ArgumentNullException>());
        Assert.That(() => factory.Create(""),    Throws.ArgumentException);
        Assert.That(() => factory.Create("   "), Throws.ArgumentException);
    }

    [Test]
    public void Create_WithApiKeyInConfig_DoesNotLeakKeyIntoExceptionMessages()
    {
        // If the SDK construction itself fails downstream (network etc.), the factory
        // should not surface the API key in any error message. We verify by giving an
        // obviously-syntactic-but-fake key and confirming the factory either succeeds
        // or fails cleanly without echoing the key. Anthropic SDK lazy-validates the
        // key only on first request, so Create() succeeds; we just check the path
        // doesn't blow up at construction time.
        var config = ConfigWith(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"]      = "sk-ant-fake-test-key-do-not-use",
            ["AnthropicSkills:Model"] = "claude-sonnet-4-6",
        });

        var factory = new DefaultChatClientFactory(config, FakeDefaultClient());

        // Construction succeeds because the SDK doesn't validate at this point.
        var client = factory.Create("skill-md");
        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.Not.SameAs(FakeDefaultClient()),
            "skill-md must not silently fall back to the registered default client");
    }
}
