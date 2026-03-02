namespace GA.Business.ML.Rag.Models;

/// <summary>
///     Domain representation of a musical instrument for semantic search.
/// </summary>
public record InstrumentRagDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required string TuningId { get; init; }
    public required string TuningName { get; init; }
    public required string[] StringPitches { get; init; } // e.g. ["E2", "A2", "D3", ...]
    public int StringCount { get; init; }
    public string? Description { get; init; }
    public required string[] SemanticTags { get; init; }

    public virtual void GenerateSearchText() => SearchText = $"{Name} {TuningName} {StringCount} strings {Description}";
}
