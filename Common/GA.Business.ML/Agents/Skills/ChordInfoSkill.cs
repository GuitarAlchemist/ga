namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;

/// <summary>
/// Answers basic chord-note questions from deterministic interval formulas.
/// </summary>
[GuitarAlchemist.Registry.GaSkill("ChordInfo", "chord")]
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

        // Chord-quality-noun phrasings (issue #555, 2026-07-20). Every anchor
        // above is chord-SYMBOL-shaped ("Dm7", "Cmaj7", "F#m7b5") or
        // "what chord is <notes>". None covered "what notes are in a
        // <spelled-out quality> triad/chord" — while ScaleInfoSkill carried
        // three anchors of exactly that shape ("What notes are in C major?",
        // "Show me the notes in C major", "List the notes in Bb major"). So
        // the product's most basic question routed to the scale skill at 0.90
        // confidence and answered "C major triad" with the 7-note SCALE.
        //
        // These anchor on the chord-quality NOUN (triad / chord), never on a
        // bare "<root> <quality>" — bare forms are genuinely ambiguous and
        // belong to ScaleInfoSkill, which the corpus depends on
        // ("notes in c major" must stay a scale answer).
        //
        // Measured with bge-large at the production 0.64 threshold against a
        // 20-query set (mostly held out from these anchors): 15/20 -> 19/20,
        // no scale-side regressions. Harness intentionally included held-out
        // variants because an anchor scores 1.000 against itself and would
        // otherwise flatter the result.
        "what notes are in a C major triad",
        "what are the notes of an A minor triad",
        "what notes make up a G7 chord",
        "which notes form a B diminished triad",
        "spell a G7 chord",
        "what notes are in an E major chord",
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
        var parsed = TryParse(message);
        if (parsed is null && ReadQuestionNotes(message) is { } question)
        {
            // A singular question with a full note set names one chord ("what chord is C E G?").
            // A plural question ("which chords contain C E G?") or a partial note set ("which
            // chord has both E and G#?") asks for every common chord that holds the notes (issue #888).
            parsed = question.Plural ? null : TryIdentifyNoteSet(question.Notes);
            if (parsed is null && question.Notes.Count >= 2)
            {
                return Task.FromResult(ListChordsContaining(question.Notes));
            }
        }

        if (parsed is null)
        {
            return Task.FromResult(AgentResponse.Decline(AgentIds.Theory, "Could not parse a chord name from your question."));
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
        // The article "a" ("what is a sus4 chord") is not a chord root.
        var chordQuestion = ChordQuestionRegex().Matches(message)
            .FirstOrDefault(match => !IsArticle(message, match.Groups["root"]));
        if (chordQuestion is not null)
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

    // A lowercase "a" followed by a space is the indefinite article; a chord root written in
    // lowercase sits against its quality ("a7", "am") or is capitalized ("A sus4").
    private static bool IsArticle(string message, Group root) =>
        root.Value == "a" && root.Index + 1 < message.Length && char.IsWhiteSpace(message[root.Index + 1]);

    /// <summary>
    /// The notes named after the verb of a note-set question ("which chords contain both E and
    /// G#?" → E, G#) and whether it asks about chords in the plural, or null when the message is
    /// not one. A lowercase "a" among capitalised note names is the article ("a C and an E"), not
    /// the note A.
    /// </summary>
    private static (IReadOnlyList<string> Notes, bool Plural)? ReadQuestionNotes(string message)
    {
        var question = NoteSetQuestionRegex().Match(message);
        if (!question.Success)
        {
            return null;
        }

        var tokens = NoteTokenRegex()
            .Matches(message, question.Index + question.Length)
            .Select(match => match.Groups["note"].Value)
            .ToList();
        var hasCapitalisedNote = tokens.Any(token => char.IsUpper(token[0]));

        var notes = tokens
            .Where(token => !(hasCapitalisedNote && token == "a"))
            .Select(ChordVocabulary.NormalizeRoot)
            .Where(ChordVocabulary.PitchClasses.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (notes, question.Groups["plural"].Success);
    }

    private static (string Root, string Quality)? TryIdentifyNoteSet(IReadOnlyList<string> notes)
    {
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

    // Triads, then sevenths, with the symbol a chord chart uses.
    private static readonly (string Quality, string Symbol)[] CommonChordQualities =
    [
        ("major", ""), ("minor", "m"), ("diminished", "dim"), ("augmented", "aug"),
        ("sus2", "sus2"), ("sus4", "sus4"),
        ("dominant 7", "7"), ("major 7", "maj7"), ("minor 7", "m7"),
        ("half-diminished", "m7b5"), ("diminished 7", "dim7"),
    ];

    private static readonly string[] SharpRoots = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] FlatRoots = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] ChartRoots = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    /// <summary>
    /// Every common triad and seventh chord holding all of <paramref name="notes"/>. Symmetric
    /// chords (augmented, diminished seventh) are listed once, from the first root that reaches
    /// the set. Roots the user named keep their spelling; other roots follow the accidentals the
    /// user wrote, or the usual chart spelling when they wrote none.
    /// </summary>
    private static AgentResponse ListChordsContaining(IReadOnlyList<string> notes)
    {
        var wanted = notes.Select(note => ChordVocabulary.PitchClasses[note]).ToHashSet();
        var roots = notes.Any(note => note.Length > 1 && note[1] == '#') ? SharpRoots
            : notes.Any(note => note.Length > 1 && note[1] == 'b') ? FlatRoots
            : ChartRoots;
        var firstPc = ChordVocabulary.PitchClasses[notes[0]];
        var rootOrder = notes.Select(note => ChordVocabulary.PitchClasses[note])
            .Concat(Enumerable.Range(0, 12).Select(step => (firstPc + step) % 12))
            .Distinct()
            .ToList();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var (quality, symbol) in CommonChordQualities)
        {
            var formula = ChordVocabulary.GetFormula(quality);
            foreach (var rootPc in rootOrder)
            {
                var chordPcs = formula.Intervals.Select(interval => (rootPc + interval) % 12).ToHashSet();
                // Same quality and same notes is the same chord under another root (Caug = Eaug);
                // Fsus2 and Csus4 share notes but are different chords, so both are listed.
                if (!wanted.IsSubsetOf(chordPcs) || !seen.Add($"{quality}:{string.Join(",", chordPcs.Order())}"))
                {
                    continue;
                }

                var root = notes.FirstOrDefault(note => ChordVocabulary.PitchClasses[note] == rootPc) ?? roots[rootPc];
                var tones = formula.Intervals.Zip(formula.LetterSteps, (interval, letterSteps) => Spell(root, rootPc + interval, letterSteps));
                lines.Add($"- **{root}{symbol}** ({string.Join(" ", tones)}) — {QualityDisplayName(formula.Quality)}");
            }
        }

        var named = JoinNotes(notes);
        var result = lines.Count == 0
            ? $"No common triad or seventh chord contains {named}."
            : $"Common chords that contain {named}:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";

        return new AgentResponse
        {
            AgentId = AgentIds.Theory,
            Result = result,
            Confidence = 1.0f,
            Evidence =
            [
                $"Notes: {string.Join(", ", notes)}",
                "Searched: major, minor, diminished, augmented, sus2 and sus4 triads; dominant, major, minor, half-diminished and diminished seventh chords",
                $"Matches: {lines.Count}"
            ],
            Assumptions = []
        };
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
    // Issue #888: also the plural and qualified forms ("which common chords contain both E and
    // G#?", "what chords have a C and an E"). "Are" stays out: "what chords are in C major" asks
    // for a key's chords, not for the chords holding some notes. Only neutral qualifiers are
    // accepted: a quality word ("which minor chords contain C and E?") narrows the question in a
    // way the list does not apply, so the skill leaves that question to another route.
    [GeneratedRegex(@"\b(?:which|what)\s+(?:(?:common|possible|basic|simple|standard|the)\s+){0,2}chord(?<plural>s)?\s+(?:is|contains?|containing|has|have|uses?|includes?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoteSetQuestionRegex();

    [GeneratedRegex(@"(?<![A-Za-z])(?<note>[A-Ga-g][#b]?)(?![A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex NoteTokenRegex();
}
