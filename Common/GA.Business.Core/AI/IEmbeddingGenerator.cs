namespace GA.Business.Core.AI;

using System.Threading.Tasks;
using GA.Business.Core.Fretboard.Voicings.Search;

/// <summary>
/// Abstraction for generating vector embeddings locally or via API
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    /// Gets the dimension of the generated embeddings
    /// </summary>
    int Dimension { get; }

    /// <summary>
    /// Generate embedding for a voicing document (structured data)
    /// </summary>
    Task<double[]> GenerateEmbeddingAsync(VoicingDocument document);
    
    // Batch support
    Task<double[][]> GenerateBatchEmbeddingsAsync(IEnumerable<VoicingDocument> documents);

    /// <summary>
    /// Generate embedding for a text query (search)
    /// </summary>
    Task<double[]> GenerateEmbeddingAsync(string text);
}
