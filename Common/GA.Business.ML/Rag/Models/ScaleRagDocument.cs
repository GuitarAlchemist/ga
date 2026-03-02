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

    /// <inheritdoc />
    /// <remarks>
    ///     Follows rag-engineer semantic chunking principles:
    ///     - Labelled fields improve embedding recall vs. bare concatenation.
    ///     - SemanticTags are appended last so they act as soft signal boosters.
    ///     - All nullable fields are guarded to avoid literal "null" tokens in the vector.
    /// </remarks>
    public override string ToEmbeddingString()
    {
        var parts = new List<string>
        {
            $"Scale: {Name}",
        };

        if (Aliases is { Length: > 0 })
            parts.Add($"Also known as: {string.Join(", ", Aliases)}");

        parts.Add($"Formula: {Formula}");
        parts.Add($"Pitch classes: {string.Join(" ", PitchClasses)}");
        parts.Add($"Interval class vector: {IntervalClassVector}");

        if (!string.IsNullOrWhiteSpace(ModeName))
            parts.Add($"Mode: {ModeName}");

        if (!string.IsNullOrWhiteSpace(Family))
            parts.Add($"Family: {Family}");

        if (IsDiatonic)
            parts.Add("diatonic");

        if (SemanticTags.Length > 0)
            parts.Add(string.Join(" ", SemanticTags));

        return string.Join(". ", parts);
    }
}
