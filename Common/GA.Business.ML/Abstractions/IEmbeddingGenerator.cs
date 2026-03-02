namespace GA.Business.ML.Abstractions;

using Rag.Models;

/// <summary>
///     Abstraction for generating vector embeddings locally or via API
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    ///     Gets the dimension of the generated embeddings
    /// </summary>
    int Dimension { get; }

    /// <summary>
    ///     Generate embedding for a voicing document (structured data)
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(ChordVoicingRagDocument document);
}
