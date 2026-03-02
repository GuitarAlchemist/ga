namespace GA.Business.ML.Rag.Models;

using System.Collections.Generic;

/// <summary>
///     Represents a labeled harmonic progression in the domain corpus.
/// </summary>
public record ProgressionRagDocument : RagDocumentBase
{
    public string StyleLabel { get; set; } = string.Empty; // "Jazz", "Rock", "Blues", etc.
    // Chords are now RagDocuments too
    public List<ChordVoicingRagDocument> Chords { get; set; } = [];
    public string Source { get; set; } = string.Empty;
    // Metadata inherited from DocumentBase? No, DocumentBase has Metadata.

    /// <inheritdoc />
    /// <remarks>
    ///     Follows rag-engineer semantic chunking principles:
    ///     - Style label and source are labelled explicitly for better embedding recall.
    ///     - Chord names are extracted from voicing documents and listed.
    ///     - Metadata key/value pairs are rendered as "key: value" to avoid
    ///       the dictionary ToString() producing garbage tokens.
    ///     - Empty values are skipped to avoid polluting the embedding vector.
    /// </remarks>
    public override string ToEmbeddingString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(StyleLabel))
            parts.Add($"Style: {StyleLabel}");

        if (!string.IsNullOrWhiteSpace(Source))
            parts.Add($"Source: {Source}");

        var chordNames = Chords
            .Select(c => c.ChordName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (chordNames.Count > 0)
            parts.Add($"Chords: {string.Join(", ", chordNames)}");

        // Render metadata as "key: value" pairs — avoids the raw dictionary noise
        var metaParts = Metadata
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{kv.Key}: {kv.Value}");

        parts.AddRange(metaParts);

        return parts.Count > 0
            ? string.Join(". ", parts)
            : StyleLabel; // fallback — always return something non-empty
    }
}
