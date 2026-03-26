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
    public static async Task BroadcastGraphUpdate(IHubContext<GovernanceHub> hubContext, GovernanceGraph graph)
    {
        await hubContext.Clients.Group("governance").SendAsync("GraphUpdate", graph);
    }

    /// <summary>
    ///     Broadcast a single node health change to all subscribed clients.
    /// </summary>
    public static async Task BroadcastNodeChanged(
        IHubContext<GovernanceHub> hubContext,
        string nodeId,
        HealthMetrics health,
        string healthStatus,
        string color)
    {
        await hubContext.Clients.Group("governance").SendAsync("NodeChanged", new
        {
            nodeId,
            health,
            healthStatus,
            color,
            timestamp = DateTime.UtcNow,
        });
    }

    public static int ConnectionCount => _connectionCount;
}
