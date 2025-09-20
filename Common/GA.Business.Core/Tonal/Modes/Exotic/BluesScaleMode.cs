namespace GA.Business.Core.Tonal.Modes.Exotic;

using Scales;
using Primitives;
using Primitives.Exotic;

/// <summary>
/// A blues scale mode
/// </summary>
/// <remarks>
/// The blues scale is a six-note scale commonly used in blues, jazz, and rock music.
/// It consists of the root, flat third, fourth, flat fifth, fifth, and flat seventh.
///
/// <see href="https://en.wikipedia.org/wiki/Blues_scale"/>
/// </remarks>
[PublicAPI]
public sealed class BluesScaleMode(BluesScaleDegree degree) : TonalScaleMode<BluesScaleDegree>(Scale.Blues, degree),
    IStaticEnumerable<BluesScaleMode>
{
    // Static instances for each mode
    public static BluesScaleMode Blues => new(BluesScaleDegree.Blues);
    public static BluesScaleMode MinorBlues => new(BluesScaleDegree.MinorBlues);
    public static BluesScaleMode BluesPhrygian => new(BluesScaleDegree.BluesPhrygian);
    public static BluesScaleMode BluesDorian => new(BluesScaleDegree.BluesDorian);
    public static BluesScaleMode BluesMixolydian => new(BluesScaleDegree.BluesMixolydian);
    public static BluesScaleMode BluesAeolian => new(BluesScaleDegree.BluesAeolian);

    // Collection and access methods
    public static IEnumerable<BluesScaleMode> Items => BluesScaleDegree.Items.Select(degree => new BluesScaleMode(degree));
    public static BluesScaleMode Get(BluesScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static BluesScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<BluesScaleDegree, BluesScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
}
