namespace GaMcpServer.Tests;

using System.IO.Pipelines;
using System.Reflection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

/// <summary>
/// Calls one tool through an in-memory MCP client and server, as a client's tools/call does,
/// so a test sees what the client receives (text content and <c>isError</c>) rather than the
/// C# return value or exception.
/// </summary>
internal static class McpToolInvoker
{
    public static async Task<(bool IsError, string Text)> CallAsync(
        MethodInfo method,
        object? target,
        params (string Name, object? Value)[] arguments)
    {
        var tool = McpServerTool.Create(method, target);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var server = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
            new McpServerOptions { ToolCollection = [tool] });
        var serverRun = server.RunAsync(cts.Token);

        await using (var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            cancellationToken: cts.Token))
        {
            var result = await client.CallToolAsync(
                tool.ProtocolTool.Name,
                arguments.ToDictionary(a => a.Name, a => a.Value),
                cancellationToken: cts.Token);
            var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));

            await cts.CancelAsync();
            try { await serverRun; } catch (OperationCanceledException) { }
            return (result.IsError == true, text);
        }
    }
}
