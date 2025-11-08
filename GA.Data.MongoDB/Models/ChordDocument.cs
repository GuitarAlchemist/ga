namespace GA.Data.MongoDB.Models;

using References;

[PublicAPI]
public sealed record ChordDocument : DocumentBase
{
    public required string Name { get; init; }
    public required string Root { get; init; }
    public required string Quality { get; init; }
    public required List<string> Intervals { get; init; }
    public required List<string> Notes { get; init; }
    public List<ScaleReference> RelatedScales { get; init; } = [];
    public List<ProgressionReference> CommonProgressions { get; init; } = [];
}
