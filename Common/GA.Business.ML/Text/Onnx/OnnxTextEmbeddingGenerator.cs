namespace GA.Business.ML.Text.Onnx;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

/// <summary>
/// <see cref="IEmbeddingGenerator{String,Embedding}"/> over an in-process ONNX model, so routing
/// and the default embedder can run without an embedding server (Ollama).
/// </summary>
/// <remarks>
/// Owns <paramref name="service"/> and disposes it. Inference runs on the calling thread;
/// ONNX Runtime sessions are safe to run concurrently.
/// </remarks>
public sealed class OnnxTextEmbeddingGenerator(OnnxEmbeddingService service, string modelId)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly EmbeddingGeneratorMetadata _metadata = new("onnx", defaultModelId: modelId);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        GeneratedEmbeddings<Embedding<float>> result = [];
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = await service.GenerateEmbeddingAsync(value, cancellationToken);
            result.Add(new Embedding<float>(vector) { ModelId = modelId, CreatedAt = DateTimeOffset.UtcNow });
        }

        return result;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
        : serviceKey is null && serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose() => service.Dispose();
}
