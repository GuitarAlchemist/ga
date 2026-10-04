namespace GA.Domain.Services.Chords.Parsing;

using System.Text.RegularExpressions;
using Core.Primitives.Notes;

/// <summary>
///     Parses chord symbols into Chord objects
/// </summary>
/// <remarks>
///     A symbol it can't read is rejected, never read as a major triad: SpectralRagOrchestrator tries it on a whole
///     chat message and on each of its words.
/// </remarks>
public class ChordSymbolParser
{
    // The root is a capital letter, as chord symbols write it: "a", "am" or "and" in a sentence are not chords
    private static readonly Regex _chordSymbolRegex = new(
        @"^([A-G][#b]?)(.*)$", RegexOptions.Compiled);

    // A bass note after a slash, as in "C/E"; "6/9" has none
    private static readonly Regex _bassRegex = new(@"^(.*)/([A-G][#b]?)$", RegexOptions.Compiled);

    // A capital M alone or before a number is major, as in "CM7"; lower-cased, it would read as minor
    private static readonly Regex _majorMRegex = new(@"^M(?=\d|$)", RegexOptions.Compiled);

    // An altered fifth, ninth, eleventh or thirteenth, read where the previous one ends
    private static readonly Regex _alterationRegex = new(@"\G([b#])(5|9|11|13)", RegexOptions.Compiled);

    /// <summary>
    ///     Parses a chord symbol string into a Chord object
    /// </summary>
    public Chord Parse(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Chord symbol cannot be null or empty", nameof(symbol));
        }

        // ♯ and ♭ as # and b, and the triangles that mean a major seventh as one
        var normalized = symbol.Trim().Replace('♯', '#').Replace('♭', 'b').Replace('Δ', '△').Replace('∆', '△');
        var match = _chordSymbolRegex.Match(normalized);
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid chord symbol: {symbol}", nameof(symbol));
        }

        var root = Note.Accidented.Parse(match.Groups[1].Value, null);
        var suffix = match.Groups[2].Value;
        Note.Accidented? bass = null;
        if (_bassRegex.Match(suffix) is { Success: true } slash)
        {
            suffix = slash.Groups[1].Value;
            bass = Note.Accidented.Parse(slash.Groups[2].Value, null);
        }

        var formula = ParseChordSuffix(suffix)
                      ?? throw new ArgumentException($"Unknown chord symbol: {symbol}", nameof(symbol));
        if (bass is not null)
        {
            formula = WithBass(formula, (bass.PitchClass.Value - root.PitchClass.Value + 12) % 12);
        }

        return new(root, formula, symbol);
    }

    /// <summary>
    ///     Tries to parse a chord symbol string into a Chord object
    /// </summary>
    public bool TryParse(string symbol, out Chord? chord)
    {
        try
        {
            chord = Parse(symbol);
            return true;
        }
        catch
        {
            chord = null;
            return false;
        }
    }

    private static ChordFormula? ParseChordSuffix(string suffix)
    {
        suffix = _majorMRegex.Replace(suffix.Trim(), "maj");

        // Normalize the suffix; parentheses and commas only group alterations: "7(b9,#11)" is "7b9#11"
        suffix = suffix.ToLowerInvariant().Replace(" ", "").Replace("(", "").Replace(")", "").Replace(",", "");

        return KnownSuffix(suffix) ?? ParseAlteredSuffix(suffix);
    }

    private static ChordFormula? KnownSuffix(string suffix) =>
        suffix switch
        {
            "" or "maj" or "major" => CommonChordFormulas.Major,
            "m" or "min" or "minor" or "mi" or "-" => CommonChordFormulas.Minor,
            "dim" or "°" => CommonChordFormulas.Diminished,
            "aug" or "+" => CommonChordFormulas.Augmented,
            "5" => ChordFormula.FromSemitones("Power Chord", 7),
            "sus2" => CreateSus2Formula(),
            "sus4" or "sus" => CreateSus4Formula(),
            "6" => CreateSixthFormula(),
            "m6" => CreateMinorSixthFormula(),
            "7" => CommonChordFormulas.Dominant7,
            "maj7" or "ma7" or "△" or "△7" => CommonChordFormulas.Major7,
            "m7" or "min7" or "mi7" or "-7" => CommonChordFormulas.Minor7,
            "mmaj7" or "minmaj7" => ChordFormula.FromSemitones("Minor Major 7th", 3, 7, 11),
            "dim7" or "°7" or "o7" => CreateDiminished7Formula(),
            "m7b5" or "ø" or "ø7" => CreateHalfDiminished7Formula(),
            "+7" or "aug7" => ChordFormula.FromSemitones("Augmented 7th", 4, 8, 10),
            "7sus4" or "7sus" => ChordFormula.FromSemitones("Dominant 7th Sus4", 5, 7, 10),
            "9" => CreateDominant9Formula(),
            "9sus4" or "9sus" => ChordFormula.FromSemitones("Dominant 9th Sus4", 5, 7, 10, 14),
            "maj9" or "△9" => CreateMajor9Formula(),
            "m9" or "min9" or "-9" => CreateMinor9Formula(),
            "11" => CreateDominant11Formula(),
            "maj11" or "△11" => CreateMajor11Formula(),
            "m11" or "min11" or "-11" => CreateMinor11Formula(),
            "13" => CreateDominant13Formula(),
            "maj13" or "△13" => CreateMajor13Formula(),
            "m13" or "min13" or "-13" => CreateMinor13Formula(),
            "add9" => CreateAdd9Formula(),
            "add2" => ChordFormula.FromSemitones("Add2", 2, 4, 7),
            "madd9" => CreateMinorAdd9Formula(),
            "6/9" or "69" => CreateSixNineFormula(),
            "m6/9" or "m69" => CreateMinorSixNineFormula(),
            "alt" or "7alt" => CreateAlteredDominantFormula(),
            _ => null
        };

    /// <summary>
    ///     A chord followed by altered tones, each replacing its natural tone: "7b9#11", "9#11", "maj7#11", "mb5"
    /// </summary>
    private static ChordFormula? ParseAlteredSuffix(string suffix)
    {
        var start = suffix.IndexOfAny(['b', '#']);
        if (start < 0 || (start == 0 ? CommonChordFormulas.Major : KnownSuffix(suffix[..start])) is not { } chord)
        {
            return null;
        }

        var semitones = chord.Intervals.Select(i => i.Interval.Semitones.Value).ToList();
        var position = start;
        for (var alteration = _alterationRegex.Match(suffix, position); alteration.Success; alteration = _alterationRegex.Match(suffix, position))
        {
            var natural = alteration.Groups[2].Value switch { "5" => 7, "9" => 14, "11" => 17, _ => 21 };
            semitones.Remove(natural);
            semitones.Add(natural + (alteration.Groups[1].Value == "#" ? 1 : -1));
            position += alteration.Length;
        }

        return position == suffix.Length ? ChordFormula.FromSemitones(suffix, [.. semitones.Distinct().Order()]) : null;
    }

    /// <summary>
    ///     A slash chord keeps its chord and adds its bass when the bass isn't a chord tone: C/F# is C E F# G
    /// </summary>
    private static ChordFormula WithBass(ChordFormula formula, int bass)
    {
        var semitones = formula.Intervals.Select(i => i.Interval.Semitones.Value).ToList();
        return bass == 0 || semitones.Any(s => s % 12 == bass)
            ? formula
            : ChordFormula.FromSemitones(formula.Name, [.. semitones, bass]);
    }

    // Implementation via ChordFormula.FromSemitones to avoid direct Interval construction here
    private static ChordFormula CreateSus2Formula() => ChordFormula.FromSemitones("Sus2", 2, 7);
    private static ChordFormula CreateSus4Formula() => ChordFormula.FromSemitones("Sus4", 5, 7);
    private static ChordFormula CreateSixthFormula() => ChordFormula.FromSemitones("Sixth", 4, 7, 9);
    private static ChordFormula CreateMinorSixthFormula() => ChordFormula.FromSemitones("Minor Sixth", 3, 7, 9);
    private static ChordFormula CreateDiminished7Formula() => ChordFormula.FromSemitones("Diminished 7th", 3, 6, 9);

    private static ChordFormula CreateHalfDiminished7Formula() =>
        ChordFormula.FromSemitones("Half Diminished 7th", 3, 6, 10);

    private static ChordFormula CreateDominant9Formula() => ChordFormula.FromSemitones("Dominant 9th", 4, 7, 10, 14);
    private static ChordFormula CreateMajor9Formula() => ChordFormula.FromSemitones("Major 9th", 4, 7, 11, 14);
    private static ChordFormula CreateMinor9Formula() => ChordFormula.FromSemitones("Minor 9th", 3, 7, 10, 14);

    private static ChordFormula CreateDominant11Formula() =>
        ChordFormula.FromSemitones("Dominant 11th", 4, 7, 10, 14, 17);

    private static ChordFormula CreateMajor11Formula() => ChordFormula.FromSemitones("Major 11th", 4, 7, 11, 14, 17);
    private static ChordFormula CreateMinor11Formula() => ChordFormula.FromSemitones("Minor 11th", 3, 7, 10, 14, 17);

    private static ChordFormula CreateDominant13Formula() =>
        ChordFormula.FromSemitones("Dominant 13th", 4, 7, 10, 14, 17, 21);

    private static ChordFormula CreateMajor13Formula() =>
        ChordFormula.FromSemitones("Major 13th", 4, 7, 11, 14, 17, 21);

    private static ChordFormula CreateMinor13Formula() =>
        ChordFormula.FromSemitones("Minor 13th", 3, 7, 10, 14, 17, 21);

    private static ChordFormula CreateAdd9Formula() => ChordFormula.FromSemitones("Add9", 4, 7, 14);
    private static ChordFormula CreateMinorAdd9Formula() => ChordFormula.FromSemitones("Minor Add9", 3, 7, 14);
    private static ChordFormula CreateSixNineFormula() => ChordFormula.FromSemitones("6/9", 4, 7, 9, 14);
    private static ChordFormula CreateMinorSixNineFormula() => ChordFormula.FromSemitones("Minor 6/9", 3, 7, 9, 14);

    // The third and seventh with the four altered tones, as ChordAlterationService describes it: b9, #9, #11, b13
    private static ChordFormula CreateAlteredDominantFormula() =>
        ChordFormula.FromSemitones("Altered Dominant", 4, 10, 13, 15, 18, 20);
}
