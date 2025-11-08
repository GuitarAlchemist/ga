namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record InstrumentDocument : DocumentBase
{
    public required string Name { get; init; }
    public required string Category { get; init; } = "Other";
    public required int StringCount { get; init; }
    public List<TuningDocument> Tunings { get; init; } = [];
    public string? Description { get; init; }
    public string? Family { get; init; }
    public string? Range { get; init; }
}
