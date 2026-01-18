namespace GA.Business.ML.Embeddings;

using System.Collections.Generic;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// Abstraction for a vector index that stores and retrieves voicing documents.
/// </summary>
public interface IVectorIndex
{
    /// <summary>
    /// Gets the list of all documents in the index.
    /// </summary>
    IReadOnlyList<VoicingDocument> Documents { get; }

    /// <summary>
    /// Searches for similar voicings using cosine similarity.
    /// </summary>
    IEnumerable<(VoicingDocument Doc, double Score)> Search(double[] queryVector, int topK = 10);
    
    /// <summary>
    /// Finds a document by exact match on ChordName or Id.
    /// </summary>
    VoicingDocument? FindByIdentity(string identity);
}
