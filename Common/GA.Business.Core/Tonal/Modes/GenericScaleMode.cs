namespace GA.Business.Core.Tonal.Modes;

using Primitives;
using Scales;

/// <summary>
///     A generic scale mode that can be used for any scale.
/// </summary>
/// <remarks>
///     This class is used when a specific scale mode class is not available.
/// </remarks>
[PublicAPI]
public sealed class GenericScaleMode : TonalScaleMode<GenericScaleDegree>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GenericScaleMode" /> class.
    /// </summary>
    /// <param name="parentScale">The parent scale.</param>
    /// <param name="degree">The scale degree.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is out of range.</exception>
    public GenericScaleMode(Scale parentScale, int degree) : base(parentScale, degree)
    {
        if (degree < 1 || degree > parentScale.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(degree),
                "Degree must be between 1 and the number of notes in the parent scale.");
        }

        Degree = degree;
    }

    /// <summary>
    ///     Gets the degree of this mode in the parent scale.
    /// </summary>
    public int Degree { get; }

    /// <summary>
    ///     Gets the name of this mode.
    /// </summary>
    public override string Name => $"Mode {Degree}";
}
