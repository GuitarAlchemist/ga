namespace GA.Business.ML.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Text.Onnx;

/// <summary>
///     Adapter for generating text embeddings using the ONNX-based service.
/// </summary>
public sealed class OnnxEmbeddingGenerator(string modelPath, string vocabPath) : IDisposable
{
    private readonly OnnxEmbeddingService _service = new(new OnnxEmbeddingOptions
    {
        ModelPath = modelPath,
        VocabularyPath = vocabPath
    });

    // Typical dimension for MiniLM-L6-v2 is 384
    public int Dimension => 384;

    public async Task<double[]> GenerateEmbeddingAsync(string text)
    {
        var floatEmbedding = await _service.GenerateEmbeddingAsync(text);
        return [.. floatEmbedding.Select(f => (double)f)];
    }

    public void Dispose()
    {
        _service.Dispose();
    }
}
