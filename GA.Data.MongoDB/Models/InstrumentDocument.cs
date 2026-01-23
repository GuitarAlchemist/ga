namespace GA.Data.MongoDB.Models;

using GA.Domain.Core.Instruments;
using Rag;

[PublicAPI]
public sealed record InstrumentDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required string Category { get; init; } = "Other";
    public required int StringCount { get; init; }
    public List<TuningDocument> Tunings { get; init; } = [];
    public string? Description { get; init; }
    public string? Family { get; init; }
    public string? Range { get; init; }

    public override void GenerateSearchText()
    {
        var searchParts = new List<string>
        {
            Name,
            Category,
            StringCount.ToString(),
            Description ?? string.Empty,
            Family ?? string.Empty,
            Range ?? string.Empty,
            string.Join(" ", Tunings.Select(t => t.Name))
        };

        SearchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}
