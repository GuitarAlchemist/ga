namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record IntervalDocument : DocumentBase
{
    public required string Name { get; init; }
    public required int Semitones { get; init; }
    public required string Quality { get; init; }
    public required int Size { get; init; }
    public bool IsCompound { get; init; }

    public IntervalDocument() {}
}