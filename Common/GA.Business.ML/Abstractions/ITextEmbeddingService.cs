namespace GA.Business.ML.Abstractions;

/// <summary>
/// Core interface for text embedding generation services
/// </summary>
public interface ITextEmbeddingService
{
    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    /// <param name="text">Input text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector as float array</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for batch text embedding generation
/// </summary>
public interface IBatchTextEmbeddingService : ITextEmbeddingService
{
    /// <summary>
    /// Generate embeddings for multiple texts in a single batch
    /// </summary>
    /// <param name="texts">Input texts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of embedding vectors</returns>
    Task<float[][]> GenerateBatchEmbeddingsAsync(string[] texts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Legacy interface for compatibility with existing MongoDB services
/// </summary>
[Obsolete("Use ITextEmbeddingService instead. This interface is kept for backward compatibility.")]
public interface ILegacyEmbeddingService
{
    /// <summary>
    /// Generate embedding for a single text (legacy format)
    /// </summary>
    /// <param name="text">Input text</param>
    /// <returns>Embedding vector as List of floats</returns>
    Task<List<float>> GenerateEmbeddingAsync(string text);
}
