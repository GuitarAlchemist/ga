namespace GA.Business.ML.Agents.Plugins;

using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol;

/// <summary>
/// Configuration for a single federated MCP server (TARS, ix, etc.).
/// </summary>
public sealed record FederationEndpoint(string Command, string Args, string? WorkingDir = null);

/// <summary>
/// MCP federation client that connects to TARS and ix MCP servers via stdio transport.
/// Discovers tools across all federated repos and routes calls to the correct server.
/// Degrades gracefully when a server is unavailable.
/// </summary>
public sealed class FederationClient(
    IConfiguration config,
    ILogger<FederationClient> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, McpClient?> _clients = new();
    private readonly ConcurrentDictionary<string, IReadOnlyList<McpClientTool>> _tools = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    /// <summary>
    /// Discovers available tools from a federated repo, or all repos if none specified.
    /// </summary>
    public async Task<IReadOnlyList<McpClientTool>> DiscoverToolsAsync(string? repo = null, CancellationToken ct = default)
    {
        var repos = repo is not null ? [repo] : GetConfiguredRepos();
        var allTools = new List<McpClientTool>();

        foreach (var r in repos)
        {
            if (_tools.TryGetValue(r, out var cached))
            {
                allTools.AddRange(cached);
                continue;
            }

            var client = await GetOrConnectAsync(r, ct);
            if (client is null) continue;

            try
            {
                var tools = (IReadOnlyList<McpClientTool>)await client.ListToolsAsync(cancellationToken: ct);
                _tools[r] = tools;
                allTools.AddRange(tools);
                logger.LogInformation("FederationClient: discovered {Count} tools from {Repo}", tools.Count, r);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FederationClient: failed to list tools from {Repo}", r);
            }
        }

        return allTools;
    }

    /// <summary>
    /// Calls a tool by name, searching across all federated servers.
    /// </summary>
    public async Task<CallToolResult?> CallToolAsync(
        string toolName,
        Dictionary<string, object?> args,
        CancellationToken ct = default)
    {
        // Find which repo owns this tool
        foreach (var (repoName, tools) in _tools)
        {
            var tool = tools.FirstOrDefault(t => t.Name == toolName);
            if (tool is null) continue;

            var client = _clients.GetValueOrDefault(repoName);
            if (client is null) continue;

            try
            {
                var result = await client.CallToolAsync(toolName, args, cancellationToken: ct);
                return result;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FederationClient: tool call {Tool} failed on {Repo}", toolName, repoName);
                return null;
            }
        }

        logger.LogWarning("FederationClient: tool {Tool} not found in any federated server", toolName);
        return null;
    }

    /// <summary>
    /// Lists all tools across all federated servers.
    /// </summary>
    public IReadOnlyList<(string Repo, string ToolName)> ListAllTools()
    {
        return _tools
            .SelectMany(kv => kv.Value.Select(t => (kv.Key, t.Name)))
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (name, client) in _clients)
        {
            if (client is IAsyncDisposable disposable)
            {
                try { await disposable.DisposeAsync(); }
                catch (Exception ex) { logger.LogWarning(ex, "FederationClient: error disposing {Repo}", name); }
            }
        }
        _clients.Clear();
        _tools.Clear();
        _connectLock.Dispose();
    }

    private string[] GetConfiguredRepos()
    {
        var repos = new List<string>();
        if (config["Federation:Tars:Command"] is not null) repos.Add("tars");
        if (config["Federation:Ix:Command"] is not null) repos.Add("ix");
        return repos.ToArray();
    }

    private async Task<McpClient?> GetOrConnectAsync(string repo, CancellationToken ct)
    {
        if (_clients.TryGetValue(repo, out var existing))
            return existing;

        await _connectLock.WaitAsync(ct);
        try
        {
            if (_clients.TryGetValue(repo, out existing))
                return existing;

            var section = $"Federation:{char.ToUpperInvariant(repo[0])}{repo[1..]}";
            var command = config[$"{section}:Command"];
            var args = config[$"{section}:Args"];
            var workDir = config[$"{section}:WorkingDir"];

            if (command is null)
            {
                logger.LogDebug("FederationClient: no config for {Repo}, skipping", repo);
                _clients[repo] = null;
                return null;
            }

            try
            {
                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Command = command,
                    Arguments = args?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [],
                    WorkingDirectory = workDir,
                    Name = $"ga-federation-{repo}",
                });

                var client = await McpClient.CreateAsync(transport, cancellationToken: ct);
                _clients[repo] = client;
                logger.LogInformation("FederationClient: connected to {Repo} via {Cmd} {Args}", repo, command, args);
                return client;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FederationClient: failed to connect to {Repo}", repo);
                _clients[repo] = null;
                return null;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }
}
