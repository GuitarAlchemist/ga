// DemerzelBridge — MCP server that bridges Claude Code to Demerzel's ACP agents.
// Claude Code talks MCP (stdio). This bridge translates MCP tool calls to ACP HTTP runs.
//
// Usage in .mcp.json:
//   "demerzel": {
//     "type": "stdio",
//     "command": "dotnet",
//     "args": ["run", "--project", "Apps/demerzel-bridge/DemerzelBridge/DemerzelBridge.csproj", "--no-build"],
//     "env": { "DEMERZEL_API_KEY": "${DEMERZEL_API_KEY}", "DEMERZEL_URL": "http://localhost:8200" }
//   }

using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// ---------------------------------------------------------------------------
// Entry point — stdio MCP server (must be before type declarations)
// ---------------------------------------------------------------------------

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
return;

// ---------------------------------------------------------------------------
// ACP client types
// ---------------------------------------------------------------------------

record AcpMessagePart(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("content_type")] string ContentType = "text/plain");

record AcpMessage(
    [property: JsonPropertyName("parts")] List<AcpMessagePart> Parts);

record AcpRunRequest(
    [property: JsonPropertyName("agent_name")] string AgentName,
    [property: JsonPropertyName("input")] List<AcpMessage> Input);

record AcpRunResponse(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("output")] List<AcpOutputMessage>? Output,
    [property: JsonPropertyName("error")] string? Error);

record AcpOutputMessage(
    [property: JsonPropertyName("parts")] List<AcpMessagePart> Parts);

// ---------------------------------------------------------------------------
// ACP HTTP client
// ---------------------------------------------------------------------------

static class DemerzelClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private static readonly string BaseUrl = Environment.GetEnvironmentVariable("DEMERZEL_URL") ?? "http://localhost:8200";
    private static readonly string? ApiKey = Environment.GetEnvironmentVariable("DEMERZEL_API_KEY");

    public static async Task<string> RunAgent(string agentName, string message, CancellationToken ct = default)
    {
        var request = new AcpRunRequest(agentName, [new([new(message)])]);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/runs")
        {
            Content = JsonContent.Create(request),
        };

        if (!string.IsNullOrEmpty(ApiKey))
            httpRequest.Headers.Add("Authorization", $"Bearer {ApiKey}");

        var response = await Http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AcpRunResponse>(ct);
        if (result?.Error != null)
            return $"Error: {result.Error}";

        if (result?.Output == null || result.Output.Count == 0)
            return "No output from agent.";

        return string.Join("\n\n", result.Output.SelectMany(m => m.Parts.Select(p => p.Content)));
    }
}

// ---------------------------------------------------------------------------
// MCP Tools — one per Demerzel ACP agent
// ---------------------------------------------------------------------------

[McpServerToolType]
public static class DemerzelTools
{
    [McpServerTool(Name = "demerzel_governance")]
    [Description("Query Demerzel governance — beliefs (T/F/U/C), policies, constitutions, strategies, learning journal. Commands: 'list beliefs', 'list policies', 'constitution epistemic', or natural language.")]
    public static async Task<string> Governance(
        [Description("Command or query")] string query,
        CancellationToken ct) =>
        await DemerzelClient.RunAgent("demerzel-governance", query, ct);

    [McpServerTool(Name = "demerzel_pipeline")]
    [Description("Run dev pipeline via Demerzel — brainstorm, plan, implement, review, compound. Uses Ollama LLM.")]
    public static async Task<string> Pipeline(
        [Description("JSON {title, stage} or plain text like 'brainstorm: Fix CI'")] string request,
        CancellationToken ct) =>
        await DemerzelClient.RunAgent("demerzel-pipeline", request, ct);

    [McpServerTool(Name = "demerzel_epistemic")]
    [Description("Epistemic Constitution (E-0 to E-9). Commands: show beliefs/strategies/tensor, methylate, demethylate, amnesia, broadcast.")]
    public static async Task<string> Epistemic(
        [Description("Epistemic command")] string command,
        CancellationToken ct) =>
        await DemerzelClient.RunAgent("demerzel-epistemic", command, ct);

    [McpServerTool(Name = "demerzel_whats_next")]
    [Description("Ask Demerzel what to work on next — scans GitHub, CI/CD, governance. Returns prioritized recommendations.")]
    public static async Task<string> WhatsNext(
        [Description("Optional focus area")] string query = "",
        CancellationToken ct = default) =>
        await DemerzelClient.RunAgent("demerzel-whats-next",
            string.IsNullOrWhiteSpace(query) ? "What should I work on next?" : query, ct);
}

// Entry point is at the top of this file (top-level statements).
