namespace GA.Business.Core.Fretboard;

using Positions;
using Notes;
using Primitives;

/// <summary>
/// Represent available positions for a given instrument/tuning
/// </summary>
[PublicAPI]
public class Fretboard
{
    private static readonly Lazy<Tuning> _lazyDefaultTuning = new(() => new(PitchCollection.Parse("E2 A2 D3 G3 B3 E4")));
    private readonly Lazy<PositionCollection> _lazyPositions;

    public static readonly Fretboard Default = new();
    public static Tuning DefaultTuning => _lazyDefaultTuning.Value;

    public Fretboard() : this(
        DefaultTuning, 24)
    {
    }

    public Fretboard(
        Tuning tuning,
        int fretCount)
    {
        Tuning = tuning;
        FretCount = fretCount;
        StringCount = tuning.PitchCollection.Count;

        _lazyPositions = new(GetPositions);

        PositionCollection GetPositions()
        {
            var list = new List<Position>();
            foreach (var str in Strings)
            {
                // Muted
                list.Add(new Position.Muted(str));

                // Played 
                var midiNote = Tuning[str].MidiNote;
                var frets = Fret.Range(0, FretCount - 1);
                foreach (var fret in frets)
                {
                    var positionLocation = new PositionLocation(str, fret);
                    list.Add(new Position.Played(positionLocation, midiNote++));
                }
            }
            return new(list);
        }
    }

    public Tuning Tuning { get; }
    public int StringCount { get; }
    public int FretCount { get; }
    public Fret? Capo { get; set; }
    public IReadOnlyCollection<Str> Strings => Str.Range(StringCount);
    public IReadOnlyCollection<Fret> Frets => Fret.Range(Fret.Min.Value, FretCount);
    public PositionCollection Positions => _lazyPositions.Value;

    public override string ToString()
    {
        using var memoryStream = new MemoryStream();
        using TextWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8);
        FretboardTextWriterRenderer.Render(this, textWriter);
        var s = Encoding.UTF8.GetString(memoryStream.ToArray());
        memoryStream.Flush();
        return s;
    }
}