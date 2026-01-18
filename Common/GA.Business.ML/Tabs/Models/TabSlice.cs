namespace GA.Business.ML.Tabs.Models;

/// <summary>
/// Represents a vertical slice of time in the tablature.
/// Contains all notes played simultaneously at this moment.
/// </summary>
public record TabSlice
{
    public List<TabNote> Notes { get; init; } = new();
    
    /// <summary>
    /// True if this slice represents a bar line delimiter ("|").
    /// </summary>
    public bool IsBarLine { get; init; }

    /// <summary>
    /// True if this slice is empty (whitespace/dashes only).
    /// </summary>
    public bool IsEmpty => Notes.Count == 0 && !IsBarLine;
}
