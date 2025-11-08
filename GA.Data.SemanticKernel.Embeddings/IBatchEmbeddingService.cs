namespace GA.Data.SemanticKernel.Embeddings;

/// <summary>
///     Interface for batch embedding generation
/// </summary>
public interface IBatchEmbeddingService
{
    Task<float[][]> GenerateBatchEmbeddingsAsync(string[] texts, CancellationToken cancellationToken = default);
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
