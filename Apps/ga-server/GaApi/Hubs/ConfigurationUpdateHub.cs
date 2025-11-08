namespace GaApi.Hubs;

using GA.Business.Core;
using Microsoft.AspNetCore.SignalR;

// using GA.Business.Core.Services // REMOVED - namespace does not exist;

/// <summary>
///     SignalR hub for real-time configuration updates and notifications
/// </summary>
public class ConfigurationUpdateHub(ILogger<ConfigurationUpdateHub> logger, ConfigurationReloadService reloadService)
    : Hub
{
    /// <summary>
    ///     Join a configuration group to receive updates
    /// </summary>
    public async Task JoinConfigurationGroup(string configurationType)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"config_{configurationType}");
        logger.LogInformation("Client {ConnectionId} joined configuration group {ConfigurationType}",
            Context.ConnectionId, configurationType);
    }

    /// <summary>
    ///     Leave a configuration group
    /// </summary>
    public async Task LeaveConfigurationGroup(string configurationType)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"config_{configurationType}");
        logger.LogInformation("Client {ConnectionId} left configuration group {ConfigurationType}",
            Context.ConnectionId, configurationType);
    }

    /// <summary>
    ///     Subscribe to all configuration updates
    /// </summary>
    public async Task SubscribeToAllUpdates()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "config_all");
        logger.LogInformation("Client {ConnectionId} subscribed to all configuration updates", Context.ConnectionId);
    }

    /// <summary>
    ///     Get current configuration status
    /// </summary>
    public async Task<ConfigurationStatus> GetConfigurationStatus()
    {
        try
        {
            var status = await reloadService.GetConfigurationStatusAsync();
            return status;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting configuration status for client {ConnectionId}", Context.ConnectionId);
            throw;
        }
    }

    /// <summary>
    ///     Manually trigger configuration reload
    /// </summary>
    public async Task ReloadConfiguration(string configurationType)
    {
        try
        {
            logger.LogInformation("Client {ConnectionId} requested reload of {ConfigurationType}",
                Context.ConnectionId, configurationType);

            await reloadService.ReloadConfigurationAsync(configurationType);

            // Notify all clients in the group
            await Clients.Group($"config_{configurationType}").SendAsync("ConfigurationReloaded", new
            {
                ConfigurationType = configurationType,
                Timestamp = DateTime.UtcNow,
                ReloadedBy = Context.ConnectionId
            });

            // Notify all subscribers
            await Clients.Group("config_all").SendAsync("ConfigurationReloaded", new
            {
                ConfigurationType = configurationType,
                Timestamp = DateTime.UtcNow,
                ReloadedBy = Context.ConnectionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reloading configuration {ConfigurationType} for client {ConnectionId}",
                configurationType, Context.ConnectionId);

            await Clients.Caller.SendAsync("ConfigurationReloadError", new
            {
                ConfigurationType = configurationType,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    ///     Reload all configurations
    /// </summary>
    public async Task ReloadAllConfigurations()
    {
        try
        {
            logger.LogInformation("Client {ConnectionId} requested reload of all configurations", Context.ConnectionId);

            await reloadService.ReloadAllConfigurationsAsync();

            // Notify all subscribers
            await Clients.Group("config_all").SendAsync("AllConfigurationsReloaded", new
            {
                Timestamp = DateTime.UtcNow,
                ReloadedBy = Context.ConnectionId
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reloading all configurations for client {ConnectionId}", Context.ConnectionId);

            await Clients.Caller.SendAsync("ConfigurationReloadError", new
            {
                ConfigurationType = "All",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client {ConnectionId} connected to configuration hub", Context.ConnectionId);

        // Send current status to new client
        try
        {
            var status = await reloadService.GetConfigurationStatusAsync();
            await Clients.Caller.SendAsync("ConfigurationStatus", status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending initial status to client {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client {ConnectionId} disconnected from configuration hub", Context.ConnectionId);

        if (exception != null)
        {
            logger.LogError(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
///     Service for broadcasting configuration updates via SignalR
/// </summary>
public class ConfigurationBroadcastService(
    IHubContext<ConfigurationUpdateHub> hubContext,
    ILogger<ConfigurationBroadcastService> logger)
{
    /// <summary>
    ///     Broadcast configuration file change notification
    /// </summary>
    public async Task BroadcastConfigurationChanged(string configurationType, string fileName,
        string changeType = "Modified")
    {
        try
        {
            var notification = new ConfigurationChangeNotification
            {
                ConfigurationType = configurationType,
                FileName = fileName,
                ChangeType = changeType,
                Timestamp = DateTime.UtcNow
            };

            // Send to specific configuration group
            await hubContext.Clients.Group($"config_{configurationType}")
                .SendAsync("ConfigurationChanged", notification);

            // Send to all subscribers
            await hubContext.Clients.Group("config_all")
                .SendAsync("ConfigurationChanged", notification);

            logger.LogInformation("Broadcasted configuration change: {ConfigurationType} - {ChangeType}",
                configurationType, changeType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting configuration change for {ConfigurationType}", configurationType);
        }
    }

    /// <summary>
    ///     Broadcast configuration reload completion
    /// </summary>
    public async Task BroadcastConfigurationReloaded(string configurationType, int itemCount)
    {
        try
        {
            var notification = new ConfigurationReloadNotification
            {
                ConfigurationType = configurationType,
                ItemCount = itemCount,
                Timestamp = DateTime.UtcNow,
                Success = true
            };

            // Send to specific configuration group
            await hubContext.Clients.Group($"config_{configurationType}")
                .SendAsync("ConfigurationReloaded", notification);

            // Send to all subscribers
            await hubContext.Clients.Group("config_all")
                .SendAsync("ConfigurationReloaded", notification);

            logger.LogInformation(
                "Broadcasted configuration reload completion: {ConfigurationType} - {ItemCount} items",
                configurationType, itemCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting configuration reload for {ConfigurationType}", configurationType);
        }
    }

    /// <summary>
    ///     Broadcast configuration reload error
    /// </summary>
    public async Task BroadcastConfigurationError(string configurationType, string error)
    {
        try
        {
            var notification = new ConfigurationReloadNotification
            {
                ConfigurationType = configurationType,
                Timestamp = DateTime.UtcNow,
                Success = false,
                ErrorMessage = error
            };

            // Send to specific configuration group
            await hubContext.Clients.Group($"config_{configurationType}")
                .SendAsync("ConfigurationError", notification);

            // Send to all subscribers
            await hubContext.Clients.Group("config_all")
                .SendAsync("ConfigurationError", notification);

            logger.LogWarning("Broadcasted configuration error: {ConfigurationType} - {Error}",
                configurationType, error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting configuration error for {ConfigurationType}", configurationType);
        }
    }

    /// <summary>
    ///     Broadcast system statistics update
    /// </summary>
    public async Task BroadcastStatisticsUpdate(object statistics)
    {
        try
        {
            await hubContext.Clients.Group("config_all")
                .SendAsync("StatisticsUpdated", new
                {
                    Statistics = statistics,
                    Timestamp = DateTime.UtcNow
                });

            logger.LogInformation("Broadcasted statistics update");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error broadcasting statistics update");
        }
    }
}

/// <summary>
///     Configuration change notification model
/// </summary>
public class ConfigurationChangeNotification
{
    public string ConfigurationType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
///     Configuration reload notification model
/// </summary>
public class ConfigurationReloadNotification
{
    public string ConfigurationType { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
///     Enhanced configuration watcher with SignalR integration
/// </summary>
public class EnhancedConfigurationWatcherService(
    ILogger<EnhancedConfigurationWatcherService> logger,
    IServiceProvider serviceProvider,
    ConfigurationBroadcastService broadcastService)
    : ConfigurationWatcherService(
        serviceProvider.GetRequiredService<ILogger<ConfigurationWatcherService>>(),
        serviceProvider)
{
    protected override async Task OnConfigurationFileChanged(string? fullPath, string? fileName)
    {
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        try
        {
            // Call base implementation
            await base.OnConfigurationFileChanged(fullPath, fileName);

            // Determine configuration type
            var configurationType = GetConfigurationTypeFromFileName(fileName);

            // Broadcast the change
            await broadcastService.BroadcastConfigurationChanged(configurationType, fileName);

            // Get updated item count and broadcast reload completion
            var itemCount = await GetItemCountForConfiguration(configurationType);
            await broadcastService.BroadcastConfigurationReloaded(configurationType, itemCount);

            logger.LogInformation("Successfully processed and broadcasted configuration change: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing configuration change for {FileName}", fileName);

            var configurationType = GetConfigurationTypeFromFileName(fileName ?? "Unknown");
            await broadcastService.BroadcastConfigurationError(configurationType, ex.Message);
        }
    }

    private string GetConfigurationTypeFromFileName(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "iconicchords.yaml" => "IconicChords",
            "chordprogressions.yaml" => "ChordProgressions",
            "guitartechniques.yaml" => "GuitarTechniques",
            "specializedtunings.yaml" => "SpecializedTunings",
            "modalinterchange.yaml" => "ModalInterchange",
            "voiceleading.yaml" => "VoiceLeading",
            "pedaltones.yaml" => "PedalTones",
            "atonaltechniques.yaml" => "AtonalTechniques",
            "stringinstrumenttechniques.yaml" => "StringInstrumentTechniques",
            "advancedharmony.yaml" => "AdvancedHarmony",
            "keymodulationtechniques.yaml" => "KeyModulationTechniques",
            _ => "Unknown"
        };
    }

    private Task<int> GetItemCountForConfiguration(string configurationType)
    {
        try
        {
            var count = configurationType switch
            {
                "IconicChords" => IconicChordsService.GetAllChords().Count(),
                "ChordProgressions" => ChordProgressionsService.GetAllProgressions().Count(),
                "GuitarTechniques" => GuitarTechniquesService.GetAllTechniques().Count(),
                "SpecializedTunings" => SpecializedTuningsService.GetAllTunings().Count(),
                _ => 0
            };

            return Task.FromResult(count);
        }
        catch
        {
            return Task.FromResult(0);
        }
    }
}
