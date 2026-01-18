namespace GA.Business.ML.Tabs.Models;

/// <summary>
/// Represents a parsed block of tablature (typically 6 lines).
/// </summary>
public record TabBlock
{
    /// <summary>
    /// The sequence of time slices extracted from the block.
    /// </summary>
    public List<TabSlice> Slices { get; init; } = new();

    /// <summary>
    /// Number of strings detected (default 6).
    /// </summary>
    public int StringCount { get; init; } = 6;
    
    /// <summary>
    /// Raw text lines for debugging/display.
    /// </summary>
    public List<string> RawLines { get; init; } = new();
}
