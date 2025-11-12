namespace GA.Business.Core.Fretboard.Voicings.Core;

/// <summary>
/// Voicing with embedding data for vector search
/// </summary>
public record VoicingEmbedding(
    string Id,
    string ChordName,
    string? VoicingType,
    string? Position,
    string? Difficulty,
    string? ModeName,
    string? ModalFamily,
    string[] SemanticTags,
    string PrimeFormId,
    int TranslationOffset,
    string Diagram,
    int[] MidiNotes,
    string PitchClassSet,
    string IntervalClassVector,
    int MinFret,
    int MaxFret,
    bool BarreRequired,
    int HandStretch,
    string Description,
    double[] Embedding);

/// <summary>
/// Performance characteristics of a voicing search strategy
/// </summary>
public record VoicingSearchPerformance(
    TimeSpan ExpectedSearchTime,
    long MemoryUsageMb,
    bool RequiresGpu,
    bool RequiresNetwork);

/// <summary>
/// Voicing search statistics
/// </summary>
public record VoicingSearchStats(
    long TotalVoicings,
    long MemoryUsageMb,
    TimeSpan AverageSearchTime,
    long TotalSearches);

