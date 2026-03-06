namespace GA.Domain.Core.Theory.Tonal.Modes.Pentatonic;

using GA.Core.Collections.Abstractions;
using Primitives.Pentatonic;
using Scales;

/// <summary>
///     A major pentatonic scale mode
/// </summary>
/// <remarks>
///     Pentatonic scales are five-note scales widely used in various musical traditions.
///     <see href="https://en.wikipedia.org/wiki/Pentatonic_scale" /><br/>
///     <see href="https://ianring.com/musictheory/scales/1193" />
/// </remarks>
[PublicAPI]
public sealed class MajorPentatonicMode(MajorPentatonicScaleDegree degree) : TonalScaleMode<MajorPentatonicScaleDegree>(
        Scale.MajorPentatonic, degree),
    IStaticEnumerable<MajorPentatonicMode>
{
    private static readonly Lazy<ScaleModeCollection<MajorPentatonicScaleDegree, MajorPentatonicMode>>
        _lazyModeByDegree =
            new(() => new([.. Items]));

    public static IReadOnlyCollection<MajorPentatonicMode> All =>
    [
        .. MajorPentatonicScaleDegree.Items.Select(degree => new MajorPentatonicMode(degree))
    ];

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => "Major pentatonic",
        2 => "Egyptian",
        3 => "Blues minor",
        4 => "Blues major",
        5 => "Minor pentatonic",
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };

    public static IEnumerable<MajorPentatonicMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<MajorPentatonicScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public override string ToString() => $"{Name} - {Formula}";

    public static MajorPentatonicMode Get(MajorPentatonicScaleDegree degree) => _lazyModeByDegree.Value[degree];

    public static MajorPentatonicMode Get(int degree) => _lazyModeByDegree.Value[degree];
}
