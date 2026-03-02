namespace GA.Business.ML.Embeddings;

using System.Numerics.Tensors;

/// <summary>
///     In-memory vector index for development and testing scenarios.
/// </summary>
/// <remarks>
///     This implementation is optimized for simplicity and quick iteration.
///     For production use with large datasets, consider <see cref="QdrantVectorIndex" />
///     or <see cref="FileBasedVectorIndex" /> with persistence.
/// </remarks>
public sealed class InMemoryVectorIndex : IVectorIndex
{
    private readonly List<ChordVoicingRagDocument> _documents = [];
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public IReadOnlyList<ChordVoicingRagDocument> Documents
    {
        get
        {
            lock (_lock)
            {
                return [.. _documents];
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<(ChordVoicingRagDocument Doc, double Score)> Search(float[] queryVector, int topK = 10)
    {
        if (queryVector.Length == 0)
        {
            return [];
        }

        List<ChordVoicingRagDocument> snapshot;
        lock (_lock)
        {
            snapshot = [.. _documents];
        }

        if (snapshot.Count == 0)
        {
            return [];
        }

        // Score all documents
        var scored = snapshot
            .Where(d => d.Embedding != null && d.Embedding.Length == queryVector.Length)
            .Select(doc => (Doc: doc, Score: CosineSimilarity(queryVector, doc.Embedding!)))
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return scored;
    }

    /// <inheritdoc />
    public ChordVoicingRagDocument? FindByIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
        {
            return null;
        }

        lock (_lock)
        {
            return _documents.FirstOrDefault(d =>
                d.Id == identity ||
                (d.ChordName != null && d.ChordName.Equals(identity, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <inheritdoc />
    public Task<bool> IsStaleAsync(string currentSchemaVersion) =>
        // In-memory index is never stale - it's always rebuilt from scratch
        Task.FromResult(false);

    /// <summary>
    ///     Adds a single document to the index.
    /// </summary>
    public void Add(ChordVoicingRagDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_lock)
        {
            _documents.Add(document);
        }
    }

    /// <summary>
    ///     Adds multiple documents to the index.
    /// </summary>
    public void AddRange(IEnumerable<ChordVoicingRagDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        lock (_lock)
        {
            _documents.AddRange(documents);
        }
    }

    /// <inheritdoc />
    public Task IndexAsync(IEnumerable<ChordVoicingRagDocument> docs)
    {
        AddRange(docs);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Clears all documents from the index.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _documents.Clear();
        }
    }

    /// <summary>
    ///     Computes cosine similarity between two vectors.
    /// </summary>
    private static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0.0;
        }

        var dotProduct = TensorPrimitives.Dot(a, b);
        var normA = MathF.Sqrt(TensorPrimitives.Dot(a, a));
        var normB = MathF.Sqrt(TensorPrimitives.Dot(b, b));

        if (normA < 1e-10f || normB < 1e-10f)
        {
            return 0.0;
        }

        return dotProduct / (normA * normB);
    }
}
