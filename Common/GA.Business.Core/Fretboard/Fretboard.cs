using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Config;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

[PublicAPI]
public class Fretboard
{
    public static readonly Fretboard Default = Guitar();
    private readonly Lazy<IReadOnlyCollection<Position>> _lazyAllPositions;
    private readonly Lazy<OpenPositions> _lazyOpenPositions;
    private readonly Lazy<FrettedPositions> _lazyFrettedPositions;

    public static Fretboard Guitar() => new(Tuning.Guitar.Standard, 22);
    public static Fretboard Ukulele() => new(Tuning.Ukulele.Standard, 15);

    public Fretboard(
        Tuning tuning,
        int fretCount)
    {
        Tuning = tuning;
        FretCount = fretCount;
        StringCount = tuning.Pitches.Count;

        _lazyAllPositions = new(GetAllPositions);
        _lazyOpenPositions = new(GetOpenPositions);
        _lazyFrettedPositions = new(GetFrettedPositions);
    }

    public Tuning Tuning { get; }
    public int StringCount { get; }
    public int FretCount { get; }
    public Fret CapoFret { get; } // TODO
    public IReadOnlyCollection<Str> Strings => Str.GetCollection(StringCount);
    public IReadOnlyCollection<Fret> Frets => Fret.GetCollection(Fret.Min.Value, FretCount);
    public IReadOnlyCollection<Position> Positions => _lazyAllPositions.Value;
    public OpenPositions OpenPositions => _lazyOpenPositions.Value;
    public FrettedPositions FrettedPositions => _lazyFrettedPositions.Value;
    public FretPositions this[Fret fret] => new(_lazyFrettedPositions.Value);

    private IReadOnlyCollection<Position> GetAllPositions()
    {
        IEnumerable<Position> StringPositions(Str str)
        {
            // Muted
            yield return new Position.Muted(str);

            // Fretted (Open)
            var pitch = Tuning[str];
            var openPosition = new Position.Open(str, pitch);
            yield return openPosition;

            // Fretted
            var midiNote = pitch.MidiNote;
            foreach (var fret in Fret.GetCollection(1, FretCount - 1))
            {
                var frettedPosition =  new Position.Fretted(str, fret, pitch);
                yield return frettedPosition;

                midiNote++;
                pitch = midiNote.Pitch;
            }
        }

        IEnumerable<Position> AllPositions()
        {
            foreach (var str in Str.GetCollection(StringCount))
            foreach (var position in StringPositions(str))
                yield return position;
        }

        var result = AllPositions().ToImmutableList();

        return result;
    }

    private OpenPositions GetOpenPositions()
    {
        var positions =
            Str.GetCollection(StringCount)
               .Select(str =>
               {
                   var pitch = Tuning[str];
                   var item = new Position.Open(str, pitch);

                   return item;
               })
               .ToImmutableList();

        var result = new OpenPositions(positions);

        return result;
    }

    private FrettedPositions GetFrettedPositions()
    {
        var positions = 
            Positions
                .Where(position => position is Position.Fretted)
                .Cast<Position.Fretted>()
                .ToImmutableList();

        return new(positions);
    }
}