using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Config;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

[PublicAPI]
public class Fretboard
{
    public static readonly Fretboard Default = Guitar();
    private readonly Lazy<IReadOnlyCollection<Position>> _lazyAllPositions;
    private readonly Lazy<IReadOnlyCollection<Position.Open>> _lazyOpenPositions;

    public static Fretboard Guitar() => new(Tuning.Guitar.Default);
    public static Fretboard Ukulele() => new(Tuning.Ukulele.Default, 15);

    public Fretboard(
        Tuning tuning,
        int fretCount = Fret.DefaultCount)
    {
        Tuning = tuning;
        FretCount = fretCount;
        StringCount = tuning.Pitches.Count;

        _lazyAllPositions = new(GetAllPositions);
        _lazyOpenPositions = new(GetOpenPositions);
    }

    public Tuning Tuning { get; }
    public int StringCount { get; }
    public int FretCount { get; }
    public IReadOnlyCollection<Str> Strings => Str.GetCollection(StringCount);
    public IReadOnlyCollection<Fret> Frets => Fret.GetCollection(Fret.Min.Value, FretCount);
    public IReadOnlyCollection<Position> Positions => _lazyAllPositions.Value;
    public IReadOnlyCollection<Position.Open> OpenPositions => _lazyOpenPositions.Value;

    private IReadOnlyCollection<Position> GetAllPositions()
    {
        IEnumerable<Position> StringPositions(Str str)
        {
            // Muted
            yield return new Position.Muted(str);

            // Fretted (Open)
            var midiNote = Tuning[str].GetMidiNote();
            yield return new Position.Open(str, midiNote++);

            // Fretted
            foreach (var fret in Fret.GetCollection(1, FretCount - 1))
                yield return new Position.Fretted(str, fret, midiNote++);
        }

        IEnumerable<Position> AllPositions()
        {
            foreach (var str in Str.GetCollection(StringCount))
            foreach (var position in StringPositions(str))
                yield return position;
        }

        return AllPositions().ToImmutableList();

    }

    private IReadOnlyCollection<Position.Open> GetOpenPositions()
    {
        return
            Str.GetCollection(StringCount)
               .Select(str => new Position.Open(str, Tuning[str].GetMidiNote()))
               .ToImmutableList();
    }
}