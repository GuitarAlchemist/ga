namespace GA.Business.Core.Tonal.Modes.Exotic;

using Scales;
using Primitives;
using Primitives.Exotic;

/// <summary>
/// A Tritone scale mode (Petrushka scale)
/// </summary>
/// <remarks>
/// The Tritone scale is a symmetrical scale built from alternating half steps and minor thirds.
/// It consists of the notes C, Db, E, F#, G, A.
/// It's used in jazz and film scoring, and was notably used by Stravinsky in his ballet "Petrushka".
/// Due to its symmetrical structure, it has only two distinct modes.
///
/// <see href="https://en.wikipedia.org/wiki/Tritone_scale"/>
/// </remarks>
[PublicAPI]
public sealed class TritoneScaleMode(TritoneScaleDegree degree) : TonalScaleMode<TritoneScaleDegree>(Scale.Tritone, degree),
    IStaticEnumerable<TritoneScaleMode>
{
    // Static instances for each mode
    public static TritoneScaleMode Tritone => new(TritoneScaleDegree.Tritone);
    public static TritoneScaleMode Petrushka => new(TritoneScaleDegree.Petrushka);

    // Collection and access methods
    public static IEnumerable<TritoneScaleMode> Items => TritoneScaleDegree.Items.Select(degree => new TritoneScaleMode(degree));
    public static TritoneScaleMode Get(TritoneScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static TritoneScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<TritoneScaleDegree, TritoneScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
}
