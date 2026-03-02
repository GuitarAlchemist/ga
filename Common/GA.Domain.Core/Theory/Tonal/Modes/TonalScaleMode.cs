namespace GA.Domain.Core.Theory.Tonal.Modes;

using Core.Primitives.Notes;
using GA.Core.Abstractions;
using Scales;

/// <summary>
///     Abstract base class for scale modes that have a tonal center.
/// </summary>
/// <typeparam name="TScaleDegree">The type of scale degree.</typeparam>
/// <remarks>
///     Tonal scale modes are derived from a parent scale and have a clear tonal center.
///     Examples include major scale modes (Ionian, Dorian, etc.) and minor scale modes.
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="TonalScaleMode{TScaleDegree}" /> class.
/// </remarks>
/// <param name="parentScale">The parent scale.</param>
/// <param name="degree">The scale degree.</param>
[PublicAPI]
public abstract class TonalScaleMode<TScaleDegree>(Scale parentScale, TScaleDegree degree) : ScaleMode<TScaleDegree>(parentScale, degree), ITonalScaleMode
    where TScaleDegree : IValueObject
{

    /// <summary>
    ///     Gets the tonal center (root note) of the scale mode.
    /// </summary>
    public Note TonalCenter => Notes.First();
}
