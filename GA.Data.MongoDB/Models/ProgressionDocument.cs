namespace GA.Data.MongoDB.Models;

using Rag;

[PublicAPI]
public sealed record ProgressionDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public List<string> Chords { get; init; } = [];
    public List<string> RomanNumerals { get; init; } = [];
    public required string Category { get; init; } // e.g., "Jazz", "Blues", "Pop"
    public string? Description { get; init; }

    public override void GenerateSearchText()
    {
        var searchParts = new List<string>
        {
            Name,
            Key,
            Category,
            Description ?? string.Empty,
            string.Join(" ", Chords),
            string.Join(" ", RomanNumerals)
        };

        SearchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}
