namespace GaApi.Hubs;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Controllers;
using Models;
using Services;

/// <summary>
///     Tracks a connected Prime Radiant viewer for presence display.
/// </summary>
public sealed record ViewerInfo(
    string ConnectionId,
    string Color,
    string Browser,
    DateTime ConnectedAt);

/// <summary>
///     SignalR hub for real-time governance graph updates.
///     Unauthenticated — Prime Radiant clients connect without tokens.
///
///     Server → Client events:
///       "GraphUpdate"       — full governance graph JSON
///       "HealthUpdate"      — partial update with changed node health metrics
///       "NodeChanged"       — single node update (id, health, color)
///       "BeliefUpdate"      — pushed when a belief state changes (tetravalent T/F/U/C)
///       "BeliefsSnapshot"   — full list of current belief states
///       "AlgedonicSignal"   — pain/pleasure signal from belief state transitions
///       "ViewersChanged"    — current list of connected viewers (presence)
///       "Connected"         — welcome message with current node count
///
///     Client → Server methods:
///       Subscribe()        — register for updates
///       SubscribeBeliefs() — register for belief state updates
///       RequestRefresh()   — request immediate full graph push
///       GetViewers()       — returns current viewer list to calling client
/// </summary>
public sealed class GovernanceHub(
    ILogger<GovernanceHub> logger)
    : Hub
{
    private static int _connectionCount;

    // ─── Viewer presence tracking ───
    private static readonly ConcurrentDictionary<string, ViewerInfo> ConnectedViewers = new();

    private static readonly string[] ViewerPalette =
    [
        "#58a6ff", "#3fb950", "#d2a8ff", "#f0883e",
        "#ff7b72", "#79c0ff", "#7ee787", "#ffa657",
        "#ff9bce", "#d1d5da",
    ];

    private static string ParseBrowser(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Unknown";
        if (userAgent.Contains("Edg/", StringComparison.Ordinal)) return "Edge";
        if (userAgent.Contains("Chrome/", StringComparison.Ordinal)) return "Chrome";
        if (userAgent.Contains("Firefox/", StringComparison.Ordinal)) return "Firefox";
        if (userAgent.Contains("Safari/", StringComparison.Ordinal)) return "Safari";
        return "Unknown";
    }

    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _connectionCount);
        logger.LogInformation("Governance client connected: {ConnectionId} (total: {Count})",
            Context.ConnectionId, _connectionCount);

        // Register viewer presence
        var userAgent = Context.GetHttpContext()?.Request.Headers.UserAgent.ToString();
        var color = ViewerPalette[Math.Abs(Context.ConnectionId.GetHashCode()) % ViewerPalette.Length];
        var viewer = new ViewerInfo(Context.ConnectionId, color, ParseBrowser(userAgent), DateTime.UtcNow);
        ConnectedViewers.TryAdd(Context.ConnectionId, viewer);

        await Clients.Caller.SendAsync("Connected", new
        {
            message = "Connected to Governance Hub",
            connections = _connectionCount,
            timestamp = DateTime.UtcNow,
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectionCount);
        ConnectedViewers.TryRemove(Context.ConnectionId, out _);
        logger.LogInformation("Governance client disconnected: {ConnectionId} (total: {Count})",
            Context.ConnectionId, _connectionCount);

        // Notify remaining governance subscribers
        await Clients.Group("governance").SendAsync("ViewersChanged", ConnectedViewers.Values.ToList());
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    ///     Client subscribes to governance updates.
    ///     Sends the full current graph immediately.
    /// </summary>
    public async Task Subscribe()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "governance");
        logger.LogInformation("Client {ConnectionId} subscribed to governance updates", Context.ConnectionId);

        // Push current graph immediately
        await RequestRefresh();

        // Broadcast updated viewer list to all governance subscribers
        await Clients.Group("governance").SendAsync("ViewersChanged", ConnectedViewers.Values.ToList());
    }

    /// <summary>
    ///     Returns the current viewer list to the calling client.
    /// </summary>
    public async Task GetViewers() =>
        await Clients.Caller.SendAsync("ViewersChanged", ConnectedViewers.Values.ToList());

    /// <summary>
    ///     Broadcast the current viewer list to all governance subscribers.
    ///     Called from backend services when viewer state needs refreshing.
    /// </summary>
    public static async Task BroadcastViewers(IHubContext<GovernanceHub> hubContext) =>
        await hubContext.Clients.Group("governance").SendAsync("ViewersChanged", ConnectedViewers.Values.ToList());

    /// <summary>
    ///     Client requests an immediate full graph refresh.
    /// </summary>
    public async Task RequestRefresh()
    {
        // Use the GovernanceController's cached graph
        // (accessed via static cache — same data, no duplicate computation)
        var controller = Context.GetHttpContext()?.RequestServices.GetService<GovernanceController>();
        if (controller != null)
        {
            var result = controller.GetGraph();
            if (result.Value != null)
            {
                await Clients.Caller.SendAsync("GraphUpdate", result.Value);
            }
        }
    }

    /// <summary>
    ///     Broadcast a governance graph update to all subscribed clients.
    ///     Called from backend services when governance state changes.
    /// </summary>
    public static async Task BroadcastGraphUpdate(IHubContext<GovernanceHub> hubContext, GovernanceGraph graph) =>
        await hubContext.Clients.Group("governance").SendAsync("GraphUpdate", graph);

    /// <summary>
    ///     Broadcast a single node health change to all subscribed clients.
    /// </summary>
    public static async Task BroadcastNodeChanged(
        IHubContext<GovernanceHub> hubContext,
        string nodeId,
        HealthMetrics health,
        string healthStatus,
        string color) =>
        await hubContext.Clients.Group("governance").SendAsync("NodeChanged", new
        {
            nodeId,
            health,
            healthStatus,
            color,
            timestamp = DateTime.UtcNow,
        });

    /// <summary>
    ///     Client subscribes to belief state updates.
    ///     Sends the full current belief snapshot immediately.
    /// </summary>
    public async Task SubscribeBeliefs()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "beliefs");
        logger.LogInformation("Client {ConnectionId} subscribed to belief updates", Context.ConnectionId);

        // Push current beliefs immediately
        var beliefService = Context.GetHttpContext()?.RequestServices.GetService<BeliefStateService>();
        if (beliefService != null)
        {
            var beliefs = beliefService.GetBeliefs();
            await Clients.Caller.SendAsync("BeliefsSnapshot", beliefs);
        }
    }

    /// <summary>
    ///     Broadcast a belief state update to all subscribed clients.
    ///     Called from backend services when a belief state changes.
    /// </summary>
    public static async Task BroadcastBeliefUpdate(IHubContext<GovernanceHub> hubContext, BeliefState belief) =>
        await hubContext.Clients.Group("beliefs").SendAsync("BeliefUpdate", belief);

    /// <summary>
    ///     Broadcast a full beliefs snapshot to all subscribed clients.
    /// </summary>
    public static async Task BroadcastBeliefsSnapshot(IHubContext<GovernanceHub> hubContext, List<BeliefState> beliefs) =>
        await hubContext.Clients.Group("beliefs").SendAsync("BeliefsSnapshot", beliefs);

    /// <summary>
    ///     Broadcast an algedonic signal (pain/pleasure) to all subscribed governance clients.
    ///     Called from backend services when a belief state transition triggers a signal.
    /// </summary>
    public static async Task BroadcastAlgedonicSignal(IHubContext<GovernanceHub> hubContext, AlgedonicSignalDto signal) =>
        await hubContext.Clients.Group("governance").SendAsync("AlgedonicSignal", signal);

    // ─── Screenshot capture ───

    private static string? _latestScreenshotBase64;
    private static string? _latestScreenshotFormat;
    private static DateTime? _latestScreenshotTime;
    private static readonly Lock ScreenshotLock = new();

    /// <summary>
    ///     Client submits a captured screenshot (base64-encoded image data).
    ///     Called by Prime Radiant frontend after capturing the Three.js canvas.
    /// </summary>
    public Task SubmitScreenshot(string base64Image, string format)
    {
        logger.LogInformation("Screenshot received from {ConnectionId} ({Format}, {Length} chars)",
            Context.ConnectionId, format, base64Image.Length);

        lock (ScreenshotLock)
        {
            _latestScreenshotBase64 = base64Image;
            _latestScreenshotFormat = format;
            _latestScreenshotTime = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Request a screenshot from all connected Prime Radiant clients.
    ///     Called from backend services or the REST API.
    /// </summary>
    public static async Task RequestScreenshotFromClients(IHubContext<GovernanceHub> hubContext, string reason = "") =>
        await hubContext.Clients.Group("governance").SendAsync("RequestScreenshot", new
        {
            reason,
            timestamp = DateTime.UtcNow,
        });

    /// <summary>
    ///     Get the latest captured screenshot data.
    /// </summary>
    public static (string? Base64, string? Format, DateTime? CapturedAt) GetLatestScreenshot()
    {
        lock (ScreenshotLock)
        {
            return (_latestScreenshotBase64, _latestScreenshotFormat, _latestScreenshotTime);
        }
    }

    public static int ConnectionCount => _connectionCount;
}
