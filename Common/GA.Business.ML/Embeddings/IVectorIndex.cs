namespace GA.Business.ML.Embeddings;

/// <summary>
///     Abstraction for a vector index that stores and retrieves voicing documents.
/// </summary>
public interface IVectorIndex
{
    /// <summary>
    ///     Gets the list of all documents in the index.
    /// </summary>
    IReadOnlyList<ChordVoicingRagDocument> Documents { get; }

    /// <summary>
    ///     Searches for similar voicings using cosine similarity.
    /// </summary>
    IEnumerable<(ChordVoicingRagDocument Doc, double Score)> Search(float[] queryVector, int topK = 10);

    /// <summary>
    ///     Finds a document by exact match on ChordName or Id.
    /// </summary>
    ChordVoicingRagDocument? FindByIdentity(string identity);

    /// <summary>
    ///     Checks if the index contains documents with outdated schema versions or missing embeddings.
    /// </summary>
    /// <param name="currentSchemaVersion">The expected version from EmbeddingSchema.</param>
    /// <returns>True if the index is stale and needs reindexing.</returns>
    Task<bool> IsStaleAsync(string currentSchemaVersion);

    /// <summary>
    ///     Upserts a collection of documents into the vector index.
    /// </summary>
    Task IndexAsync(IEnumerable<ChordVoicingRagDocument> docs);
}
