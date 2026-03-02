namespace GaApi.Configuration;

using System.ComponentModel.DataAnnotations;

/// <summary>
///     Configuration options for caching
/// </summary>
public class CachingOptions
{
    public const string SectionName = "Caching";

    /// <summary>
    ///     Regular cache configuration
    /// </summary>
    [Required]
    public CacheTypeOptions Regular { get; set; } = new();

    /// <summary>
    ///     Semantic cache configuration
    /// </summary>
    [Required]
    public CacheTypeOptions Semantic { get; set; } = new();

    /// <summary>
    ///     Validate the configuration
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        var (regularValid, regularErrors) = Regular.Validate("Regular");
        if (!regularValid)
        {
            errors.AddRange(regularErrors);
        }

        var (semanticValid, semanticErrors) = Semantic.Validate("Semantic");
        if (!semanticValid)
        {
            errors.AddRange(semanticErrors);
        }

        return (errors.Count == 0, errors);
    }
}
