namespace GA.Data.MongoDB.Models;

[PublicAPI]
public sealed record SetClassDocument : DocumentBase
{
    public required int Cardinality { get; init; }
    public required string IntervalClassVector { get; init; }
    public required int PrimeFormId { get; init; }
    public required bool IsModal { get; init; }
    public string? ModalFamily { get; init; }
}
