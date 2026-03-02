namespace GA.Data.MongoDB.Models;

using References;
using Rag;

[PublicAPI]
public sealed record ChordDocument : RagDocumentBase
{
    public required string Name { get; init; }
    public required string Root { get; init; }
    public required string Quality { get; init; }
    public required List<string> Intervals { get; init; }
    public required List<string> Notes { get; init; }
    public List<ScaleReference> RelatedScales { get; init; } = [];
    public List<ProgressionReference> CommonProgressions { get; init; } = [];

    public override string ToEmbeddingString()
    {
        var searchParts = new List<string>
        {
            Name,
            Root,
            Quality,
            string.Join(" ", Intervals),
            string.Join(" ", Notes),
            string.Join(" ", RelatedScales.Select(s => s.Name)),
            string.Join(" ", CommonProgressions.Select(p => p.Name))
        };

        return string.Join(" ", searchParts.Where(s => !string.IsNullOrEmpty(s)));
    }
}
