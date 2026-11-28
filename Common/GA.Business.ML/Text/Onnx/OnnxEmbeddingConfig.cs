namespace GA.Business.ML.Text.Onnx;

using System;
using System.IO;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Builds an in-process ONNX embedding generator from a configuration section, so an embedding
/// purpose runs without an embedding server.
/// </summary>
/// <remarks>
/// Keys under the section (e.g. <c>AI:Embedding:Routing</c>):
/// <list type="bullet">
/// <item><c>OnnxDir</c>: directory holding <c>onnx/model.onnx</c> and <c>vocab.txt</c>, as
/// published on Hugging Face (BAAI/bge-large-en-v1.5, nomic-ai/nomic-embed-text-v1.5).</item>
/// <item><c>Pooling</c>: <c>cls</c> (bge) or <c>mean</c> (nomic, the default).</item>
/// <item><c>Model</c>: model id reported on the embeddings.</item>
/// </list>
/// Parity with Ollama's vectors for both models is checked by <c>OnnxOllamaParityTests</c>
/// (cosine ≥ 0.99999), so thresholds calibrated on Ollama carry over.
/// </remarks>
public static class OnnxEmbeddingConfig
{
    /// <summary>True when the section names an ONNX model directory.</summary>
    public static bool IsConfigured(IConfiguration configuration, string section) =>
        !string.IsNullOrWhiteSpace(configuration[$"{section}:OnnxDir"]);

    /// <summary>Creates the generator; throws when the model files are missing.</summary>
    public static IEmbeddingGenerator<string, Embedding<float>> Create(
        IConfiguration configuration, string section, ILoggerFactory? loggerFactory = null)
    {
        var dir = configuration[$"{section}:OnnxDir"]
                  ?? throw new InvalidOperationException($"{section}:OnnxDir is not set.");
        var modelPath = Path.Combine(dir, "onnx", "model.onnx");
        var vocabPath = Path.Combine(dir, "vocab.txt");
        if (!File.Exists(modelPath) || !File.Exists(vocabPath))
        {
            throw new FileNotFoundException(
                $"{section}:OnnxDir '{dir}' must contain onnx/model.onnx and vocab.txt.", modelPath);
        }

        var pooling = string.Equals(configuration[$"{section}:Pooling"], "cls", StringComparison.OrdinalIgnoreCase)
            ? OnnxEmbeddingPoolingStrategy.Cls
            : OnnxEmbeddingPoolingStrategy.Mean;
        var service = new OnnxEmbeddingService(
            new OnnxEmbeddingOptions
            {
                ModelPath = modelPath,
                VocabularyPath = vocabPath,
                MaxTokens = 512,
                PoolingStrategy = pooling,
                PadToMaxTokens = false,
            },
            loggerFactory?.CreateLogger<OnnxEmbeddingService>());
        return new OnnxTextEmbeddingGenerator(service, configuration[$"{section}:Model"] ?? Path.GetFileName(dir.TrimEnd('/', '\\')));
    }
}
