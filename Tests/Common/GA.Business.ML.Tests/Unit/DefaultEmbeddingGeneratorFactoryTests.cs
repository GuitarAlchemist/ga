namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Moq;

/// <summary>
/// The purpose factory (plan #420 Phase 1) must leave behaviour UNCHANGED until a
/// per-purpose model override is configured: no override → the global default
/// generator; an override → a distinct, cached generator. This is what makes the
/// bge-large routing swap a routing-only, reversible change.
/// </summary>
[TestFixture]
public class DefaultEmbeddingGeneratorFactoryTests
{
    private static IConfiguration Config(params (string Key, string Value)[] kv) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(kv.ToDictionary(x => x.Key, x => (string?)x.Value))
            .Build();

    private static IEmbeddingGenerator<string, Embedding<float>> StubDefault() =>
        new Mock<IEmbeddingGenerator<string, Embedding<float>>>().Object;

    [Test]
    public void Create_NoOverride_ReturnsDefaultGenerator()
    {
        var def = StubDefault();
        var factory = new DefaultEmbeddingGeneratorFactory(Config(), def);

        Assert.That(factory.Create("routing"), Is.SameAs(def),
            "with no AI:Embedding:routing:Model override, routing must use the default embedder (zero behaviour change)");
    }

    [Test]
    public void Create_Override_ReturnsDistinctGenerator_AndCachesPerPurpose()
    {
        var def = StubDefault();
        var factory = new DefaultEmbeddingGeneratorFactory(
            Config(("AI:Embedding:routing:Model", "bge-large"), ("Ollama:BaseUrl", "http://localhost:11434")),
            def);

        var a = factory.Create("routing");
        var b = factory.Create("routing");

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.Not.Null);
            Assert.That(a, Is.Not.SameAs(def), "an override must NOT be the default generator");
            Assert.That(b, Is.SameAs(a), "override instances must be cached per purpose");
        });
    }

    [Test]
    public void Create_OverridesOneePurpose_LeavesOthersOnDefault()
    {
        var def = StubDefault();
        var factory = new DefaultEmbeddingGeneratorFactory(
            Config(("AI:Embedding:routing:Model", "bge-large")), def);

        Assert.Multiple(() =>
        {
            Assert.That(factory.Create("routing"), Is.Not.SameAs(def), "routing is overridden");
            Assert.That(factory.Create("memory"), Is.SameAs(def), "memory has no override → default (decoupled)");
        });
    }

    [Test]
    public void Create_NullDefault_NoOverride_ReturnsNull()
    {
        // Callers treat embeddings as optional (SemanticIntentRouter.IsAvailable),
        // so an unconfigured purpose with no default must surface as null, not throw.
        var factory = new DefaultEmbeddingGeneratorFactory(Config(), defaultGenerator: null);

        Assert.That(factory.Create("routing"), Is.Null);
    }
}
