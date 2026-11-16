namespace GaApi.Services;

using Controllers;
using Hubs;
using Microsoft.AspNetCore.SignalR;

/// <summary>
///     Background service that watches the Demerzel governance directory
///     for file changes and pushes updates to connected Prime Radiant clients
///     via the GovernanceHub SignalR connection.
/// </summary>
public sealed class GovernanceWatcherService(
    IConfiguration configuration,
    IHubContext<GovernanceHub> hubContext,
    BeliefStateService beliefStateService,
    VisualCriticService visualCriticService,
    ILogger<GovernanceWatcherService> logger)
    : BackgroundService
{
    private FileSystemWatcher? _watcher;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var demerzelRoot = configuration["Governance:DemerzelRoot"]
            ?? DemerzelRootLocator.Find();

        if (demerzelRoot == null || !Directory.Exists(demerzelRoot))
        {
            logger.LogWarning("Demerzel root not found, governance watcher disabled");
            return;
        }

        logger.LogInformation("Governance watcher started on {Root}", demerzelRoot);

        // Watch for file changes in the governance directory
        _watcher = new FileSystemWatcher(demerzelRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true,
        };

        // Debounce: collect changes for 2 seconds before broadcasting
        var debounceTimer = new System.Timers.Timer(2000) { AutoReset = false };
        debounceTimer.Elapsed += async (_, _) =>
        {
            try
            {
                if (GovernanceHub.ConnectionCount == 0)
                {
                    logger.LogDebug("No governance clients connected, skipping broadcast");
                    return;
                }

                logger.LogInformation("Governance files changed, broadcasting to {Count} clients. " +
                    "Algedonic signal evaluation pending for governance file changes.",
                    GovernanceHub.ConnectionCount);

                // Force refresh the controller cache
                var controller = new GovernanceController(configuration, logger as ILogger<GovernanceController>
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GovernanceController>.Instance, hubContext, beliefStateService, visualCriticService);
                controller.Refresh();
                var result = controller.GetGraph();

                if (result.Value != null)
                {
                    await GovernanceHub.BroadcastGraphUpdate(hubContext, result.Value);
                }

                // Also broadcast updated belief states
                var beliefs = beliefStateService.GetBeliefs();
                if (beliefs.Count > 0)
                {
                    await GovernanceHub.BroadcastBeliefsSnapshot(hubContext, beliefs);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to broadcast governance update");
            }
        };

        _watcher.Changed += (_, e) =>
        {
            // Only react to governance-relevant files
            if (IsGovernanceFile(e.FullPath))
            {
                logger.LogDebug("Governance file changed: {Path}", e.Name);
                debounceTimer.Stop();
                debounceTimer.Start();
            }
        };
        _watcher.Created += (_, e) =>
        {
            if (IsGovernanceFile(e.FullPath))
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            }
        };
        _watcher.Deleted += (_, e) =>
        {
            if (IsGovernanceFile(e.FullPath))
            {
                debounceTimer.Stop();
                debounceTimer.Start();
            }
        };

        // Also push periodic heartbeats every 30 seconds with connection count
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(30000, stoppingToken);
        }
    }

    private static bool IsGovernanceFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext is ".yaml" or ".md" or ".json" or ".ixql" or ".test.md";
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}
