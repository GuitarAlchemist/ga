using System.Collections.Immutable;
using GA.Business.Core.Scales;
using GA.Business.Core.Tonal.Primitives;

namespace GA.Business.Core.Tonal.Modes;

/// <summary>
/// See https://en.wikipedia.org/wiki/Minor_scale
/// </summary>
/// <summary>
/// A mode of the harmonic minor scale.
/// </summary>
[PublicAPI]
public sealed class HarmonicMinorMode : MinorScaleMode<HarmonicMinorScaleDegree>
{
    public static HarmonicMinorMode HarmonicMinorModeMinorScale => new(1);
    public static HarmonicMinorMode LocrianNaturalSixth => new(2);
    public static HarmonicMinorMode IonianAugmented => new(3);
    public static HarmonicMinorMode DorianSharpFourth => new(4);
    public static HarmonicMinorMode PhrygianDominant => new(5);
    public static HarmonicMinorMode LydianSharpSecond => new(6);
    public static HarmonicMinorMode Alteredd7 => new(7);

    public static IReadOnlyCollection<HarmonicMinorMode> All => HarmonicMinorScaleDegree.All.Select(degree => new HarmonicMinorMode(degree)).ToImmutableList();

    public HarmonicMinorMode(HarmonicMinorScaleDegree degree)
        : base(Scale.HarmonicMinor, degree)
    {
    }

    public override string Name => ScaleDegree.Value switch
    {
        1 => "HarmonicMinorMode minor",
        2 => "locrian \u266E6",
        3 => "Ionian augmented",
        4 => "Dorian \u266F4",
        5 => "Phrygian dominant",
        6 => "Lydian \u266F2",
        7 => "Altered bb7",
        _ => throw new ArgumentOutOfRangeException(nameof(ScaleDegree))
    };
}