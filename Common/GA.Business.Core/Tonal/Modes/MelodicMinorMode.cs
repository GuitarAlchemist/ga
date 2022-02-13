namespace GA.Business.Core.Tonal.Modes;

using System.Collections.Immutable;

using Scales;
using Primitives;

[PublicAPI]
public sealed class MelodicMinorMode : MinorScaleMode<MelodicMinorScaleDegree>
{
    public static MelodicMinorMode MelodicMinorModeMinor => new(1);
    public static MelodicMinorMode DorianFlatSecond => new(2);
    public static MelodicMinorMode LydianAugmented => new(3);
    public static MelodicMinorMode LydianDominant => new(4);
    public static MelodicMinorMode MixolydianFlatSixth => new(5);
    public static MelodicMinorMode LocrianNaturalSecond => new(6);
    public static MelodicMinorMode Altered => new(7);

    public static IReadOnlyCollection<MelodicMinorMode> All => MelodicMinorScaleDegree.All.Select(degree => new MelodicMinorMode(degree)).ToImmutableList();

    public static MelodicMinorMode Get(HarmonicMinorScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static MelodicMinorMode Get(int degree) => _lazyModeByDegree.Value[degree];


    public MelodicMinorMode(MelodicMinorScaleDegree degree)
        : base(Scale.MelodicMinor, degree)
    {
    }

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => "MelodicMinorMode minor",
        2 => "Dorian \u266D2",
        3 => "Lydian \u266F5",
        4 => "Lydian dominant",
        5 => "Mixolydian \u266D6",
        6 => "Locrian \u266E2",
        7 => "Altered",
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };

    public override string ToString() => $"{Name} - {Formula}";

    private static Lazy<ScaleModeCollection<MelodicMinorScaleDegree, MelodicMinorMode>> _lazyModeByDegree = new(() => new(All));
}