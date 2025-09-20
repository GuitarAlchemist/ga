namespace GA.Business.Core.Tonal.Modes.Pentatonic;

using Scales;
using Primitives;
using Primitives.Pentatonic;

/// <summary>
/// A Hirajoshi scale mode (Japanese pentatonic scale)
/// </summary>
/// <remarks>
/// The Hirajoshi scale is a traditional Japanese pentatonic scale.
/// It consists of the notes C, Db, F, G, and Ab.
/// It's used in traditional Japanese music and modern compositions.
///
/// <see href="https://en.wikipedia.org/wiki/Hirajoshi_scale"/>
/// </remarks>
[PublicAPI]
public sealed class HirajoshiScaleMode(HirajoshiScaleDegree degree) : TonalScaleMode<HirajoshiScaleDegree>(Scale.JapaneseHirajoshi, degree),
    IStaticEnumerable<HirajoshiScaleMode>
{
    // Static instances for each mode
    public static HirajoshiScaleMode Hirajoshi => new(HirajoshiScaleDegree.Hirajoshi);
    public static HirajoshiScaleMode HirajoshiKumoi => new(HirajoshiScaleDegree.HirajoshiKumoi);
    public static HirajoshiScaleMode HirajoshiHonKumoi => new(HirajoshiScaleDegree.HirajoshiHonKumoi);
    public static HirajoshiScaleMode HirajoshiIwato => new(HirajoshiScaleDegree.HirajoshiIwato);
    public static HirajoshiScaleMode HirajoshiAkebono => new(HirajoshiScaleDegree.HirajoshiAkebono);

    // Collection and access methods
    public static IEnumerable<HirajoshiScaleMode> Items => HirajoshiScaleDegree.Items.Select(degree => new HirajoshiScaleMode(degree));
    public static HirajoshiScaleMode Get(HirajoshiScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static HirajoshiScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<HirajoshiScaleDegree, HirajoshiScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
}
