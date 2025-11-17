namespace GA.Business.Core.Unified;

using Atonal;
using Atonal.Primitives;

/// <summary>
///     Root-agnostic identity for a unified mode class.
///     Combines IntervalClassVector and (optional) prime PitchClassSet id for caching.
/// </summary>
public readonly record struct UnifiedModeId(IntervalClassVectorId IntervalClassVectorId, PitchClassSetId PrimeSetId)
{
    /// <inheritdoc/>
    public override string ToString() => $"ICV:{IntervalClassVectorId.Value}-PCS:{PrimeSetId.Value}";
}
