namespace GA.Business.ML.Agents.Plugins;

using Microsoft.Extensions.AI;

/// <summary>
/// Provides <see cref="AIFunction"/> instances surfaced from an in-process MCP server.
/// Initialized lazily on first use; the server is started once and shared.
/// </summary>
public interface IMcpToolsProvider
{
    /// <summary>
    /// Returns the MCP tool functions, starting the in-process MCP server on first call.
    /// Subsequent calls return the cached tool list.
    /// </summary>
    ValueTask<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct = default);
}
