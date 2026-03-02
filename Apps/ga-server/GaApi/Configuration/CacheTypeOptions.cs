namespace GaApi.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
///     Configuration for a specific cache type
/// </summary>
public class CacheTypeOptions
{
    /// <summary>
    ///     Cache expiration time in minutes
    /// </summary>
    [Range(1, 1440, ErrorMessage = "ExpirationMinutes must be between 1 and 1440 (24 hours)")]
    public int ExpirationMinutes { get; set; } = 15;

    /// <summary>
    ///     Maximum number of items in cache
    /// </summary>
    [Range(10, 100000, ErrorMessage = "SizeLimit must be between 10 and 100000")]
    public int SizeLimit { get; set; } = 1000;

    /// <summary>
    ///     Validate the configuration
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate(string cacheName)
    {
        var errors = new List<string>();

        if (ExpirationMinutes < 1 || ExpirationMinutes > 1440)
        {
            errors.Add($"{cacheName}.ExpirationMinutes must be between 1 and 1440 (24 hours)");
        }

        if (SizeLimit < 10 || SizeLimit > 100000)
        {
            errors.Add($"{cacheName}.SizeLimit must be between 10 and 100000");
        }

        return (errors.Count == 0, errors);
    }
}