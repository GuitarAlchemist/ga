namespace GA.Business.Core.Tests.Embeddings;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ML.Extensions;
using ML.Text.Onnx;

/// <summary>
/// Pins how an embedding purpose switches to an in-process ONNX model, so CI and production can
/// route without an Ollama server.
/// </summary>
[TestFixture]
public class OnnxEmbeddingConfigTests
{
    [Test]
    public void PurposeWithoutOnnxDir_KeepsTheDefaultGenerator()
    {
        var fallback = new Moq.Mock<IEmbeddingGenerator<string, Embedding<float>>>().Object;
        var factory = new DefaultEmbeddingGeneratorFactory(Config([]), fallback);

        Assert.That(factory.Create("routing"), Is.SameAs(fallback));
    }

    [Test]
    public void PurposeWithMissingModelFiles_FailsNamingTheDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"onnx-missing-{Guid.NewGuid():N}");
        var factory = new DefaultEmbeddingGeneratorFactory(
            Config(new() { ["AI:Embedding:Routing:OnnxDir"] = dir }), defaultGenerator: null);

        var ex = Assert.Throws<FileNotFoundException>(() => factory.Create("routing"));
        Assert.That(ex!.Message, Does.Contain(dir));
    }

    [Test]
    public void OnnxDefaultProvider_IsRegistered()
    {
        var services = new ServiceCollection();

        services.AddTextEmbeddings("onnx", Config(new() { ["AI:Embedding:Default:OnnxDir"] = "unused" }));

        Assert.That(services.Any(d => d.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>)));
    }

    [Test]
    public void IsConfigured_ReadsOnnxDir()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OnnxEmbeddingConfig.IsConfigured(Config(new() { ["AI:Embedding:Routing:OnnxDir"] = "d" }), "AI:Embedding:Routing"));
            Assert.That(OnnxEmbeddingConfig.IsConfigured(Config([]), "AI:Embedding:Routing"), Is.False);
        });
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
