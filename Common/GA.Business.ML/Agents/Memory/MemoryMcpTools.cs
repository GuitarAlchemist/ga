namespace GA.Business.ML.Agents.Memory;

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
/// Treat the data the LLM writes via these tools as a SHARED knowledge
/// pool: facts that should be visible to every session ("user prefers
/// drop-D tuning examples", "this codebase uses .NET 10"). User-private
/// conversation history is captured by <see cref="Hooks.MemoryHook"/>
/// instead, which IS session-scoped because it reads
/// <see cref="Hooks.ChatHookContext.SessionId"/>.
/// </para>
/// <para>
/// Future plumbing could expose a session-scoped MCP variant by
/// surfacing the current ChatHookContext via an async-local accessor,
/// but that ships separately — keeping these tools un-scoped preserves
/// backward compatibility with the existing SKILL.md instructions that
/// rely on shared-knowledge semantics.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class MemoryMcpTools(MemoryStore store)
{
    [McpServerTool]
    [Description("Search the SHARED agent memory by keyword. Returns matching entries from the global knowledge pool (not session-private history). Use for cross-session facts like preferences or learned terminology.")]
    public IReadOnlyList<MemoryEntry> MemorySearch(string query, string? type = null)
        => store.Search(sessionId: MemoryStore.GlobalSessionId, query: query, type: type);

    [McpServerTool]
    [Description("Write or update a SHARED memory entry visible to every session. Use this for cross-session facts. Per-conversation context is captured automatically by the MemoryHook and is NOT written via this tool.")]
    public string MemoryWrite(string key, string type, string content, string[]? tags = null)
    {
        store.Write(sessionId: MemoryStore.GlobalSessionId, key: key, type: type, content: content, tags: tags);
        return $"Memory '{key}' saved (global / shared scope).";
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
