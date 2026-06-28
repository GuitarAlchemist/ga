namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;

/// <summary>
/// Answers basic chord-note questions from deterministic interval formulas.
/// </summary>
[GuitarAlchemist.Registry.GaSkill("ChordInfoSkill", "chord")]
public sealed partial class ChordInfoSkill(ILogger<ChordInfoSkill> logger) : IOrchestratorSkill
{
    // Root map, root/quality normalization, and the interval formula table live in the shared
    // ChordVocabulary seam (architecture-review candidate #3) — same source as ChordMcpTools, so the
    // two can't drift on quality parsing (e.g. the "CM" → C-major case-sensitivity fix).
    // NaturalLetters / NaturalPitchClasses moved to ChordSpelling (PR #102). Spell() below also
    // delegates so this skill and ChordMcpTools share a single source of truth for enharmonic accounting.

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
        // Chord-identification-from-notes phrasings (BACKLOG dealbreaker #5,
        // 2026-05-14). Without these, "what chord is F A C E" misrouted to
        // skill.chordsubstitution because the substitution skill's
        // alternation patterns matched "F" + "A" as two chords. Anchored
        // examples make the chordinfo intent dominate for these queries.
        "What chord is C E G",
        "What chord is F A C E",
        "Which chord contains the notes G B D F",
        "What chord is C E G Bb D",
        // v0.5 corpus expansion (2026-05-12): "anatomy of" / "break down"
        // / "add 9" / "sus" / "maj13" patterns weren't covered. These
        // were misrouting to diatonicchords because "chord" + extension
        // numerals pulled toward that intent.
        "anatomy of a D7sus4 chord",
        "break down Gmaj13 for me",
        "what is a C add 9 chord",
        "give me the notes of an F#m7b5",
        "tones in a Bb diminished seventh",
        // Bare-symbol queries without the word "chord" — "What is C7b9",
        // "What is Cmaj9", "What is Dm7b5". Without these, the semantic
        // router scored "What is C7b9" below threshold against the longer
        // examples and fell through to the LLM cascade (corpus regression
        // #216, 2026-05-16). The CompactChordRegex already handles the
        // parse; these examples just route the prompt to the right skill.
        "What is C7b9",
        "What is Cmaj9",
        "What is Dm7b5",
        "What is F#m7",
        "What is Bbdim7",
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
        if (!ChordVocabulary.TryGetPitchClass(root, out var rootPc))
        {
            return Task.FromResult(AgentResponse.CannotHelp(AgentIds.Theory, $"I do not recognise \"{root}\" as a chord root."));
        }

        var formula     = ChordVocabulary.GetFormula(quality);
        var displayName = QualityDisplayName(formula.Quality);
        var noteNames   = formula.Intervals
            .Zip(formula.LetterSteps, (interval, letterSteps) => Spell(root, rootPc + interval, letterSteps))
            .ToList();

        logger.LogDebug(
            "ChordInfoSkill: resolved {Root} {Quality} -> [{Notes}]",
            root,
            displayName,
            string.Join(", ", noteNames));

        return Task.FromResult(new AgentResponse
        {
            AgentId = AgentIds.Theory,
            Result = $"{root} {displayName} contains {JoinNotes(noteNames)}.",
            Confidence = 1.0f,
            Evidence =
            [
                $"Root: {root}",
                $"Quality: {displayName}",
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
            return (ChordVocabulary.NormalizeRoot(chordQuestion.Groups["root"].Value), ChordVocabulary.NormalizeQuality(chordQuestion.Groups["quality"].Value));
        }

        var compact = CompactChordRegex().Match(message);
        if (compact.Success)
        {
            return (ChordVocabulary.NormalizeRoot(compact.Groups["root"].Value), ChordVocabulary.NormalizeQuality(compact.Groups["quality"].Value));
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
            .Select(match => ChordVocabulary.NormalizeRoot(match.Groups["note"].Value))
            .Where(ChordVocabulary.PitchClasses.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Accept 3–5 note sets. 3 covers triads + sus, 4 covers 7th-family
        // and add9, 5 covers 9th-family (dominant/major/minor 9). Cap at 5
        // because 6+ note sets (11th/13th) have multiple legitimate
        // identifications and the first-match algorithm can't pick a
        // "best" answer without voicing context.
        if (notes.Count is < 3 or > 5)
        {
            return null;
        }

        var pitchClasses = notes
            .Select(note => ChordVocabulary.PitchClasses[note])
            .Order()
            .ToArray();

        foreach (var root in notes)
        {
            var rootPc = ChordVocabulary.PitchClasses[root];
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

    // Presentation only: expand a terse canonical quality from ChordVocabulary to this skill's prose
    // label. Most are "{quality} chord"; the four with parenthetical aliases keep their exact wording.
    private static string QualityDisplayName(string quality) => quality switch
    {
        "half-diminished"    => "half-diminished chord (m7b5)",
        "dominant 7 sharp 5" => "dominant 7 sharp 5 chord (augmented dominant)",
        "dominant 7 sharp 9" => "dominant 7 sharp 9 chord (\"Hendrix chord\")",
        "altered dominant"   => "altered dominant chord (7alt)",
        _                    => $"{quality} chord",
    };

    private static IEnumerable<(string Quality, ChordFormula Formula)> CandidateFormulas()
    {
        // Note-set identification (TryIdentifyNoteSet) iterates these and
        // matches when the given pitch classes equal a formula's transposed
        // intervals. Order doesn't affect correctness; keep simple triads
        // first so basic chord identification is fast.
        yield return ("major", ChordVocabulary.GetFormula("major"));
        yield return ("minor", ChordVocabulary.GetFormula("minor"));
        yield return ("diminished", ChordVocabulary.GetFormula("diminished"));
        yield return ("augmented", ChordVocabulary.GetFormula("augmented"));
        yield return ("sus2", ChordVocabulary.GetFormula("sus2"));
        yield return ("sus4", ChordVocabulary.GetFormula("sus4"));
        yield return ("major 6", ChordVocabulary.GetFormula("major 6"));
        yield return ("minor 6", ChordVocabulary.GetFormula("minor 6"));
        yield return ("dominant 7", ChordVocabulary.GetFormula("dominant 7"));
        yield return ("major 7", ChordVocabulary.GetFormula("major 7"));
        yield return ("minor 7", ChordVocabulary.GetFormula("minor 7"));
        yield return ("diminished 7", ChordVocabulary.GetFormula("diminished 7"));
        yield return ("half-diminished", ChordVocabulary.GetFormula("half-diminished"));
        // 4-note add-9 and 5-note 9th-family for note-set identification.
        // BACKLOG dealbreaker #5 — "what chord is C E G Bb D" → "C9".
        yield return ("add9", ChordVocabulary.GetFormula("add9"));
        yield return ("dominant 9", ChordVocabulary.GetFormula("dominant 9"));
        yield return ("major 9", ChordVocabulary.GetFormula("major 9"));
        yield return ("minor 9", ChordVocabulary.GetFormula("minor 9"));
        // 7-with-altered-fifth covers e.g. Cmaj7 with #5 in voicings;
        // common enough in jazz that "what chord is C E G# B" should ID.
        yield return ("dominant 7 flat 5", ChordVocabulary.GetFormula("dominant 7 flat 5"));
        yield return ("dominant 7 sharp 5", ChordVocabulary.GetFormula("dominant 7 sharp 5"));
    }

    private static bool HasChordIntent(string message)
    {
        var normalized = message.ToLowerInvariant();
        return normalized.Contains("chord", StringComparison.Ordinal) ||
               normalized.Contains("triad", StringComparison.Ordinal) ||
               normalized.Contains("note", StringComparison.Ordinal) ||
               // Question lead-ins that don't carry the word "chord" but
               // still identify a chord-spelling intent — e.g. "what is
               // C7b9", "what's Dm9", "tell me about Bbsus2", "describe
               // F#m7b5". Without this the prompt routes to the LLM
               // cascade and times out (corpus regression #216, 2026-05-16).
               QuestionLeadInRegex().IsMatch(normalized);
    }

    // Lead-in patterns that signal "ask about a single noun". Combined with
    // CompactChordRegex matching the noun, this catches "what is C7b9"-style
    // bare-symbol queries.
    [GeneratedRegex(@"^\s*(what\s+is|what's|whats|tell\s+me\s+about|describe)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuestionLeadInRegex();

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
    [GeneratedRegex(@"\b(?<root>[A-Ga-g][#b]?)(?<quality>maj13|min13|m13|maj11|min11|m11|maj9|min9|m9|maj7|min7|maj6|min6|m6|m7b5|m7|dim7|7b5|7#5|7b9|7#9|7alt|alt|13|11|9|7|6|maj|min|dim|aug|sus2|sus4|sus|add9|m|\+)\b(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CompactChordRegex();

    // "what/which chord (is|contains|has|uses) X Y Z" — accepts both the
    // chord-identification phrasing and the prior "contains/has/uses"
    // patterns. The "is" branch was missing 2026-05-14 — caught by the
    // multi-LLM correctness review's testing-gap note on PR #210.
    [GeneratedRegex(@"\b(?:which|what)\s+chord\s+(?:is|contains|has|uses)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoteSetQuestionRegex();

    [GeneratedRegex(@"(?<![A-Za-z])(?<note>[A-Ga-g][#b]?)(?![A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex NoteTokenRegex();
}
