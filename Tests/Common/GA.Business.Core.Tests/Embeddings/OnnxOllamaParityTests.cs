namespace GA.Business.Core.Tests.Embeddings;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ML.Providers;
using ML.Text.Onnx;

/// <summary>
/// Checks that the in-process ONNX embedders reproduce the vectors Ollama serves today, so the
/// router thresholds calibrated on Ollama (AI:Routing:MinConfidence 0.64 for bge-large) still
/// hold. Needs the ONNX models on disk and a running Ollama: explicit, never in CI.
/// </summary>
/// <remarks>
/// GA_ONNX_BGE_DIR and GA_ONNX_NOMIC_DIR each hold onnx/model.onnx and vocab.txt
/// (BAAI/bge-large-en-v1.5, nomic-ai/nomic-embed-text-v1.5).
/// </remarks>
[TestFixture]
[Explicit("Needs ONNX models on disk and a local Ollama")]
[Category("ModelParity")]
public class OnnxOllamaParityTests
{
    private static readonly string[] Prompts =
    [
        "What notes are in a D7 chord?",
        "Why does a minor chord sound sad compared to a major chord?",
        "Show me an easy way to play it on guitar.",
        "Give me a recipe for chocolate cake.",
        "How do I build speed with alternate picking without getting tense?",
        "Quelles sont les notes de l'accord de la mineur ?",
    ];

    [TestCase("GA_ONNX_BGE_DIR", "bge-large", OnnxEmbeddingPoolingStrategy.Cls)]
    [TestCase("GA_ONNX_NOMIC_DIR", "nomic-embed-text", OnnxEmbeddingPoolingStrategy.Mean)]
    public async Task OnnxVectors_MatchOllama(string dirVariable, string ollamaModel, OnnxEmbeddingPoolingStrategy pooling)
    {
        var dir = Environment.GetEnvironmentVariable(dirVariable);
        if (string.IsNullOrEmpty(dir)) Assert.Ignore($"{dirVariable} not set");

        using var onnx = new OnnxTextEmbeddingGenerator(
            new OnnxEmbeddingService(
                new OnnxEmbeddingOptions
                {
                    ModelPath = Path.Combine(dir, "onnx", "model.onnx"),
                    VocabularyPath = Path.Combine(dir, "vocab.txt"),
                    MaxTokens = 512,
                    PoolingStrategy = pooling,
                    PadToMaxTokens = false,
                },
                NullLogger<OnnxEmbeddingService>.Instance),
            ollamaModel);
        using var ollama = OllamaProvider.CreateEmbeddingGenerator(model: ollamaModel);

        var fromOnnx = await onnx.GenerateAsync(Prompts);
        var fromOllama = await ollama.GenerateAsync(Prompts);

        for (var i = 0; i < Prompts.Length; i++)
        {
            var cosine = Cosine(fromOnnx[i].Vector.Span, fromOllama[i].Vector.Span);
            TestContext.Out.WriteLine($"{ollamaModel} {cosine:F5} {Prompts[i]}");
            Assert.That(cosine, Is.GreaterThan(0.999), Prompts[i]);
        }
    }

    private static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        Assert.That(a.Length, Is.EqualTo(b.Length), "dimension");
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
