namespace GA.Business.Core.Tonal.Modes;

using System.Collections.Immutable;
using Scales;
using Primitives;

[PublicAPI]
public sealed class NaturalMinorMode : MinorScaleMode<NaturalMinorScaleDegree>
{
    public static NaturalMinorMode Aeolian => new(1);
    public static NaturalMinorMode Locrian => new(2);
    public static NaturalMinorMode Ionian => new(3);
    public static NaturalMinorMode Dorian => new(4);
    public static NaturalMinorMode Phrygian => new(5);
    public static NaturalMinorMode Lydian => new(6);
    public static NaturalMinorMode Mixolydian => new(7);

    public static IReadOnlyCollection<NaturalMinorMode> All =>
        NaturalMinorScaleDegree.All.Select(degree => new NaturalMinorMode(degree)).ToImmutableList();

    public NaturalMinorMode(NaturalMinorScaleDegree degree)
        : base(Scale.NaturalMinor, degree)
    {
    }

    public override string Name => ScaleDegree.Value switch
    {
        1 => nameof(Aeolian),
        2 => nameof(Locrian),
        3 => nameof(Ionian),
        4 => nameof(Dorian),
        5 => nameof(Phrygian),
        6 => nameof(Lydian),
        7 => nameof(Mixolydian),
        _ => throw new ArgumentOutOfRangeException(nameof(ScaleDegree))
    };

    public override string ToString() => $"{Name} - {Formula}";
}