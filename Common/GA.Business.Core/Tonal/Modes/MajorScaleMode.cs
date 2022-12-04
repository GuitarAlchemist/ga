namespace GA.Business.Core.Tonal.Modes;

using GA.Core.Collections;
using Scales;
using Primitives;

/// <summary>
/// A major scale mode.
/// </summary>
/// <remarks>
/// Mnemonic : I Don’t Particularly Like Modes A Lot
/// </remarks>
[PublicAPI]
public sealed class MajorScaleMode : ScaleMode<MajorScaleDegree>, 
                                     IStaticEnumerable<MajorScaleMode>
{
    public static MajorScaleMode Ionian => new(MajorScaleDegree.Ionian);

    /// <summary>
    /// https://ianring.com/musictheory/scales/1709
    /// </summary>
    public static MajorScaleMode Dorian => new(MajorScaleDegree.Dorian);
    public static MajorScaleMode Phrygian => new(MajorScaleDegree.Phrygian);
    public static MajorScaleMode Lydian => new(MajorScaleDegree.Lydian);
    public static MajorScaleMode Mixolydian => new(MajorScaleDegree.Mixolydian);
    public static MajorScaleMode Aeolian => new(MajorScaleDegree.Aeolian);
    public static MajorScaleMode Locrian => new(MajorScaleDegree.Locrian);

    public static IEnumerable<MajorScaleMode> Items => MajorScaleDegree.Items.Select(degree => new MajorScaleMode(degree));
    public static MajorScaleMode Get(MajorScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static MajorScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<MajorScaleDegree, MajorScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    public MajorScaleMode(MajorScaleDegree degree)
        : base(Scale.Major, degree)
    {
    }

    public override string Name => ParentScaleDegree.Value switch
    {
        MajorScaleDegree.DegreeValue.IonianValue => nameof(Ionian),
        MajorScaleDegree.DegreeValue.DorianValue => nameof(Dorian),
        MajorScaleDegree.DegreeValue.PhrygianValue => nameof(Phrygian),
        MajorScaleDegree.DegreeValue.LydianValue => nameof(Lydian),
        MajorScaleDegree.DegreeValue.MixolydianValue => nameof(Mixolydian),
        MajorScaleDegree.DegreeValue.AeolianValue => nameof(Aeolian),
        MajorScaleDegree.DegreeValue.LocrianValue => nameof(Locrian),
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };


}