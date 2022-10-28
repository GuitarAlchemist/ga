namespace GA.Business.Core.Fretboard;

using GA.Core;
using Config;
using Positions;
using Notes;
using Primitives;

/// <summary>
/// Represent available positions for a given instrument/tuning
/// </summary>
[PublicAPI]
public class Fretboard
{
    public static readonly Fretboard Default = new();

    private static readonly string _defaultTuning = Instruments.Instrument.Guitar.Standard.Tuning;
    private readonly Lazy<PositionCollection> _lazyPositions;
    
    public Fretboard() : this(
        new(PitchCollection.Parse(_defaultTuning)), 24)
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
                    list.Add(new Position.Played(new(str, fret), midiNote++.ToSharpPitch()));
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
        var sb = new StringBuilder(Tuning.ToString());
        sb.Append($" - {FretCount} frets");
        if (!Capo.HasValue) return sb.ToString();

        // Add capo details
        var capo = Capo.Value;
        sb.Append($" (Capo: {(Ordinal) capo.Value})");
        return sb.ToString();
    }
}