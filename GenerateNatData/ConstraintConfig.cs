namespace GenerateNatData;

/// <summary>
///     Ergonomic constraints for voicing enumeration.
///     Serialized as a 16-byte header in all output files.
/// </summary>
public sealed record ConstraintConfig
{
    public int MinNotesPlayed { get; init; } = 2;
    public int MaxFretSpan { get; init; } = 5;
    public int FretCount { get; init; } = 24;
    public string TuningId { get; init; } = "EADGBE";

    /// <summary>
    ///     Returns a stable, order-independent hash of the constraint values.
    ///     Same constraints → same hash across runs and machines.
    /// </summary>
    public int GetStableHash() => HashCode.Combine(MinNotesPlayed, MaxFretSpan, FretCount, TuningId);

    public override string ToString() =>
        $"min={MinNotesPlayed} span≤{MaxFretSpan} frets={FretCount} tuning={TuningId}";
}
