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

    public virtual void GenerateSearchText() => SearchText = $"{StyleLabel} {Source} {string.Join(" ", Metadata.Values)}";
}
