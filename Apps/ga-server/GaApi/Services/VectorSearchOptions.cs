namespace GaApi.Services;

/// <summary>
///     Configuration options for vector search strategies
/// </summary>
public class VectorSearchOptions
{
    /// <summary>
    ///     Preferred strategy order (first available will be used)
    /// </summary>
    public string[]? PreferredStrategies { get; set; }

    /// <summary>
    ///     Whether to enable automatic strategy switching based on performance
    /// </summary>
    public bool EnableAutoSwitching { get; set; } = false;

    /// <summary>
    ///     Minimum performance improvement required to switch strategies (in percentage)
    /// </summary>
    public double AutoSwitchThreshold { get; set; } = 20.0;

    /// <summary>
    ///     Whether to preload all available strategies
    /// </summary>
    public bool PreloadStrategies { get; set; } = false;

    /// <summary>
    ///     Maximum memory usage for in-memory strategies (in MB)
    /// </summary>
    public long MaxMemoryUsageMb { get; set; } = 2048;
}
