namespace GA.Business.ML.Agents.Memory;

using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// MCP tools for reading/writing persistent agent memory.
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via <see cref="GaPlugin.McpToolTypes"/>.
/// </summary>
[McpServerToolType]
public sealed class MemoryMcpTools(MemoryStore store)
{
    [McpServerTool]
    [Description("Search agent memory by keyword. Returns matching entries.")]
    public IReadOnlyList<MemoryEntry> MemorySearch(string query, string? type = null)
        => store.Search(query, type);

    [McpServerTool]
    [Description("Write or update a memory entry.")]
    public string MemoryWrite(string key, string type, string content, string[]? tags = null)
    {
        store.Write(key, type, content, tags);
        return $"Memory '{key}' saved.";
    }

    [McpServerTool]
    [Description("Read a specific memory entry by key.")]
    public MemoryEntry? MemoryRead(string key)
        => store.Read(key);

    [McpServerTool]
    [Description("Get memory store statistics.")]
    public object MemoryStats()
    {
        var (total, byType) = store.Stats();
        return new { total, byType };
    }
}
