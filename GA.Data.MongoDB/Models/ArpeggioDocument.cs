namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record ArpeggioDocument : DocumentBase
{
    public required string Name { get; init; }
    public required string Root { get; init; }
    public required List<string> Intervals { get; init; }
    public required List<string> Notes { get; init; }
    public string? Category { get; init; }
    public string? Description { get; init; }

    public ArpeggioDocument() {}
}