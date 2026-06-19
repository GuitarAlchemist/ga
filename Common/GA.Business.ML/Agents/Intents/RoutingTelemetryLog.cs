namespace GA.Business.ML.Agents.Intents;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Append-only per-day JSONL telemetry for <see cref="SemanticIntentRouter"/>
///     decisions. Shipped 2026-05-31 to close the "we're tuning the router blind"
///     gap: the regex tie-break hints in <see cref="DefaultRoutingHintProvider"/>
///     were authored against the fixed 86-prompt eval corpus, so the harness
///     measures training accuracy, not generalization. This sink captures EVERY
///     live routing decision (query + the top-3 candidates and their scores +
///     whether the router fell through) so an UNCONTAMINATED held-out eval set
///     can be mined from real traffic and hand-labeled — the prerequisite for
///     trusting any future router change (more hints OR a learned head).
///     <para>
///         Mirrors <c>VoicingTelemetryLog</c> exactly (daily rotation, error
///         swallowing, env disable) so the two telemetry streams stay operationally
///         identical. One line per <see cref="SemanticIntentRouter.RouteAsync"/>
///         call that reached the scoring stage.
///     </para>
/// </summary>
public static class RoutingTelemetryLog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Lock _writeLock = new();

    /// <summary>
    ///     Environment variable to disable telemetry (e.g. in test/CI runs).
    ///     Set <c>GA_ROUTING_NO_TELEMETRY=1</c> to suppress all writes.
    /// </summary>
    public const string DisableEnvVar = "GA_ROUTING_NO_TELEMETRY";

    /// <summary>
    ///     Resolves the directory holding the rolling JSONL files. Override via
    ///     <c>GA_ROUTING_TELEMETRY_DIR</c>; default is
    ///     <c>{repo-root}/state/telemetry/routing/</c>.
    /// </summary>
    public static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("GA_ROUTING_TELEMETRY_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        // Walk up from the app base toward the repo root looking for a `state/` sibling.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "telemetry", "routing");
            if (Directory.Exists(Path.Combine(dir.FullName, "state"))) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "state", "telemetry", "routing");
    }

    public static string CurrentDayFile() =>
        Path.Combine(ResolveDirectory(), $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    /// <summary>
    ///     Appends a single record. Silently swallows I/O errors — telemetry must
    ///     never break the routing path.
    /// </summary>
    public static void Append(RoutingTelemetryRecord record)
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
            // telemetry failure must be invisible to the routing caller
        }
    }

    /// <summary>
    ///     Reads the last <paramref name="limit"/> records from today's file (or a
    ///     specific <paramref name="day"/>). Convenience for eval-set mining + tests.
    /// </summary>
    public static IReadOnlyList<RoutingTelemetryRecord> ReadRecent(int limit = 100, DateOnly? day = null)
    {
        var target = day?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var path = Path.Combine(ResolveDirectory(), $"{target}.jsonl");
        if (!File.Exists(path)) return [];

        try
        {
            var lines = File.ReadAllLines(path);
            var slice = lines.Length > limit ? lines[^limit..] : lines;
            var records = new List<RoutingTelemetryRecord>(slice.Length);
            foreach (var line in slice)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var rec = JsonSerializer.Deserialize<RoutingTelemetryRecord>(line, JsonOpts);
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

/// <summary>One telemetry record per routing decision. Narrow on purpose: just
/// what's needed to mine a held-out eval set and study the threshold/margin.</summary>
public sealed record RoutingTelemetryRecord
{
    /// <summary>ISO-8601 UTC timestamp.</summary>
    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    /// <summary>Raw user query text (the label-able surface for eval mining).</summary>
    [JsonPropertyName("q")]
    public required string Query { get; init; }

    /// <summary>Chosen intent id, or null if the router fell through (below threshold).</summary>
    [JsonPropertyName("chosen")]
    public string? Chosen { get; init; }

    /// <summary>Final (hint-boosted) confidence of the winner, or null on fall-through.</summary>
    [JsonPropertyName("conf")]
    public double? Confidence { get; init; }

    /// <summary>The MinConfidence threshold in force for this decision.</summary>
    [JsonPropertyName("threshold")]
    public double Threshold { get; init; }

    /// <summary>True if no intent cleared the threshold (caller falls through to LLM).</summary>
    [JsonPropertyName("fell_through")]
    public bool FellThrough { get; init; }

    /// <summary>Top-1 minus top-2 final score — the escalate-on-ambiguity signal. Null if &lt;2 candidates.</summary>
    [JsonPropertyName("margin")]
    public double? Margin { get; init; }

    /// <summary>Top-N candidates (highest first), each with base cosine, hint boost, and final score.</summary>
    [JsonPropertyName("candidates")]
    public IReadOnlyList<RoutingTelemetryCandidate>? Candidates { get; init; }

    /// <summary>Routing latency in milliseconds (includes the query embedding call).</summary>
    [JsonPropertyName("ms")]
    public double LatencyMs { get; init; }
}

/// <summary>One candidate intent in a logged routing decision.</summary>
public sealed record RoutingTelemetryCandidate(
    [property: JsonPropertyName("id")]    string IntentId,
    [property: JsonPropertyName("base")]  double BaseScore,
    [property: JsonPropertyName("boost")] double Boost,
    [property: JsonPropertyName("final")] double FinalScore);
