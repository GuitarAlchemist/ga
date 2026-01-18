namespace GA.Business.ML.Embeddings;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Numerics.Tensors;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// A file-backed vector index that persists data to a JSONL file.
/// Implements basic cosine similarity search with O(N) complexity.
/// </summary>
public class FileBasedVectorIndex : IVectorIndex
{
    private readonly List<VoicingDocument> _documents = [];
    private readonly string _filePath;

    public string FilePath => _filePath;

    public IReadOnlyList<VoicingDocument> Documents => _documents;

    public FileBasedVectorIndex(string filePath = "voicing_index.jsonl")
    {
        _filePath = filePath;
    }

    public int Count => _documents.Count;

    /// <summary>
    /// Adds a document to the index (memory only until Save() is called).
    /// </summary>
    public void Add(VoicingDocument doc)
    {
        _documents.Add(doc);
    }

    /// <summary>
    /// Finds a document by exact match on ChordName or Id.
    /// </summary>
    public VoicingDocument? FindByIdentity(string identity)
    {
        return _documents.FirstOrDefault(d => 
            string.Equals(d.ChordName, identity, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Id, identity, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Searches for similar voicings using cosine similarity.
    /// </summary>
    public IEnumerable<(VoicingDocument Doc, double Score)> Search(double[] queryVector, int topK = 10)
    {
        if (queryVector == null || queryVector.Length == 0)
        {
            // If no query vector, return all documents with NaN score
            return _documents.Select(d => (d, double.NaN)).Take(topK);
        }

        return _documents
            .Where(d => d.Embedding != null && d.Embedding.Length == queryVector.Length)
            .Select(d => (Doc: d, Score: TensorPrimitives.CosineSimilarity(queryVector, d.Embedding!)))
            .OrderByDescending(x => x.Score)
            .Take(topK);
    }

    /// <summary>
    /// Saves all documents to the JSONL file.
    /// </summary>
    public void Save()
    {
        using var writer = new StreamWriter(_filePath);
        foreach (var doc in _documents)
        {
            var json = JsonSerializer.Serialize(doc);
            writer.WriteLine(json);
        }
    }

    /// <summary>
    /// Loads documents from the JSONL file.
    /// </summary>
    public bool Load()
    {
        if (!File.Exists(_filePath)) return false;

        _documents.Clear();
        foreach (var line in File.ReadLines(_filePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var doc = JsonSerializer.Deserialize<VoicingDocument>(line);
            if (doc != null) _documents.Add(doc);
        }
        return _documents.Count > 0;
    }

    private static double CosineSimilarity(float[] a, double[] b)
    {
        return TensorPrimitives.CosineSimilarity(a, b.Select(x => (float)x).ToArray());
    }
}
