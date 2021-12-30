using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Config;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

[PublicAPI]
public class Fretboard
{
    public static readonly Fretboard Default = new(Tuning.Default);

    public Fretboard(
        Tuning tuning,
        int fretCount = Fret.DefaultCount)
    {
        Tuning = tuning;
        FretCount = fretCount;
        StringCount = tuning.Pitches.Count;
    }

    public Tuning Tuning { get; }
    public int StringCount { get; }
    public int FretCount { get; }
    public IReadOnlyCollection<Str> Strings => Str.GetCollection(StringCount);
    public IReadOnlyCollection<Fret> Frets => Fret.GetCollection(Fret.Min.Value, FretCount);
    public IReadOnlyCollection<Position> Positions => GetPositions().ToImmutableList();

    private IEnumerable<Position> GetPositions()
    {
        IEnumerable<Position> StringPositions(Str str)
        {
            yield return new Position.Muted(str);

            var midiNote = Tuning[str].GetMidiNote();
            yield return new Position.Open(str, midiNote++);
            foreach (var fret in Fret.GetCollection(1, FretCount - 1))
            {
                yield return new Position.Fretted(str, fret, midiNote++);
            }
        }

        foreach (var str in Str.GetCollection(StringCount))
        {
            foreach (var position in StringPositions(str))
            {
                yield return position;
            }
        }
    }
}