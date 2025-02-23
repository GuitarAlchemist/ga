namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record NoteDocument : DocumentBase
{
    public required string Name { get; init; }
    public required int MidiNumber { get; init; }
    public required string Category { get; init; } // Natural, Sharp, Flat, etc.
    public required int PitchClass { get; init; }
    public string? Alias { get; init; }

    public NoteDocument() {}
}