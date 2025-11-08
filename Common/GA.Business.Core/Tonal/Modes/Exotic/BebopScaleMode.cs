namespace GA.Business.Core.Tonal.Modes.Exotic;

using Primitives.Exotic;
using Scales;

/// <summary>
///     A bebop scale mode
/// </summary>
/// <remarks>
///     Bebop scales are eight-note scales that add a chromatic passing tone to seven-note scales.
///     They were developed by jazz musicians to create smooth lines in jazz improvisation.
///     There are several types of bebop scales, including dominant, major, dorian, minor, etc.
///     <see href="https://en.wikipedia.org/wiki/Bebop_scale" />
/// </remarks>
[PublicAPI]
public sealed class BebopScaleMode(BebopScaleDegree degree)
    : TonalScaleMode<BebopScaleDegree>(Scale.BebopDominant, degree),
        IStaticEnumerable<BebopScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<BebopScaleDegree, BebopScaleMode>> _lazyModeByDegree =
        new(() => new(Items.ToImmutableList()));

    // Static instances for each mode
    public static BebopScaleMode BebopDominant => new(BebopScaleDegree.BebopDominant);
    public static BebopScaleMode BebopMajor => new(BebopScaleDegree.BebopMajor);
    public static BebopScaleMode BebopDorian => new(BebopScaleDegree.BebopDorian);
    public static BebopScaleMode BebopMinor => new(BebopScaleDegree.BebopMinor);
    public static BebopScaleMode BebopMelodic => new(BebopScaleDegree.BebopMelodic);
    public static BebopScaleMode BebopHarmonic => new(BebopScaleDegree.BebopHarmonic);
    public static BebopScaleMode BebopLocrian => new(BebopScaleDegree.BebopLocrian);
    public static BebopScaleMode BebopDiminished => new(BebopScaleDegree.BebopDiminished);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<BebopScaleMode> Items =>
        BebopScaleDegree.Items.Select(degree => new BebopScaleMode(degree));

    public static BebopScaleMode Get(BebopScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static BebopScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}
