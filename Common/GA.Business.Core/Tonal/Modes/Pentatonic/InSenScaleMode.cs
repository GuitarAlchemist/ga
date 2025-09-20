namespace GA.Business.Core.Tonal.Modes.Pentatonic;

using Scales;
using Primitives;
using Primitives.Pentatonic;

/// <summary>
/// An In Sen scale mode (Japanese pentatonic scale)
/// </summary>
/// <remarks>
/// The In Sen scale is a traditional Japanese pentatonic scale.
/// It consists of the notes C, Db, F, G, and Bb.
/// It's used in traditional Japanese music and modern compositions.
///
/// <see href="https://en.wikipedia.org/wiki/In_scale"/>
/// </remarks>
[PublicAPI]
public sealed class InSenScaleMode(InSenScaleDegree degree) : TonalScaleMode<InSenScaleDegree>(Scale.InSen, degree),
    IStaticEnumerable<InSenScaleMode>
{
    // Static instances for each mode
    public static InSenScaleMode InSen => new(InSenScaleDegree.InSen);
    public static InSenScaleMode InSenMode2 => new(InSenScaleDegree.InSenMode2);
    public static InSenScaleMode InSenMode3 => new(InSenScaleDegree.InSenMode3);
    public static InSenScaleMode InSenMode4 => new(InSenScaleDegree.InSenMode4);
    public static InSenScaleMode InSenMode5 => new(InSenScaleDegree.InSenMode5);

    // Collection and access methods
    public static IEnumerable<InSenScaleMode> Items => InSenScaleDegree.Items.Select(degree => new InSenScaleMode(degree));
    public static InSenScaleMode Get(InSenScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static InSenScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<InSenScaleDegree, InSenScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
}
