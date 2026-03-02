namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Core;

/// <summary>
///     Pure domain projection of a chord voicing, suitable for persistence and exchange 
///     without ML/RAG overhead (embeddings, search vectors).
/// </summary>
public record ChordVoicingSnapshot
{
    public required string Id { get; init; }
    public required string ChordName { get; init; }
    public required int[] MidiNotes { get; init; }
    public required string Diagram { get; init; }
    public string? VoicingType { get; init; }
    public string? Description { get; init; }
}
