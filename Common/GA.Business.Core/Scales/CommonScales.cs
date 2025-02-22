namespace GA.Business.Core.Scales;

/// <summary>
/// Represents a collection of common scales used in music theory and chord construction.
/// </summary>
/// <remarks>
/// References:
/// - The Guitar Grimoire: https://mikesimm.djlemonk.com/bblog/Scales-and-Modes.pdf
/// - Chord Formulas: https://www.smithfowler.org/music/Chord_Formulas.htm
/// - Guitar Lessons by Brian: https://www.guitarlessonsbybrian.com/chord_formula.pdf
/// - Music Theory Chord Patterns: https://en.wikibooks.org/wiki/Music_Theory/Complete_List_of_Chord_Patterns
/// - Jazz Chord Formulas: https://ragajunglism.org/teaching/jazz-chord-formulas/
/// </remarks>
public class CommonScales : IEnumerable<Scale>
{
    public static readonly CommonScales Instance = new();
    private readonly ImmutableList<ScaleInfo> _scaleInfos;

    private CommonScales()
    {
        _scaleInfos = GetScaleInfos();
    }

    private static ImmutableList<ScaleInfo> GetScaleInfos() =>
    [
        // Basic scales
        new("Major", new("C D E F G A B")),
        new("Natural Minor", new("A B C D E F G")),
        new("Harmonic Minor", new("A B C D E F G#")),
        new("Melodic Minor", new("A B C D E F# G#")),
        new("Major Pentatonic", new("C D E G A")),
        new("Minor Pentatonic", new("C Eb F G Bb")),
        new("Blues", new("C Eb F F# G Bb")),
        // Symmetric scales
        new("Whole Tone", new("C D E F# G# A#")),
        new("Diminished (Half-Whole)", new("C Db Eb E F# G A Bb")),
        new("Diminished (Whole-Half)", new("C D Eb F Gb Ab A B")),
        new("Augmented", new("C D# E G Ab B")),
        // Other common scales
        new("Harmonic Major", new("C D E F G Ab B")),
        new("Double Harmonic", new("C Db E F G Ab B")),
        new("Neapolitan Major", new("C Db Eb F G A B")),
        new("Neapolitan Minor", new("C Db Eb F G Ab B")),
        new("Hungarian Minor", new("C D Eb F# G Ab B")),
        new("Enigmatic", new("C Db E F# G# A# B")),
    ];

    public IEnumerator<Scale> GetEnumerator() => _scaleInfos.Select(info => info.Scale).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public Scale this[string name] => _scaleInfos.First(info => info.Name == name).Scale;

    private record struct ScaleInfo(string Name, Scale Scale);
}