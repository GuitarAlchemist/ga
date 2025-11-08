namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record PitchClassDocument : DocumentBase
{
    public required int Value { get; init; }
    public required string Name { get; init; }
    public required List<string> Notes { get; init; } = [];
}
