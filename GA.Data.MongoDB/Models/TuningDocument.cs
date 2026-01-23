namespace GA.Data.MongoDB.Models;

using GA.Domain.Core.Instruments;

[PublicAPI]
public sealed record TuningDocument : DocumentBase
{
    public required string Name { get; init; }
    public List<string> Notes { get; init; } = [];
    public required bool IsStandard { get; set; }
    public string? Description { get; init; }
}
