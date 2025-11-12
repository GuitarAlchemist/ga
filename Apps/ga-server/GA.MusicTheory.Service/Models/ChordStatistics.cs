namespace GA.MusicTheory.Service.Models;

/// <summary>
///     Statistics about the chord database
/// </summary>
public class ChordStatistics
{
    public long TotalChords { get; set; }
    public Dictionary<string, int> QualityDistribution { get; set; } = new();
    public Dictionary<string, int> ExtensionDistribution { get; set; } = new();
    public Dictionary<string, int> StackingTypeDistribution { get; set; } = new();
    public Dictionary<int, int> NoteCountDistribution { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

