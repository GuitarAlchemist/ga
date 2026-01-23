namespace GA.Business.ML.Embeddings.Validation;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;

/// <summary>
/// In-memory RAG index with partition-aware querying.
/// Enables validation of OPTIC-K schema by testing retrieval dimension by dimension.
/// </summary>
public class PartitionAwareRagIndex
{
    private readonly List<IndexedDocument> _documents = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the number of indexed documents.
    /// </summary>
    public int Count => _documents.Count;

    /// <summary>
    /// Adds a document with its embedding to the index.
    /// </summary>
    public void Add(VoicingDocument document, float[] embedding)
    {
        if (embedding.Length != EmbeddingSchema.TotalDimension)
            throw new ArgumentException($"Expected {EmbeddingSchema.TotalDimension} dimensions, got {embedding.Length}");

        lock (_lock)
        {
            _documents.Add(new IndexedDocument(document, embedding));
        }
    }

    /// <summary>
    /// Adds multiple documents.
    /// </summary>
    public void AddRange(IEnumerable<(VoicingDocument Doc, float[] Embedding)> items)
    {
        foreach (var (doc, emb) in items)
        {
            Add(doc, emb);
        }
    }

    /// <summary>
    /// Clears all indexed documents.
    /// </summary>
    public void Clear()
    {
        lock (_lock) { _documents.Clear(); }
    }

    /// <summary>
    /// Searches using the standard weighted partition similarity.
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <returns>Ranked search results with similarity scores.</returns>
    public IReadOnlyList<SearchResult> Search(float[] queryEmbedding, int topK = 10)
    {
        return SearchByPartitions(queryEmbedding, OpticKPartitions.SimilarityPartitions, topK);
    }

    /// <summary>
    /// Searches using only specific partitions.
    /// </summary>
    /// <param name="queryEmbedding">Query embedding vector.</param>
    /// <param name="partitions">Which partitions to use for similarity.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <returns>Ranked search results with per-partition similarity breakdown.</returns>
    public IReadOnlyList<SearchResult> SearchByPartitions(
        float[] queryEmbedding,
        IEnumerable<EmbeddingPartition> partitions,
        int topK = 10)
    {
        var partitionList = partitions.ToList();
        var totalWeight = partitionList.Sum(p => p.Weight);
        
        // If no weights, use equal weighting
        var useEqualWeight = totalWeight == 0;
        var equalWeight = useEqualWeight ? 1.0 / partitionList.Count : 0;

        lock (_lock)
        {
            var results = _documents.Select(doc =>
            {
                var partitionScores = new Dictionary<string, double>();
                double totalSimilarity = 0;

                foreach (var partition in partitionList)
                {
                    var similarity = partition.CosineSimilarity(queryEmbedding, doc.Embedding);
                    partitionScores[partition.Name] = similarity;

                    var weight = useEqualWeight ? equalWeight : partition.Weight / totalWeight;
                    totalSimilarity += weight * similarity;
                }

                return new SearchResult(doc.Document, totalSimilarity, partitionScores);
            })
            .OrderByDescending(r => r.Similarity)
            .Take(topK)
            .ToList();

            return results;
        }
    }

    /// <summary>
    /// Searches using only a single partition.
    /// </summary>
    public IReadOnlyList<SearchResult> SearchByPartition(
        float[] queryEmbedding,
        EmbeddingPartition partition,
        int topK = 10)
    {
        return SearchByPartitions(queryEmbedding, new[] { partition }, topK);
    }

    /// <summary>
    /// Computes similarity breakdown by partition between two embeddings.
    /// </summary>
    public PartitionSimilarityBreakdown ComputeSimilarityBreakdown(float[] a, float[] b)
    {
        var scores = new Dictionary<string, double>();
        double weightedTotal = 0;
        double totalWeight = 0;

        foreach (var partition in OpticKPartitions.All)
        {
            var similarity = partition.CosineSimilarity(a, b);
            scores[partition.Name] = similarity;

            if (partition.Weight > 0)
            {
                weightedTotal += partition.Weight * similarity;
                totalWeight += partition.Weight;
            }
        }

        var overallSimilarity = totalWeight > 0 ? weightedTotal / totalWeight : 0;

        return new PartitionSimilarityBreakdown(scores, overallSimilarity);
    }

    /// <summary>
    /// Analyzes retrieval quality per partition.
    /// </summary>
    public PartitionRetrievalReport AnalyzePartitionRetrieval(
        float[] queryEmbedding,
        string expectedDocId,
        int topK = 10)
    {
        var partitionResults = new Dictionary<string, PartitionRetrievalResult>();

        foreach (var partition in OpticKPartitions.All)
        {
            var results = SearchByPartition(queryEmbedding, partition, topK);
            var rank = results
                .Select((r, i) => (r, i))
                .FirstOrDefault(x => x.r.Document.Id == expectedDocId);

            var foundAt = rank.r != null ? rank.i + 1 : -1;
            var topSimilarity = results.FirstOrDefault()?.Similarity ?? 0;
            var expectedSimilarity = results.FirstOrDefault(r => r.Document.Id == expectedDocId)?.Similarity ?? 0;

            partitionResults[partition.Name] = new PartitionRetrievalResult(
                partition.Name,
                foundAt,
                topSimilarity,
                expectedSimilarity,
                foundAt == 1);
        }

        // Also test combined similarity
        var combinedResults = Search(queryEmbedding, topK);
        var combinedRank = combinedResults
            .Select((r, i) => (r, i))
            .FirstOrDefault(x => x.r.Document.Id == expectedDocId);

        var combinedFoundAt = combinedRank.r != null ? combinedRank.i + 1 : -1;

        return new PartitionRetrievalReport(
            expectedDocId,
            partitionResults,
            combinedFoundAt,
            combinedFoundAt == 1);
    }

    private record IndexedDocument(VoicingDocument Document, float[] Embedding);
}

/// <summary>
/// A search result with partition-level similarity breakdown.
/// </summary>
public record SearchResult(
    VoicingDocument Document,
    double Similarity,
    IReadOnlyDictionary<string, double> PartitionSimilarities);

/// <summary>
/// Breakdown of similarity by partition.
/// </summary>
public record PartitionSimilarityBreakdown(
    IReadOnlyDictionary<string, double> PartitionScores,
    double WeightedOverall);

/// <summary>
/// Result of retrieval analysis for a single partition.
/// </summary>
public record PartitionRetrievalResult(
    string PartitionName,
    int RankPosition,  // 1-based, -1 if not found
    double TopSimilarity,
    double ExpectedDocSimilarity,
    bool IsTopResult);

/// <summary>
/// Complete retrieval analysis report.
/// </summary>
public record PartitionRetrievalReport(
    string ExpectedDocId,
    IReadOnlyDictionary<string, PartitionRetrievalResult> PartitionResults,
    int CombinedRank,
    bool IsTopWithCombined)
{
    /// <summary>
    /// Partitions where the expected document was the top result.
    /// </summary>
    public IEnumerable<string> SuccessfulPartitions =>
        PartitionResults.Where(kvp => kvp.Value.IsTopResult).Select(kvp => kvp.Key);

    /// <summary>
    /// Partitions where the expected document was NOT the top result.
    /// </summary>
    public IEnumerable<string> FailedPartitions =>
        PartitionResults.Where(kvp => !kvp.Value.IsTopResult).Select(kvp => kvp.Key);

    /// <summary>
    /// Generates a human-readable report.
    /// </summary>
    public string ToReport()
    {
        var lines = new List<string>
        {
            $"=== Partition Retrieval Report for {ExpectedDocId} ===",
            "",
            $"Combined Weighted Search: Rank {(CombinedRank > 0 ? CombinedRank.ToString() : "NOT FOUND")} {(IsTopWithCombined ? "✅" : "❌")}",
            ""
        };

        foreach (var (name, result) in PartitionResults.OrderBy(kvp => kvp.Key))
        {
            var status = result.IsTopResult ? "✅" : $"❌ (rank {result.RankPosition})";
            lines.Add($"  {name,-12}: Similarity={result.ExpectedDocSimilarity:F4}  {status}");
        }

        lines.Add("");
        lines.Add($"Successful: {string.Join(", ", SuccessfulPartitions)}");
        lines.Add($"Failed:     {string.Join(", ", FailedPartitions)}");

        return string.Join(Environment.NewLine, lines);
    }
}
