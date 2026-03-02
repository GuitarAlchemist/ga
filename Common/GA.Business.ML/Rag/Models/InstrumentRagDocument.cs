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

    /// <inheritdoc />
    /// <remarks>
    ///     Follows rag-engineer semantic chunking principles:
    ///     - All fields are labelled so the embedding model understands field boundaries.
    ///     - String pitches included to support queries like "guitar tuned to drop-D".
    ///     - Description is only added when non-null to avoid a literal "null" token.
    ///     - SemanticTags appended last as soft classifiers.
    /// </remarks>
    public override string ToEmbeddingString()
    {
        var parts = new List<string>
        {
            $"Instrument: {Name}",
            $"Strings: {StringCount}",
            $"Tuning: {TuningName}",
        };

        if (StringPitches.Length > 0)
            parts.Add($"Open string pitches: {string.Join(" ", StringPitches)}");

        if (!string.IsNullOrWhiteSpace(Description))
            parts.Add(Description);

        if (SemanticTags.Length > 0)
            parts.Add(string.Join(" ", SemanticTags));

        return string.Join(". ", parts);
    }
}
