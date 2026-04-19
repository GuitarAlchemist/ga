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
    IMusicalQueryExtractor queryExtractor,
    MusicalQueryEncoder queryEncoder,
    ILogger<SemanticKnowledgeSource> logger)
    : ISemanticKnowledgeSource
{
    public async Task<IReadOnlyList<SemanticSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extractMs = 0d;
        var encodeMs = 0d;
        var embedMs = 0d;
        var searchMs = 0d;
        var queryDim = 0;
        var mode = "unknown";
        StructuredQuery? structured = null;

        try
        {
            // The strategy declares its required query-vector space; we dispatch on that
            // rather than pattern-matching the strategy name. A strategy rename tomorrow
            // would silently break the old StartsWith("OPTK") check — QuerySpace won't.
            var optickActive = voicingSearch.QuerySpace == QueryVectorSpace.OpticCompact112;

            async Task<double[]> MusicalVectorAsync(string text)
            {
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                structured = await queryExtractor.ExtractAsync(text, cancellationToken);
                extractMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

                var t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                var vec = queryEncoder.Encode(structured);
                encodeMs = System.Diagnostics.Stopwatch.GetElapsedTime(t1).TotalMilliseconds;

                queryDim = vec.Length;
                return vec;
            }

            async Task<double[]> OllamaVectorAsync(string text)
            {
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var floats = await embeddingService.GenerateEmbeddingAsync(text);
                embedMs = System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                var doubles = new double[floats.Length];
                for (var i = 0; i < floats.Length; i++) doubles[i] = floats[i];
                queryDim = doubles.Length;
                return doubles;
            }

            var generator = optickActive ? (Func<string, Task<double[]>>)MusicalVectorAsync : OllamaVectorAsync;
            mode = optickActive ? "optk" : "ollama";

            var searchStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var results = await voicingSearch.SearchAsync(query, generator, limit);
            var total = System.Diagnostics.Stopwatch.GetElapsedTime(searchStart).TotalMilliseconds;
            searchMs = Math.Max(0, total - extractMs - encodeMs - embedMs);

            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation(
                "semantic-knowledge mode={Mode} strategy={Strategy} dim={Dim} chord={Chord} mode_name={ModeName} tags={Tags} " +
                "query={Query} limit={Limit} returned={Count} extract_ms={ExtractMs:F1} encode_ms={EncodeMs:F1} " +
                "embed_ms={EmbedMs:F1} search_ms={SearchMs:F1}",
                mode, voicingSearch.StrategyName, queryDim,
                structured?.ChordSymbol, structured?.ModeName,
                structured?.Tags is { Count: > 0 } t ? string.Join(",", t) : "",
                query, limit, results.Count, extractMs, encodeMs, embedMs, searchMs);

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
            logger.LogError(ex,
                "semantic-knowledge FAILED mode={Mode} query={Query} limit={Limit}. Returning empty results.",
                mode, query, limit);
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
