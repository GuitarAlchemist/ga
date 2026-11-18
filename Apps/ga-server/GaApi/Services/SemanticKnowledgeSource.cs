namespace GaApi.Services;

using System.Text;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Notation;
using GA.Business.ML.Search;

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

            mode = optickActive ? "optk" : "ollama";

            var searchStart = System.Diagnostics.Stopwatch.GetTimestamp();
            List<VoicingSearchResult> results;
            if (optickActive)
            {
                // Extract once: the parsed chord shapes the vector and decides the ranking. The
                // vector alone ranks a chord's supersets and neighbours above it (Gadd9 above every
                // G triad, Cmaj9 above every E7#9 on the 2026-04-19 index), so voicings named after
                // the parsed chord come first. The plain vector ranking is the fallback when the
                // corpus labels no voicing with that symbol, as in VoicingAgent.
                var vector = await MusicalVectorAsync(query);
                Task<double[]> Vector(string _) => Task.FromResult(vector);

                results = structured?.ChordSymbol is { } chord && NameFilterReadsWhole(chord)
                    ? await voicingSearch.SearchAsync(
                        query, Vector, limit, new VoicingSearchFilters(ChordName: chord), cancellationToken)
                    : [];
                if (results.Count == 0)
                {
                    results = await voicingSearch.SearchAsync(query, Vector, limit, cancellationToken: cancellationToken);
                }
            }
            else
            {
                results = await voicingSearch.SearchAsync(query, OllamaVectorAsync, limit, cancellationToken: cancellationToken);
            }
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

            VoicingTelemetryLog.Append(new VoicingTelemetryRecord
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Source = "chatbot",
                Query = query,
                Chord = structured?.ChordSymbol,
                Mode = structured?.ModeName,
                Tags = structured?.Tags,
                ResultCount = results.Count,
                TopScore = results.Count > 0 ? Math.Round(results[0].Score, 4) : null,
                InstrumentFilter = null,
                LatencyMs = extractMs + encodeMs + embedMs + searchMs
            });

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
    ///     False when the chord-name filter would drop part of the symbol: it reads <c>(...)</c> as a
    ///     stored voicing tag (<c>Cmaj7(shell)</c>) and <c>/</c> as a slash bass, so <c>Em(maj7)</c>
    ///     would search a plain Em triad and <c>C6/9</c> a C6. Those keep the vector ranking, which
    ///     carries the added tone.
    /// </summary>
    private static bool NameFilterReadsWhole(string chord)
    {
        var slash = chord.IndexOf('/');
        return !chord.Contains('(') && !(slash >= 0 && slash + 1 < chord.Length && char.IsDigit(chord[slash + 1]));
    }

    /// <summary>
    ///     Formats a VoicingDocument into LLM-friendly content with rich metadata.
    /// </summary>
    private static string FormatVoicingForLlm(ChordVoicingRagDocument doc)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## {doc.ChordName}");
        sb.AppendLine($"Diagram (lowest string first): `{PlayableNotationFormatter.ToChartOrder(doc.Diagram)}`");

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
