namespace GA.Business.ML.Search;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Append-only per-day JSONL telemetry for voicing-retrieval calls. Shipped
///     2026-04-19 to close the "we're prioritizing blind" gap — without usage data
///     every next-feature decision was speculative.
///     <para>
///         One line per retrieval call across both the MCP tool path
///         (<c>VoicingSearchTool.GaSearchVoicings</c>) and the web-chatbot path
///         (<c>SemanticKnowledgeSource.SearchAsync</c>). Daily rotation by filename.
///     </para>
///     <para>
///         Schema kept deliberately narrow: the fields that drive prioritization
///         decisions (empty-result rate, vocabulary-miss rate, chord-vs-tag split,
///         latency) and nothing more. Expand only when a specific question can't be
///         answered with what's here.
///     </para>
/// </summary>
public static class VoicingTelemetryLog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Lock _writeLock = new();

    /// <summary>
    ///     Environment variable to disable telemetry (e.g. in test runs).
    ///     Set <c>GA_VOICING_NO_TELEMETRY=1</c> to suppress all writes.
    /// </summary>
    public const string DisableEnvVar = "GA_VOICING_NO_TELEMETRY";

    /// <summary>
    ///     Resolves the directory holding the rolling JSONL files.
    ///     Override via <c>GA_VOICING_TELEMETRY_DIR</c>; default is
    ///     <c>{repo-root}/state/telemetry/voicing-search/</c>.
    /// </summary>
    public static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("GA_VOICING_TELEMETRY_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        // Walk up from the app base toward the repo root looking for a `state/` sibling.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "telemetry", "voicing-search");
            if (Directory.Exists(Path.Combine(dir.FullName, "state"))) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "state", "telemetry", "voicing-search");
    }

    public static string CurrentDayFile() =>
        Path.Combine(ResolveDirectory(), $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    /// <summary>
    ///     Appends a single record. Silently swallows I/O errors — telemetry must
    ///     never break the retrieval path.
    /// </summary>
    public static void Append(VoicingTelemetryRecord record)
    {
        if (Environment.GetEnvironmentVariable(DisableEnvVar) == "1") return;

        try
        {
            var path = CurrentDayFile();
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(record, JsonOpts);
            lock (_writeLock)
            {
                File.AppendAllText(path, json + "\n");
            }
        }
        catch
        {
            // telemetry failure must be invisible to the retrieval caller
        }
    }

    /// <summary>
    ///     Reads the last <paramref name="limit"/> records from today's file (or
    ///     a specific <paramref name="day"/> if provided). Convenience for the
    ///     companion MCP read-back tool.
    /// </summary>
    public static IReadOnlyList<VoicingTelemetryRecord> ReadRecent(int limit = 100, DateOnly? day = null)
    {
        var target = day?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var path = Path.Combine(ResolveDirectory(), $"{target}.jsonl");
        if (!File.Exists(path)) return [];

        try
        {
            var lines = File.ReadAllLines(path);
            var slice = lines.Length > limit ? lines[^limit..] : lines;
            var records = new List<VoicingTelemetryRecord>(slice.Length);
            foreach (var line in slice)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var rec = JsonSerializer.Deserialize<VoicingTelemetryRecord>(line, JsonOpts);
                if (rec is not null) records.Add(rec);
            }
            return records;
        }
        catch
        {
            return [];
        }
    }
}

/// <summary>
///     One telemetry record per retrieval call. Kept narrow — just the fields
///     needed to distinguish "jazz tag hit" from "empty-result vocab miss" from
///     "voice-leading request we can't serve yet".
/// </summary>
public sealed record VoicingTelemetryRecord
{
    /// <summary>ISO-8601 UTC timestamp.</summary>
    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    /// <summary>Where the call came from: "mcp" or "chatbot".</summary>
    [JsonPropertyName("src")]
    public required string Source { get; init; }

    /// <summary>Raw user query text.</summary>
    [JsonPropertyName("q")]
    public required string Query { get; init; }

    /// <summary>Canonical chord symbol extracted, or null if none parsed.</summary>
    [JsonPropertyName("chord")]
    public string? Chord { get; init; }

    /// <summary>Mode name extracted, or null.</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>Tags extracted (canonical form); empty list if none.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Number of results returned.</summary>
    [JsonPropertyName("results")]
    public int ResultCount { get; init; }

    /// <summary>Top-ranked score, or null if empty.</summary>
    [JsonPropertyName("top")]
    public double? TopScore { get; init; }

    /// <summary>Instrument filter applied, or null.</summary>
    [JsonPropertyName("instr")]
    public string? InstrumentFilter { get; init; }

    /// <summary>End-to-end latency in milliseconds.</summary>
    [JsonPropertyName("ms")]
    public double LatencyMs { get; init; }

    /// <summary>True if the call produced zero results — the highest-value signal.</summary>
    [JsonPropertyName("empty")]
    public bool Empty => ResultCount == 0;
}
