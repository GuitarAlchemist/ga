namespace GA.Domain.Core.Theory.Tonal.Modes.Diatonic;

using GA.Core.Collections.Abstractions;
using Primitives.Diatonic;
using Scales;

/// <summary>
///     A mode of the harmonic minor scale (<see href="https://en.wikipedia.org/wiki/Harmonic_minor_scale" />).
/// </summary>
/// <remarks>
///     <see href="https://ianring.com/musictheory/scales/2477" />
/// </remarks>
[PublicAPI]
public sealed class HarmonicMinorMode(HarmonicMinorScaleDegree degree) : MinorScaleMode<HarmonicMinorScaleDegree>(
        Scale.HarmonicMinor, degree),
    IStaticEnumerable<HarmonicMinorMode>
{
    private static readonly Lazy<ScaleModeCollection<HarmonicMinorScaleDegree, HarmonicMinorMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    public static HarmonicMinorMode HarmonicMinorModeMinorScale => new(1);
    public static HarmonicMinorMode LocrianNaturalSixth => new(2);
    public static HarmonicMinorMode IonianAugmented => new(3);
    public static HarmonicMinorMode DorianSharpFourth => new(4);
    public static HarmonicMinorMode PhrygianDominant => new(5);
    public static HarmonicMinorMode LydianSharpSecond => new(6);
    public static HarmonicMinorMode Alteredd7 => new(7);

    public override string Name => ParentScaleDegree.ToName();

    public static IEnumerable<HarmonicMinorMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<HarmonicMinorScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static HarmonicMinorMode Get(HarmonicMinorScaleDegree degree) => _lazyModeByDegree.Value[degree];

    public static HarmonicMinorMode Get(int degree) => _lazyModeByDegree.Value[degree];
}
