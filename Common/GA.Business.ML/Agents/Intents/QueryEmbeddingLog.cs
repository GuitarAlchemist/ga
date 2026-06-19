namespace GA.Business.ML.Agents.Intents;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Append-only per-day JSONL sink of the EXACT query embedding the
///     <see cref="SemanticIntentRouter"/> scored with — one row per routed query —
///     plus the decision it drove. Cross-repo Contract B (GA → ix): the sibling
///     <c>ix-duck</c> analyst bench reads <c>state/quality/query-embeddings/*.jsonl</c>
///     and computes mean-top-3 cosine vs an in-domain reference set to flag
///     out-of-domain queries (the raw-cosine method ix's ROC sweep validated).
///     <para>
///         CRITICAL: this persists the vector the router actually routed on — NOT a
///         re-embed — so the OOD lens analyses real routing decisions. The embedder
///         + dim are recorded dynamically from the live generator (today
///         <c>bge-large</c> / 1024-d, per the Phase-2 routing swap), never hardcoded.
///     </para>
///     <para>
///         Mirrors <see cref="RoutingTelemetryLog"/> (daily rotation, write lock,
///         error swallowing, env disable) so the streams stay operationally
///         identical. Distinct from telemetry: telemetry keeps the decision; this
///         keeps the VECTOR (bulky), so it lands under <c>state/quality/</c> and is
///         gitignored — ix-duck reads the working tree cross-repo, no commit needed.
///     </para>
/// </summary>
public static class QueryEmbeddingLog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Lock _writeLock = new();

    /// <summary>
    ///     Set <c>GA_QUERY_EMBEDDING_NO_LOG=1</c> to suppress all writes (test/CI, or
    ///     deployments that don't want per-query vectors on disk).
    /// </summary>
    public const string DisableEnvVar = "GA_QUERY_EMBEDDING_NO_LOG";

    /// <summary>
    ///     Directory holding the rolling JSONL files. Override via
    ///     <c>GA_QUERY_EMBEDDING_DIR</c>; default <c>{repo-root}/state/quality/query-embeddings/</c>.
    /// </summary>
    public static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("GA_QUERY_EMBEDDING_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        // Walk up from the app base toward the repo root looking for a `state/` sibling.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "quality", "query-embeddings");
            if (Directory.Exists(Path.Combine(dir.FullName, "state"))) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "state", "quality", "query-embeddings");
    }

    public static string CurrentDayFile() =>
        Path.Combine(ResolveDirectory(), $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    /// <summary>
    ///     Appends a single record. Silently swallows I/O errors — this sink must
    ///     never break the routing path.
    /// </summary>
    public static void Append(QueryEmbeddingRecord record)
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
            // sink failure must be invisible to the routing caller
        }
    }
}

/// <summary>
///     One row per routed query: the decision plus the exact vector that produced
///     it. Field names match the cross-repo Contract B shape ratified with ix.
/// </summary>
public sealed record QueryEmbeddingRecord
{
    /// <summary>Stable id for this routed query (correlate with telemetry / traces).</summary>
    [JsonPropertyName("query_id")]
    public required string QueryId { get; init; }

    /// <summary>ISO-8601 UTC timestamp.</summary>
    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    /// <summary>Raw user query text (GA's own telemetry; the label-able surface).</summary>
    [JsonPropertyName("query_text")]
    public required string QueryText { get; init; }

    /// <summary>Chosen intent id, or null if the router declined (below threshold).</summary>
    [JsonPropertyName("intent")]
    public string? Intent { get; init; }

    /// <summary>How the query routed: <c>embedding</c> (cleared threshold) or <c>fallback</c> (declined).</summary>
    [JsonPropertyName("route_method")]
    public required string RouteMethod { get; init; }

    /// <summary>Final (hint-boosted) confidence of the top candidate — recorded even when declined.</summary>
    [JsonPropertyName("route_confidence")]
    public double RouteConfidence { get; init; }

    /// <summary>Embedder model id, resolved live from the generator (e.g. <c>bge-large</c>).</summary>
    [JsonPropertyName("embedder")]
    public required string Embedder { get; init; }

    /// <summary>Embedding dimensionality (== <see cref="Embedding"/> length; explicit per contract).</summary>
    [JsonPropertyName("dim")]
    public int Dim { get; init; }

    /// <summary>The exact vector the router scored with (NOT a re-embed).</summary>
    [JsonPropertyName("embedding")]
    public required IReadOnlyList<float> Embedding { get; init; }
}
