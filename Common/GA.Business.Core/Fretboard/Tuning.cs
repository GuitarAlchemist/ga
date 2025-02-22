namespace GA.Business.Core.Fretboard;

using Primitives;
using Notes;

/// <summary>
/// Represent a fretted instrument tuning
/// </summary>
/// <remarks>
/// References:
/// https://www.guitarworld.com/lessons/11-alternate-tunings-every-guitarist-should-know
/// https://www.stringsbymail.com/TuningChart.pdf
/// </remarks>
[PublicAPI]
public class Tuning : IIndexer<Str, Pitch>
{
    private readonly ImmutableDictionary<Str, Pitch> _pitchByStr;

    /// <summary>
    /// The default tuning (Guitar - E2 A2 D3 G3 B3 E4)
    /// </summary>
    public static readonly Tuning Default = new(PitchCollection.Parse("E2 A2 D3 G3 B3 E4"));

    /// <summary>
    /// Constructs a <see cref="Tuning"/> instance
    /// </summary>
    /// <param name="pitchCollection">The <see cref="PitchCollection"/></param>
    /// <exception cref="ArgumentNullException">Thrown when <param name="pitchCollection"> is null</param></exception>
    public Tuning(PitchCollection pitchCollection)
    {
        ArgumentNullException.ThrowIfNull(pitchCollection);
        
        PitchCollection = pitchCollection ?? throw new ArgumentNullException(nameof(pitchCollection));
        _pitchByStr = GetPitchByStr(pitchCollection);
    }

    /// <summary>
    /// Gets the <see cref="PitchCollection"/>
    /// </summary>
    public PitchCollection PitchCollection { get; }
    
    /// <summary>
    /// Gets the pitch associated with the specified open string
    /// </summary>
    /// <param name="str">The <see cref="Str"/></param>
    /// <returns>The <see cref="Pitch"/></returns>
    public Pitch this[Str str] => _pitchByStr[str];

    private static ImmutableDictionary<Str, Pitch> GetPitchByStr(IEnumerable<Pitch> items)
    {
        var pitchesList = items as IReadOnlyCollection<Pitch> ?? items.ToImmutableList();
        var lowestPitchFirst = pitchesList.First() < pitchesList.Last();
        if (lowestPitchFirst) pitchesList = pitchesList.Reverse().ToImmutableList();

        var str = Str.Min;
        var dict = new Dictionary<Str, Pitch>();
        foreach (var pitch in pitchesList)
        {
            dict[str++] = pitch;
        }
        return dict.ToImmutableDictionary();
    }

    /// <inheritdoc />
    public override string ToString() => PitchCollection.ToString();
}