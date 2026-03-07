namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

/// <summary>
/// Response returned by the GA script evaluation endpoint.
/// </summary>
internal sealed record GaScriptResponse(
    [property: JsonPropertyName("success")]  bool    Success,
    [property: JsonPropertyName("output")]   string  Output,
    [property: JsonPropertyName("error")]    string? Error,
    [property: JsonPropertyName("value")]    string? Value
);

[McpServerToolType]
public sealed class GaScriptTool(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Evaluate a <c>ga { }</c> F# computation expression script.
    /// Scripts use the GA Language DSL — composable pipelines over music-theory
    /// domain operations, agent calls, and I/O steps.
    /// </summary>
    /// <remarks>
    /// The script is evaluated in an FSI session hosted by the GaApi process.
    /// Built-in closures are available: domain.*, pipeline.*, agent.*, io.*
    /// </remarks>
    [McpServerTool]
    [Description(
        "Evaluate a ga { } F# computation expression script against the Guitar Alchemist DSL. " +
        "Scripts can call domain closures (domain.parseChord, domain.diatonicChords), " +
        "pipeline closures (pipeline.pullBspRooms, pipeline.embedOpticK, pipeline.storeQdrant), " +
        "agent closures (agent.theoryAgent, agent.tabAgent, agent.criticAgent), " +
        "and I/O closures (io.readFile, io.writeFile, io.httpGet, io.httpPost). " +
        "Returns structured JSON with success flag, stdout output, and optional return value.")]
    public async Task<string> EvalGaScript(
        [Description("The ga { } computation expression script to evaluate. " +
                     "Example: \"ga { let! result = GaClosureRegistry.Global.Invoke(\\\"domain.parseChord\\\", Map.ofList [\\\"symbol\\\", box \\\"Am7\\\"]); return result }\"")]
        string script,
        CancellationToken cancellationToken = default)
    {
        var client   = httpClientFactory.CreateClient("gaapi");
        var response = await client.PostAsJsonAsync(
            "/api/ga/eval",
            new { script },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Serialize(new GaScriptResponse(
                Success: false,
                Output:  string.Empty,
                Error:   $"HTTP {(int)response.StatusCode}: {body}",
                Value:   null));
        }

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return result;
    }

    [McpServerTool]
    [Description("List all available GA DSL closures by category. " +
                 "Returns a markdown table of closure names, categories, and descriptions.")]
    public async Task<string> ListGaClosures(
        [Description("Optional category filter: 'domain', 'pipeline', 'agent', or 'io'. Leave empty for all.")]
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("gaapi");
        var url    = string.IsNullOrEmpty(category)
            ? "/api/ga/closures"
            : $"/api/ga/closures?category={Uri.EscapeDataString(category)}";

        var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return $"Error listing closures: HTTP {(int)response.StatusCode}";

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
