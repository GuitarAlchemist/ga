namespace GaApi.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
///     Service for validating configuration on startup
/// </summary>
public class ConfigurationValidator(
    IOptions<CachingOptions> cachingOptions,
    ILogger<ConfigurationValidator> logger)
{
    /// <summary>
    ///     Validate all configuration options
    /// </summary>
    public (bool IsValid, List<string> Errors) ValidateAll()
    {
        var allErrors = new List<string>();

        // Validate caching options
        var (cachingValid, cachingErrors) = ValidateCachingOptions();
        if (!cachingValid)
        {
            allErrors.AddRange(cachingErrors);
        }

        // Log results
        if (allErrors.Count > 0)
        {
            logger.LogError("Configuration validation failed with {ErrorCount} errors:", allErrors.Count);
            foreach (var error in allErrors)
            {
                logger.LogError("  - {Error}", error);
            }
        }
        else
        {
            logger.LogInformation("Configuration validation passed");
        }

        return (allErrors.Count == 0, allErrors);
    }

    private (bool IsValid, List<string> Errors) ValidateCachingOptions()
    {
        try
        {
            var options = cachingOptions.Value;
            return options.Validate();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating caching options");
            return (false, new List<string> { $"Caching configuration error: {ex.Message}" });
        }
    }
}

/// <summary>
///     Hosted service that validates configuration on startup
/// </summary>
public class ConfigurationValidationService(
    ConfigurationValidator validator,
    ILogger<ConfigurationValidationService> logger,
    IHostApplicationLifetime lifetime) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Validating configuration on startup...");

        var (isValid, errors) = validator.ValidateAll();

        if (!isValid)
        {
            logger.LogCritical("Configuration validation failed. Application will not start.");
            logger.LogCritical("Errors:");
            foreach (var error in errors)
            {
                logger.LogCritical("  - {Error}", error);
            }

            // Stop the application
            lifetime.StopApplication();
            return Task.CompletedTask;
        }

        logger.LogInformation("Configuration validation successful");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
