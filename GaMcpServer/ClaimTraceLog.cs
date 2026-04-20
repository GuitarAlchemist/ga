namespace GaMcpServer;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Append-only per-day JSONL claim trace for MCP tool invocations. Every
///     successful domain-tool call becomes one line — the canonical feedstock
///     for the TARS Knowledge Graph bridge.
///     <para>
///         Design rationale: <c>ix/docs/plans/2026-04-19-tars-graph-persistence.md</c>
///         (plus the chatbot-KG follow-up plan). Extracting claims at the MCP
///         tool-call layer — not from LLM narration — sidesteps hallucination
///         extraction entirely. Every record here is grounded in a real GA
///         domain closure that succeeded with structured inputs and outputs.
///     </para>
///     <para>
///         Mirrors the <c>VoicingTelemetryLog</c> pattern deliberately: same
///         daily-rotation filename, same silent-failure contract, same env-var
///         shape. Shares no code today because the schemas differ; if a third
///         JSONL logger lands, promote both to a shared base.
///     </para>
/// </summary>
public static class ClaimTraceLog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Lock _writeLock = new();

    /// <summary>Process-lifetime session id. Claims from one MCP run share this.</summary>
    public static readonly string ProcessSessionId =
        $"ga-mcp-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

    /// <summary>Set <c>GA_CLAIMS_NO_TRACE=1</c> to suppress all writes (tests, CI).</summary>
    public const string DisableEnvVar = "GA_CLAIMS_NO_TRACE";

    /// <summary>
    ///     Resolves the directory holding the rolling JSONL files.
    ///     Override via <c>GA_CLAIMS_DIR</c>; default walks up from the app base
    ///     looking for a <c>state/</c> sibling and lands at
    ///     <c>{repo-root}/state/claims/</c>.
    /// </summary>
    public static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("GA_CLAIMS_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "state", "claims");
            if (Directory.Exists(Path.Combine(dir.FullName, "state"))) return candidate;
            dir = dir.Parent;
        }
        return Path.Combine(AppContext.BaseDirectory, "state", "claims");
    }

    public static string CurrentDayFile() =>
        Path.Combine(ResolveDirectory(), $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    /// <summary>
    ///     Appends a single record. Silently swallows I/O errors — tracing must
    ///     never break the tool-invocation path.
    /// </summary>
    public static void Append(ClaimTraceRecord record)
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
            // tracing failure must be invisible to the tool caller
        }
    }
}

/// <summary>
///     One claim trace record per successful (or failed) MCP tool invocation.
///     The TARS ChatbotClaimsBridge ingests these into the knowledge graph;
///     the schema is deliberately wide enough to let the bridge derive
///     <c>(subject, predicate, object)</c> tuples without re-running the tool.
/// </summary>
public sealed record ClaimTraceRecord
{
    /// <summary>ISO-8601 UTC timestamp.</summary>
    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    /// <summary>Process-lifetime session id (see <c>ClaimTraceLog.ProcessSessionId</c>).</summary>
    [JsonPropertyName("session")]
    public required string SessionId { get; init; }

    /// <summary>Source channel: "ga-mcp" today; "ga-api" when the GaApi wrapper lands.</summary>
    [JsonPropertyName("src")]
    public required string Source { get; init; }

    /// <summary>F# closure name invoked, e.g. "domain.parseChord".</summary>
    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    /// <summary>Structured tool inputs. Values are boxed primitives / strings.</summary>
    [JsonPropertyName("inputs")]
    public required IReadOnlyDictionary<string, object?> Inputs { get; init; }

    /// <summary>"ok" (result field populated) or "error" (error field populated).</summary>
    [JsonPropertyName("outcome")]
    public required string Outcome { get; init; }

    /// <summary>Tetravalent truth value — always "T" for ok, "U" for error. Hexavalent P/D reserved; see memory note on paused migration.</summary>
    [JsonPropertyName("truth")]
    public required string Truth { get; init; }

    /// <summary>Formatted result string from the F# closure (JSON or text depending on closure).</summary>
    [JsonPropertyName("result")]
    public string? Result { get; init; }

    /// <summary>Error message if outcome="error".</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>End-to-end latency in milliseconds.</summary>
    [JsonPropertyName("ms")]
    public double LatencyMs { get; init; }
}
