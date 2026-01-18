namespace GA.Business.ML.Tabs.Models;

using Core.Fretboard.Voicings.Search;

public record TabAnalysisResult
{
    public List<TabEvent> Events { get; init; } = new();
    
    /// <summary>
    /// Detected harmonic cadence at the end of the phrase (if any).
    /// </summary>
    public string? DetectedCadence { get; init; }
}

public record TabEvent
{
    /// <summary>
    /// Sequential index of the event (ignoring empty slices).
    /// </summary>
    public int TimestampIndex { get; init; }
    
    public TabSlice OriginalSlice { get; init; }
    
    public double[] Embedding { get; init; }
    
    public VoicingDocument Document { get; init; }
}
