namespace GaMcpServer.Tools;

using System.Text.Json;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tools for exporting GA trace artifacts for TARS cross-repo ingestion.
/// </summary>
[McpServerToolType]
public static class TraceBridgeTool
{
    private static readonly string TraceDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ga", "traces");

    [McpServerTool]
    [Description("Export recent GA trace artifacts as a JSON array for TARS promotion pipeline ingestion. " +
                 "Optional: count (default 50), sinceIso (ISO 8601 date filter).")]
    public static string ExportTraces(int count = 50, string? sinceIso = null)
    {
        if (!Directory.Exists(TraceDir))
            return "[]";

        var files = Directory.GetFiles(TraceDir, "*.json");
        Array.Sort(files);
        Array.Reverse(files); // newest first

        DateTimeOffset? since = null;
        if (sinceIso is not null && DateTimeOffset.TryParse(sinceIso, out var parsed))
            since = parsed;

        var artifacts = new List<JsonElement>();
        foreach (var file in files)
        {
            if (artifacts.Count >= count) break;
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json);

                if (since.HasValue)
                {
                    if (doc.RootElement.TryGetProperty("timestamp", out var ts)
                        && DateTimeOffset.TryParse(ts.GetString(), out var fileTs)
                        && fileTs < since.Value)
                        continue;
                }

                artifacts.Add(doc.RootElement.Clone());
            }
            catch
            {
                // Skip corrupt files
            }
        }

        return JsonSerializer.Serialize(artifacts, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Get trace bridge statistics: file count, date range, event type distribution.")]
    public static string TraceStats()
    {
        if (!Directory.Exists(TraceDir))
            return JsonSerializer.Serialize(new { fileCount = 0, message = "No trace directory found." });

        var files = Directory.GetFiles(TraceDir, "*.json");
        if (files.Length == 0)
            return JsonSerializer.Serialize(new { fileCount = 0, message = "No trace files." });

        Array.Sort(files);
        var eventTypes = new Dictionary<string, int>();
        string? oldest = null, newest = null;

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("eventType", out var et))
                {
                    var key = et.GetString() ?? "unknown";
                    eventTypes[key] = eventTypes.GetValueOrDefault(key) + 1;
                }

                if (doc.RootElement.TryGetProperty("timestamp", out var ts))
                {
                    var tsStr = ts.GetString();
                    oldest ??= tsStr;
                    newest = tsStr;
                }
            }
            catch
            {
                // Skip
            }
        }

        return JsonSerializer.Serialize(new
        {
            fileCount = files.Length,
            oldest,
            newest,
            eventTypes,
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
