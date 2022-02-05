using System.Collections.Immutable;
using GA.Business.Core.Scales;
using GA.Business.Core.Tonal.Primitives;

namespace GA.Business.Core.Tonal.Modes;

/// <summary>
/// A major scale mode.
/// </summary>
/// <remarks>
/// Mnemonic : I Don’t Particularly Like Modes A Lot
/// </remarks>
[PublicAPI]
public sealed class MajorScaleMode : ScaleMode<MajorScaleDegree>
{
    public static MajorScaleMode Ionian => new(1);
    public static MajorScaleMode Dorian => new(2);
    public static MajorScaleMode Phrygian => new(3);
    public static MajorScaleMode Lydian => new(4);
    public static MajorScaleMode Mixolydian => new(5);
    public static MajorScaleMode Aeolian => new(6);
    public static MajorScaleMode Locrian => new(7);

    public static IReadOnlyCollection<MajorScaleMode> All => MajorScaleDegree.All.Select(degree => new MajorScaleMode(degree)).ToImmutableList();

    public MajorScaleMode(MajorScaleDegree degree)
        : base(Scale.Major, degree)
    {
    }

    public override string Name => ScaleDegree.Value switch
    {
        1 => nameof(Ionian),
        2 => nameof(Dorian),
        3 => nameof(Phrygian),
        4 => nameof(Lydian),
        5 => nameof(Mixolydian),
        6 => nameof(Aeolian),
        7 => nameof(Locrian),
        _ => throw new ArgumentOutOfRangeException(nameof(ScaleDegree))
    };

}