namespace GA.Business.Core.Tonal.Modes;

using System.Linq;
using GA.Core.Abstractions;
using JetBrains.Annotations;
using Notes;
using Scales;

/// <summary>
///     Abstract base class for scale modes that have a tonal center.
/// </summary>
/// <typeparam name="TScaleDegree">The type of scale degree.</typeparam>
/// <remarks>
///     Tonal scale modes are derived from a parent scale and have a clear tonal center.
///     Examples include major scale modes (Ionian, Dorian, etc.) and minor scale modes.
/// </remarks>
[PublicAPI]
public abstract class TonalScaleMode<TScaleDegree> : ScaleMode<TScaleDegree>, ITonalScaleMode
    where TScaleDegree : IValueObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TonalScaleMode{TScaleDegree}" /> class.
    /// </summary>
    /// <param name="parentScale">The parent scale.</param>
    /// <param name="degree">The scale degree.</param>
    protected TonalScaleMode(Scale parentScale, TScaleDegree degree)
        : base(parentScale, degree)
    {
    }

    /// <summary>
    ///     Gets the tonal center (root note) of the scale mode.
    /// </summary>
    public Note TonalCenter => Notes.First();
}
