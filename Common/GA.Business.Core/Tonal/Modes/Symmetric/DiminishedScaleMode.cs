namespace GA.Business.Core.Tonal.Modes.Symmetric;

using Primitives.Symmetric;
using Scales;

/// <summary>
///     A diminished scale mode (octatonic scale)
/// </summary>
/// <remarks>
///     The diminished scale is an eight-note scale alternating whole and half steps.
///     Due to its symmetrical structure, it has only two distinct modes: half-whole and whole-half.
///     It's commonly used in jazz for improvisation over diminished and dominant chords.
///     <see href="https://en.wikipedia.org/wiki/Octatonic_scale" />
/// </remarks>
[PublicAPI]
public sealed class DiminishedScaleMode(DiminishedScaleDegree degree)
    : SymmetricScaleMode<DiminishedScaleDegree>(Scale.Diminished, degree),
        IStaticEnumerable<DiminishedScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<DiminishedScaleDegree, DiminishedScaleMode>> _lazyModeByDegree =
        new(() => new(Items.ToImmutableList()));

    // Static instances for each mode
    public static DiminishedScaleMode HalfWhole => new(DiminishedScaleDegree.HalfWhole);
    public static DiminishedScaleMode WholeHalf => new(DiminishedScaleDegree.WholeHalf);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    /// <summary>
    ///     Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    /// <remarks>
    ///     The diminished scale has only 3 distinct transpositions.
    /// </remarks>
    public override bool HasLimitedTranspositions => true;

    /// <summary>
    ///     Gets the number of distinct transpositions this scale has.
    /// </summary>
    /// <remarks>
    ///     The diminished scale has only 3 distinct transpositions.
    /// </remarks>
    public override int TranspositionCount => 3;

    // Collection and access methods
    public static IEnumerable<DiminishedScaleMode> Items =>
        DiminishedScaleDegree.Items.Select(degree => new DiminishedScaleMode(degree));

    public static DiminishedScaleMode Get(DiminishedScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static DiminishedScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}
