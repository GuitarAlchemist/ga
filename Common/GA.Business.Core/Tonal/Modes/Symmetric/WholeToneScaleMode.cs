namespace GA.Business.Core.Tonal.Modes.Symmetric;

using global::GA.Core.Collections;
using Primitives.Symmetric;
using Scales;

/// <summary>
///     A whole tone scale mode
/// </summary>
/// <remarks>
///     The whole tone scale is a hexatonic scale consisting of whole tones.
///     Due to its symmetrical structure, it has only two distinct modes.
///     <see href="https://en.wikipedia.org/wiki/Whole-tone_scale" />
/// </remarks>
[PublicAPI]
public sealed class WholeToneScaleMode(WholeToneScaleDegree degree)
    : SymmetricScaleMode<WholeToneScaleDegree>(Scale.WholeTone, degree),
        IStaticEnumerable<WholeToneScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<WholeToneScaleDegree, WholeToneScaleMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    // Static instances for each mode
    public static WholeToneScaleMode WholeTone => new(WholeToneScaleDegree.WholeTone);
    public static WholeToneScaleMode WholeTone2 => new(WholeToneScaleDegree.WholeTone2);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    /// <summary>
    ///     Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    /// <remarks>
    ///     The whole-tone scale has only 2 distinct transpositions.
    /// </remarks>
    public override bool HasLimitedTranspositions => true;

    /// <summary>
    ///     Gets the number of distinct transpositions this scale has.
    /// </summary>
    /// <remarks>
    ///     The whole-tone scale has only 2 distinct transpositions.
    /// </remarks>
    public override int TranspositionCount => 2;

    // Collection and access methods
    public static IEnumerable<WholeToneScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<WholeToneScaleDegree>.Items)
            {
                yield return new WholeToneScaleMode(degree);
            }
        }
    }

    public static WholeToneScaleMode Get(WholeToneScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static WholeToneScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}


