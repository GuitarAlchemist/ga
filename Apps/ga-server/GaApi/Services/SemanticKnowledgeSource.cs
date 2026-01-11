namespace GaApi.Services;

using System.Text;
using GA.Business.Core.Fretboard.Voicings.Search;

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

        try
        {
            // Bridge: OllamaEmbeddingService returns float[], but search expects double[]
            async Task<double[]> EmbedAsync(string text)
            {
                var floatEmbedding = await embeddingService.GenerateEmbeddingAsync(text);
                return floatEmbedding.Select(f => (double)f).ToArray();
            }

            // Use the real voicing search with embedding generation
            var results = await voicingSearch.SearchAsync(
                query,
                EmbedAsync,
                limit);

            cancellationToken.ThrowIfCancellationRequested();

            // Convert VoicingSearchResult to SemanticSearchResult with rich content
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
            logger.LogWarning(ex, "Voicing search failed for query: {Query}. Returning empty results.", query);
            return [];
        }
    }

    /// <summary>
    ///     Formats a VoicingDocument into LLM-friendly content with rich metadata.
    /// </summary>
    private static string FormatVoicingForLlm(VoicingDocument doc)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"## {doc.ChordName}");
        sb.AppendLine($"Diagram: `{doc.Diagram}`");
        
        if (!string.IsNullOrWhiteSpace(doc.TexturalDescription))
            sb.AppendLine($"Texture: {doc.TexturalDescription}");
        
        if (doc.SemanticTags is { Length: > 0 })
            sb.AppendLine($"Tags: {string.Join(", ", doc.SemanticTags)}");
        
        if (!string.IsNullOrWhiteSpace(doc.HarmonicFunction))
            sb.AppendLine($"Function: {doc.HarmonicFunction}");
        
        if (!string.IsNullOrWhiteSpace(doc.Difficulty))
            sb.AppendLine($"Difficulty: {doc.Difficulty}");
        
        if (doc.AlternateNames is { Length: > 0 })
            sb.AppendLine($"Also known as: {string.Join(", ", doc.AlternateNames)}");
        
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
