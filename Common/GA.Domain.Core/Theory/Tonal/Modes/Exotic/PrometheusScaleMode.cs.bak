namespace GA.Business.Core.Tonal.Modes.Exotic;

using Primitives.Exotic;
using Scales;

/// <summary>
///     A Prometheus scale mode
/// </summary>
/// <remarks>
///     The Prometheus scale was created by Alexander Scriabin and is a hexatonic scale.
///     It consists of C, D, E, F#, A, and Bb.
///     It's used in impressionist music and Scriabin's compositions.
///     <see href="https://en.wikipedia.org/wiki/Mystic_chord" />
/// </remarks>
[PublicAPI]
public sealed class PrometheusScaleMode(PrometheusScaleDegree degree)
    : TonalScaleMode<PrometheusScaleDegree>(Scale.Prometheus, degree),
        IStaticEnumerable<PrometheusScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<PrometheusScaleDegree, PrometheusScaleMode>> _lazyModeByDegree =
        new(() => new(Items.ToImmutableList()));

    // Static instances for each mode
    public static PrometheusScaleMode Prometheus => new(PrometheusScaleDegree.Prometheus);
    public static PrometheusScaleMode PrometheusNeapolitan => new(PrometheusScaleDegree.PrometheusNeapolitan);
    public static PrometheusScaleMode PrometheusPhrygian => new(PrometheusScaleDegree.PrometheusPhrygian);
    public static PrometheusScaleMode PrometheusLydian => new(PrometheusScaleDegree.PrometheusLydian);
    public static PrometheusScaleMode PrometheusMixolydian => new(PrometheusScaleDegree.PrometheusMixolydian);
    public static PrometheusScaleMode PrometheusLocrian => new(PrometheusScaleDegree.PrometheusLocrian);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<PrometheusScaleMode> Items =>
        PrometheusScaleDegree.Items.Select(degree => new PrometheusScaleMode(degree));

    public static PrometheusScaleMode Get(PrometheusScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static PrometheusScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}
