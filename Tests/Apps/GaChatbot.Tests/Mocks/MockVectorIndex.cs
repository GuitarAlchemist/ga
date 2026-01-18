namespace GaChatbot.Tests.Mocks;

using System.Collections.Generic;
using System.Linq;
using GA.Business.ML.Embeddings;
using GA.Business.Core.Fretboard.Voicings.Search;

public class MockVectorIndex : IVectorIndex
{
    private readonly List<VoicingDocument> _documents = new();

    public IReadOnlyList<VoicingDocument> Documents => _documents;

    public void Add(VoicingDocument doc)
    {
        _documents.Add(doc);
    }

    public void AddRange(IEnumerable<VoicingDocument> docs)
    {
        _documents.AddRange(docs);
    }

    public IEnumerable<(VoicingDocument Doc, double Score)> Search(double[] queryVector, int topK = 10)
    {
        // Return all documents with a perfect score for testing purposes,
        // or refine logic if we need to test scoring.
        return _documents.Select(d => (d, 1.0)).Take(topK);
    }

    public VoicingDocument? FindByIdentity(string identity)
    {
        return _documents.FirstOrDefault(d => 
            d.Id == identity || 
            (d.ChordName != null && d.ChordName.Equals(identity, System.StringComparison.OrdinalIgnoreCase)));
    }
}
