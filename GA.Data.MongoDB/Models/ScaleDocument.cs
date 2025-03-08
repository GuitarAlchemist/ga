namespace GA.Data.MongoDB.Models;

using Rag;

[PublicAPI]
public sealed record ScaleDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required List<string> Notes { get; init; }
    public required List<string> Intervals { get; init; }
    public required string IntervalClassVector { get; init; }
    public required bool IsModal { get; init; }
    public string? ModalFamily { get; init; }
    public required bool IsNormalForm { get; init; }
    public required bool IsClusterFree { get; init; }
    public string? ScaleVideoUrl { get; init; }
    public required string ScalePageUrl { get; init; }
    public List<string>? Modes { get; init; }
    public string? Description { get; init; }
    public string? Usage { get; init; }
    public List<string> Tags { get; init; } = [];

    public ScaleDocument() {}

    public override void GenerateSearchText()
    {
        var searchParts = new List<string>
        {
            Name,
            string.Join(" ", Notes),
            string.Join(" ", Intervals),
            IntervalClassVector,
            ModalFamily ?? string.Empty,
            string.Join(" ", Modes ?? []),
            Description ?? string.Empty,
            Usage ?? string.Empty,
            string.Join(" ", Tags)
        };

        SearchText = string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}