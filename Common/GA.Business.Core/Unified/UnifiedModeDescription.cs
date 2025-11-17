namespace GA.Business.Core.Unified;

/// <summary>
///     Unified human-facing description.
/// </summary>
public sealed class UnifiedModeDescription
{
    public required string PrimaryName { get; init; }
    public required string Summary { get; init; }
    public string? Symmetry { get; init; }
    public string? IntervalClassVector { get; init; }
    public int Cardinality { get; init; }
    public int RotationIndex { get; init; }
    public string? FamilyInfo { get; init; }
    public string? PrimeForm { get; init; }
    public string? ForteNumber { get; init; }
}
