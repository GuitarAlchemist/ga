namespace GaMcpServer.Tools;

using System.ComponentModel;
using System.Text.Json;
using GA.Business.ML.Search;
using ModelContextProtocol.Server;

/// <summary>
///     MCP read-back for voicing-retrieval telemetry. Lets Claude Code (or an agent)
///     inspect "what have users actually been asking?" without leaving the chat
///     session. Provides the raw records plus a pre-computed summary so the most
///     common priorities-unlocking question ("what % of queries return empty?")
///     is one tool call away.
/// </summary>
[McpServerToolType]
public static class VoicingTelemetryTool
{
    [McpServerTool]
    [Description(
        "Inspect voicing-retrieval telemetry — what queries have users typed, which " +
        "returned empty, tag-hit rate, median latency. Reads the rolling JSONL at " +
        "state/telemetry/voicing-search/YYYY-MM-DD.jsonl. Returns a summary block " +
        "plus the last N records. Use this to prioritize next features based on " +
        "real usage instead of guessing.")]
    public static string GaVoicingTelemetry(
        [Description("How many recent records to include in `records` (default 50, capped at 500)")]
        int limit = 50,
        [Description("Specific day to read (YYYY-MM-DD). Defaults to today (UTC).")]
        string? day = null)
    {
        limit = Math.Clamp(limit, 1, 500);

        DateOnly? parsedDay = null;
        if (!string.IsNullOrWhiteSpace(day) && DateOnly.TryParse(day, out var d))
        {
            parsedDay = d;
        }

        var records = VoicingTelemetryLog.ReadRecent(limit, parsedDay);

        if (records.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                day = parsedDay?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
                summary = new { totalQueries = 0 },
                note = "No telemetry yet for this day. The writer activates on the first retrieval call after the server restart."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        // ── Summary statistics ────────────────────────────────────────────────
        var total = records.Count;
        var empty = records.Count(r => r.Empty);
        var withChord = records.Count(r => r.Chord is not null);
        var withMode = records.Count(r => r.Mode is not null);
        var withTags = records.Count(r => r.Tags is { Count: > 0 });
        var mcpCalls = records.Count(r => r.Source == "mcp");
        var chatbotCalls = records.Count(r => r.Source == "chatbot");

        var latencies = records.Select(r => r.LatencyMs).OrderBy(x => x).ToArray();
        var p50 = latencies.Length > 0 ? latencies[latencies.Length / 2] : 0;
        var p95 = latencies.Length > 0 ? latencies[(int)(latencies.Length * 0.95)] : 0;

        // Top-10 most-queried chord symbols — surfaces the user's actual vocabulary.
        var topChords = records
            .Where(r => r.Chord is not null)
            .GroupBy(r => r.Chord!)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { chord = g.Key, count = g.Count() })
            .ToArray();

        // Most common empty-result queries — the queue of features to build / tags to add.
        var topEmptyQueries = records
            .Where(r => r.Empty)
            .GroupBy(r => r.Query)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { query = g.Key, count = g.Count() })
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            day = parsedDay?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd"),
            summary = new
            {
                totalQueries = total,
                emptyResults = empty,
                emptyRate = total > 0 ? Math.Round(100.0 * empty / total, 1) : 0.0,
                chordHitRate = total > 0 ? Math.Round(100.0 * withChord / total, 1) : 0.0,
                modeHitRate = total > 0 ? Math.Round(100.0 * withMode / total, 1) : 0.0,
                tagHitRate = total > 0 ? Math.Round(100.0 * withTags / total, 1) : 0.0,
                bySource = new { mcp = mcpCalls, chatbot = chatbotCalls },
                latencyP50Ms = Math.Round(p50, 1),
                latencyP95Ms = Math.Round(p95, 1),
            },
            topChords,
            topEmptyQueries,
            records
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
