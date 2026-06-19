namespace GA.Business.ML.Agents.Intents;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Append-only per-day JSONL log of SHADOW routing comparisons (Hermes
///     Spike-A). One line per <see cref="SemanticIntentRouter.RouteAsync"/> call
///     while shadow mode is on: the production router's chosen intent vs the
///     learned head's chosen intent on the SAME query embedding. Lets us measure
///     head-vs-production agreement and mine a real-traffic eval set without
///     touching routing behavior.
///     <para>
///         Self-gated: only written when <see cref="LearnedHeadShadow.Instance"/>
///         is active (<c>GA_ROUTER_SHADOW=1</c>), so unlike the main telemetry it
///         does NOT honor <c>GA_ROUTING_NO_TELEMETRY</c> — that lets a held-out
///         smoke suppress production telemetry while still capturing shadow rows.
///         Output dir overridable via <c>GA_ROUTER_SHADOW_DIR</c>; default
///         <c>{repo-root}/state/telemetry/routing-shadow/</c>.
///     </para>
/// </summary>
public static class RoutingShadowLog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Lock WriteLock = new();

    public static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("GA_ROUTER_SHADOW_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "state")))
                return Path.Combine(dir.FullName, "state", "telemetry", "routing-shadow");
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "state", "telemetry", "routing-shadow");
    }

    public static string CurrentDayFile() =>
        Path.Combine(ResolveDirectory(), $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

    public static void Append(RoutingShadowRecord record)
    {
        try
        {
            var path = CurrentDayFile();
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(record, JsonOpts);
            lock (WriteLock)
            {
                File.AppendAllText(path, json + "\n");
            }
        }
        catch
        {
            // shadow logging failure must be invisible to the routing caller
        }
    }
}

/// <summary>One shadow comparison record per routing decision.</summary>
public sealed record RoutingShadowRecord
{
    [JsonPropertyName("ts")] public required string Timestamp { get; init; }
    [JsonPropertyName("q")] public required string Query { get; init; }

    /// <summary>Intent the PRODUCTION router chose (null = fell through / declined).</summary>
    [JsonPropertyName("prod")] public string? ProdChosen { get; init; }

    /// <summary>Intent the LEARNED HEAD chose (null = declined below tau).</summary>
    [JsonPropertyName("head")] public string? HeadChosen { get; init; }

    [JsonPropertyName("head_conf")] public double HeadConfidence { get; init; }
    [JsonPropertyName("head_declined")] public bool HeadDeclined { get; init; }

    /// <summary>True when production and head agree (including both declining).</summary>
    [JsonPropertyName("agree")] public bool Agree { get; init; }
}
