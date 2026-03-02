namespace GA.Business.ML.Rag.Models;

/// <summary>
///     Domain representation of a voicing document for semantic search.
///     Consistent with OPTIC-K v1.6.
/// </summary>
public record ChordVoicingRagDocument : RagDocumentBase
{
    public required string SearchableText { get; init; }
    public override string ToEmbeddingString() => SearchableText;

    public string? ChordName { get; init; }
    public string? VoicingType { get; init; }
    public string? Position { get; init; }
    public string? Difficulty { get; init; }
    public string? ModeName { get; init; }
    public string? ModalFamily { get; init; }
    public required string[] PossibleKeys { get; init; }
    public string? CagedShape { get; init; }
    public required string[] SemanticTags { get; init; }
    public string? PrimeFormId { get; init; }
    public string? ForteCode { get; init; }
    public int TranslationOffset { get; init; }
    public required string YamlAnalysis { get; init; }
    public required string Diagram { get; init; }
    public required int[] MidiNotes { get; init; }
    public required int[] PitchClasses { get; init; }
    public required string PitchClassSet { get; init; }
    public required string IntervalClassVector { get; init; }
    public int MinFret { get; init; }
    public int MaxFret { get; init; }
    public int HandStretch { get; init; }
    public bool BarreRequired { get; init; }
    public required string AnalysisEngine { get; init; }
    public required string AnalysisVersion { get; init; }
    public string? SchemaVersion { get; init; }
    public required string[] Jobs { get; init; }
    public required string TuningId { get; init; }
    public required string PitchClassSetId { get; init; }
    public int? RootPitchClass { get; init; }
    public int MidiBassNote { get; init; }
    public string? StackingType { get; init; }
    public string? HarmonicFunction { get; init; }
    public string? Quality { get; init; }
    public bool IsNaturallyOccurring { get; init; }
    public bool IsRootless { get; init; }
    public bool HasGuideTones { get; init; }
    public int Inversion { get; init; }
    public string[]? OmittedTones { get; init; }
    public double Brightness { get; init; }
    public double Consonance { get; init; }
    public double Roughness { get; init; }
    public int? TopPitchClass { get; init; }
    public string? TexturalDescription { get; init; }
    public string[]? DoubledTones { get; init; }
    public string[]? AlternateNames { get; init; }
    public double DifficultyScore { get; init; }
    public double[]? TextEmbedding { get; init; }
}
