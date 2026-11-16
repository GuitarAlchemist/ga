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

    [TestCase("https://ollama.example.com", true)]
    [TestCase("http://localhost:11434", false)]
    public void Create_Override_KeepsTheOllamaAccessHeaders_LikeTheDefaultEmbedder(string baseUrl, bool expectHeaders)
    {
        // The default embedder (OllamaProvider.CreateEmbeddingGeneratorFromConfig) sends the
        // Cloudflare Access service token to an HTTPS Ollama; a purpose override must reach
        // the same gated endpoint, so it must carry the same headers (and never over HTTP).
        var factory = new DefaultEmbeddingGeneratorFactory(
            Config(
                ("AI:Embedding:routing:Model", "bge-large"),
                ("Ollama:BaseUrl", baseUrl),
                ("Ollama:AccessClientId", "id.access"),
                ("Ollama:AccessClientSecret", "secret")),
            StubDefault());

        var headers = HttpClientOf(factory.Create("routing")!)?.DefaultRequestHeaders;

        Assert.That(headers?.Contains("CF-Access-Client-Id") == true, Is.EqualTo(expectHeaders));
        Assert.That(headers?.Contains("CF-Access-Client-Secret") == true, Is.EqualTo(expectHeaders));
    }

    // The Ollama generator keeps its HttpClient private; read whichever field holds it.
    private static HttpClient? HttpClientOf(object generator) =>
        generator.GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Where(field => typeof(HttpClient).IsAssignableFrom(field.FieldType))
            .Select(field => field.GetValue(generator) as HttpClient)
            .FirstOrDefault(client => client is not null);

    [Test]
    public void Create_NullDefault_NoOverride_ReturnsNull()
    {
        // Callers treat embeddings as optional (SemanticIntentRouter.IsAvailable),
        // so an unconfigured purpose with no default must surface as null, not throw.
        var factory = new DefaultEmbeddingGeneratorFactory(Config(), defaultGenerator: null);

        Assert.That(factory.Create("routing"), Is.Null);
    }
}
