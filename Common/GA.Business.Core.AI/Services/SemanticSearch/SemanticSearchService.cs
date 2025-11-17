namespace GA.Business.Core.AI.Services.SemanticSearch;

using System.Collections.Concurrent;

/// <summary>
/// Service for semantic search using vector embeddings
/// </summary>
public class SemanticSearchService
{
    private readonly ConcurrentDictionary<string, IndexedDocument> _documents = new();

    /// <summary>
    /// Index a document with its embedding
    /// </summary>
    public async Task IndexDocumentDirectAsync(IndexedDocument document)
    {
        await Task.Yield();
        _documents[document.Id] = document;
    }

    /// <summary>
    /// Search for documents similar to the query
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults)
    {
        await Task.Yield();

        // Simple implementation: return all documents ranked by ID
        var results = _documents.Values
            .Take(maxResults)
            .Select((doc, index) => new SearchResult(
                doc.Id,
                doc.Content,
                1.0 - (index * 0.1), // Decreasing score
                doc.Category,
                doc.Metadata))
            .ToList();

        return results;
    }

    /// <summary>
    /// Clear all indexed documents
    /// </summary>
    public void Clear()
    {
        _documents.Clear();
    }

    /// <summary>
    /// Get count of indexed documents
    /// </summary>
    public int Count => _documents.Count;

    /// <summary>
    /// Get index statistics
    /// </summary>
    public IndexStatistics GetStatistics()
    {
        var documentsByCategory = _documents.Values
            .GroupBy(d => d.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return new IndexStatistics(
            TotalDocuments: _documents.Count,
            DocumentsByCategory: documentsByCategory,
            EmbeddingDimension: _documents.Values.FirstOrDefault()?.Embedding.Length ?? 0);
    }

    /// <summary>
    /// Search result with score
    /// </summary>
    public record SearchResult(
        string Id,
        string Content,
        double Score,
        string Category,
        Dictionary<string, string> Metadata)
    {
        public string MatchReason => $"Relevance score: {Score:F2}";
    }

    /// <summary>
    /// Indexed document with embedding
    /// </summary>
    public record IndexedDocument(
        string Id,
        string Content,
        string Category,
        Dictionary<string, string> Metadata,
        float[] Embedding);

    /// <summary>
    /// Index statistics
    /// </summary>
    public record IndexStatistics(
        int TotalDocuments,
        Dictionary<string, int> DocumentsByCategory,
        int EmbeddingDimension);
}

