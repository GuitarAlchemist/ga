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
            return (false, [$"Caching configuration error: {ex.Message}"]);
        }
    }
}
