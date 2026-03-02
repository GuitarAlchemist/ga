namespace GA.Domain.Core.Theory.Tonal.Modes;

using GA.Core.Abstractions;
using Scales;

/// <summary>
///     Abstract base class for symmetric scale modes that don't have a tonal center.
/// </summary>
/// <typeparam name="TScaleDegree">The type of scale degree.</typeparam>
/// <remarks>
///     Symmetric scale modes have repeating interval patterns that create tonal ambiguity.
///     Examples include whole-tone scales, octatonic scales, and Messiaen's modes of limited transposition.
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="SymmetricScaleMode{TScaleDegree}" /> class.
/// </remarks>
/// <param name="parentScale">The parent scale.</param>
/// <param name="degree">The scale degree.</param>
[PublicAPI]
public abstract class SymmetricScaleMode<TScaleDegree>(Scale parentScale, TScaleDegree degree) : ScaleMode<TScaleDegree>(parentScale, degree), ISymmetricScaleMode
    where TScaleDegree : IValueObject
{

    /// <summary>
    ///     Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    /// <remarks>
    ///     A scale has limited transpositions when it maps onto itself before all 12 transpositions are exhausted.
    /// </remarks>
    public abstract bool HasLimitedTranspositions { get; }

    /// <summary>
    ///     Gets the number of distinct transpositions this scale has.
    /// </summary>
    /// <remarks>
    ///     For example, the whole-tone scale has only 2 distinct transpositions.
    /// </remarks>
    public abstract int TranspositionCount { get; }
}
