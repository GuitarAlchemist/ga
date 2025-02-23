namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record KeyDocument : DocumentBase
{
    public required string Name { get; init; }
    public required string Root { get; init; }
    public required string Mode { get; init; } // Major, Minor
    public required List<string> AccidentedNotes { get; init; }
    public required int NumberOfAccidentals { get; init; }

    public KeyDocument() {}
}