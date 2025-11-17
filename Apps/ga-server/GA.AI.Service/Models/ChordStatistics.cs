namespace GA.AI.Service.Models;

/// <summary>
/// Statistical information about chords
/// </summary>
public class ChordStatistics
{
    /// <summary>
    /// Total number of chords
    /// </summary>
    public int TotalChords { get; set; }

    /// <summary>
    /// Number of unique chord qualities
    /// </summary>
    public int UniqueQualities { get; set; }

    /// <summary>
    /// Number of unique root notes
    /// </summary>
    public int UniqueRoots { get; set; }

    /// <summary>
    /// Most common chord quality
    /// </summary>
    public string MostCommonQuality { get; set; } = string.Empty;

    /// <summary>
    /// Most common root note
    /// </summary>
    public string MostCommonRoot { get; set; } = string.Empty;

    /// <summary>
    /// Distribution of chord qualities
    /// </summary>
    public Dictionary<string, int> QualityDistribution { get; set; } = new();

    /// <summary>
    /// Distribution of root notes
    /// </summary>
    public Dictionary<string, int> RootDistribution { get; set; } = new();

    /// <summary>
    /// Average number of notes per chord
    /// </summary>
    public double AverageNotesPerChord { get; set; }

    /// <summary>
    /// Distribution of chord extensions
    /// </summary>
    public Dictionary<string, int> ExtensionDistribution { get; set; } = new();

    /// <summary>
    /// Distribution of chord stacking types
    /// </summary>
    public Dictionary<string, int> StackingTypeDistribution { get; set; } = new();

    /// <summary>
    /// Distribution of note counts
    /// </summary>
    public Dictionary<int, int> NoteCountDistribution { get; set; } = new();

    /// <summary>
    /// When the statistics were calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
