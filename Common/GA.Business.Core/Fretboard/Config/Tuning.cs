using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes;
using static GA.Business.Core.Notes.Pitch.Sharp;

namespace GA.Business.Core.Fretboard.Config;

public class Tuning
{
    private readonly IReadOnlyDictionary<Str, Pitch> _pitchByStr;
    public static Tuning Default => new(E2, G2, D3, G3, B3, E4);

    public Tuning(params Pitch[] pitches)
    {
        Pitches = pitches ?? throw new ArgumentNullException(nameof(pitches));
        _pitchByStr = GetPitchByStr(pitches);
    }

    public IReadOnlyCollection<Pitch> Pitches { get; }
    public Pitch this[Str str] => _pitchByStr[str];

    private static IReadOnlyDictionary<Str, Pitch> GetPitchByStr(IEnumerable<Pitch> pitches)
    {
        var pitchesList = pitches.ToImmutableList();
        var lowestPitchFirst = pitchesList.First().GetMidiNote() < pitchesList.Last().GetMidiNote();
        if (lowestPitchFirst) pitchesList = pitchesList.Reverse();

        var str = Str.Min;
        var dict = new Dictionary<Str, Pitch>();
        foreach (var pitch in pitchesList)
        {
            dict[str++] = pitch;
        }

        // Result
        var result = dict.ToImmutableDictionary();

        return result;
    }

}