namespace GA.Business.ML.Tabs.Models;

public record TabAnalysisResult
{
    public List<TabEvent> Events { get; init; } = [];

    /// <summary>
    ///     Detected harmonic cadence at the end of the phrase (if any).
    /// </summary>
    public string? DetectedCadence { get; init; }
}

public record TabEvent
{
    /// <summary>
    ///     Sequential index of the event (ignoring empty slices).
    /// </summary>
    public int TimestampIndex { get; init; }

    public TabSlice OriginalSlice { get; init; }

    public float[] Embedding { get; init; }

    public ChordVoicingRagDocument Document { get; init; }
}
