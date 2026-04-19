namespace GaMcpServer.Tools;

using System.ComponentModel;
using System.Text.Json;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Search;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool exposing OPTIC-K voicing search. Accepts natural language — the typed
///     extractor pulls chord/mode/tags, the musical encoder produces a 112-dim OPTK vector,
///     and the mmap reader returns top-K voicings with diagrams and metadata.
///
///     No cloud dependency: the LLM-fallback tier is intentionally disabled here. Callers
///     who want fuzzy-query support can either (a) use the higher-level <c>ga_voicing_chat</c>
///     path through GaApi, or (b) supply a pre-parsed chord symbol in their query text.
/// </summary>
[McpServerToolType]
public static class VoicingSearchTool
{
    private static readonly Lazy<string> IndexPath = new(() =>
    {
        var overridePath = Environment.GetEnvironmentVariable("GA_OPTICK_INDEX_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        // Walk up from the entrypoint directory toward the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "voicings", "optick.index");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "optick.index not found. Set GA_OPTICK_INDEX_PATH or place the file under state/voicings/optick.index.");
    });

    private static readonly Lazy<OptickSearchStrategy> Strategy = new(() =>
        new OptickSearchStrategy(IndexPath.Value));

    private static readonly Lazy<MusicalQueryEncoder> Encoder = new(() =>
        new MusicalQueryEncoder(
            new TheoryVectorService(),
            new ModalVectorService(),
            new SymbolicVectorService(),
            new RootVectorService()));

    private static readonly Lazy<TypedMusicalQueryExtractor> Extractor = new(() =>
        new TypedMusicalQueryExtractor());

    [McpServerTool]
    [Description(
        "Search the OPTIC-K voicing index by natural-language query. " +
        "Parses chord symbols (Cmaj7, F#m7b5), mode names (Lydian, Dorian), and " +
        "technique/style tags (drop2, shell, jazz) from the query, composes a 112-dim " +
        "OPTK v4 vector via partition-weighted musical encoding, and returns top-K " +
        "matching voicings. Works fully offline. Fuzzy queries without a recognizable " +
        "chord return empty results — supply a chord symbol for meaningful retrieval.")]
    public static async Task<string> GaSearchVoicings(
        [Description("Natural-language query (e.g. 'Cmaj7 drop2 jazz', 'F# Lydian', 'Dm7')")]
        string query,
        [Description("Maximum results to return (default 10, capped at 50)")]
        int limit = 10,
        [Description("Optional instrument filter: guitar | bass | ukulele")]
        string? instrument = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { error = "query is required" });
        }

        limit = Math.Clamp(limit, 1, 50);

        var structured = await Extractor.Value.ExtractAsync(query);
        var hasIntent = structured.ChordSymbol is not null
                        || (structured.PitchClasses is { Length: > 0 })
                        || structured.ModeName is not null
                        || (structured.Tags is { Count: > 0 });

        if (!hasIntent)
        {
            return JsonSerializer.Serialize(new
            {
                query,
                interpreted = (object?)null,
                results = Array.Empty<object>(),
                note = "No chord, mode, or known tag recognized. Supply a chord symbol " +
                       "(e.g. 'Cmaj7') or a tag (e.g. 'drop2', 'jazz') for retrieval."
            });
        }

        var queryVector = Encoder.Value.Encode(structured);

        // Filter by instrument via hybrid search path so the mmap's per-instrument
        // slice is used when requested.
        List<GA.Business.ML.Search.VoicingSearchResult> hits;
        if (!string.IsNullOrWhiteSpace(instrument))
        {
            var filters = new GA.Business.ML.Search.VoicingSearchFilters(VoicingType: instrument);
            hits = await Strategy.Value.HybridSearchAsync(queryVector, filters, limit);
        }
        else
        {
            hits = await Strategy.Value.SemanticSearchAsync(queryVector, limit);
        }

        return JsonSerializer.Serialize(new
        {
            query,
            interpreted = new
            {
                chord = structured.ChordSymbol,
                rootPitchClass = structured.RootPitchClass,
                pitchClasses = structured.PitchClasses,
                mode = structured.ModeName,
                tags = structured.Tags
            },
            resultCount = hits.Count,
            results = hits.Select(r => new
            {
                score = Math.Round(r.Score, 4),
                diagram = r.Document.Diagram,
                chordName = r.Document.ChordName,
                instrument = r.Document.VoicingType,
                midiNotes = r.Document.MidiNotes,
                pitchClasses = r.Document.PitchClasses
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description(
        "Return basic statistics about the OPTIC-K voicing index currently loaded: " +
        "total voicing count, index path, schema hash. Useful as a liveness check.")]
    public static string GaVoicingIndexInfo()
    {
        try
        {
            var stats = Strategy.Value.GetStats();
            return JsonSerializer.Serialize(new
            {
                indexPath = IndexPath.Value,
                totalVoicings = stats.TotalVoicings,
                averageSearchMs = stats.AverageSearchTime.TotalMilliseconds,
                totalSearches = stats.TotalSearches,
                schemaHash = $"0x{GA.Business.ML.Embeddings.EmbeddingSchema.SchemaHashV4:X8}",
                dimension = OptickIndexReader.Dimension
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
