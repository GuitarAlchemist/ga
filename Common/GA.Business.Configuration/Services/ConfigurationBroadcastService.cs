namespace GA.Business.Core.Services;

/// <summary>
/// Service for broadcasting configuration changes and errors
/// </summary>
public class ConfigurationBroadcastService(ILogger<ConfigurationBroadcastService> logger)
{
    private readonly List<IConfigurationChangeListener> _listeners = [];

    /// <summary>
    /// Register a listener for configuration changes
    /// </summary>
    public void RegisterListener(IConfigurationChangeListener listener)
    {
        _listeners.Add(listener);
        logger.LogDebug("Registered configuration change listener: {ListenerType}", listener.GetType().Name);
    }

    /// <summary>
    /// Unregister a listener
    /// </summary>
    public void UnregisterListener(IConfigurationChangeListener listener)
    {
        _listeners.Remove(listener);
        logger.LogDebug("Unregistered configuration change listener: {ListenerType}", listener.GetType().Name);
    }

    /// <summary>
    /// Broadcast a configuration change event
    /// </summary>
    public async Task BroadcastConfigurationChange(string configurationType, string fileName, ConfigurationChangeType changeType)
    {
        logger.LogInformation("Broadcasting configuration change: {ConfigurationType} - {FileName} - {ChangeType}", 
                             configurationType, fileName, changeType);

        var changeEvent = new ConfigurationChangeEvent
        {
            ConfigurationType = configurationType,
            FileName = fileName,
            ChangeType = changeType,
            Timestamp = DateTime.UtcNow
        };

        var tasks = _listeners.Select(listener => NotifyListenerSafely(listener, changeEvent));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Broadcast a configuration error
    /// </summary>
    public async Task BroadcastConfigurationError(string configurationType, string errorMessage)
    {
        logger.LogError("Broadcasting configuration error: {ConfigurationType} - {ErrorMessage}", 
                       configurationType, errorMessage);

        var errorEvent = new ConfigurationErrorEvent
        {
            ConfigurationType = configurationType,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        };

        var tasks = _listeners.Select(listener => NotifyListenerErrorSafely(listener, errorEvent));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Broadcast a configuration validation result
    /// </summary>
    public async Task BroadcastValidationResult(string configurationType, bool isValid, int violationCount)
    {
        logger.LogInformation("Broadcasting validation result: {ConfigurationType} - Valid: {IsValid} - Violations: {ViolationCount}", 
                             configurationType, isValid, violationCount);

        var validationEvent = new ConfigurationValidationEvent
        {
            ConfigurationType = configurationType,
            IsValid = isValid,
            ViolationCount = violationCount,
            Timestamp = DateTime.UtcNow
        };

        var tasks = _listeners.Select(listener => NotifyListenerValidationSafely(listener, validationEvent));
        await Task.WhenAll(tasks);
    }

    private async Task NotifyListenerSafely(IConfigurationChangeListener listener, ConfigurationChangeEvent changeEvent)
    {
        try
        {
            await listener.OnConfigurationChanged(changeEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error notifying configuration change listener {ListenerType}", listener.GetType().Name);
        }
    }

    private async Task NotifyListenerErrorSafely(IConfigurationChangeListener listener, ConfigurationErrorEvent errorEvent)
    {
        try
        {
            await listener.OnConfigurationError(errorEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error notifying configuration error listener {ListenerType}", listener.GetType().Name);
        }
    }

    private async Task NotifyListenerValidationSafely(IConfigurationChangeListener listener, ConfigurationValidationEvent validationEvent)
    {
        try
        {
            await listener.OnValidationCompleted(validationEvent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error notifying validation listener {ListenerType}", listener.GetType().Name);
        }
    }
}

/// <summary>
/// Interface for configuration change listeners
/// </summary>
public interface IConfigurationChangeListener
{
    Task OnConfigurationChanged(ConfigurationChangeEvent changeEvent);
    Task OnConfigurationError(ConfigurationErrorEvent errorEvent);
    Task OnValidationCompleted(ConfigurationValidationEvent validationEvent);
}

/// <summary>
/// Configuration change event
/// </summary>
public class ConfigurationChangeEvent
{
    public string ConfigurationType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ConfigurationChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Configuration error event
/// </summary>
public class ConfigurationErrorEvent
{
    public string ConfigurationType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Configuration validation event
/// </summary>
public class ConfigurationValidationEvent
{
    public string ConfigurationType { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public int ViolationCount { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Types of configuration changes
/// </summary>
public enum ConfigurationChangeType
{
    Created,
    Updated,
    Deleted,
    Renamed,
    Moved
}
