namespace GA.Business.Core.Tonal.Modes.Diatonic;

using Scales;
using Primitives;
using Primitives.Diatonic;

/// <summary>
/// A major scale mode
/// </summary>
/// <remarks>
/// Mnemonic: I Don't Particularly Like Modes A Lot => Ionian, Dorian, etc...
///
/// <see href="https://ianring.com/musictheory/scales/1709"/>
/// </remarks>
[PublicAPI]
public sealed class MajorScaleMode(MajorScaleDegree degree) : TonalScaleMode<MajorScaleDegree>(Scale.Major, degree),
    IStaticEnumerable<MajorScaleMode>
{
    public static MajorScaleMode FromDegree(MajorScaleDegree degree) => new(degree);
    
    // Collection and access methods
    public static IEnumerable<MajorScaleMode> Items => MajorScaleDegree.Items.Select(degree => new MajorScaleMode(degree));
    public static MajorScaleMode Get(MajorScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static MajorScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<MajorScaleDegree, MajorScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    public override string ToString() => $"{Name} - {Formula}";
}
