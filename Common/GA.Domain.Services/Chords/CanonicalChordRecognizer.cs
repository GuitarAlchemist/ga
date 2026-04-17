namespace GA.Domain.Services.Chords;

using Business.Core.Analysis.Voicings;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;

/// <summary>
///     Content-enumerated chord recognizer. Given a pitch-class set, identifies the chord
///     by constraint satisfaction against <see cref="CanonicalChordPatternCatalog" />.
/// <para>
///     Replaces the first-match-wins logic in <c>VoicingHarmonicAnalyzer.IdentifyChord</c>.
///     Recognition depends only on pitch-class content and the optional bass hint for
///     slash notation, never on register. Same pitch-class set → same CanonicalName
///     on every instrument.
/// </para>
/// </summary>
public static class CanonicalChordRecognizer
{
    /// <summary>
    ///     Recognizes the chord identity of a pitch-class set.
    /// </summary>
    /// <param name="pcSet">The voicing's distinct pitch classes.</param>
    /// <param name="bassNote">Optional: the bass pitch class for slash-chord notation.</param>
    /// <returns>A <see cref="CanonicalChordResult" /> with the canonical identity and any slash suffix.</returns>
    public static CanonicalChordResult Identify(PitchClassSet pcSet, PitchClass? bassNote = null)
    {
        ArgumentNullException.ThrowIfNull(pcSet);

        var pcs = pcSet.Select(p => p.Value).Distinct().OrderBy(v => v).ToArray();
        return pcs.Length switch
        {
            0 => Empty(),
            1 => IdentifyUnison(pcs[0]),
            2 => IdentifyDyad(pcs[0], pcs[1]),
            _ => IdentifyChordSet(pcs, pcSet, bassNote),
        };
    }

    /// <summary>Empty PC-set fallback (shouldn't happen but defensive).</summary>
    private static CanonicalChordResult Empty() =>
        new(
            CanonicalName: "(empty)",
            Root: null,
            Quality: "empty",
            Extension: null,
            Alterations: [],
            SlashSuffix: null,
            PatternName: null,
            MatchDistance: -1,
            IsNaturallyOccurring: false);

    /// <summary>Single pitch class (unison or octave doubling only).</summary>
    private static CanonicalChordResult IdentifyUnison(int pc) =>
        new(
            CanonicalName: $"{GetNoteName(pc)} (unison)",
            Root: GetNoteName(pc),
            Quality: "unison",
            Extension: null,
            Alterations: [],
            SlashSuffix: null,
            PatternName: "unison",
            MatchDistance: 0,
            IsNaturallyOccurring: true);

    /// <summary>Two distinct pitch classes — emit interval name.</summary>
    private static CanonicalChordResult IdentifyDyad(int pc1, int pc2)
    {
        var interval = (pc2 - pc1 + 12) % 12;

        // Power-chord detection (P5) must happen BEFORE the smaller-interval swap,
        // because after swapping, canonicalInterval is always ≤ 6 and can never be 7.
        // A P5 from pc1 to pc2 (interval == 7) → root is pc1.
        // A P5 from pc2 to pc1 (interval == 5, i.e. P4 from pc1 to pc2) → root is pc2.
        if (interval is 7 or 5)
        {
            var powerRoot = interval == 7 ? pc1 : pc2;
            return new CanonicalChordResult(
                CanonicalName: $"{GetNoteName(powerRoot)}5",
                Root: GetNoteName(powerRoot),
                Quality: "power",
                Extension: null,
                Alterations: [],
                SlashSuffix: null,
                PatternName: "power-chord",
                MatchDistance: 0,
                IsNaturallyOccurring: true);
        }

        // Non-power-chord dyad: pick the smaller interval for the canonical orientation
        // so {C, E} is named "C + E (Major 3rd)" rather than "E + C (Minor 6th)".
        var (root, other) = interval <= 6 ? (pc1, pc2) : (pc2, pc1);
        var canonicalInterval = (other - root + 12) % 12;

        var intervalName = canonicalInterval switch
        {
            0 => "unison",
            1 => "Minor 2nd",
            2 => "Major 2nd",
            3 => "Minor 3rd",
            4 => "Major 3rd",
            5 => "Perfect 4th",
            6 => "Tritone",
            _ => $"IC{canonicalInterval}"
        };

        return new CanonicalChordResult(
            CanonicalName: $"{GetNoteName(root)} + {GetNoteName(other)} ({intervalName})",
            Root: GetNoteName(root),
            Quality: "dyad",
            Extension: null,
            Alterations: [],
            SlashSuffix: null,
            PatternName: $"dyad-ic{canonicalInterval}",
            MatchDistance: 0,
            IsNaturallyOccurring: canonicalInterval is 3 or 4);
    }

    /// <summary>
    ///     Full chord recognition for 3+ pitch classes. Tries every PC as root,
    ///     matches against the canonical pattern catalog, picks the best candidate.
    /// </summary>
    private static CanonicalChordResult IdentifyChordSet(int[] pcs, PitchClassSet pcSet, PitchClass? bassNote)
    {
        var candidates = new List<Candidate>();

        foreach (var root in pcs)
        {
            var intervalsFromRoot = pcs.Select(pc => (pc - root + 12) % 12).ToArray();
            var rootIntervalSet = new HashSet<int>(intervalsFromRoot);

            foreach (var pattern in CanonicalChordPatternCatalog.All)
            {
                // Allow tight fits; exact or "missing at most 1" (e.g., no-5 voicings)
                // or "1 extra" (e.g., add-chord variants caught by a stricter pattern elsewhere).
                var match = pattern.TryMatch(rootIntervalSet, maxMissing: 1, maxExtra: 1);
                if (match is null) continue;
                candidates.Add(new Candidate(root, pattern, match.Value));
            }
        }

        if (candidates.Count == 0)
            return FallbackFromForte(pcSet);

        // Ranking:
        //   1) smallest edit distance (missing + extra)
        //   2) exact matches preferred (prefer Missing==0 over Extra==0 when tied)
        //   3) pattern priority (simpler patterns win)
        //   4) root commonness (C/F/G over Ab/F#/C#)
        var best = candidates
            .OrderBy(c => c.Match.Distance)
            .ThenBy(c => c.Match.Missing == 0 ? 0 : 1)
            .ThenBy(c => c.Pattern.Priority)
            .ThenBy(c => RootCommonness(c.Root))
            .First();

        return BuildResult(best, bassNote);
    }

    private static CanonicalChordResult BuildResult(Candidate best, PitchClass? bassNote)
    {
        var rootName = GetNoteName(best.Root);
        var suffix = ChordSuffix(best.Pattern);
        var canonicalName = $"{rootName}{suffix}";

        // Slash notation (voicing-specific)
        string? slashSuffix = null;
        if (bassNote.HasValue && bassNote.Value.Value != best.Root)
            slashSuffix = $"/{GetNoteName(bassNote.Value.Value)}";

        return new CanonicalChordResult(
            CanonicalName: canonicalName,
            Root: rootName,
            Quality: best.Pattern.Quality,
            Extension: best.Pattern.Extension,
            Alterations: best.Pattern.Alterations,
            SlashSuffix: slashSuffix,
            PatternName: best.Pattern.Name,
            MatchDistance: best.Match.Distance,
            IsNaturallyOccurring: best.Match.IsExact);
    }

    /// <summary>
    ///     Converts a pattern to a compact chord suffix. E.g. major-triad → "",
    ///     dominant-7-sharp-9 → "7#9", minor-major-7 → "m(maj7)".
    /// </summary>
    private static string ChordSuffix(ChordIntervalPattern pattern)
    {
        // Compose from quality + extension + alterations. This avoids hard-coding
        // a lookup table for every pattern name.
        var quality = pattern.Quality switch
        {
            "major" => "",
            "minor" => "m",
            "diminished" => "dim",
            "augmented" => "aug",
            "dominant" => "",
            "altered-dominant" => "",
            "suspended" => "sus",  // replaced by sus2/sus4 from alterations
            "quartal" => " quartal",
            "shell" => "",
            "power" => "",
            "set-class" => "",
            _ => pattern.Quality,
        };

        var extension = pattern.Extension switch
        {
            "triad" => "",
            "6th" => "6",
            "7th" => "7",
            "9th" => "9",
            "11th" => "11",
            "13th" => "13",
            "add" => "",
            "dyad" => "",
            null => "",
            _ => pattern.Extension,
        };

        var alterations = pattern.Alterations.Length > 0
            ? string.Join("", pattern.Alterations.Select(a => FormatAlteration(a)))
            : "";

        // Special-case reassembly for known patterns:
        //   sus2 / sus4 → "sus2" / "sus4" (replaces the "sus" quality)
        //   minor-major-7 → "m(maj7)"
        //   minor-7 → "m7", minor-6 → "m6", etc.
        return pattern.Name switch
        {
            "sus2" => "sus2",
            "sus4" => "sus4",
            "7-sus2" => "7sus2",
            "7-sus4" => "7sus4",
            "9-sus4" => "9sus4",
            "13-sus4" => "13sus4",
            "minor-major-7" => "m(maj7)",
            "minor-major-9" => "m(maj9)",
            "augmented-major-7" => "aug(maj7)",
            "augmented-7" => "aug7",
            "half-diminished-7" => "m7b5",
            "half-diminished-9" => "m9b5",
            "diminished-7" => "dim7",
            "power-chord" => "5",
            "add-9" => "add9",
            "minor-add-9" => "m(add9)",
            "add-11" => "add11",
            "minor-add-11" => "m(add11)",
            "6-9" => "6/9",
            "minor-6-9" => "m6/9",
            "quartal-3" => "(quartal)",
            "quartal-4" => "7(quartal)",
            "quartal-5" => "9(quartal)",
            "shell-7" => "7(shell)",
            "shell-major-7" => "maj7(shell)",
            "shell-minor-7" => "m7(shell)",
            "whole-tone-hexachord" => "(whole-tone)",
            "augmented-hexachord" => "(aug hex)",
            "octatonic-half-whole" => "(oct H-W)",
            "octatonic-whole-half" => "(oct W-H)",
            _ => quality + extension + alterations,
        };
    }

    private static string FormatAlteration(string alt) => alt switch
    {
        "b5" => "b5",
        "#5" => "#5",
        "b9" => "b9",
        "#9" => "#9",
        "b11" => "b11",
        "#11" => "#11",
        "b13" => "b13",
        "#13" => "#13",
        "add9" => "",   // handled at pattern level
        "add11" => "",
        "sus2" => "",
        "sus4" => "",
        "maj7" => "",
        "maj9" => "",
        _ => alt,
    };

    /// <summary>
    ///     No canonical pattern matched — emit the Forte number as the name so the
    ///     chatbot can still say something meaningful.
    /// </summary>
    private static CanonicalChordResult FallbackFromForte(PitchClassSet pcSet)
    {
        var forte = ProgrammaticForteCatalog.GetForteNumber(pcSet);
        var cardinality = pcSet.Count;
        var cardinalityName = cardinality switch
        {
            3 => "trichord",
            4 => "tetrachord",
            5 => "pentachord",
            6 => "hexachord",
            7 => "heptachord",
            8 => "octachord",
            9 => "nonachord",
            10 => "decachord",
            11 => "undecachord",
            12 => "dodecachord",
            _ => $"{cardinality}-note set",
        };

        var forteLabel = forte.HasValue ? $"Forte {forte}" : "set";
        return new CanonicalChordResult(
            CanonicalName: $"{forteLabel} ({cardinalityName})",
            Root: null,
            Quality: "set-class",
            Extension: null,
            Alterations: [],
            SlashSuffix: null,
            PatternName: forte?.ToString(),
            MatchDistance: -1,
            IsNaturallyOccurring: false);
    }

    /// <summary>
    ///     Root commonness score (lower = more common/preferred).
    ///     Based on circle-of-fifths distance from C, slightly biased toward natural notes.
    /// </summary>
    private static int RootCommonness(int pc) => pc switch
    {
        0 => 0,   // C
        7 => 1,   // G
        5 => 1,   // F
        2 => 2,   // D
        10 => 2,  // Bb
        9 => 3,   // A
        3 => 3,   // Eb
        4 => 4,   // E
        8 => 4,   // Ab
        11 => 5,  // B
        1 => 5,   // Db
        6 => 6,   // Gb / F#
        _ => 10,
    };

    private static string GetNoteName(int pc)
    {
        string[] names = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
        return names[((pc % 12) + 12) % 12];
    }

    private readonly record struct Candidate(int Root, ChordIntervalPattern Pattern, MatchResult Match);
}

/// <summary>
///     Result of canonical chord recognition. Separates invariant chord identity
///     (<see cref="CanonicalName" />) from voicing-specific slash notation
///     (<see cref="SlashSuffix" />) so downstream consumers can choose which to use.
/// </summary>
/// <param name="CanonicalName">Register-invariant chord name ("C Major 7", "Dm7b5", "F#13b9").
///     Same pitch-class set → same name on every instrument.</param>
/// <param name="Root">Root note name, or null if no root was determined (e.g., empty set).</param>
/// <param name="Quality">Chord quality family ("major", "minor", "dominant", "altered-dominant",
///     "suspended", "diminished", "augmented", "dyad", "power", "set-class", "unison", "empty").</param>
/// <param name="Extension">Extension label ("triad", "7th", "9th", "11th", "13th", "6th", "add", null).</param>
/// <param name="Alterations">Non-canonical tones like "#9", "b13", etc.</param>
/// <param name="SlashSuffix">Optional bass-note suffix ("/E"). Voicing-specific — do NOT use
///     for partition-invariant embedding dims.</param>
/// <param name="PatternName">Kebab-case pattern identifier, or null if fallback was used.</param>
/// <param name="MatchDistance">Sum of (missing + extra) intervals; 0 = exact match, -1 = fallback.</param>
/// <param name="IsNaturallyOccurring">True when the recognition produced an exact, natural match.</param>
public record CanonicalChordResult(
    string CanonicalName,
    string? Root,
    string Quality,
    string? Extension,
    string[] Alterations,
    string? SlashSuffix,
    string? PatternName,
    int MatchDistance,
    bool IsNaturallyOccurring)
{
    /// <summary>The full display name including slash notation when present.</summary>
    public string DisplayName => SlashSuffix is null
        ? CanonicalName
        : $"{CanonicalName}{SlashSuffix}";
}
