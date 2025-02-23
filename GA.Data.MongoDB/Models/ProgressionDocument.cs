namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record ProgressionDocument : DocumentBase
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public List<string> Chords { get; init; } = [];
    public List<string> RomanNumerals { get; init; } = [];
    public required string Category { get; init; } // e.g., "Jazz", "Blues", "Pop"
    public string? Description { get; init; }
}
