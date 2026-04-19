namespace GaMcpServer.Tools;

using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GA.Business.Core.Analysis.Voicings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Search;
using GA.Domain.Services;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool exposing OPTIC-K voicing search. Accepts natural language — the typed
///     extractor pulls chord/mode/tags, the musical encoder produces a 112-dim OPTK vector,
///     and the mmap reader returns top-K voicings with diagrams and metadata.
///
///     When the typed extractor finds nothing, callers that opt in via
///     <c>allowSampling: true</c> trigger an MCP <c>sampling/createMessage</c> callback
///     to the client (e.g. Claude Code) so the client's own model can extract
///     <c>{chord, mode, tags}</c> from truly fuzzy phrasing. Default remains off so the
///     offline-only behavior is preserved for callers that don't want cloud latency.
/// </summary>
[McpServerToolType]
public static class VoicingSearchTool
{
    /// <summary>Depth guard for sampling reentrancy (design guard #7 — max depth 1).</summary>
    private static readonly AsyncLocal<int> _samplingDepth = new();

    /// <summary>
    ///     Pinned system prompt for the sampling call. Kept short and deterministic.
    ///     Hash-pinned at startup (see <see cref="SamplingSystemPromptSha256"/>) so an
    ///     accidental edit forces a code review instead of silently changing behavior.
    /// </summary>
    private const string SamplingSystemPrompt =
        "You extract musical intent from a guitar-voicing query. Respond with JSON only — no prose, no code fences.\n" +
        "Schema: {\"chord\": \"<canonical symbol like Cmaj7 or null>\", \"mode\": \"<scale name or null>\", \"tags\": [\"lowercase tokens\"]}\n" +
        "Rules: 'chord' = root+quality form, null if not mentioned. 'mode' = null if no mode/scale named. " +
        "'tags' ≤ 6 lowercase style/technique tokens (jazz, blues, rock, drop2, shell, quartal, rootless, etc.).";

    /// <summary>
    ///     SHA-256 of <see cref="SamplingSystemPrompt"/> — verified at first sampling call
    ///     so any accidental edit to the prompt forces a code review. If this hash changes
    ///     without an intentional update here, the sampling path hard-fails closed.
    /// </summary>
    private const string SamplingSystemPromptSha256 =
        "61ed0aadc1ebbb48c4207aa73a61ad553952e1586f76e6613eb675d2cc94c8e5";

    private static readonly Lazy<bool> SamplingPromptVerified = new(() =>
    {
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SamplingSystemPrompt)))
                            .ToLowerInvariant();
        return actual == SamplingSystemPromptSha256;
    });

    private static readonly JsonSerializerOptions SamplingJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        "matching voicings. Works fully offline for canonical vocabulary. " +
        "For fuzzy phrasing (\"something warm and mellow\") the caller can opt in to " +
        "MCP sampling via allowSampling=true — the client's model extracts chord/mode/tags.")]
    public static async Task<string> GaSearchVoicings(
        McpServer server,
        [Description("Natural-language query (e.g. 'Cmaj7 drop2 jazz', 'F# Lydian', 'Dm7')")]
        string query,
        [Description("Maximum results to return (default 10, capped at 50)")]
        int limit = 10,
        [Description("Optional instrument filter: guitar | bass | ukulele")]
        string? instrument = null,
        [Description("When true AND the typed extractor finds nothing, call back to the client " +
                     "for LLM-based query extraction via MCP sampling. Default false keeps the " +
                     "tool fully offline. Adds up to ~10s latency on fuzzy queries.")]
        bool allowSampling = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(query))
        {
            return JsonSerializer.Serialize(new { error = "query is required" });
        }

        // Guard #1 — input length cap.
        if (query.Length > 512)
        {
            return JsonSerializer.Serialize(new
            {
                error = "query too long",
                note = "The query must be 512 characters or fewer. Please shorten it."
            });
        }

        limit = Math.Clamp(limit, 1, 50);

        var structured = await Extractor.Value.ExtractAsync(query, cancellationToken);
        var samplingFired = false;

        // Fuzzy fallback — typed extractor found nothing, caller opted in.
        if (!HasIntent(structured) && allowSampling)
        {
            var sampled = await TrySampleQueryAsync(server, query, cancellationToken);
            if (sampled is not null)
            {
                structured = sampled;
                samplingFired = true;
            }
        }
        var hasIntent = HasIntent(structured);

        // Explicit instrument parameter wins over the one parsed out of the query text.
        // This lets programmatic callers override natural-language hints if they want.
        var effectiveInstrument = string.IsNullOrWhiteSpace(instrument)
            ? structured.Instrument
            : instrument;

        if (!hasIntent)
        {
            stopwatch.Stop();
            VoicingTelemetryLog.Append(new VoicingTelemetryRecord
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Source = "mcp",
                Query = query,
                Chord = null, Mode = null, Tags = null,
                ResultCount = 0,
                TopScore = null,
                InstrumentFilter = effectiveInstrument,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds
            });
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
        if (!string.IsNullOrWhiteSpace(effectiveInstrument))
        {
            var filters = new GA.Business.ML.Search.VoicingSearchFilters(VoicingType: effectiveInstrument);
            hits = await Strategy.Value.HybridSearchAsync(queryVector, filters, limit);
        }
        else
        {
            hits = await Strategy.Value.SemanticSearchAsync(queryVector, limit);
        }

        stopwatch.Stop();
        VoicingTelemetryLog.Append(new VoicingTelemetryRecord
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Source = "mcp",
            Query = query,
            Chord = structured.ChordSymbol,
            Mode = structured.ModeName,
            Tags = structured.Tags,
            ResultCount = hits.Count,
            TopScore = hits.Count > 0 ? Math.Round(hits[0].Score, 4) : null,
            InstrumentFilter = effectiveInstrument,
            LatencyMs = stopwatch.Elapsed.TotalMilliseconds
        });

        return JsonSerializer.Serialize(new
        {
            query,
            interpreted = new
            {
                chord = structured.ChordSymbol,
                rootPitchClass = structured.RootPitchClass,
                pitchClasses = structured.PitchClasses,
                mode = structured.ModeName,
                tags = structured.Tags,
                instrument = effectiveInstrument,
                samplingFired,
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

    private static bool HasIntent(StructuredQuery q) =>
        q.ChordSymbol is not null
        || (q.PitchClasses is { Length: > 0 })
        || q.ModeName is not null
        || (q.Tags is { Count: > 0 });

    /// <summary>
    ///     MCP sampling fallback: call back to the client's model to extract musical
    ///     intent from a fuzzy query. Implements all ten guards from
    ///     <c>docs/plans/2026-04-19-mcp-sampling-fallback.md</c>. Returns null on any
    ///     failure — callers fall through to the honest-empty response.
    /// </summary>
    private static async Task<StructuredQuery?> TrySampleQueryAsync(
        McpServer server, string query, CancellationToken outerCt)
    {
        // Guard #7 — depth counter (prevents nested sampling → tool call → sampling).
        if (_samplingDepth.Value >= 1) return null;

        // Guard #8 — capability probe. Client must advertise Sampling.
        if (server.ClientCapabilities?.Sampling is null) return null;

        // Startup verification for the pinned system prompt. If the constant has been
        // edited without also updating the hash, refuse to sample — forces code review.
        if (!SamplingPromptVerified.Value) return null;

        // Guard #2 — control-char strip (U+0000..U+001F).
        var safeQuery = new string(query.Where(c => c >= ' ').ToArray());
        if (string.IsNullOrWhiteSpace(safeQuery)) return null;

        // Guard #6 — hard 10 s timeout.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        _samplingDepth.Value++;
        try
        {
            var req = new CreateMessageRequestParams
            {
                // Guard #3 — delimited wrapping. Cheap defense-in-depth against
                // naive prompt-injection attempts.
                Messages =
                [
                    new SamplingMessage
                    {
                        Role = Role.User,
                        Content = [new TextContentBlock { Text = $"<query>{safeQuery}</query>" }]
                    }
                ],
                SystemPrompt = SamplingSystemPrompt,
                MaxTokens = 128,         // Guard #4 — output-token cap.
                Temperature = 0.0f,      // Guard #5 — deterministic.
            };

            var response = await server.SampleAsync(req, cts.Token);
            var text = response.Content?
                .OfType<TextContentBlock>()
                .Select(t => t.Text)
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
            return ParseAndAllowList(text);
        }
        catch
        {
            return null;
        }
        finally
        {
            _samplingDepth.Value--;
        }
    }

    /// <summary>
    ///     Guard #9 — output allow-list. Reject anything the deterministic parser
    ///     doesn't already accept, so a malicious sampling response can't inject
    ///     arbitrary tags or fake chord symbols that bypass the canonical vocabulary.
    /// </summary>
    private static StructuredQuery? ParseAndAllowList(string llmText)
    {
        var firstBrace = llmText.IndexOf('{');
        var lastBrace = llmText.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace) return null;

        RawSampling? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawSampling>(llmText[firstBrace..(lastBrace + 1)], SamplingJsonOpts);
        }
        catch (JsonException) { return null; }
        if (raw is null) return null;

        int? root = null;
        int[]? pcs = null;
        string? chord = null;
        if (!string.IsNullOrWhiteSpace(raw.Chord)
            && ChordPitchClasses.TryParse(raw.Chord, out var r, out var pitches))
        {
            chord = raw.Chord;
            root = r;
            pcs = pitches;
        }

        var registry = SymbolicTagRegistry.Instance;
        var tags = raw.Tags?
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length >= 3)
            .Select(t => t.ToLowerInvariant())
            .Where(t => registry.GetBitIndex(t).HasValue)
            .Distinct()
            .ToList();

        return new StructuredQuery(
            ChordSymbol: chord,
            RootPitchClass: root,
            PitchClasses: pcs,
            ModeName: string.IsNullOrWhiteSpace(raw.Mode) ? null : raw.Mode,
            Tags: tags is null || tags.Count == 0 ? null : tags);
    }

    private sealed record RawSampling
    {
        [JsonPropertyName("chord")] public string? Chord { get; init; }
        [JsonPropertyName("mode")]  public string? Mode { get; init; }
        [JsonPropertyName("tags")]  public List<string>? Tags { get; init; }
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
