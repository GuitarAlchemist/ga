namespace GA.Business.Core.Fretboard;

using Config;
using Primitives;

[PublicAPI]
public class Fretboard
{
    public static readonly Fretboard Default = Guitar();
    private readonly Lazy<IReadOnlyCollection<Position>> _lazyAllPositions;
    private readonly Lazy<OpenPositions> _lazyOpenPositions;
    private readonly Lazy<FrettedPositions> _lazyFrettedPositions;

    public static Fretboard Guitar() => new(Tuning.Guitar.Standard, Tuning.Guitar.DefaultFretCount);
    public static Fretboard Bass() => new(Tuning.Bass.Standard, Tuning.Bass.DefaultFretCount);
    public static Fretboard Ukulele() => new(Tuning.Ukulele.Standard, Tuning.Ukulele.DefaultFretCount);
    public static Fretboard Banjo() => new(Tuning.Banjo.Cello, Tuning.Banjo.DefaultFretCount);
    public static Fretboard Mandolin() => new(Tuning.Mandolin.Standard, Tuning.Mandolin.DefaultFretCount);
    public static Fretboard Balalaika() => new(Tuning.Balalaika.Alto, Tuning.Balalaika.DefaultFretCount);

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
    public Fret? CapoFret { get; set; }
    public IReadOnlyCollection<Str> Strings => Str.GetCollection(StringCount);
    public IReadOnlyCollection<Fret> Frets => Fret.Collection(Fret.Min.Value, FretCount);
    public IReadOnlyCollection<Position> Positions => _lazyAllPositions.Value;
    public OpenPositions OpenPositions => _lazyOpenPositions.Value;
    public FrettedPositions FrettedPositions => _lazyFrettedPositions.Value;
    public FretPositions this[Fret fret] => FrettedPositions[fret];

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
            foreach (var fret in Fret.Collection(1, FretCount - 1))
            {
                midiNote++;
                pitch = midiNote.ToSharpPitch();
                var frettedPosition =  new Position.Fretted(str, fret, pitch);
                yield return frettedPosition;
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