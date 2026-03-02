namespace GaApi.Models;

/// <summary>
///     Chord complexity statistics
/// </summary>
public class ComplexityStats
{
    /// <summary>
    ///     Average complexity score
    /// </summary>
    public double AverageComplexity { get; set; }

    /// <summary>
    ///     Minimum complexity score
    /// </summary>
    public double MinComplexity { get; set; }

    /// <summary>
    ///     Maximum complexity score
    /// </summary>
    public double MaxComplexity { get; set; }

    /// <summary>
    ///     Standard deviation of complexity
    /// </summary>
    public double ComplexityStdDev { get; set; }

    /// <summary>
    ///     Distribution of complexity ranges
    /// </summary>
    public Dictionary<string, int> ComplexityRanges { get; set; } = [];
}
