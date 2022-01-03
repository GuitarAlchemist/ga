using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes;
using static GA.Business.Core.Notes.Pitch.Sharp;

namespace GA.Business.Core.Fretboard.Config;

/// <summary>
/// Guitar tuning
/// </summary>
/// <remarks>
/// See https://www.guitarworld.com/lessons/11-alternate-tunings-every-guitarist-should-know
/// </remarks>
public class Tuning
{
    private readonly IReadOnlyDictionary<Str, Pitch> _pitchByStr;

    public static class Guitar
    {
        public static Tuning Default => new("Guitar / Standard", E2, A2, D3, G3, B3, E4);
        public static Tuning DropD => new("Guitar / Drop D", D2, A2, D3, G3, B3, E4);
        public static Tuning DoubleDropD => new("Guitar / Double drop D", D2, A2, D3, G3, B3, D4);
        public static Tuning Dadgad => new("Guitar / DAGGAD", D2, A2, D3, G3, A3, D4);
        public static Tuning OpenD => new("Guitar / Open D", D2, A2, D3, FSharp3, A3, D4);
    }

    public static class Ukulele
    {
        public static Tuning Default => new("Ukulele / Standard", G4, C4, E4, A4);
    }

    public Tuning(
        string name,
        params Pitch[] pitches)
    {
        Name = name;
        Pitches = pitches ?? throw new ArgumentNullException(nameof(pitches));
        _pitchByStr = GetPitchByStr(pitches);
    }

    public string Name { get; }
    public IReadOnlyCollection<Pitch> Pitches { get; }
    public Pitch this[Str str] => _pitchByStr[str];

    private static IReadOnlyDictionary<Str, Pitch> GetPitchByStr(IEnumerable<Pitch> pitches)
    {
        var pitchesList = pitches.ToImmutableList();
        var lowestPitchFirst = pitchesList.First() < pitchesList.Last();
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

    public override string ToString() => Name;
}