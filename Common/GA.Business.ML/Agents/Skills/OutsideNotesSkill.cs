namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Answers "why does &lt;note&gt; sound outside over &lt;chord&gt;?" — classifies a single
/// pitch against a chord as a <b>chord tone</b>, an <b>available tension</b>, or an
/// <b>avoid note</b>, and explains why with the scale-degree label and the interval.
/// Pure domain compute; no LLM at the skill layer. Confidence = 1.0.
/// </summary>
/// <remarks>
/// <para>
/// Built 2026-07-20 to close the BACKLOG North Star item "Why does this sound
/// outside?". Reuses <see cref="ChordVocabulary"/> for chord-symbol → interval
/// formula and root → pitch class, so the chord model stays consistent with
/// <see cref="ChordInfoSkill"/> and the MCP chord tools.
/// </para>
/// <para>
/// The classification rule is the standard jazz-pedagogy definition and is
/// derived <b>purely from the chord tones</b> — no per-quality tension table:
/// a non-chord-tone that sits a <b>semitone above a chord tone</b> is an
/// <i>avoid note</i> (it forms a b9 clash with that chord tone); any other
/// non-chord-tone is an <i>available tension</i>. Worked: over Cmaj7 {C,E,G,B},
/// F (the 11) is a semitone above E (the 3rd) → avoid; A (the 13) is a whole
/// step above G → tension. This correctly makes the natural 11 an avoid note
/// over major/dominant chords but NOT over minor chords (m7 has no major 3rd
/// for the 11 to clash with).
/// </para>
/// <para>
/// v1 handles the "&lt;note&gt; … over/against/on &lt;chord&gt;" shape (a single
/// concrete note and a single concrete chord). Degree-word queries ("why does
/// the 4th clash") and note-vs-key ("F outside in C major") are out of scope.
/// </para>
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("OutsideNotes", "harmony")]
public sealed partial class OutsideNotesSkill(ILogger<OutsideNotesSkill> logger) : IOrchestratorSkill
{
    public string Name => "OutsideNotes";

    public string Description =>
        "Explains why a single note sounds inside or outside over a chord: " +
        "classifies the note as a chord tone, an available tension (9, #11, 13), " +
        "or an avoid note, with the scale-degree label and the interval. E.g. " +
        "'why does F sound outside over Cmaj7' → F is the 11, a semitone above " +
        "the 3rd (E), so it clashes. Pure lookup — no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "why does F sound outside over Cmaj7",
        "why does that note clash over the chord",
        "is F an avoid note over Cmaj7",
        "what is F over G7",
        "why does the b9 sound so tense over C7",
        "is A a chord tone or a tension over Cmaj7",
        "why does Bb sound outside over C major",
        "is F# an avoid note or a tension over Cmaj7",
        // "clash" phrasing — without a note-over-chord anchor carrying the word,
        // held-out "why does X clash over Y" drifted to GrothendieckDelta (which
        // scores harmonic clash BETWEEN chords, not a note against a chord).
        "why does F clash over a Cmaj7 chord",
        "why does the note clash over this chord",
    ];

    // Intent keywords — must co-occur with a preposition + a chord token for
    // the CanHandle fallback to fire, so ordinary chatter can't trigger it.
    private static readonly string[] OutsideKeywords =
    [
        "outside", "avoid", "tension", "clash", "clashes", "dissonant",
        "dissonance", "sound bad", "sounds bad", "sound off", "sounds off",
        "sound wrong", "chord tone", "why does", "inside or outside",
    ];

    private static readonly string[] Prepositions = ["over", "against", "on top of", " on "];

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var q = message.ToLowerInvariant();
        var hasIntent = OutsideKeywords.Any(k => q.Contains(k, StringComparison.Ordinal));
        if (!hasIntent) return false;
        var hasPrep = Prepositions.Any(p => q.Contains(p, StringComparison.Ordinal));
        if (!hasPrep) return false;
        // Needs a real chord token (root + something) to classify against.
        return ChordAfterPrepRegex().IsMatch(message);
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var parsed = ParseNoteOverChord(message);
        if (parsed is null)
            return Task.FromResult(CannotHandle());

        var (notePc, noteName, rootPc, rootName, quality) = parsed.Value;
        var verdict = Classify(rootPc, quality, notePc);

        var chordLabel = $"{rootName}{QualityDisplaySuffix(quality)}";
        var sb = new StringBuilder();
        sb.AppendLine($"**{noteName}** over **{chordLabel}**: {verdict.Headline}.");
        sb.AppendLine();
        sb.AppendLine(verdict.Explanation);

        logger.LogDebug("OutsideNotesSkill: {Note} over {Root} {Quality} → {Kind} ({Degree})",
            noteName, rootName, quality, verdict.Kind, verdict.DegreeLabel);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = $"skill.{Name.ToLowerInvariant()}",
            Result     = sb.ToString(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"Note: {noteName} (pitch class {notePc})",
                $"Chord: {chordLabel} — tones {string.Join(", ", ChordToneNames(rootPc, quality))}",
                $"Relation: {verdict.DegreeLabel}, {verdict.Kind}",
            ],
            Assumptions = ["Single note classified against a single chord; no key context assumed."],
            Data = new
            {
                note = noteName,
                chord = chordLabel,
                degree = verdict.DegreeLabel,
                classification = verdict.Kind.ToString(),
            },
        });
    }

    // ── Classification (the domain core) ─────────────────────────────────────

    internal enum RelationKind { ChordTone, Tension, Avoid }

    internal readonly record struct Verdict(
        RelationKind Kind, string DegreeLabel, string Headline, string Explanation);

    /// <summary>
    /// Classify pitch class <paramref name="notePc"/> against the chord rooted at
    /// <paramref name="rootPc"/> with the given canonical <paramref name="quality"/>.
    /// Rule: chord tone if the interval is in the formula; otherwise an avoid note
    /// if it sits a semitone above any chord tone; otherwise an available tension.
    /// </summary>
    internal static Verdict Classify(int rootPc, string quality, int notePc)
    {
        var formula = ChordVocabulary.GetFormula(quality);
        var chordPcs = formula.Intervals.Select(i => ((i % 12) + 12) % 12).ToHashSet();
        var rel = ((notePc - rootPc) % 12 + 12) % 12;

        if (chordPcs.Contains(rel))
        {
            var function = ChordToneFunction(formula, rel);
            return new Verdict(
                RelationKind.ChordTone,
                function,
                $"a chord tone — the {function}",
                $"It's part of the chord itself (the {function}), so it sounds fully consonant — " +
                "as inside as a note can be over this chord.");
        }

        var degree = ExtensionLabel(rel);
        // Avoid note = a semitone above a chord tone (forms a b9 clash with it).
        var clashPc = chordPcs.FirstOrDefault(ct => (ct + 1) % 12 == rel, -1);
        if (clashPc >= 0)
        {
            var clashName = ChordToneFunction(formula, clashPc);
            // On dominant chords the b9/#9/#11/b13 half-step clashes are the
            // classic "altered" tensions — still tense, but used deliberately,
            // so the advice differs from a plain avoid note over a major chord.
            var isDominant = quality.StartsWith("dominant", StringComparison.Ordinal)
                          || quality == "altered dominant";
            var advice = isDominant
                ? "That half-step rub is why it sounds outside — but over a dominant chord it's " +
                  $"exactly the kind of altered tension players reach for ({degree} on the V), so " +
                  "it's usable if you resolve it, not a note to simply avoid."
                : "That half-step rub is the classic \"avoid note\" clash: play it as a fast " +
                  "passing tone, but don't land or sustain on it.";
            return new Verdict(
                RelationKind.Avoid,
                degree,
                $"an avoid note — the {degree}",
                $"It's the {degree}, sitting a semitone above the {clashName} of the chord. " +
                advice);
        }

        return new Verdict(
            RelationKind.Tension,
            degree,
            $"an available tension — the {degree}",
            $"It's the {degree} — a non-chord tone, but it isn't a semitone above any chord " +
            "tone, so it adds colour without clashing. Safe to sustain as an extension.");
    }

    /// <summary>Name the chord-tone function for an in-chord relative pitch (e.g. "major third", "5th").</summary>
    private static string ChordToneFunction(ChordFormula formula, int rel)
    {
        for (var i = 0; i < formula.Intervals.Count; i++)
        {
            if (((formula.Intervals[i] % 12) + 12) % 12 == rel)
                return formula.IntervalNames[i];
        }
        return ExtensionLabel(rel);
    }

    /// <summary>Scale-degree label for a relative pitch class (root-relative, 0–11).</summary>
    internal static string ExtensionLabel(int rel) => (((rel % 12) + 12) % 12) switch
    {
        0  => "root",
        1  => "b9 (flat ninth)",
        2  => "9 (ninth)",
        3  => "#9 (sharp ninth)",
        4  => "major 3rd",
        5  => "11 (natural eleventh)",
        6  => "#11 (sharp eleventh)",
        7  => "5th",
        8  => "b13 (flat thirteenth)",
        9  => "13 (thirteenth)",
        10 => "b7 (minor seventh)",
        _  => "major 7th",   // 11
    };

    private static IReadOnlyList<string> ChordToneNames(int rootPc, string quality)
    {
        var formula = ChordVocabulary.GetFormula(quality);
        return [.. formula.Intervals.Select(i => PcName(((rootPc + i) % 12 + 12) % 12))];
    }

    private static readonly string[] SharpNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static string PcName(int pc) => SharpNames[((pc % 12) + 12) % 12];

    private static string QualityDisplaySuffix(string quality) => quality switch
    {
        "major" => "",
        "minor" => "m",
        _       => " " + quality,
    };

    // ── Parsing ──────────────────────────────────────────────────────────────

    internal readonly record struct ParsedQuery(
        int NotePc, string NoteName, int RootPc, string RootName, string Quality);

    /// <summary>
    /// Parse "&lt;note&gt; … over/against/on &lt;chord&gt;". Splits on the last
    /// preposition, reads the chord from the right side and the note from the left.
    /// Returns null if either can't be resolved.
    /// </summary>
    internal static ParsedQuery? ParseNoteOverChord(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;

        // Locate the chord after a preposition (over / against / on).
        var chordMatch = ChordAfterPrepRegex().Match(message);
        if (!chordMatch.Success) return null;

        var rootRaw = ChordVocabulary.NormalizeRoot(chordMatch.Groups["root"].Value);
        if (!ChordVocabulary.TryGetPitchClass(rootRaw, out var rootPc)) return null;
        var quality = ChordVocabulary.NormalizeQuality(chordMatch.Groups["qual"].Value);

        // The note is the LAST note-shaped token to the LEFT of the preposition,
        // so we don't accidentally read the chord root as the note.
        var prepStart = message.LastIndexOf(chordMatch.Groups["prep"].Value,
            chordMatch.Index + chordMatch.Groups["prep"].Length, StringComparison.OrdinalIgnoreCase);
        var left = prepStart > 0 ? message[..prepStart] : message[..chordMatch.Index];

        Match? noteMatch = null;
        foreach (Match m in NoteTokenRegex().Matches(left))
            noteMatch = m;
        if (noteMatch is null) return null;

        var noteRaw = ChordVocabulary.NormalizeRoot(noteMatch.Groups["note"].Value);
        if (!ChordVocabulary.TryGetPitchClass(noteRaw, out var notePc)) return null;

        return new ParsedQuery(notePc, noteRaw, rootPc, rootRaw, quality);
    }

    // "<prep> <root><quality>" — prep is over/against/on; root is A–G + optional
    // accidental; quality is the trailing chord-symbol run (letters/digits/#/b/°/ø/+).
    [GeneratedRegex(@"(?<prep>\bover\b|\bagainst\b|\bon\b)\s+(?:a\s+|an\s+|the\s+)?(?<root>[A-G][#b]?)(?<qual>(?:maj|min|m|M|dim|aug|sus|add|alt|ø|°|Δ|\+|\d|#|b)*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ChordAfterPrepRegex();

    // A standalone note token: case-sensitive root letter + optional accidental,
    // not immediately followed by a chord-quality character (so "Cmaj7" on the
    // left isn't read as the note "C").
    [GeneratedRegex(@"\b(?<note>[A-G][#b]?)(?![A-Za-z0-9#])")]
    private static partial Regex NoteTokenRegex();

    // ── Response helpers ─────────────────────────────────────────────────────

    private AgentResponse CannotHandle() => new()
    {
        AgentId    = $"skill.{Name.ToLowerInvariant()}",
        Result     =
            "Tell me a single note and a single chord and I'll say whether it's a chord " +
            "tone, a tension, or an avoid note — e.g. \"why does F sound outside over Cmaj7\" " +
            "or \"is A a tension over Cmaj7\".",
        Confidence = 0.1f,
        Evidence   = ["OutsideNotesSkill: could not parse a <note> over <chord> pair"],
        Assumptions = ["Query did not match the '<note> ... over <chord>' shape."],
    };
}
