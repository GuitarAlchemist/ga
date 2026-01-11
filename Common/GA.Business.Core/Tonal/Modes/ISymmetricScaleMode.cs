namespace GA.Business.Core.Tonal.Modes;

using JetBrains.Annotations;

/// <summary>
///     Interface for symmetric scale modes that don't have a tonal center.
/// </summary>
/// <remarks>
///     Symmetric scales have repeating interval patterns that create tonal ambiguity.
///     Examples include whole-tone scales, octatonic scales, and Messiaen's modes of limited transposition.
/// </remarks>
[PublicAPI]
public interface ISymmetricScaleMode
{
    /// <summary>
    ///     Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    /// <remarks>
    ///     A scale has limited transpositions when it maps onto itself before all 12 transpositions are exhausted.
    /// </remarks>
    bool HasLimitedTranspositions { get; }

    /// <summary>
    ///     Gets the number of distinct transpositions this scale has.
    /// </summary>
    /// <remarks>
    ///     For example, the whole-tone scale has only 2 distinct transpositions.
    /// </remarks>
    int TranspositionCount { get; }
}
