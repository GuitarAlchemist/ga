namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool for remote-controlling the Prime Radiant scene.
///
///     Called from ix harness via ix_ga_bridge, or directly from any
///     MCP client. Communicates with the GA API server (GaApi) via
///     HTTP, which in turn broadcasts commands to connected Prime
///     Radiant browser instances via SignalR.
///
///     Wire: MCP client → GaMcpServer → HTTP → GaApi → SignalR → browser
/// </summary>
[McpServerToolType]
public static class SceneControlTool
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("GAAPI_BASE_URL")
                      ?? "https://localhost:7001";
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>
    ///     Navigate a connected Prime Radiant instance to a celestial body
    ///     (planet or moon), wait for the camera to settle, capture a
    ///     screenshot, and return the result.
    ///
    ///     Targets: sun, mercury, venus, earth, moon, mars, jupiter, saturn,
    ///     uranus, neptune, asteroid-belt, titan, europa, io, etc.
    /// </summary>
    [McpServerTool]
    [Description("Navigate the Prime Radiant to a celestial body, capture a screenshot, and return the base64-encoded image. Use for visual QA of the 3D solar system renderer.")]
    public static async Task<string> GaNavigateAndCapture(
        [Description("Target body name (e.g. 'moon', 'earth', 'jupiter')")] string target,
        [Description("Milliseconds to wait for camera animation before capturing (default 2000)")] int waitMs = 2000)
    {
        try
        {
            var response = await Http.PostAsJsonAsync("/api/governance/navigate-and-capture", new
            {
                target,
                waitMs,
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new
                {
                    error = $"GaApi returned {response.StatusCode}",
                    detail = errorBody,
                    hint = "Is the GA API server running? Is a Prime Radiant client connected?",
                });
            }

            // The response is a PNG image — encode as base64 for MCP transport
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(imageBytes);
            var capturedAt = response.Headers.TryGetValues("X-Screenshot-Captured-At", out var vals)
                ? vals.FirstOrDefault()
                : null;

            return JsonSerializer.Serialize(new
            {
                target,
                capturedAt,
                format = "image/png",
                base64Length = base64.Length,
                dataUrl = $"data:image/png;base64,{base64}",
            });
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Could not reach GaApi server",
                detail = ex.Message,
                hint = "Start the GA API server: pwsh Scripts/start-all.ps1",
            });
        }
    }

    /// <summary>
    ///     Navigate the Prime Radiant to a body without capturing a screenshot.
    ///     Useful for setting up a scene before a separate capture command.
    /// </summary>
    [McpServerTool]
    [Description("Navigate the Prime Radiant to a celestial body without capturing. Use GaNavigateAndCapture if you also need a screenshot.")]
    public static async Task<string> GaNavigateTo(
        [Description("Target body name (e.g. 'moon', 'earth', 'jupiter')")] string target)
    {
        try
        {
            var response = await Http.PostAsJsonAsync("/api/governance/navigate", new { target });
            var body = await response.Content.ReadAsStringAsync();
            return body;
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Could not reach GaApi server",
                detail = ex.Message,
            });
        }
    }
}
