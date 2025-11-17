namespace GA.Business.Core.Tonal.Modes.Pentatonic;

using global::GA.Core.Collections;
using Primitives.Pentatonic;
using Scales;

/// <summary>
///     A major pentatonic scale mode
/// </summary>
/// <remarks>
///     Pentatonic scales are five-note scales widely used in various musical traditions.
/// </remarks>
[PublicAPI]
public sealed class MajorPentatonicMode(MajorPentatonicScaleDegree degree) : TonalScaleMode<MajorPentatonicScaleDegree>(
        Scale.MajorPentatonic, degree),
    IStaticEnumerable<MajorPentatonicMode>
{
    private static readonly Lazy<ScaleModeCollection<MajorPentatonicScaleDegree, MajorPentatonicMode>>
        _lazyModeByDegree = new(() => new([.. Items]));

    public static IReadOnlyCollection<MajorPentatonicMode> All => [.. MajorPentatonicScaleDegree.Items.Select(degree => new MajorPentatonicMode(degree))];

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
                yield return new MajorPentatonicMode(degree);
            }
        }
    }

    public override string ToString()
    {
        return $"{Name} - {Formula}";
    }

    public static MajorPentatonicMode Get(MajorPentatonicScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static MajorPentatonicMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}


