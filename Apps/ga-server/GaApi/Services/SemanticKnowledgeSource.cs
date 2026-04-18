namespace GaApi.Services;

using System.Text;
using GA.Business.ML.Search;
using GA.Business.ML.Rag.Models;

/// <summary>
///     Implementation that bridges EnhancedVoicingSearchService to the chatbot.
///     Converts voicing search results into LLM-friendly content with YAML analysis.
/// </summary>
public sealed class SemanticKnowledgeSource(
    EnhancedVoicingSearchService voicingSearch,
    OllamaEmbeddingService embeddingService,
    ILogger<SemanticKnowledgeSource> logger)
    : ISemanticKnowledgeSource
{
    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embedMs = 0d;
        var searchMs = 0d;

        try
        {
            // Bridge: OllamaEmbeddingService returns float[], but search expects double[].
            // Time the embedding call separately from the ANN scan for retrieval diagnostics.
            async Task<double[]> EmbedAsync(string text)
            {
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var floatEmbedding = await embeddingService.GenerateEmbeddingAsync(text);
                embedMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                return [.. floatEmbedding.Select(f => (double)f)];
            }

            var searchStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var results = await voicingSearch.SearchAsync(query, EmbedAsync, limit);
            searchMs = System.Diagnostics.Stopwatch.GetElapsedTime(searchStart).TotalMilliseconds - embedMs;

            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "semantic-knowledge query={Query} limit={Limit} returned={Count} embed_ms={EmbedMs:F1} search_ms={SearchMs:F1}",
                query, limit, results.Count, embedMs, Math.Max(0, searchMs));

            return results
                .Select(r => new SemanticSearchResult(
                    FormatVoicingForLlm(r.Document),
                    r.Score))
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Upgraded from Warning to Error — this path silently produced empty
            // results for every chatbot query and masked root-cause failures in
            // the embedding/search pipeline. Keep returning [] so the chatbot still
            // serves an answer, but make the failure loud in logs.
            logger.LogError(ex,
                "semantic-knowledge FAILED query={Query} limit={Limit} embed_ms={EmbedMs:F1} search_ms={SearchMs:F1}. Returning empty results.",
                query, limit, embedMs, Math.Max(0, searchMs));
            return [];
        }
    }

    /// <summary>
    ///     Formats a VoicingDocument into LLM-friendly content with rich metadata.
    /// </summary>
    private static string FormatVoicingForLlm(ChordVoicingRagDocument doc)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## {doc.ChordName}");
        sb.AppendLine($"Diagram: `{doc.Diagram}`");

        if (!string.IsNullOrWhiteSpace(doc.TexturalDescription))
        {
            sb.AppendLine($"Texture: {doc.TexturalDescription}");
        }

        if (doc.SemanticTags is { Length: > 0 })
        {
            sb.AppendLine($"Tags: {string.Join(", ", doc.SemanticTags)}");
        }

        if (!string.IsNullOrWhiteSpace(doc.HarmonicFunction))
        {
            sb.AppendLine($"Function: {doc.HarmonicFunction}");
        }

        if (!string.IsNullOrWhiteSpace(doc.Difficulty))
        {
            sb.AppendLine($"Difficulty: {doc.Difficulty}");
        }

        if (doc.AlternateNames is { Length: > 0 })
        {
            sb.AppendLine($"Also known as: {string.Join(", ", doc.AlternateNames)}");
        }

        // Include YAML analysis if available
        if (!string.IsNullOrWhiteSpace(doc.YamlAnalysis) && doc.YamlAnalysis != "{}")
        {
            sb.AppendLine();
            sb.AppendLine("```yaml");
            sb.AppendLine(doc.YamlAnalysis);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }
}
