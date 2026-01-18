namespace GA.Business.Core.Abstractions;

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
}
