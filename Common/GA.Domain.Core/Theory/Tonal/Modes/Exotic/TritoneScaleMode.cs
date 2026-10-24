namespace GA.Domain.Core.Theory.Tonal.Modes.Exotic;

using GA.Core.Collections.Abstractions;
using Primitives.Exotic;
using Scales;

/// <summary>
///     A Tritone scale mode (Petrushka scale)
/// </summary>
/// <remarks>
///     The Tritone scale joins two major triads a tritone apart, C E G and Gb Bb Db (the Petrushka chord).
///     It consists of the notes C, Db, E, Gb, G, Bb.
///     It's used in jazz and film scoring, and was notably used by Stravinsky in his ballet "Petrushka".
///     Its steps, a half step, a minor third and a whole step, repeat a tritone higher: it has three distinct
///     modes and six transpositions.
///     <see href="https://en.wikipedia.org/wiki/Tritone_scale" />
/// </remarks>
[PublicAPI]
public sealed class TritoneScaleMode(TritoneScaleDegree degree)
    : TonalScaleMode<TritoneScaleDegree>(Scale.Tritone, degree),
        IStaticEnumerable<TritoneScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<TritoneScaleDegree, TritoneScaleMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    // Static instances for each mode
    public static TritoneScaleMode Tritone => new(TritoneScaleDegree.Tritone);
    public static TritoneScaleMode Petrushka => new(TritoneScaleDegree.Petrushka);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<TritoneScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<TritoneScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static TritoneScaleMode Get(TritoneScaleDegree degree) => _lazyModeByDegree.Value[degree];

    public static TritoneScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
}
