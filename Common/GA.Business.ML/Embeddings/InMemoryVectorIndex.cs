namespace GA.Business.ML.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using System.Threading.Tasks;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;

/// <summary>
/// In-memory vector index for development and testing scenarios.
/// </summary>
/// <remarks>
/// This implementation is optimized for simplicity and quick iteration.
/// For production use with large datasets, consider <see cref="QdrantVectorIndex"/>
/// or <see cref="FileBasedVectorIndex"/> with persistence.
/// </remarks>
public sealed class InMemoryVectorIndex : IVectorIndex
{
    private readonly List<VoicingDocument> _documents = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public IReadOnlyList<VoicingDocument> Documents
    {
        get
        {
            lock (_lock)
            {
                return _documents.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Adds a single document to the index.
    /// </summary>
    public void Add(VoicingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_lock)
        {
            _documents.Add(document);
        }
    }

    /// <summary>
    /// Adds multiple documents to the index.
    /// </summary>
    public void AddRange(IEnumerable<VoicingDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        lock (_lock)
        {
            _documents.AddRange(documents);
        }
    }

    /// <summary>
    /// Clears all documents from the index.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _documents.Clear();
        }
    }

    /// <inheritdoc />
    public IEnumerable<(VoicingDocument Doc, double Score)> Search(double[] queryVector, int topK = 10)
    {
        if (queryVector.Length == 0)
        {
            return Enumerable.Empty<(VoicingDocument, double)>();
        }

        List<VoicingDocument> snapshot;
        lock (_lock)
        {
            snapshot = _documents.ToList();
        }

        if (snapshot.Count == 0)
        {
            return Enumerable.Empty<(VoicingDocument, double)>();
        }

        // Convert query to float for TensorPrimitives
        var queryFloat = Array.ConvertAll(queryVector, v => (float)v);

        // Score all documents
        var scored = snapshot
            .Where(d => d.Embedding != null && d.Embedding.Length == queryVector.Length)
            .Select(doc =>
            {
                var docFloat = Array.ConvertAll(doc.Embedding!, v => (float)v);
                var similarity = CosineSimilarity(queryFloat, docFloat);
                return (Doc: doc, Score: similarity);
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return scored;
    }

    /// <inheritdoc />
    public VoicingDocument? FindByIdentity(string identity)
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
    public Task<bool> IsStaleAsync(string currentSchemaVersion)
    {
        // In-memory index is never stale - it's always rebuilt from scratch
        return Task.FromResult(false);
    }

    /// <summary>
    /// Computes cosine similarity between two vectors.
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
