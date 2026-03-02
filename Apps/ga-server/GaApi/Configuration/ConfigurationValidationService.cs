namespace GaApi.Configuration;

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
