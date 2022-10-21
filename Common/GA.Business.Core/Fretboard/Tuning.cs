namespace GA.Business.Core.Fretboard;

using GA.Core;
using Primitives;
using Notes;

/// <summary>
/// Guitar tuning
/// </summary>
/// <remarks>
/// References:
/// https://www.guitarworld.com/lessons/11-alternate-tunings-every-guitarist-should-know
/// https://www.stringsbymail.com/TuningChart.pdf
/// </remarks>
[PublicAPI]
public class Tuning : IIndexer<Str, Pitch>
{
    private readonly IReadOnlyDictionary<Str, Pitch> _pitchByStr;

    public Tuning(PitchCollection pitchCollection)
    {
        if (pitchCollection == null) throw new ArgumentNullException(nameof(pitchCollection));
        PitchCollection = pitchCollection ?? throw new ArgumentNullException(nameof(pitchCollection));
        _pitchByStr = PitchByStr(pitchCollection);
    }

    /// <summary>
    /// Gets the <see cref="PitchCollection"/>
    /// </summary>
    public PitchCollection PitchCollection { get; }
    public Pitch this[Str str] => _pitchByStr[str];

    private static IReadOnlyDictionary<Str, Pitch> PitchByStr(IEnumerable<Pitch> pitches)
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

    public override string ToString() => PitchCollection.ToString() ?? string.Empty;
}