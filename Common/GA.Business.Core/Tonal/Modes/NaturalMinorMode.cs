namespace GA.Business.Core.Tonal.Modes;

using Scales;
using Primitives;

[PublicAPI]
public sealed class NaturalMinorMode(NaturalMinorScaleDegree degree) : MinorScaleMode<NaturalMinorScaleDegree>(
        Scale.NaturalMinor, degree),
    IStaticEnumerable<NaturalMinorMode>
{
    public static NaturalMinorMode Aeolian => new(1);
    public static NaturalMinorMode Locrian => new(2);
    public static NaturalMinorMode Ionian => new(3);
    public static NaturalMinorMode Dorian => new(4);
    public static NaturalMinorMode Phrygian => new(5);
    public static NaturalMinorMode Lydian => new(6);
    public static NaturalMinorMode Mixolydian => new(7);

    
    public static IEnumerable<NaturalMinorMode> Items => NaturalMinorScaleDegree.Items.Select(degree => new NaturalMinorMode(degree));
    public static NaturalMinorMode Get(NaturalMinorScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static NaturalMinorMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<NaturalMinorScaleDegree, NaturalMinorMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => nameof(Aeolian),
        2 => nameof(Locrian),
        3 => nameof(Ionian),
        4 => nameof(Dorian),
        5 => nameof(Phrygian),
        6 => nameof(Lydian),
        7 => nameof(Mixolydian),
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };

    public override string ToString() => $"{Name} - {Formula}";
}