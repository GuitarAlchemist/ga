namespace GA.Business.ML.Agents.Memory;

using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// MCP tools for spawning and managing subagents.
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via <see cref="GA.Business.Core.Orchestration.Plugins.GaPlugin.McpToolTypes"/>.
/// </summary>
[McpServerToolType]
public sealed class SubagentMcpTools(SubagentManager manager)
{
    [McpServerTool(Name = "ga_subagent_spawn")]
    [Description("Spawn a subagent to work on a goal. Returns the subagent ID.")]
    public string Spawn(string goal, int maxDurationMinutes = 5, string? agentHint = null)
    {
        var request = new SubagentRequest(goal, maxDurationMinutes, AgentHint: agentHint);
        var id = manager.Spawn(request);
        return $"Subagent spawned: {id}";
    }

    [McpServerTool(Name = "ga_subagent_status")]
    [Description("Get the status of a subagent by ID.")]
    public object? Status(string id)
    {
        if (!Guid.TryParse(id, out var guid)) return "Invalid ID format";
        var info = manager.GetStatus(guid);
        if (info is null) return "Subagent not found";
        var result = manager.GetResult(guid);
        return new
        {
            info.Id,
            info.Goal,
            info.Status,
            info.Progress,
            info.StartedAt,
            Output = result?.Output,
            Error = result?.Error
        };
    }

    [McpServerTool(Name = "ga_subagent_cancel")]
    [Description("Cancel an active subagent by ID.")]
    public string Cancel(string id)
    {
        if (!Guid.TryParse(id, out var guid)) return "Invalid ID format";
        return manager.Cancel(guid) ? $"Subagent {id} cancelled" : $"Subagent {id} not found or already completed";
    }
}
