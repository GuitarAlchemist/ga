namespace GA.Business.Core.Tonal.Modes.Symmetric;

using Primitives;
using Scales;

/// <summary>
/// A generic symmetric scale mode that can be used for any symmetric scale.
/// </summary>
/// <remarks>
/// This class is used when a specific symmetric scale mode class is not available.
/// </remarks>
[PublicAPI]
public sealed class GenericSymmetricScaleMode : SymmetricScaleMode<GenericScaleDegree>
{
    private readonly bool _hasLimitedTranspositions;
    private readonly int _transpositionCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericSymmetricScaleMode"/> class.
    /// </summary>
    /// <param name="parentScale">The parent scale.</param>
    /// <param name="degree">The scale degree.</param>
    /// <param name="hasLimitedTranspositions">Whether the scale has limited transpositions.</param>
    /// <param name="transpositionCount">The number of distinct transpositions.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the degree is out of range.</exception>
    public GenericSymmetricScaleMode(
        Scale parentScale, 
        int degree, 
        bool hasLimitedTranspositions = true, 
        int transpositionCount = 12) : base(parentScale, degree)
    {
        if (degree < 1 || degree > parentScale.Count)
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be between 1 and the number of notes in the parent scale.");

        Degree = degree;
        _hasLimitedTranspositions = hasLimitedTranspositions;
        _transpositionCount = transpositionCount;
    }

    /// <summary>
    /// Gets the degree of this mode in the parent scale.
    /// </summary>
    public int Degree { get; }

    /// <summary>
    /// Gets the name of this mode.
    /// </summary>
    public override string Name => $"Symmetric Mode {Degree}";

    /// <summary>
    /// Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    public override bool HasLimitedTranspositions => _hasLimitedTranspositions;

    /// <summary>
    /// Gets the number of distinct transpositions this scale has.
    /// </summary>
    public override int TranspositionCount => _transpositionCount;
}
