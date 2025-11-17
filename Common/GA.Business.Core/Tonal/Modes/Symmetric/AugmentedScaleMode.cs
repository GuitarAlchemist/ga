namespace GA.Business.Core.Tonal.Modes.Symmetric;

using global::GA.Core.Collections;

using Primitives.Symmetric;
using Scales;

/// <summary>
///     An augmented scale mode
/// </summary>
/// <remarks>
///     The augmented scale is a symmetrical scale built from alternating minor thirds and half steps.
///     Due to its symmetrical structure, it has only four distinct modes.
///     It's used in jazz and 20th-century classical music.
///     <see href="https://en.wikipedia.org/wiki/Hexatonic_scale#Augmented_scale" />
/// </remarks>
[PublicAPI]
public sealed class AugmentedScaleMode(AugmentedScaleDegree degree)
    : SymmetricScaleMode<AugmentedScaleDegree>(Scale.Augmented, degree),
        IStaticEnumerable<AugmentedScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<AugmentedScaleDegree, AugmentedScaleMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    // Static instances for each mode
    public static AugmentedScaleMode Augmented => new(AugmentedScaleDegree.Augmented);
    public static AugmentedScaleMode AugmentedInversed => new(AugmentedScaleDegree.AugmentedInversed);
    public static AugmentedScaleMode AugmentedDominant => new(AugmentedScaleDegree.AugmentedDominant);
    public static AugmentedScaleMode AugmentedLydian => new(AugmentedScaleDegree.AugmentedLydian);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    /// <summary>
    ///     Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    /// <remarks>
    ///     The augmented scale has only 4 distinct transpositions.
    /// </remarks>
    public override bool HasLimitedTranspositions => true;

    /// <summary>
    ///     Gets the number of distinct transpositions this scale has.
    /// </summary>
    /// <remarks>
    ///     The augmented scale has only 4 distinct transpositions.
    /// </remarks>
    public override int TranspositionCount => 4;

    // Collection and access methods
    public static IEnumerable<AugmentedScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<AugmentedScaleDegree>.Items)
            {
                yield return new AugmentedScaleMode(degree);
            }
        }
    }

    public static AugmentedScaleMode Get(AugmentedScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static AugmentedScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}


