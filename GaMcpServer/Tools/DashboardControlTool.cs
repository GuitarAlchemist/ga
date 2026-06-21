namespace GaMcpServer.Tools;

using System.Net.Http.Json;
using System.Text.Json;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tools for remote-controlling the /test#dev development dashboard.
///
///     Extends the Prime Radiant SceneControlTool pattern to the full dev
///     dashboard. Lets agents verify UI state (which tab is open, what's
///     visible, current state, screenshot) without writing a Playwright spec
///     and without waiting for deploy lag.
///
///     Wire: MCP client → GaMcpServer → HTTP → GaApi → DevDashboardHub → SPA
///
///     Phase 1 (this file) is read-only — navigate / state / screenshot / refresh.
///     Phase 2 (deferred) would gate writes behind Cloudflare Access.
///
///     Sub-tabs (keep in sync with DashboardController.ValidSubTabs and the
///     DevelopmentSection.tsx DEV_SUB_TABS array):
///         summary, architecture, product, project, qa, sentrux, harness, annotations
/// </summary>
[McpServerToolType]
public static class DashboardControlTool
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

    [McpServerTool]
    [Description("Navigate the /test#dev dashboard to a sub-tab (summary|architecture|product|project|qa|sentrux|harness|annotations). All connected SPA clients switch the tab. Returns the tab and count of clients notified.")]
    public static async Task<string> GaDashboardNavigate(
        [Description("One of: summary | architecture | product | project | qa | sentrux | harness | annotations")] string subTab)
    {
        try
        {
            var resp = await Http.PostAsJsonAsync("/api/dashboard/navigate", new { subTab });
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"GaApi returned {(int)resp.StatusCode}",
                    detail = body,
                    hint = "Is GaApi running and is a dashboard tab open at /test#dev/*?",
                });
            }
            return body;
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Could not reach GaApi server",
                detail = ex.Message,
                hint = "Start GaApi: pwsh Scripts/start-all.ps1",
            });
        }
    }

    [McpServerTool]
    [Description("Query the current state of the /test#dev dashboard: current tab, visible components, algedonic-unack count, in-flight PR count, scroll position, viewport size. Requires a connected SPA client.")]
    public static async Task<string> GaDashboardState()
    {
        try
        {
            var resp = await Http.GetAsync("/api/dashboard/state");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"GaApi returned {(int)resp.StatusCode}",
                    detail = body,
                    hint = "Is a dashboard tab open at /test#dev/*? RequestState times out at 5s.",
                });
            }
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

    [McpServerTool]
    [Description("Capture a screenshot of the /test#dev dashboard. If subTab is provided, navigate there first (700ms settle) then capture. Returns { base64_png, captured_at, format, sub_tab }.")]
    public static async Task<string> GaDashboardScreenshot(
        [Description("Optional sub-tab to navigate to before capturing (summary|architecture|product|project|qa|sentrux|harness|annotations)")] string? subTab = null,
        [Description("Capture the full scrollable page instead of just the viewport (default false)")] bool fullPage = false)
    {
        try
        {
            var resp = await Http.PostAsJsonAsync("/api/dashboard/screenshot", new { subTab, fullPage });
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"GaApi returned {(int)resp.StatusCode}",
                    detail = body,
                    hint = "Is a dashboard tab open at /test#dev/*? Screenshot client times out at 8s.",
                });
            }
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

    [McpServerTool]
    [Description("Tell the /test#dev dashboard to refresh its data. If endpoint is null, refreshes everything; otherwise just the named fetcher (e.g. '/dev-data/sentrux/health'). Use after a backend state change so the agent observes fresh data.")]
    public static async Task<string> GaDashboardRefresh(
        [Description("Optional fetcher path (e.g. /dev-data/sentrux/health). Null = refresh all.")] string? endpoint = null)
    {
        try
        {
            var resp = await Http.PostAsJsonAsync("/api/dashboard/refresh", new { endpoint });
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"GaApi returned {(int)resp.StatusCode}",
                    detail = body,
                });
            }
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
