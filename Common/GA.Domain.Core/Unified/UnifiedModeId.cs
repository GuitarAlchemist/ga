namespace GA.Domain.Core.Unified;

using Theory.Atonal;

/// <summary>
///     Root-agnostic identity for a unified mode class.
///     Combines IntervalClassVector and (optional) prime PitchClassSet id for caching.
/// </summary>
public readonly record struct UnifiedModeId(IntervalClassVectorId IntervalClassVectorId, PitchClassSetId PrimeSetId)
{
    /// <inheritdoc/>
    public override string ToString() => $"ICV:{IntervalClassVectorId.Value}-PCS:{PrimeSetId.Value}";
}
