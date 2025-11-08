namespace GA.Business.Core.Tonal.Modes.Diatonic;

using Primitives.Diatonic;
using Scales;

/// <summary>
///     A harmonic major scale mode
/// </summary>
/// <remarks>
///     The harmonic major scale is a major scale with a lowered sixth degree.
///     It has seven distinct modes, each with unique characteristics.
///     <see href="https://en.wikipedia.org/wiki/Harmonic_major_scale" />
/// </remarks>
[PublicAPI]
public sealed class HarmonicMajorScaleMode(HarmonicMajorScaleDegree degree)
    : TonalScaleMode<HarmonicMajorScaleDegree>(Scale.HarmonicMajor, degree),
        IStaticEnumerable<HarmonicMajorScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<HarmonicMajorScaleDegree, HarmonicMajorScaleMode>>
        _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Static instances for each mode
    public static HarmonicMajorScaleMode HarmonicMajor => new(HarmonicMajorScaleDegree.HarmonicMajor);
    public static HarmonicMajorScaleMode DorianFlatFifth => new(HarmonicMajorScaleDegree.DorianFlatFifth);
    public static HarmonicMajorScaleMode PhrygianFlatFourth => new(HarmonicMajorScaleDegree.PhrygianFlatFourth);
    public static HarmonicMajorScaleMode LydianFlatThird => new(HarmonicMajorScaleDegree.LydianFlatThird);
    public static HarmonicMajorScaleMode MixolydianFlatSecond => new(HarmonicMajorScaleDegree.MixolydianFlatSecond);

    public static HarmonicMajorScaleMode LydianAugmentedSharpSecond =>
        new(HarmonicMajorScaleDegree.LydianAugmentedSharpSecond);

    public static HarmonicMajorScaleMode LocrianDoubleFlat7 => new(HarmonicMajorScaleDegree.LocrianDoubleFlat7);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<HarmonicMajorScaleMode> Items =>
        HarmonicMajorScaleDegree.Items.Select(degree => new HarmonicMajorScaleMode(degree));

    public static HarmonicMajorScaleMode Get(HarmonicMajorScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static HarmonicMajorScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}
