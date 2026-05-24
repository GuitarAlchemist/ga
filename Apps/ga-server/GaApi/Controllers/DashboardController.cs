namespace GaApi.Controllers;

using GaApi.Hubs;
using Microsoft.AspNetCore.SignalR;

/// <summary>
///     HTTP front-door for the /test#dev dashboard MCP control hub.
///
///     GaMcpServer's DashboardControlTool POSTs here; we relay over SignalR
///     to the SPA via <see cref="DevDashboardHub"/>. Same pattern as
///     GovernanceController.NavigateAndCapture for Prime Radiant.
///
///     Phase 1 is read-only — see DevDashboardHub for the longer note.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DashboardController(
    ILogger<DashboardController> logger,
    IHubContext<DevDashboardHub> hubContext)
    : ControllerBase
{
    /// <summary>
    ///     The 8 sub-tabs the SPA's DevelopmentSection.tsx exposes.
    ///     Kept in sync with DEV_SUB_TABS in that file.
    /// </summary>
    public static readonly HashSet<string> ValidSubTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "summary", "architecture", "product", "project", "qa",
        "sentrux", "harness", "annotations",
    };

    [HttpPost("navigate")]
    public async Task<ActionResult> Navigate([FromBody] NavigateSubTabRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SubTab) || !ValidSubTabs.Contains(request.SubTab))
        {
            return BadRequest(new
            {
                error = "subTab must be one of: " + string.Join(", ", ValidSubTabs),
                received = request.SubTab,
            });
        }

        var notified = await DevDashboardHub.BroadcastNavigateTo(hubContext, request.SubTab.ToLowerInvariant());
        logger.LogInformation("Dashboard navigate broadcast: subTab={SubTab}, clients={Count}", request.SubTab, notified);
        return Ok(new { ok = true, tab = request.SubTab.ToLowerInvariant(), clients_notified = notified });
    }

    [HttpGet("state")]
    public async Task<ActionResult> GetState()
    {
        if (DevDashboardHub.ConnectionCount == 0)
        {
            return NotFound(new
            {
                error = "No dashboard client connected",
                hint = "Open /test#dev/* in a browser tab to register a client",
            });
        }

        var stateJson = await DevDashboardHub.RequestStateFromClient(hubContext);
        if (stateJson == null)
        {
            return StatusCode(504, new
            {
                error = "Client did not respond within timeout",
                connectedClients = DevDashboardHub.ConnectionCount,
            });
        }

        // Pass through the client's JSON without re-parsing — schema is
        // owned by the McpControlProvider on the SPA side.
        return Content(stateJson, "application/json");
    }

    [HttpPost("screenshot")]
    public async Task<ActionResult> Screenshot([FromBody] DashboardScreenshotRequest request)
    {
        if (DevDashboardHub.ConnectionCount == 0)
        {
            return NotFound(new
            {
                error = "No dashboard client connected",
                hint = "Open /test#dev/* in a browser tab to register a client",
            });
        }

        // Optionally navigate first
        if (!string.IsNullOrWhiteSpace(request.SubTab))
        {
            if (!ValidSubTabs.Contains(request.SubTab))
            {
                return BadRequest(new
                {
                    error = "subTab must be one of: " + string.Join(", ", ValidSubTabs),
                    received = request.SubTab,
                });
            }
            await DevDashboardHub.BroadcastNavigateTo(hubContext, request.SubTab.ToLowerInvariant());
            // Give the SPA a moment to switch + render the tab before capturing.
            await Task.Delay(700);
        }

        var (base64, format) = await DevDashboardHub.RequestScreenshotFromClient(
            hubContext,
            request.SubTab,
            request.FullPage ?? false);

        if (base64 == null)
        {
            return StatusCode(504, new
            {
                error = "Client did not return a screenshot within timeout",
                connectedClients = DevDashboardHub.ConnectionCount,
            });
        }

        return Ok(new
        {
            base64_png = base64,
            captured_at = DateTime.UtcNow.ToString("O"),
            format = format ?? "image/png",
            sub_tab = request.SubTab?.ToLowerInvariant(),
        });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult> Refresh([FromBody] DashboardRefreshRequest? request = null)
    {
        var endpoint = request?.Endpoint;
        var notified = await DevDashboardHub.BroadcastRefresh(hubContext, endpoint);
        return Ok(new
        {
            refreshed = endpoint == null ? new[] { "*" } : new[] { endpoint },
            clients_notified = notified,
        });
    }
}

public record NavigateSubTabRequest(string SubTab);

public record DashboardScreenshotRequest(string? SubTab, bool? FullPage);

public record DashboardRefreshRequest(string? Endpoint);
