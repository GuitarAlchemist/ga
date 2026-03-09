namespace GA.Business.ML.Agents.Plugins;

using System.IO.Pipelines;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Assembles an in-process MCP server from registered <see cref="IChatPlugin.McpToolTypes"/>
/// and exposes the resulting tools as <see cref="AIFunction"/> instances via a <see cref="Pipe"/> pair.
///
/// The server is started once on first call to <see cref="GetToolsAsync"/> and the result cached.
/// All <see cref="SkillMdDrivenSkill"/> instances share a single MCP client connection.
/// </summary>
public sealed class InProcessMcpToolsProvider(
    IReadOnlyList<Type> toolTypes,
    IServiceProvider hostServices,
    ILogger<InProcessMcpToolsProvider> logger) : IMcpToolsProvider, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IReadOnlyList<AIFunction>? _tools;
    private McpClient? _client;

    public async ValueTask<IReadOnlyList<AIFunction>> GetToolsAsync(CancellationToken ct = default)
    {
        if (_tools is not null)
            return _tools;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_tools is not null)
                return _tools;

            _tools = await StartInProcessServerAsync(ct);
            return _tools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IReadOnlyList<AIFunction>> StartInProcessServerAsync(CancellationToken ct)
    {
        if (toolTypes.Count == 0)
        {
            logger.LogDebug("InProcessMcpToolsProvider: no McpToolTypes registered — returning empty tool list");
            return [];
        }

        // Pipe pair — no subprocess, no HTTP, no network
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        // ── Build in-process MCP server ──────────────────────────────────────
        var serverServices = new ServiceCollection();
        serverServices.AddLogging();

        // Copy domain services the tool types require from the host container
        // (registered via GaPlugin or other plugins that have McpToolTypes)
        // For now we forward the full IServiceProvider — tools can scope down as needed
        serverServices.AddSingleton(hostServices);

        var mcpBuilder = serverServices
            .AddMcpServer()
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream());

        foreach (var toolType in toolTypes)
            mcpBuilder.WithTools(toolType);

        var serverProvider = serverServices.BuildServiceProvider();
        var server = serverProvider.GetRequiredService<ModelContextProtocol.Server.McpServer>();

        // Fire-and-forget: runs until the host shuts down or the pipe closes
        _ = server.RunAsync(ct);

        // ── Connect MCP client ───────────────────────────────────────────────
        var clientTransport = new StreamClientTransport(
            serverInput:  clientToServer.Writer.AsStream(),
            serverOutput: serverToClient.Reader.AsStream());

        _client = await McpClient.CreateAsync(clientTransport, cancellationToken: ct);

        var toolList = await _client.ListToolsAsync(cancellationToken: ct);
        logger.LogInformation(
            "InProcessMcpToolsProvider: started with {Count} MCP tools ({Types})",
            toolList.Count,
            string.Join(", ", toolList.Select(t => t.Name)));

        return toolList.Cast<AIFunction>().ToList().AsReadOnly();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        _initLock.Dispose();
    }
}
