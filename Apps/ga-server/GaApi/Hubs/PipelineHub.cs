namespace GaApi.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
///     SignalR hub for real-time pipeline progress updates.
///     Broadcasts stage transitions, logs, and completion events
///     to the Prime Radiant BrainstormPanel.
///
///     Server → Client events:
///       "PipelineStarted"   — pipeline run initiated
///       "StageStarted"      — a stage (brainstorm/plan/build/review/compound) began
///       "StageCompleted"    — a stage finished with result text
///       "PipelineCompleted" — all stages done
///       "PipelineError"     — stage failed with error
///       "PipelineAborted"   — user cancelled
/// </summary>
public sealed class PipelineHub(ILogger<PipelineHub> logger) : Hub
{
    public const string GroupName = "pipeline";

    public async Task Subscribe()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
        logger.LogInformation("Client {Id} subscribed to pipeline updates", Context.ConnectionId);
    }

    public async Task Unsubscribe() =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName);

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", "Pipeline hub ready");
        await base.OnConnectedAsync();
    }
}
