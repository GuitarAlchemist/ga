namespace GA.Business.ML.Agents;

/// <summary>
///     Shared chord-symbol vocabulary crossed by both <see cref="Mcp.ChordMcpTools"/> (MCP transport
///     adapter) and <see cref="Skills.ChordInfoSkill"/> (orchestrator adapter): the root→pitch-class
///     map, root/quality normalization, and the interval formula table.
/// </summary>
/// <remarks>
///     Both adapters previously carried their own copies. The copies had drifted in a load-bearing way:
///     the skill lowercased the whole quality token before matching, so <c>"CM"</c> resolved to C
///     <em>minor</em> instead of C major (a regression of the PR #80 fix that the MCP tool already
///     carried). Consolidating here (candidate #3 of the architecture review) gives one home for the
///     case-sensitive <c>M</c>/<c>M7</c> handling and the formula data. <see cref="GetFormula"/>
///     returns the terse canonical <see cref="ChordFormula.Quality"/>; each adapter renders its own
///     display label from that (the MCP tool surfaces the terse quality; the skill expands it to prose).
/// </remarks>
public static class ChordVocabulary
{
    /// <summary>
    ///     Root spelling → pitch class (0–11), case-insensitive, including the enharmonic edge cases
    ///     (B#, Cb, E#, Fb) so unusual but valid roots resolve.
    /// </summary>
    public static IReadOnlyDictionary<string, int> PitchClasses { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0,  ["C#"] = 1,  ["Db"] = 1,
            ["D"] = 2,  ["D#"] = 3,  ["Eb"] = 3,
            ["E"] = 4,  ["F"] = 5,   ["F#"] = 6, ["Gb"] = 6,
            ["G"] = 7,  ["G#"] = 8,  ["Ab"] = 8,
            ["A"] = 9,  ["A#"] = 10, ["Bb"] = 10,
            ["B"] = 11, ["B#"] = 0,  ["Cb"] = 11, ["E#"] = 5, ["Fb"] = 4,
        };

    /// <summary>Looks up a normalized root's pitch class; false if the root is not recognised.</summary>
    public static bool TryGetPitchClass(string root, out int pitchClass) =>
        PitchClasses.TryGetValue(root, out pitchClass);

    /// <summary>Normalises a root for display + lookup: first letter upper, accidental lower (<c>"c#"</c> → <c>"C#"</c>).</summary>
    public static string NormalizeRoot(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.Length switch
        {
            0 => string.Empty,
            1 => trimmed.ToUpperInvariant(),
            _ => char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant(),
        };
    }

    /// <summary>
    ///     Normalises a quality suffix to its canonical form (the key <see cref="GetFormula"/> switches on).
    /// </summary>
    /// <remarks>
    ///     The uppercase <c>"M"</c> / <c>"M7"</c> arms are matched <b>case-sensitively before</b> the
    ///     lowercase fallback: <c>"M"</c> means major and must not fold through <c>ToLowerInvariant()</c>
    ///     to <c>"m"</c> (minor). This is the PR #80 fix; consolidating it here keeps the skill from
    ///     re-introducing the <c>"CM"</c> → C-minor regression.
    /// </remarks>
    public static string NormalizeQuality(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed switch
        {
            "M"  => "major",
            "M7" => "major 7",
            _    => trimmed.ToLowerInvariant() switch
            {
                "" => "major",
                "maj" or "major" => "major",
                "m" or "min" or "minor" => "minor",
                "dim" or "diminished" => "diminished",
                "aug" or "augmented" or "+" => "augmented",
                "5" or "no3" or "power" => "power",
                // Triad extensions
                "sus2" => "sus2",
                "sus4" or "sus" => "sus4",
                "add9" => "add9",
                "6" or "maj6" or "major 6" => "major 6",
                "m6" or "min6" or "minor 6" => "minor 6",
                // 7th family
                "7" or "dominant" or "dom7" or "dominant 7" => "dominant 7",
                "maj7" or "major 7" or "ma7" or "Δ7" => "major 7",
                "m7" or "min7" or "minor 7" or "-7" => "minor 7",
                "dim7" or "diminished 7" or "diminished7" or "°7" => "diminished 7",
                "m7b5" or "min7b5" or "ø" or "ø7" or "half-diminished" or "half diminished" or "minor 7 flat 5" => "half-diminished",
                "7b5" or "dominant 7 flat 5" => "dominant 7 flat 5",
                "7#5" or "7+5" or "dominant 7 sharp 5" => "dominant 7 sharp 5",
                "7b9" or "dominant 7 flat 9" => "dominant 7 flat 9",
                "7#9" or "dominant 7 sharp 9" => "dominant 7 sharp 9",
                "7alt" or "alt" or "altered" or "altered dominant" => "altered dominant",
                // 9 / 11 / 13 extensions
                "9" or "dominant 9" => "dominant 9",
                "maj9" or "major 9" => "major 9",
                "m9" or "min9" or "minor 9" => "minor 9",
                "11" or "dominant 11" => "dominant 11",
                "maj11" or "major 11" => "major 11",
                "m11" or "min11" or "minor 11" => "minor 11",
                "13" or "dominant 13" => "dominant 13",
                "maj13" or "major 13" => "major 13",
                "m13" or "min13" or "minor 13" => "minor 13",
                var other => other,
            },
        };
    }

    /// <summary>
    ///     Interval formula for a canonical quality (the output of <see cref="NormalizeQuality"/>).
    ///     Unknown qualities fall back to a major triad — the same default both adapters carried.
    /// </summary>
    public static ChordFormula GetFormula(string quality) => quality switch
    {
        "minor" => new("minor", [0, 3, 7], [0, 2, 4], ["root", "minor third", "perfect fifth"]),
        "diminished" => new("diminished", [0, 3, 6], [0, 2, 4], ["root", "minor third", "diminished fifth"]),
        "augmented" => new("augmented", [0, 4, 8], [0, 2, 4], ["root", "major third", "augmented fifth"]),
        "power" => new("power", [0, 7], [0, 4], ["root", "perfect fifth"]),
        "sus2" => new("sus2", [0, 2, 7], [0, 1, 4], ["root", "major second", "perfect fifth"]),
        "sus4" => new("sus4", [0, 5, 7], [0, 3, 4], ["root", "perfect fourth", "perfect fifth"]),
        "add9" => new("add9", [0, 4, 7, 14], [0, 2, 4, 8], ["root", "major third", "perfect fifth", "ninth"]),
        "major 6" => new("major 6", [0, 4, 7, 9], [0, 2, 4, 5], ["root", "major third", "perfect fifth", "major sixth"]),
        "minor 6" => new("minor 6", [0, 3, 7, 9], [0, 2, 4, 5], ["root", "minor third", "perfect fifth", "major sixth"]),
        "dominant 7" => new("dominant 7", [0, 4, 7, 10], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "minor seventh"]),
        "major 7" => new("major 7", [0, 4, 7, 11], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "major seventh"]),
        "minor 7" => new("minor 7", [0, 3, 7, 10], [0, 2, 4, 6], ["root", "minor third", "perfect fifth", "minor seventh"]),
        "diminished 7" => new("diminished 7", [0, 3, 6, 9], [0, 2, 4, 6], ["root", "minor third", "diminished fifth", "diminished seventh"]),
        "half-diminished" => new("half-diminished", [0, 3, 6, 10], [0, 2, 4, 6], ["root", "minor third", "diminished fifth", "minor seventh"]),
        "dominant 7 flat 5" => new("dominant 7 flat 5", [0, 4, 6, 10], [0, 2, 4, 6], ["root", "major third", "diminished fifth", "minor seventh"]),
        "dominant 7 sharp 5" => new("dominant 7 sharp 5", [0, 4, 8, 10], [0, 2, 4, 6], ["root", "major third", "augmented fifth", "minor seventh"]),
        "dominant 7 flat 9" => new("dominant 7 flat 9", [0, 4, 7, 10, 13], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "minor seventh", "minor ninth"]),
        "dominant 7 sharp 9" => new("dominant 7 sharp 9", [0, 4, 7, 10, 15], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "minor seventh", "augmented ninth"]),
        "altered dominant" => new("altered dominant", [0, 4, 6, 8, 10, 13, 15], [0, 2, 3, 4, 6, 8, 8], ["root", "major third", "flat fifth", "sharp fifth", "minor seventh", "flat ninth", "sharp ninth"]),
        "dominant 9" => new("dominant 9", [0, 4, 7, 10, 14], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "minor seventh", "major ninth"]),
        "major 9" => new("major 9", [0, 4, 7, 11, 14], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "major seventh", "major ninth"]),
        "minor 9" => new("minor 9", [0, 3, 7, 10, 14], [0, 2, 4, 6, 8], ["root", "minor third", "perfect fifth", "minor seventh", "major ninth"]),
        "dominant 11" => new("dominant 11", [0, 4, 7, 10, 14, 17], [0, 2, 4, 6, 8, 10], ["root", "major third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh"]),
        "major 11" => new("major 11", [0, 4, 7, 11, 14, 17], [0, 2, 4, 6, 8, 10], ["root", "major third", "perfect fifth", "major seventh", "major ninth", "perfect eleventh"]),
        "minor 11" => new("minor 11", [0, 3, 7, 10, 14, 17], [0, 2, 4, 6, 8, 10], ["root", "minor third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh"]),
        "dominant 13" => new("dominant 13", [0, 4, 7, 10, 14, 17, 21], [0, 2, 4, 6, 8, 10, 12], ["root", "major third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh", "major thirteenth"]),
        "major 13" => new("major 13", [0, 4, 7, 11, 14, 17, 21], [0, 2, 4, 6, 8, 10, 12], ["root", "major third", "perfect fifth", "major seventh", "major ninth", "perfect eleventh", "major thirteenth"]),
        "minor 13" => new("minor 13", [0, 3, 7, 10, 14, 17, 21], [0, 2, 4, 6, 8, 10, 12], ["root", "minor third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh", "major thirteenth"]),
        _ => new("major", [0, 4, 7], [0, 2, 4], ["root", "major third", "perfect fifth"]),
    };
}

/// <summary>
///     A chord's interval formula. <see cref="Quality"/> is the terse canonical name (e.g.
///     <c>"minor 7"</c>); adapters render their own display label from it.
/// </summary>
/// <param name="Quality">Terse canonical quality (the <see cref="ChordVocabulary.NormalizeQuality"/> key).</param>
/// <param name="Intervals">Semitone offsets from the root.</param>
/// <param name="LetterSteps">Diatonic letter steps from the root (drives enharmonic spelling).</param>
/// <param name="IntervalNames">Human-readable interval names, parallel to <paramref name="Intervals"/>.</param>
public sealed record ChordFormula(
    string Quality,
    IReadOnlyList<int> Intervals,
    IReadOnlyList<int> LetterSteps,
    IReadOnlyList<string> IntervalNames);
