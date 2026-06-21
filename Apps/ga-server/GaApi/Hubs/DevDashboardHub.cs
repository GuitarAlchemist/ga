namespace GaApi.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
///     SignalR hub for remote control of the /test#dev development dashboard.
///
///     Extends the Prime Radiant MCP→SignalR pattern to the full dev dashboard
///     so agents can verify UI state (navigate tabs, capture screenshots,
///     read current state) without writing a Playwright spec.
///
///     Wire: MCP client → GaMcpServer → HTTP → GaApi → SignalR → SPA
///
///     Phase 1 (this hub) is read-only:
///       - NavigateTo / Refresh / RequestState / Screenshot
///     Phase 2 (future PR, gated on CF Access) would add writes:
///       - InvokeRescan / DismissAlgedonic / RunAction
///
///     Server → Client events:
///       "NavigateTo"      — { subTab } — switch /test#dev/{subTab}
///       "Refresh"         — { endpoint? } — invalidate a fetcher or all
///       "RequestState"    — { requestId } — client should reply via SubmitState
///       "RequestScreenshot" — { requestId, subTab?, fullPage? } — reply via SubmitScreenshot
///
///     Client → Server methods:
///       Subscribe()                         — register for dashboard control
///       SubmitState(requestId, json)        — return-channel for RequestState
///       SubmitScreenshot(requestId, base64, format) — return-channel for screenshot
/// </summary>
public sealed class DevDashboardHub(
    ILogger<DevDashboardHub> logger)
    : Hub
{
    public const string GroupName = "dev-dashboard";

    private static int _connectionCount;

    // ─── Return-channel storage (request → response) ───
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<string>> StateResponses = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TaskCompletionSource<(string Base64, string Format)>> ScreenshotResponses = new();

    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _connectionCount);
        logger.LogInformation("DevDashboard client connected: {ConnectionId} (total: {Count})",
            Context.ConnectionId, _connectionCount);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectionCount);
        logger.LogInformation("DevDashboard client disconnected: {ConnectionId} (total: {Count})",
            Context.ConnectionId, _connectionCount);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    ///     Client subscribes to dev-dashboard control events. Called from the
    ///     React McpControlProvider after the SPA mounts on a /test#dev/* route.
    /// </summary>
    public async Task Subscribe()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        logger.LogInformation("Client {ConnectionId} subscribed to dev-dashboard control", Context.ConnectionId);
    }

    /// <summary>
    ///     Client replies with its current dashboard state (return channel
    ///     for RequestState).
    /// </summary>
    public Task SubmitState(string requestId, string stateJson)
    {
        if (StateResponses.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(stateJson);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Client replies with a captured screenshot (return channel
    ///     for RequestScreenshot).
    /// </summary>
    public Task SubmitScreenshot(string requestId, string base64Image, string format)
    {
        if (ScreenshotResponses.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult((base64Image, format));
        }
        return Task.CompletedTask;
    }

    // ─── Server-side broadcast helpers (called from DashboardController) ───

    /// <summary>
    ///     Broadcast a NavigateTo command. All connected dashboard clients
    ///     will switch to the named sub-tab (summary | architecture | ... ).
    /// </summary>
    public static async Task<int> BroadcastNavigateTo(IHubContext<DevDashboardHub> hubContext, string subTab)
    {
        await hubContext.Clients.Group(GroupName).SendAsync("NavigateTo", new
        {
            subTab,
            timestamp = DateTime.UtcNow,
        });
        return _connectionCount;
    }

    /// <summary>
    ///     Broadcast a Refresh command. When endpoint is null, clients refresh
    ///     everything. Otherwise just the named fetcher (e.g. "/dev-data/sentrux/health").
    /// </summary>
    public static async Task<int> BroadcastRefresh(IHubContext<DevDashboardHub> hubContext, string? endpoint)
    {
        await hubContext.Clients.Group(GroupName).SendAsync("Refresh", new
        {
            endpoint,
            timestamp = DateTime.UtcNow,
        });
        return _connectionCount;
    }

    /// <summary>
    ///     Ask a connected client for its current dashboard state. Returns
    ///     null on timeout (no connected client or client didn't respond
    ///     within timeoutMs).
    /// </summary>
    public static async Task<string?> RequestStateFromClient(
        IHubContext<DevDashboardHub> hubContext,
        int timeoutMs = 5000)
    {
        if (_connectionCount == 0) return null;

        var requestId = Guid.NewGuid().ToString("n");
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        StateResponses[requestId] = tcs;

        try
        {
            await hubContext.Clients.Group(GroupName).SendAsync("RequestState", new
            {
                requestId,
                timestamp = DateTime.UtcNow,
            });

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                if (StateResponses.TryRemove(requestId, out var stale))
                {
                    stale.TrySetCanceled();
                }
            });

            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        finally
        {
            StateResponses.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    ///     Ask a connected client to capture a screenshot. Returns
    ///     (null, null) on timeout.
    /// </summary>
    public static async Task<(string? Base64, string? Format)> RequestScreenshotFromClient(
        IHubContext<DevDashboardHub> hubContext,
        string? subTab,
        bool fullPage,
        int timeoutMs = 8000)
    {
        if (_connectionCount == 0) return (null, null);

        var requestId = Guid.NewGuid().ToString("n");
        var tcs = new TaskCompletionSource<(string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        ScreenshotResponses[requestId] = tcs;

        try
        {
            await hubContext.Clients.Group(GroupName).SendAsync("RequestScreenshot", new
            {
                requestId,
                subTab,
                fullPage,
                timestamp = DateTime.UtcNow,
            });

            using var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                if (ScreenshotResponses.TryRemove(requestId, out var stale))
                {
                    stale.TrySetCanceled();
                }
            });

            var (b64, fmt) = await tcs.Task;
            return (b64, fmt);
        }
        catch (TaskCanceledException)
        {
            return (null, null);
        }
        finally
        {
            ScreenshotResponses.TryRemove(requestId, out _);
        }
    }

    public static int ConnectionCount => _connectionCount;
}
