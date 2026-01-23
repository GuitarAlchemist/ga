
namespace GaChatbot.Services;

using System.Numerics.Tensors;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;

/// <summary>
/// A lightweight in-memory vector store for MVP retrieval.
/// Replaces the Mock implementation with actual Cosine Similarity search.
/// </summary>
public class InMemoryVectorIndex : IVectorIndex
{
    private readonly List<VoicingDocument> _documents = new();

    public IReadOnlyList<VoicingDocument> Documents => _documents;

    public void Add(VoicingDocument doc) => _documents.Add(doc);
    public void AddRange(IEnumerable<VoicingDocument> docs) => _documents.AddRange(docs);

    public IEnumerable<(VoicingDocument Doc, double Score)> Search(double[] queryVector, int topK = 10)
    {
        var queryFloats = queryVector.Select(x => (float)x).ToArray();
        // Simple linear scan - fast enough for <10k items
        return _documents
            .Where(d => d.Embedding != null)
            .Select(d => 
            {
                // Convert double[] to float[] for TensorPrimitives
                var floats = d.Embedding!.Select(x => (float)x).ToArray();
                return new { Doc = d, Sim = TensorPrimitives.CosineSimilarity(floats, queryFloats) };
            })
            .OrderByDescending(x => x.Sim)
            .Take(topK)
            .Select(x => (x.Doc, (double)x.Sim));
    }
    
    public VoicingDocument? FindByIdentity(string identity)
    {
        // Basic lookup for canonical chord (e.g. "C", "maj7")
        // Just finds the first match for now
        return _documents.FirstOrDefault(d => 
            d.ChordName != null &&
            d.ChordName.Contains(identity, StringComparison.OrdinalIgnoreCase));
    }

    public Task<bool> IsStaleAsync(string currentSchemaVersion)
    {
        if (!_documents.Any()) return Task.FromResult(false);

        // Stale if any document has a different schema version or missing/wrong-sized embedding
        bool isStale = _documents.Any(d => 
            d.SchemaVersion != currentSchemaVersion || 
            d.Embedding == null || 
            d.Embedding.Length != EmbeddingSchema.TotalDimension);

        return Task.FromResult(isStale);
    }
}