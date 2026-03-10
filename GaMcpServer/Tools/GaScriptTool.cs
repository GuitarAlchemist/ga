namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GA.Business.DSL.Parsers;
using ModelContextProtocol.Server;

/// <summary>
/// Response returned by the GA script evaluation endpoint.
/// </summary>
internal sealed record GaScriptResponse(
    [property: JsonPropertyName("success")]     bool                Success,
    [property: JsonPropertyName("output")]      string              Output,
    [property: JsonPropertyName("error")]       string?             Error,
    [property: JsonPropertyName("value")]       string?             Value,
    [property: JsonPropertyName("diagnostics")] GaDiagnosticEntry[] Diagnostics,
    [property: JsonPropertyName("elapsedMs")]   double              ElapsedMs
);

internal sealed record GaDiagnosticEntry(
    [property: JsonPropertyName("code")]     string Code,
    [property: JsonPropertyName("message")]  string Message,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("line")]     int    Line,
    [property: JsonPropertyName("column")]   int    Column
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
    /// Script syntax and type errors are returned in the <c>diagnostics</c> array
    /// rather than thrown, so callers can read the error and self-correct.
    /// </remarks>
    [McpServerTool]
    [Description(
        "Evaluate a ga { } F# computation expression script against the Guitar Alchemist DSL. " +
        "Scripts can call domain closures (domain.parseChord, domain.diatonicChords), " +
        "pipeline closures (pipeline.pullBspRooms, pipeline.embedOpticK, pipeline.storeQdrant), " +
        "agent closures (agent.theoryAgent, agent.tabAgent, agent.criticAgent), " +
        "and I/O closures (io.readFile, io.writeFile, io.httpGet, io.httpPost). " +
        "Returns structured JSON with success flag, stdout output, diagnostics array " +
        "(Code/Message/Severity/Line/Column for self-correction), and ElapsedMs timing.")]
    public async Task<string> EvalGaScript(
        [Description("The ga { } computation expression script to evaluate. " +
                     "Example: \"ga { let! result = GaClosureRegistry.Global.Invoke(\\\"domain.parseChord\\\", Map.ofList [\\\"symbol\\\", box \\\"Am7\\\"]); return result }\"")]
        string script,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(10);
        var client   = httpClientFactory.CreateClient("gaapi");
        var response = await client.PostAsJsonAsync(
            "/api/ga/eval",
            new { script },
            cancellationToken);
        progress?.Report(90);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            progress?.Report(100);
            return JsonSerializer.Serialize(new GaScriptResponse(
                Success:     false,
                Output:      string.Empty,
                Error:       $"HTTP {(int)response.StatusCode}: {body}",
                Value:       null,
                Diagnostics: [],
                ElapsedMs:   0));
        }

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        progress?.Report(100);
        return result;
    }

    [McpServerTool]
    [Description("List all available GA DSL closures by category. " +
                 "Returns a JSON array of closure objects with name, category, description, tags, " +
                 "inputSchema, and outputType fields.")]
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

    [McpServerTool]
    [Description(
        "Get the input/output schema for a named GA closure. " +
        "Returns JSON with name, category, description, tags, inputSchema (param→type map), " +
        "and outputType. Use this before calling EvalGaScript to verify parameter names.")]
    public async Task<string> GetClosureSchema(
        [Description("Closure name, e.g. 'domain.parseChord', 'agent.theoryAgent', 'io.readFile'.")]
        string name,
        CancellationToken cancellationToken = default)
    {
        var client   = httpClientFactory.CreateClient("gaapi");
        var response = await client.GetAsync(
            $"/api/ga/closures/{Uri.EscapeDataString(name)}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return $"Closure '{name}' not found. Use ListGaClosures to see all available closures.";

        if (!response.IsSuccessStatusCode)
            return $"Error fetching closure schema: HTTP {(int)response.StatusCode}";

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    [McpServerTool]
    [Description(
        "Parse and desugar a GA surface-syntax script (pipeline/workflow/node/edge declarations) " +
        "into the equivalent F# ga { } computation expression — without executing it. " +
        "Use this to inspect or debug what a surface script will evaluate to before running it with EvalGaScript. " +
        "Example input:\n" +
        "  pipeline embed {\n" +
        "    node \"rooms\" kind=io { closure = \"pipeline.pullBspRooms\" output = rooms }\n" +
        "    node \"embed\"  kind=pipe { closure = \"pipeline.embedOpticK\" input = [rooms] output = vecs }\n" +
        "    edge \"rooms\" -> \"embed\"\n" +
        "  }")]
    public static string TranspileGaScript(
        [Description("GA surface syntax script containing pipeline/workflow/node/edge declarations")]
        string source)
    {
        var result = GaSurfaceSyntaxParser.transpile(source);
        if (result.IsOk)
            return $"// Transpiled F# ga {{ }} block:\n{result.ResultValue}";
        return $"Parse error: {result.ErrorValue}";
    }
}
