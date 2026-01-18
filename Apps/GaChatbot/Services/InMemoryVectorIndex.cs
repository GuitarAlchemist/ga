using System.Numerics.Tensors;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;

namespace GaChatbot.Services;

/// <summary>
/// A lightweight in-memory vector store for MVP retrieval.
/// Replaces the Mock implementation with actual Cosine Similarity search.
/// </summary>
public class InMemoryVectorIndex
{
    private readonly List<VoicingDocument> _documents = new();

    public void Add(VoicingDocument doc) => _documents.Add(doc);
    public void AddRange(IEnumerable<VoicingDocument> docs) => _documents.AddRange(docs);

    public IEnumerable<(VoicingDocument Doc, double Sim)> Search(float[] queryVector, int limit = 50)
    {
        // Simple linear scan - fast enough for <10k items
        return _documents
            .Where(d => d.Embedding != null)
            .Select(d => 
            {
                // Convert double[] to float[] for TensorPrimitives
                var floats = d.Embedding!.Select(x => (float)x).ToArray();
                return new { Doc = d, Sim = TensorPrimitives.CosineSimilarity(floats, queryVector) };
            })
            .OrderByDescending(x => x.Sim)
            .Take(limit)
            .Select(x => (x.Doc, (double)x.Sim));
    }
    
    public VoicingDocument? FindByIdentity(string nameFragment)
    {
        // Basic lookup for canonical chord (e.g. "C", "maj7")
        // Just finds the first match for now
        return _documents.FirstOrDefault(d => 
            d.ChordName != null &&
            d.ChordName.Contains(nameFragment, StringComparison.OrdinalIgnoreCase));
    }
}
