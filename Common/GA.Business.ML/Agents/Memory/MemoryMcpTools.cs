namespace GA.Business.ML.Agents.Memory;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// MCP tools for reading/writing persistent agent memory.
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via <see cref="GaPlugin.McpToolTypes"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Session scoping (2026-05-10):</b> these tools operate on the
/// <em>global</em> memory partition (entries with SessionId=null). LLM
/// callers don't have per-call access to the active SignalR ConnectionId
/// — that's transport-layer context — so they cannot scope reads/writes
/// to a session even if they wanted to.
/// </para>
/// <para>
/// <b>SC-001 hardening (2026-05-11):</b> <see cref="MemoryWrite"/> is
/// now <em>off by default</em> behind <c>Memory:AllowLlmGlobalWrite</c>.
/// Without explicit operator opt-in, the LLM cannot write to the global
/// partition. This closes the prompt-injection-to-cross-session-retrieval
/// attack window that Phase A/B opened. Reads (Search/Read/Stats) remain
/// enabled — they're inert without writers.
/// </para>
/// <para>
/// When the flag IS opted in, every successful write is auto-tagged
/// with <c>origin:mcp-tool</c> so <see cref="Hooks.MemoryHook"/>'s
/// retrieval-injection path can filter these entries (defense in depth
/// — even if the flag is mistakenly enabled, the retrieval surface
/// stays safe).
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class MemoryMcpTools(
    MemoryStore store,
    IConfiguration configuration,
    ILogger<MemoryMcpTools> logger)
{
    /// <summary>
    /// Default-OFF config flag. Operators flip this only when they
    /// understand the cross-session retrieval implications.
    /// </summary>
    private readonly bool _allowLlmGlobalWrite =
        configuration.GetValue<bool?>("Memory:AllowLlmGlobalWrite") ?? false;

    /// <summary>Tag automatically applied to MCP-tool-originated writes
    /// so the retrieval path can identify and exclude them.</summary>
    public const string McpOriginTag = "origin:mcp-tool";

    [McpServerTool]
    [Description("Search the SHARED agent memory by keyword. Returns matching entries from the global knowledge pool (not session-private history). Use for cross-session facts like preferences or learned terminology.")]
    public IReadOnlyList<MemoryEntry> MemorySearch(string query, string? type = null)
        => store.Search(sessionId: MemoryStore.GlobalSessionId, query: query, type: type);

    [McpServerTool]
    [Description("Write or update a SHARED memory entry. DISABLED BY DEFAULT — administrators must opt in via Memory:AllowLlmGlobalWrite=true to allow LLM-driven writes to the global pool. When disabled, returns a refusal message. When enabled, the entry is auto-tagged 'origin:mcp-tool' so retrieval hooks can filter it. Use sparingly for stable shared facts; per-conversation context belongs in session memory (handled automatically by the MemoryHook).")]
    public string MemoryWrite(string key, string type, string content, string[]? tags = null)
    {
        if (!_allowLlmGlobalWrite)
        {
            // Truncate attacker-controllable fields before logging — per PR
            // #161 review F-11, an unbounded key from a prompt-injected
            // tool call could flood the log file.
            var safeKey  = (key  ?? string.Empty)[..Math.Min(256, (key  ?? string.Empty).Length)];
            var safeType = (type ?? string.Empty)[..Math.Min(64,  (type ?? string.Empty).Length)];
            logger.LogInformation(
                "MemoryMcpTools.MemoryWrite refused: AllowLlmGlobalWrite=false. " +
                "Key='{Key}', Type='{Type}', ContentLength={Length}",
                safeKey, safeType, content?.Length ?? 0);
            // Per PR #161 review F-6: terse refusal — no hints about the
            // MemoryHook architecture or the exact config-key name.
            // Operators see the full reason in the LogInformation above.
            return "Memory write is not enabled.";
        }

        // Auto-tag with origin so MemoryHook can filter on retrieval —
        // defense in depth in case the flag is accidentally enabled.
        // Per PR #161 review M1: dedup if the caller already supplied
        // the origin tag (system-controlled tag, never trust caller's
        // copy of it).
        var withOrigin = (tags ?? [])
            .Where(t => !string.Equals(t, McpOriginTag, StringComparison.Ordinal))
            .Append(McpOriginTag)
            .ToArray();
        store.Write(sessionId: MemoryStore.GlobalSessionId, key: key, type: type, content: content, tags: withOrigin);
        logger.LogInformation(
            "MemoryMcpTools.MemoryWrite accepted (AllowLlmGlobalWrite=true): key='{Key}' type='{Type}'",
            key, type);
        return $"Memory '{key}' saved (global / shared scope, tagged '{McpOriginTag}').";
    }

    [McpServerTool]
    [Description("Read a SHARED memory entry by key. Returns null if not found in the global knowledge pool.")]
    public MemoryEntry? MemoryRead(string key)
        => store.Read(sessionId: MemoryStore.GlobalSessionId, key: key);

    [McpServerTool]
    [Description("Get statistics for the SHARED memory partition (entries visible to every session).")]
    public object MemoryStats()
    {
        var (total, byType) = store.Stats(MemoryStore.GlobalSessionId);
        var grandTotal      = store.TotalEntriesAllSessions();
        return new
        {
            total,
            byType,
            grandTotal,
            note = grandTotal > total
                ? $"{grandTotal - total} additional entries are session-scoped and not exposed via this tool."
                : "all entries are in the global partition",
        };
    }
}
