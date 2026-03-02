namespace GA.Business.ML.Rag.Models;

/// <summary>
///     Domain representation of a musical scale for semantic search.
/// </summary>
public record ScaleRagDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required string Formula { get; init; } // e.g., "1 2 b3 4 5 b6 b7"
    public string[]? Aliases { get; init; }
    public required string[] PitchClasses { get; init; }
    public required string IntervalClassVector { get; init; }
    public bool IsDiatonic { get; init; }
    public string? ModeName { get; init; }
    public string? Family { get; init; }
    public required string[] SemanticTags { get; init; }
    
    public virtual void GenerateSearchText() => SearchText = $"{Name} {string.Join(" ", Aliases ?? [])} {Formula} {Family}";
}
