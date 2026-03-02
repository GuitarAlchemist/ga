namespace GA.Business.ML.Embeddings;

using System.Numerics.Tensors;

/// <summary>
///     A file-backed vector index that persists data to a JSONL file.
///     Implements basic cosine similarity search with O(N) complexity.
/// </summary>
public class FileBasedVectorIndex(string filePath = "voicing_index.jsonl") : IVectorIndex
{
    private readonly List<ChordVoicingRagDocument> _documents = [];

    public string FilePath { get; } = filePath;

    public int Count => _documents.Count;

    public IReadOnlyList<ChordVoicingRagDocument> Documents => _documents;

    /// <summary>
    ///     Finds a document by exact match on ChordName or Id.
    /// </summary>
    public ChordVoicingRagDocument? FindByIdentity(string identity) => _documents.FirstOrDefault(d =>
        string.Equals(d.ChordName, identity, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(d.Id, identity, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Searches for similar voicings using cosine similarity.
    /// </summary>
    public IEnumerable<(ChordVoicingRagDocument Doc, double Score)> Search(float[] queryVector, int topK = 10)
    {
        if (queryVector == null || queryVector.Length == 0)
        {
            // If no query vector, return all documents with NaN score
            return _documents.Select(d => (d, double.NaN)).Take(topK);
        }

        return _documents
            .Where(d => d.Embedding != null && d.Embedding.Length == queryVector.Length)
            .Select(d => (Doc: d, Score: (double)TensorPrimitives.CosineSimilarity(queryVector, d.Embedding!)))
            .OrderByDescending(x => x.Score)
            .Take(topK);
    }

    public Task<bool> IsStaleAsync(string currentSchemaVersion)
    {
        if (_documents.Count == 0)
        {
            // If empty, try loading first
            if (!Load())
            {
                return Task.FromResult(false);
            }
        }

        // Stale if any document has a different schema version or missing/wrong-sized embedding
        return Task.FromResult(_documents.Any(d =>
            d.SchemaVersion != currentSchemaVersion ||
            d.Embedding is not { Length: EmbeddingSchema.TotalDimension }));
    }

    /// <summary>
    ///     Adds a document to the index (memory only until Save() is called).
    /// </summary>
    public void Add(ChordVoicingRagDocument doc) => _documents.Add(doc);

    /// <summary>
    ///     Saves all documents to the JSONL file.
    /// </summary>
    public void Save()
    {
        using var writer = new StreamWriter(FilePath);
        foreach (var doc in _documents)
        {
            var json = JsonSerializer.Serialize(doc);
            writer.WriteLine(json);
        }
    }

    /// <summary>
    ///     Loads documents from the JSONL file.
    /// </summary>
    public bool Load()
    {
        if (!File.Exists(FilePath))
        {
            return false;
        }

        _documents.Clear();
        foreach (var line in File.ReadLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var doc = JsonSerializer.Deserialize<ChordVoicingRagDocument>(line);
            if (doc != null)
            {
                _documents.Add(doc);
            }
        }

        return _documents.Count > 0;
    }
}
