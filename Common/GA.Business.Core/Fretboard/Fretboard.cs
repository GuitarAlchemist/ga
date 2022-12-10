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
    private readonly Lazy<RelativeFretVectorCollection> _lazyRelativePositions;

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
        _lazyRelativePositions = new(new RelativeFretVectorCollection(StringCount));
    }

    public Tuning Tuning { get; }
    public int StringCount { get; }
    public int FretCount { get; }
    public Fret? Capo { get; set; }

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{Str}"/>
    /// </summary>
    public IReadOnlyCollection<Str> Strings => Str.Range(StringCount);

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{Fret}"/>
    /// </summary>
    public IReadOnlyCollection<Fret> Frets => Fret.Range(Fret.Min.Value, FretCount);

    /// <summary>
    /// Gets the <see cref="PositionCollection"/>
    /// </summary>
    public PositionCollection Positions => _lazyPositions.Value;

    /// <summary>
    /// Gets the <see cref="RelativeFretVectorCollection"/>
    /// </summary>
    public RelativeFretVectorCollection RelativePositions => _lazyRelativePositions.Value;

    public override string ToString()
    {
        using var memoryStream = new MemoryStream();
        using TextWriter textWriter = new StreamWriter(memoryStream, Encoding.UTF8);
        FretboardTextWriterRenderer.Render(this, textWriter);
        var s = Encoding.UTF8.GetString(memoryStream.ToArray());
        memoryStream.Flush();
        return s;
    }

    private PositionCollection GetPositions()
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