namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;

/// <summary>
/// Answers basic chord-note questions from deterministic interval formulas.
/// </summary>
public sealed partial class ChordInfoSkill(ILogger<ChordInfoSkill> logger) : IOrchestratorSkill
{
    private static readonly IReadOnlyDictionary<string, int> PitchClasses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0,
        ["C#"] = 1,
        ["Db"] = 1,
        ["D"] = 2,
        ["D#"] = 3,
        ["Eb"] = 3,
        ["E"] = 4,
        ["F"] = 5,
        ["F#"] = 6,
        ["Gb"] = 6,
        ["G"] = 7,
        ["G#"] = 8,
        ["Ab"] = 8,
        ["A"] = 9,
        ["A#"] = 10,
        ["Bb"] = 10,
        ["B"] = 11,
        ["B#"] = 0,
        ["Cb"] = 11,
        ["E#"] = 5,
        ["Fb"] = 4,
    };

    // NaturalLetters / NaturalPitchClasses moved to ChordSpelling (PR #102).
    // Spell() below also delegates so this skill and ChordMcpTools share
    // a single source of truth for enharmonic accounting.

    public string Name => "ChordInfo";

    public string Description =>
        "Lists the notes inside one named chord. Single-chord spelling " +
        "lookup: Cmaj7 returns C E G B, Dm7 returns D F A C, F#m7b5 " +
        "returns F# A C E. Pure domain computation, zero LLM calls.";

    // PR (post-baseline-2026-05-11 corpus v0.4) — added the "tell me
    // about <chord>" and "what makes a chord X" surfaces. Failing eval
    // prompts: "tell me about Dm7" → was routing to modes; "what makes
    // a chord a major seventh" → was routing to diatonicchords. Both
    // are spell-the-chord asks dressed in different phrasing.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What is a C major chord?",
        "What notes are in Dm7?",
        "Notes in an F minor chord",
        "What chord contains C E G?",
        "Spell a B7 chord",
        "What notes are in a Cmaj7?",
        "Tell me the tones in F#m7b5",
        "tell me about Dm7",
        "tell me about a Cmaj7 chord",
        "what makes a chord a major seventh",
        "what makes a chord diminished",
        "what makes a chord a dominant seventh",
        // v0.5 corpus expansion (2026-05-12): "anatomy of" / "break down"
        // / "add 9" / "sus" / "maj13" patterns weren't covered. These
        // were misrouting to diatonicchords because "chord" + extension
        // numerals pulled toward that intent.
        "anatomy of a D7sus4 chord",
        "break down Gmaj13 for me",
        "what is a C add 9 chord",
        "give me the notes of an F#m7b5",
        "tones in a Bb diminished seventh",
    ];

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return ChordQuestionRegex().IsMatch(message) ||
               (HasChordIntent(message) && CompactChordRegex().IsMatch(message)) ||
               NoteSetQuestionRegex().IsMatch(message);
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var parsed = TryParse(message) ?? TryIdentifyNoteSet(message);
        if (parsed is null)
        {
            return Task.FromResult(AgentResponse.CannotHelp(AgentIds.Theory, "Could not parse a chord name from your question."));
        }

        var (root, quality) = parsed.Value;
        if (!PitchClasses.TryGetValue(root, out var rootPc))
        {
            return Task.FromResult(AgentResponse.CannotHelp(AgentIds.Theory, $"I do not recognise \"{root}\" as a chord root."));
        }

        var formula = GetFormula(quality);
        var noteNames = formula.Intervals
            .Zip(formula.LetterSteps, (interval, letterSteps) => Spell(root, rootPc + interval, letterSteps))
            .ToList();

        logger.LogDebug(
            "ChordInfoSkill: resolved {Root} {Quality} -> [{Notes}]",
            root,
            formula.DisplayName,
            string.Join(", ", noteNames));

        return Task.FromResult(new AgentResponse
        {
            AgentId = AgentIds.Theory,
            Result = $"{root} {formula.DisplayName} contains {JoinNotes(noteNames)}.",
            Confidence = 1.0f,
            Evidence =
            [
                $"Root: {root}",
                $"Quality: {formula.DisplayName}",
                $"Intervals: {string.Join(", ", formula.IntervalNames)}",
                $"Notes: {string.Join(", ", noteNames)}"
            ],
            Assumptions = []
        });
    }

    private static (string Root, string Quality)? TryParse(string message)
    {
        var chordQuestion = ChordQuestionRegex().Match(message);
        if (chordQuestion.Success)
        {
            return (NormalizeRoot(chordQuestion.Groups["root"].Value), NormalizeQuality(chordQuestion.Groups["quality"].Value));
        }

        var compact = CompactChordRegex().Match(message);
        if (compact.Success)
        {
            return (NormalizeRoot(compact.Groups["root"].Value), NormalizeQuality(compact.Groups["quality"].Value));
        }

        return null;
    }

    private static (string Root, string Quality)? TryIdentifyNoteSet(string message)
    {
        if (!NoteSetQuestionRegex().IsMatch(message))
        {
            return null;
        }

        var notes = NoteTokenRegex()
            .Matches(message)
            .Select(match => NormalizeRoot(match.Groups["note"].Value))
            .Where(PitchClasses.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (notes.Count is < 3 or > 4)
        {
            return null;
        }

        var pitchClasses = notes
            .Select(note => PitchClasses[note])
            .Order()
            .ToArray();

        foreach (var root in notes)
        {
            var rootPc = PitchClasses[root];
            foreach (var (quality, formula) in CandidateFormulas())
            {
                if (formula.Intervals.Count != pitchClasses.Length)
                {
                    continue;
                }

                var candidate = formula.Intervals
                    .Select(interval => ((rootPc + interval) % 12 + 12) % 12)
                    .Order()
                    .ToArray();

                if (candidate.SequenceEqual(pitchClasses))
                {
                    return (root, quality);
                }
            }
        }

        return null;
    }

    private static string NormalizeRoot(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 1
            ? trimmed.ToUpperInvariant()
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string NormalizeQuality(string value)
    {
        // Strip whitespace + lowercase; the parser writes the canonical form
        // that NormalizeQuality returns into Quality, and GetFormula keys on
        // that form. Adding a new chord = entry here + entry in GetFormula +
        // entry in CandidateFormulas.
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
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
            "7" or "dominant" or "dominant 7" => "dominant 7",
            "maj7" or "major 7" or "ma7" or "Δ7" => "major 7",
            "m7" or "min7" or "minor 7" or "-7" => "minor 7",
            "dim7" or "°7" => "diminished 7",
            "m7b5" or "ø" or "ø7" or "half-diminished" or "half diminished" or "minor 7 flat 5" => "half-diminished",
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
            _ => normalized
        };
    }

    private static ChordFormula GetFormula(string quality) =>
        quality switch
        {
            "minor" => new ChordFormula("minor chord", [0, 3, 7], [0, 2, 4], ["root", "minor third", "perfect fifth"]),
            "diminished" => new ChordFormula("diminished chord", [0, 3, 6], [0, 2, 4], ["root", "minor third", "diminished fifth"]),
            "augmented" => new ChordFormula("augmented chord", [0, 4, 8], [0, 2, 4], ["root", "major third", "augmented fifth"]),
            "power" => new ChordFormula("power chord", [0, 7], [0, 4], ["root", "perfect fifth"]),
            "sus2" => new ChordFormula("sus2 chord", [0, 2, 7], [0, 1, 4], ["root", "major second", "perfect fifth"]),
            "sus4" => new ChordFormula("sus4 chord", [0, 5, 7], [0, 3, 4], ["root", "perfect fourth", "perfect fifth"]),
            "add9" => new ChordFormula("add9 chord", [0, 4, 7, 14], [0, 2, 4, 8], ["root", "major third", "perfect fifth", "ninth"]),
            "major 6" => new ChordFormula("major 6 chord", [0, 4, 7, 9], [0, 2, 4, 5], ["root", "major third", "perfect fifth", "major sixth"]),
            "minor 6" => new ChordFormula("minor 6 chord", [0, 3, 7, 9], [0, 2, 4, 5], ["root", "minor third", "perfect fifth", "major sixth"]),
            "dominant 7" => new ChordFormula("dominant 7 chord", [0, 4, 7, 10], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "minor seventh"]),
            "major 7" => new ChordFormula("major 7 chord", [0, 4, 7, 11], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "major seventh"]),
            "minor 7" => new ChordFormula("minor 7 chord", [0, 3, 7, 10], [0, 2, 4, 6], ["root", "minor third", "perfect fifth", "minor seventh"]),
            "diminished 7" => new ChordFormula("diminished 7 chord", [0, 3, 6, 9], [0, 2, 4, 6], ["root", "minor third", "diminished fifth", "diminished seventh"]),
            "half-diminished" => new ChordFormula("half-diminished chord (m7b5)", [0, 3, 6, 10], [0, 2, 4, 6], ["root", "minor third", "diminished fifth", "minor seventh"]),
            "dominant 7 flat 5" => new ChordFormula("dominant 7 flat 5 chord", [0, 4, 6, 10], [0, 2, 4, 6], ["root", "major third", "diminished fifth", "minor seventh"]),
            "dominant 7 sharp 5" => new ChordFormula("dominant 7 sharp 5 chord (augmented dominant)", [0, 4, 8, 10], [0, 2, 4, 6], ["root", "major third", "augmented fifth", "minor seventh"]),
            "dominant 7 flat 9" => new ChordFormula("dominant 7 flat 9 chord", [0, 4, 7, 10, 13], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "minor seventh", "minor ninth"]),
            "dominant 7 sharp 9" => new ChordFormula("dominant 7 sharp 9 chord (\"Hendrix chord\")", [0, 4, 7, 10, 15], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "minor seventh", "augmented ninth"]),
            "altered dominant" => new ChordFormula("altered dominant chord (7alt)", [0, 4, 6, 8, 10, 13, 15], [0, 2, 3, 4, 6, 8, 8], ["root", "major third", "flat fifth", "sharp fifth", "minor seventh", "flat ninth", "sharp ninth"]),
            "dominant 9" => new ChordFormula("dominant 9 chord", [0, 4, 7, 10, 14], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "minor seventh", "major ninth"]),
            "major 9" => new ChordFormula("major 9 chord", [0, 4, 7, 11, 14], [0, 2, 4, 6, 8], ["root", "major third", "perfect fifth", "major seventh", "major ninth"]),
            "minor 9" => new ChordFormula("minor 9 chord", [0, 3, 7, 10, 14], [0, 2, 4, 6, 8], ["root", "minor third", "perfect fifth", "minor seventh", "major ninth"]),
            "dominant 11" => new ChordFormula("dominant 11 chord", [0, 4, 7, 10, 14, 17], [0, 2, 4, 6, 8, 10], ["root", "major third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh"]),
            "major 11" => new ChordFormula("major 11 chord", [0, 4, 7, 11, 14, 17], [0, 2, 4, 6, 8, 10], ["root", "major third", "perfect fifth", "major seventh", "major ninth", "perfect eleventh"]),
            "minor 11" => new ChordFormula("minor 11 chord", [0, 3, 7, 10, 14, 17], [0, 2, 4, 6, 8, 10], ["root", "minor third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh"]),
            "dominant 13" => new ChordFormula("dominant 13 chord", [0, 4, 7, 10, 14, 17, 21], [0, 2, 4, 6, 8, 10, 12], ["root", "major third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh", "major thirteenth"]),
            "major 13" => new ChordFormula("major 13 chord", [0, 4, 7, 11, 14, 17, 21], [0, 2, 4, 6, 8, 10, 12], ["root", "major third", "perfect fifth", "major seventh", "major ninth", "perfect eleventh", "major thirteenth"]),
            "minor 13" => new ChordFormula("minor 13 chord", [0, 3, 7, 10, 14, 17, 21], [0, 2, 4, 6, 8, 10, 12], ["root", "minor third", "perfect fifth", "minor seventh", "major ninth", "perfect eleventh", "major thirteenth"]),
            _ => new ChordFormula("major chord", [0, 4, 7], [0, 2, 4], ["root", "major third", "perfect fifth"])
        };

    private static IEnumerable<(string Quality, ChordFormula Formula)> CandidateFormulas()
    {
        // Note-set identification (TryIdentifyNoteSet) iterates these and
        // matches when the given pitch classes equal a formula's transposed
        // intervals. Order doesn't affect correctness; keep simple triads
        // first so basic chord identification is fast.
        yield return ("major", GetFormula("major"));
        yield return ("minor", GetFormula("minor"));
        yield return ("diminished", GetFormula("diminished"));
        yield return ("augmented", GetFormula("augmented"));
        yield return ("sus2", GetFormula("sus2"));
        yield return ("sus4", GetFormula("sus4"));
        yield return ("major 6", GetFormula("major 6"));
        yield return ("minor 6", GetFormula("minor 6"));
        yield return ("dominant 7", GetFormula("dominant 7"));
        yield return ("major 7", GetFormula("major 7"));
        yield return ("minor 7", GetFormula("minor 7"));
        yield return ("diminished 7", GetFormula("diminished 7"));
        yield return ("half-diminished", GetFormula("half-diminished"));
    }

    private static bool HasChordIntent(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("chord", StringComparison.Ordinal) ||
               normalized.Contains("triad", StringComparison.Ordinal) ||
               normalized.Contains("note", StringComparison.Ordinal);
    }

    // Delegates to the shared helper (PR #102) so this skill and
    // ChordMcpTools cannot drift on enharmonic accounting.
    private static string Spell(string root, int pitchClass, int letterSteps) =>
        GA.Business.ML.Agents.Mcp.ChordSpelling.Spell(root, pitchClass, letterSteps);

    private static string JoinNotes(IReadOnlyList<string> notes) =>
        notes.Count switch
        {
            0 => string.Empty,
            1 => notes[0],
            2 => $"{notes[0]} and {notes[1]}",
            _ => $"{string.Join(", ", notes.Take(notes.Count - 1))}, and {notes[^1]}"
        };

    [GeneratedRegex(@"\b(?<root>[A-Ga-g][#b]?)\s*(?<quality>major 13|minor 13|dominant 13|major 11|minor 11|dominant 11|major 9|minor 9|dominant 9|major 7|minor 7|dominant 7|major 6|minor 6|half[- ]diminished|altered dominant|major|minor|maj|min|diminished|dim|augmented|aug|sus[24]?|add9|7alt|alt|13|11|9|7|6)?\s*(?:chord|triad)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ChordQuestionRegex();

    // Compact form supports: triads (maj/min/m/dim/aug/+, sus2/sus4, add9,
    // power 5/no3), 6 / m6, 7th family (maj7/m7/7/dim7/m7b5/7b5/7#5/7b9/7#9),
    // upper extensions (9/maj9/m9/11/maj11/m11/13/maj13/m13), and altered
    // dominant (7alt/alt). Order matters — LONGER alternations match first,
    // so "maj13" wins over "maj" and "m7b5" wins over "m7" / "m".
    //
    // Quality is REQUIRED (not optional). Without this, bare letters like
    // "a" in "Show me a Cmaj9 chord" matched as root="a" quality="" → "A
    // major" before the regex ever reached the real "Cmaj9". Caught
    // 2026-05-13 during the extended-chord-parser probe.
    [GeneratedRegex(@"\b(?<root>[A-Ga-g][#b]?)(?<quality>maj13|min13|m13|maj11|min11|m11|maj9|min9|m9|maj7|min7|m7b5|m7|dim7|7b5|7#5|7b9|7#9|7alt|alt|13|11|9|7|6|m6|maj|min|dim|aug|sus2|sus4|sus|add9|m|\+)\b(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompactChordRegex();

    [GeneratedRegex(@"\b(?:which|what)\s+chord\s+(?:contains|has|uses)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoteSetQuestionRegex();

    [GeneratedRegex(@"(?<![A-Za-z])(?<note>[A-Ga-g][#b]?)(?![A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex NoteTokenRegex();

    private sealed record ChordFormula(
        string DisplayName,
        IReadOnlyList<int> Intervals,
        IReadOnlyList<int> LetterSteps,
        IReadOnlyList<string> IntervalNames);
}
