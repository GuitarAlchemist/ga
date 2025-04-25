namespace GA.Business.Core.Tonal.Modes.Pentatonic;

using Scales;
using Primitives;
using Primitives.Pentatonic;

/// <summary>
/// A major pentatonic scale mode
/// </summary>
/// <remarks>
/// Pentatonic scales are five-note scales widely used in various musical traditions.
/// </remarks>
[PublicAPI]
public sealed class MajorPentatonicMode(MajorPentatonicScaleDegree degree) : TonalScaleMode<MajorPentatonicScaleDegree>(
    Scale.MajorPentatonic, degree),
    IStaticEnumerable<MajorPentatonicMode>
{
    public static IReadOnlyCollection<MajorPentatonicMode> All => MajorPentatonicScaleDegree.Items.Select(degree => new MajorPentatonicMode(degree)).ToImmutableList();

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => "Major pentatonic",
        2 => "Egyptian",
        3 => "Blues minor",
        4 => "Blues major",
        5 => "Minor pentatonic",
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };

    public override string ToString() => $"{Name} - {Formula}";

    public static IEnumerable<MajorPentatonicMode> Items => MajorPentatonicScaleDegree.Items.Select(degree => new MajorPentatonicMode(degree));
    public static MajorPentatonicMode Get(MajorPentatonicScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static MajorPentatonicMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<MajorPentatonicScaleDegree, MajorPentatonicMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));
}
