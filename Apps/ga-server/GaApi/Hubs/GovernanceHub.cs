namespace GaApi.Hubs;

using Microsoft.AspNetCore.SignalR;
using Controllers;

/// <summary>
///     SignalR hub for real-time governance graph updates.
///     Unauthenticated — Prime Radiant clients connect without tokens.
///
///     Server → Client events:
///       "GraphUpdate"    — full governance graph JSON
///       "HealthUpdate"   — partial update with changed node health metrics
///       "NodeChanged"    — single node update (id, health, color)
///       "Connected"      — welcome message with current node count
///
///     Client → Server methods:
///       Subscribe()      — register for updates
///       RequestRefresh() — request immediate full graph push
/// </summary>
public sealed class GovernanceHub(
    ILogger<GovernanceHub> logger)
    : Hub
{
    private static int _connectionCount;

    public override async Task OnConnectedAsync()
    {
        Interlocked.Increment(ref _connectionCount);
        logger.LogInformation("Governance client connected: {ConnectionId} (total: {Count})",
            Context.ConnectionId, _connectionCount);

        await Clients.Caller.SendAsync("Connected", new
        {
            message = "Connected to Governance Hub",
            connections = _connectionCount,
            timestamp = DateTime.UtcNow,
        });

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Interlocked.Decrement(ref _connectionCount);
        logger.LogInformation("Governance client disconnected: {ConnectionId} (total: {Count})",
            Context.ConnectionId, _connectionCount);
        return base.OnDisconnectedAsync(exception);
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
    }

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
