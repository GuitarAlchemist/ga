using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using GA.Business.Core.Configuration;

namespace GA.Business.Core.Services;

/// <summary>
/// Background service that watches YAML configuration files for changes and hot-reloads them
/// </summary>
public class ConfigurationWatcherService(ILogger<ConfigurationWatcherService> logger, IServiceProvider serviceProvider)
    : BackgroundService
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Dictionary<string, DateTime> _lastProcessedTimes = [];
    private readonly object _lock = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting configuration file watcher service");

        try
        {
            SetupFileWatchers();
            
            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Configuration watcher service was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in configuration watcher service");
        }
        finally
        {
            DisposeWatchers();
        }
    }

    private void SetupFileWatchers()
    {
        var configurationFiles = new[]
        {
            "IconicChords.yaml",
            "ChordProgressions.yaml", 
            "GuitarTechniques.yaml",
            "SpecializedTunings.yaml",
            "ModalInterchange.yaml",
            "VoiceLeading.yaml",
            "PedalTones.yaml",
            "AtonalTechniques.yaml",
            "StringInstrumentTechniques.yaml",
            "AdvancedHarmony.yaml",
            "KeyModulationTechniques.yaml"
        };

        var configDirectory = GetConfigurationDirectory();
        
        if (!Directory.Exists(configDirectory))
        {
            logger.LogWarning("Configuration directory not found: {Directory}", configDirectory);
            return;
        }

        foreach (var fileName in configurationFiles)
        {
            var filePath = Path.Combine(configDirectory, fileName);
            
            if (File.Exists(filePath))
            {
                SetupFileWatcher(configDirectory, fileName);
                logger.LogInformation("Watching configuration file: {FileName}", fileName);
            }
            else
            {
                logger.LogWarning("Configuration file not found: {FilePath}", filePath);
            }
        }
    }

    private void SetupFileWatcher(string directory, string fileName)
    {
        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnWatcherChanged;
        watcher.Error += OnWatcherError;

        _watchers.Add(watcher);
    }

    protected virtual async Task OnConfigurationFileChanged(string? fullPath, string? fileName)
    {
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(fileName))
            return;

        // Debounce rapid file changes
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_lastProcessedTimes.TryGetValue(fullPath, out var lastProcessed) &&
                now - lastProcessed < TimeSpan.FromSeconds(2))
            {
                return;
            }
            _lastProcessedTimes[fullPath] = now;
        }

        logger.LogInformation("Configuration file changed: {FileName}", fileName);

        try
        {
            // Wait a bit for file write to complete
            await Task.Delay(500);

            await ReloadConfiguration(fileName);
            await UpdateCache(fileName);

            logger.LogInformation("Successfully reloaded configuration: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reloading configuration file: {FileName}", fileName);
        }
    }

    private Task ReloadConfiguration(string fileName)
    {
        switch (fileName.ToLowerInvariant())
        {
            case "iconicchords.yaml":
                IconicChordsConfigLoader.ReloadConfiguration();
                break;
                
            case "chordprogressions.yaml":
                ChordProgressionsConfigLoader.ReloadConfiguration();
                break;
                
            case "guitartechniques.yaml":
                GuitarTechniquesConfigLoader.ReloadConfiguration();
                break;
                
            case "specializedtunings.yaml":
                SpecializedTuningsConfigLoader.ReloadConfiguration();
                break;
                
            default:
                logger.LogWarning("Unknown configuration file: {FileName}", fileName);
                break;
        }

        return Task.CompletedTask;
    }

    private async Task UpdateCache(string fileName)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var cacheService = scope.ServiceProvider.GetService<MusicalKnowledgeCacheService>();
            
            if (cacheService == null)
            {
                logger.LogWarning("Cache service not available for updating");
                return;
            }

            switch (fileName.ToLowerInvariant())
            {
                case "iconicchords.yaml":
                    await cacheService.SynchronizeIconicChordsAsync();
                    break;
                    
                case "chordprogressions.yaml":
                    await cacheService.SynchronizeChordProgressionsAsync();
                    break;
                    
                case "guitartechniques.yaml":
                    await cacheService.SynchronizeGuitarTechniquesAsync();
                    break;
                    
                case "specializedtunings.yaml":
                    await cacheService.SynchronizeSpecializedTuningsAsync();
                    break;
            }

            logger.LogInformation("Cache updated for configuration: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating cache for configuration: {FileName}", fileName);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        logger.LogError(e.GetException(), "File watcher error occurred");

        if (sender is not FileSystemWatcher watcher)
        {
            return;
        }

        _ = RestartWatcherAsync(watcher);
    }

    private void OnWatcherChanged(object? sender, FileSystemEventArgs e)
    {
        var task = OnConfigurationFileChanged(e.FullPath, e.Name);

        if (!task.IsCompleted)
        {
            task.ContinueWith(
                t => logger.LogError(t.Exception, "Unhandled error processing configuration change for {FileName}", e.Name),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        else if (task.IsFaulted && task.Exception is not null)
        {
            logger.LogError(task.Exception, "Unhandled error processing configuration change for {FileName}", e.Name);
        }
    }

    private async Task RestartWatcherAsync(FileSystemWatcher watcher)
    {
        try
        {
            watcher.EnableRaisingEvents = false;
            await Task.Delay(1000);
            watcher.EnableRaisingEvents = true;
            logger.LogInformation("Restarted file watcher");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart file watcher");
        }
    }

    private string GetConfigurationDirectory()
    {
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory),
            Path.Combine(Directory.GetCurrentDirectory()),
            Path.Combine(Directory.GetCurrentDirectory(), "Common", "GA.Business.Config"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common", "GA.Business.Config")
        };

        foreach (var path in possiblePaths)
        {
            if (Directory.Exists(path) && Directory.GetFiles(path, "*.yaml").Any())
            {
                return path;
            }
        }

        return possiblePaths[2]; // Default to the expected location
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error disposing file watcher");
            }
        }
        _watchers.Clear();
    }

    public override void Dispose()
    {
        DisposeWatchers();
        base.Dispose();
    }
}

/// <summary>
/// Service for manually triggering configuration reloads
/// </summary>
public class ConfigurationReloadService(
    ILogger<ConfigurationReloadService> logger,
    MusicalKnowledgeCacheService? cacheService = null)
{
    /// <summary>
    /// Manually reload all configurations
    /// </summary>
    public async Task ReloadAllConfigurationsAsync()
    {
        logger.LogInformation("Starting manual reload of all configurations");

        try
        {
            // Reload YAML configurations
            MusicalKnowledgeService.ReloadAllConfigurations();
            
            // Update cache if available
            if (cacheService != null)
            {
                await cacheService.SynchronizeAllAsync();
            }

            logger.LogInformation("Successfully reloaded all configurations");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during manual configuration reload");
            throw;
        }
    }

    /// <summary>
    /// Reload specific configuration
    /// </summary>
    public async Task ReloadConfigurationAsync(string configurationType)
    {
        logger.LogInformation("Starting manual reload of {ConfigurationType}", configurationType);

        try
        {
            switch (configurationType.ToLowerInvariant())
            {
                case "iconicchords":
                    IconicChordsConfigLoader.ReloadConfiguration();
                    if (cacheService != null)
                        await cacheService.SynchronizeIconicChordsAsync();
                    break;
                    
                case "chordprogressions":
                    ChordProgressionsConfigLoader.ReloadConfiguration();
                    if (cacheService != null)
                        await cacheService.SynchronizeChordProgressionsAsync();
                    break;
                    
                case "guitartechniques":
                    GuitarTechniquesConfigLoader.ReloadConfiguration();
                    if (cacheService != null)
                        await cacheService.SynchronizeGuitarTechniquesAsync();
                    break;
                    
                case "specializedtunings":
                    SpecializedTuningsConfigLoader.ReloadConfiguration();
                    if (cacheService != null)
                        await cacheService.SynchronizeSpecializedTuningsAsync();
                    break;
                    
                default:
                    throw new ArgumentException($"Unknown configuration type: {configurationType}");
            }

            logger.LogInformation("Successfully reloaded {ConfigurationType}", configurationType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reloading {ConfigurationType}", configurationType);
            throw;
        }
    }

    /// <summary>
    /// Get configuration status information
    /// </summary>
    public Task<ConfigurationStatus> GetConfigurationStatusAsync()
    {
        var status = new ConfigurationStatus
        {
            LastReloaded = DateTime.UtcNow,
            Configurations = []
        };

        try
        {
            // Check each configuration
            var iconicChordsCount = IconicChordsService.GetAllChords().Count();
            status.Configurations.Add(new ConfigurationInfo
            {
                Type = "IconicChords",
                ItemCount = iconicChordsCount,
                IsLoaded = iconicChordsCount > 0,
                LastUpdated = DateTime.UtcNow
            });

            var progressionsCount = ChordProgressionsService.GetAllProgressions().Count();
            status.Configurations.Add(new ConfigurationInfo
            {
                Type = "ChordProgressions", 
                ItemCount = progressionsCount,
                IsLoaded = progressionsCount > 0,
                LastUpdated = DateTime.UtcNow
            });

            var techniquesCount = GuitarTechniquesService.GetAllTechniques().Count();
            status.Configurations.Add(new ConfigurationInfo
            {
                Type = "GuitarTechniques",
                ItemCount = techniquesCount,
                IsLoaded = techniquesCount > 0,
                LastUpdated = DateTime.UtcNow
            });

            var tuningsCount = SpecializedTuningsService.GetAllTunings().Count();
            status.Configurations.Add(new ConfigurationInfo
            {
                Type = "SpecializedTunings",
                ItemCount = tuningsCount,
                IsLoaded = tuningsCount > 0,
                LastUpdated = DateTime.UtcNow
            });

            status.IsHealthy = status.Configurations.All(c => c.IsLoaded);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting configuration status");
            status.IsHealthy = false;
            status.ErrorMessage = ex.Message;
        }

        return Task.FromResult(status);
    }
}

/// <summary>
/// Configuration status information
/// </summary>
public class ConfigurationStatus
{
    public bool IsHealthy { get; set; }
    public DateTime LastReloaded { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ConfigurationInfo> Configurations { get; set; } = [];
}

/// <summary>
/// Individual configuration information
/// </summary>
public class ConfigurationInfo
{
    public string Type { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public bool IsLoaded { get; set; }
    public DateTime LastUpdated { get; set; }
}
