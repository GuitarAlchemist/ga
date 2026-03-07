namespace GA.Domain.Core.Theory.Tonal.Modes.Diatonic;

using GA.Core.Collections.Abstractions;
using Primitives.Diatonic;
using Scales;

/// <summary>
///     A major scale mode
/// </summary>
/// <remarks>
///     Mnemonic: I Don't Particularly Like Modes A Lot => Ionian, Dorian, etc...<br/>
///     <see href="https://en.wikipedia.org/wiki/Major_scale" /><br/>
///     <see href="https://ianring.com/musictheory/scales/1709" />
/// </remarks>
[PublicAPI]
public sealed class MajorScaleMode(MajorScaleDegree degree) : TonalScaleMode<MajorScaleDegree>(Scale.Major, degree),
    IStaticEnumerable<MajorScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<MajorScaleDegree, MajorScaleMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<MajorScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<MajorScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static MajorScaleMode FromDegree(MajorScaleDegree degree) => new(degree);

    public static MajorScaleMode Get(MajorScaleDegree degree) => _lazyModeByDegree.Value[degree];

    public static MajorScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];

    public override string ToString() => $"{Name} - {Formula}";
}
