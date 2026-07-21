namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.ML.Search;

/// <summary>
/// Answers "what scale can I use to solo over X" style prompts. Wraps the
/// typed chord parser to extract a chord symbol from the user query, infers
/// the chord quality, and returns matching modes / scales drawn from a
/// canonical chord-scale mapping. Pure domain compute; no LLM at the skill
/// layer.
/// </summary>
/// <remarks>
/// <para>
/// <b>v2 (2026-07-20)</b> adds the progression path: when the query contains a
/// run of two or more chord symbols ("Am F C G"), each chord is classified
/// independently and returned with its arpeggio and chord-scale candidates.
/// This closes the BACKLOG North Star item "which arpeggio fits this chord
/// progression?".
/// </para>
/// <para>
/// The per-chord classification is deliberately <b>key-agnostic</b>. The
/// obvious alternative — infer the key, then map each chord to a scale degree
/// and hand back that degree's mode — is what
/// <c>GaMcpServer/Tools/GuitaristProblemTools.cs</c> does, and it is wrong for
/// any borrowed or secondary chord: it locates the degree by root pitch-class
/// alone, so an A major chord in C major is still reported as degree vi and
/// handed Aeolian, putting a natural C against the chord's C#. Reusing
/// <see cref="InferQuality"/> per chord cannot make that mistake, because it
/// reads the quality the user actually wrote.
/// </para>
/// <para>
/// Roman-numeral queries ("how do I solo over a ii-V-I") are still out of
/// scope — there are no chord symbols to classify without a stated key. The
/// no-chord branch says so explicitly rather than guessing.
/// </para>
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("Improvisation", "scale")]
public sealed partial class ImprovisationSkill(
    ILogger<ImprovisationSkill> logger,
    IMusicalQueryExtractor extractor) : IOrchestratorSkill
{
    public string Name => "Improvisation";

    public string Description =>
        "Suggests scales, modes and arpeggios to improvise / solo over a chord " +
        "or a whole chord progression. For a single chord (e.g. 'Cmaj7', 'G7', " +
        "'Am7b5') it returns canonical chord-scale candidates with a one-line " +
        "color note. For a run of chords (e.g. 'Am F C G') it returns, for each " +
        "chord in turn, the matching arpeggio and the scales to play over it.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "what scale can I use to solo over Cmaj7?",
        "what scales work over G7?",
        "modes to solo over Dm7",
        "which mode fits Cmaj7",
        "chord-scale for G7",
        "improvise over Am7",
        "scale for Fmaj7#11",
        "what to play over a Bm7b5",
        "improvisation choices for E7alt",
        "solo over a Cmaj9 chord",
        // v2 (2026-07-20) progression anchors. These carry MORE than one chord
        // symbol and lean on the noun "progression" / "over these chords" so the
        // router separates them from single-chord scale lookups and from
        // ProgressionCompletionSkill (which is about what chord comes NEXT, not
        // what to play over the existing ones). Keep at least two chord symbols
        // in every anchor — that multi-chord shape is the routing signal.
        "which arpeggio fits Am F C G",
        "what arpeggios work over Dm7 G7 Cmaj7",
        "arpeggios to solo over Am F C G",
        "what scales fit over the progression Cmaj7 A7 Dm7 G7",
        "how do I improvise over Am F C G",
        "which arpeggio for each chord in Em C G D",
        // "solo over the changes" — jazz idiom for improvising through a
        // progression. Held-out testing showed this phrasing drifting to
        // practiceroutine (0.78); it belongs here. "changes" = the chords.
        "what do I solo over the changes Dm7 G7 Cmaj7",
    ];

    // Intent keywords. The CanHandle gate is "improvisation intent AND a
    // chord-shaped token", same pattern as ChordVoicingsSkill (hardened in
    // PR #251) so a bare lowercase article in normal English doesn't trigger.
    private static readonly string[] ImprovKeywords =
    [
        "solo", "improvise", "improvisation", "improv", "improvising",
        "what scale", "scale for", "scale over", "scale to use", "scales for",
        "scales over", "scales work", "what to play over", "play over",
        "modes for", "modes over", "modes to solo", "modal choice", "modal choices",
        // 2026-05-17 post-PR-253 feature-dev review: cover ExamplePrompts
        // phrasings that the original keyword list missed in the CanHandle
        // fallback path (semantic router catches them; fallback didn't).
        "which mode", "which modes", "what mode", "what modes",
        "chord-scale", "chord scale",
        // v2 (2026-07-20) progression / arpeggio intent. "arpeggio" makes the
        // "which arpeggio fits ..." family reachable through the CanHandle
        // fallback path, not just the semantic router.
        "arpeggio", "arpeggios",
    ];

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var q = message.ToLowerInvariant();
        // Whole-word match so a keyword embedded in an unrelated word doesn't
        // fire (e.g. "solo" inside "Solomon" — see ga#261).
        var hasImprovIntent = ImprovKeywords.Any(k => ChordIntentMatching.ContainsWord(q, k));
        if (!hasImprovIntent) return false;
        // Require a real chord token (uppercase root + accidental/quality/digit).
        // Without IgnoreCase, bare lowercase "a"/"e" articles never trigger.
        return ChordSuffixRegex().IsMatch(message)
               || ChordWithSpacedQualityRegex().IsMatch(message);
    }

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        // Progression path (v2): if the query names two or more chord symbols,
        // classify each independently and return per-chord arpeggio + scales.
        // Checked BEFORE the single-chord extractor, whose ExtractAsync collapses
        // a run to one symbol.
        var run = ExtractChordRun(message);
        if (run.Count >= 2)
            return BuildProgressionResponse(run);

        var structured = await extractor.ExtractAsync(message, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(structured.ChordSymbol))
        {
            return new AgentResponse
            {
                AgentId = $"skill.{Name.ToLowerInvariant()}",
                Result =
                    "I couldn't find a chord name in your request. Name one chord " +
                    "(e.g. 'Cmaj7', 'G7', 'Am7b5') and I'll suggest scales to improvise " +
                    "over it, or a run of chords (e.g. 'Am F C G') and I'll give the " +
                    "arpeggio and scales for each. Roman-numeral progressions (ii-V-I) " +
                    "need concrete chord symbols — name the chords in your key.",
                Confidence = 0.2f,
                Evidence = [],
                Assumptions = ["Typed parser produced no ChordSymbol from the query."],
            };
        }

        var root = ExtractRoot(structured.ChordSymbol);
        var quality = InferQuality(structured.ChordSymbol);
        var candidates = ScalesFor(quality);

        var sb = new StringBuilder();
        sb.AppendLine($"For **{structured.ChordSymbol}** (quality: {quality.Display}), try:");
        sb.AppendLine();
        foreach (var c in candidates)
        {
            sb.AppendLine($"- **{root} {c.Name}** — {c.Color}");
        }
        sb.AppendLine();
        sb.AppendLine(quality.AdvisoryNote);

        logger.LogDebug(
            "ImprovisationSkill: {Chord} → quality {Quality} → {Count} scale(s)",
            structured.ChordSymbol, quality.Display, candidates.Count);

        return new AgentResponse
        {
            AgentId = $"skill.{Name.ToLowerInvariant()}",
            Result = sb.ToString(),
            Confidence = 0.85f,
            Evidence = candidates.Select(c => $"{root} {c.Name}: {c.Color}").ToList(),
            Assumptions = candidates.Count == 0
                ? ["Chord quality not recognized; defaulted to chord root's major scale."]
                : [],
            Data = new
            {
                interpreted = structured,
                chordRoot = root,
                chordQuality = quality.Display,
                scales = candidates.Select(c => new { name = c.Name, color = c.Color }).ToList(),
            },
        };
    }

    // -------------------------------------------------------------------
    // Progression path (v2). Extract a run of chord symbols in query order,
    // then classify each with the same InferQuality used for single chords.
    // -------------------------------------------------------------------

    /// <summary>
    /// Pull chord symbols out of <paramref name="message"/> in order, de-noising
    /// the English words around them. Case-sensitive root (a bare lowercase "a"
    /// is the article, not an A chord) with a required accidental / quality /
    /// extension-digit suffix — same shape as <see cref="ChordSuffixRegex"/> but
    /// capturing every match rather than testing for one.
    /// </summary>
    // Internal for direct unit-test coverage of the tokenizer.
    internal static IReadOnlyList<string> ExtractChordRun(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return [];
        var chords = new List<string>();
        foreach (Match m in ChordTokenRegex().Matches(message))
        {
            var tok = m.Value.Trim();
            // A quality-less single uppercase letter (A-G) with a following
            // space is ambiguous with a plain note / roman-ish token, but in a
            // run like "Am F C G" the bare "F", "C", "G" ARE chords. Accept
            // them; the run length gate (>=2) already excludes lone letters.
            if (tok.Length > 0) chords.Add(tok);
        }
        return chords;
    }

    private AgentResponse BuildProgressionResponse(IReadOnlyList<string> chords)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Over **{string.Join(" – ", chords)}**, for each chord:");
        sb.AppendLine();

        var evidence = new List<string>();
        var data = new List<object>();

        foreach (var chord in chords)
        {
            var root = ExtractRoot(chord);
            var quality = InferQuality(chord);
            var arpeggio = ArpeggioFor(root, quality);
            var scales = ScalesFor(quality);
            // Best/most-common scale first — that is the one to lead with per chord.
            var lead = scales.Count > 0 ? scales[0] : new Scale("Major scale of the root", "fallback");

            sb.AppendLine(
                $"- **{chord}** → arpeggio **{arpeggio}**, play **{root} {lead.Name}** " +
                $"({lead.Color}).");

            evidence.Add($"{chord}: arpeggio {arpeggio}; scales {string.Join(", ", scales.Select(s => root + " " + s.Name))}");
            data.Add(new
            {
                chord,
                quality = quality.Display,
                arpeggio,
                scales = scales.Select(s => new { name = $"{root} {s.Name}", color = s.Color }).ToList(),
            });
        }

        sb.AppendLine();
        sb.AppendLine(
            "Each arpeggio spells the chord tones; the scale adds the passing notes " +
            "for lines between them.");

        logger.LogDebug("ImprovisationSkill: progression [{Chords}] → {Count} chords classified",
            string.Join(" ", chords), chords.Count);

        return new AgentResponse
        {
            AgentId = $"skill.{Name.ToLowerInvariant()}",
            Result = sb.ToString(),
            Confidence = 0.85f,
            Evidence = evidence,
            Assumptions = ["Each chord classified independently from its written quality; no key inferred."],
            Data = new { progression = chords, perChord = data },
        };
    }

    /// <summary>Chord-tone arpeggio label for a classified quality, e.g. "Am7", "Fmaj7".</summary>
    // Internal for unit-test coverage — the single most-broken behavior of the
    // MCP arpeggio tool was root + full-suffix concatenation ("Amm7").
    internal static string ArpeggioFor(string root, QualityClass quality) =>
        root + quality.Kind switch
        {
            QualityKind.Major            => "",       // C  → major triad
            QualityKind.Major7           => "maj7",
            QualityKind.LydianMaj7       => "maj7#11",
            QualityKind.Dominant7        => "7",
            QualityKind.AlteredDominant  => "7alt",
            QualityKind.Altered          => "7alt",
            QualityKind.SuspendedDominant => "7sus4",
            QualityKind.Minor            => "m",      // Am → minor triad
            QualityKind.Minor7           => "m7",
            QualityKind.MinorMajor7      => "mMaj7",
            QualityKind.HalfDiminished   => "m7b5",
            QualityKind.Diminished       => "dim",
            QualityKind.Diminished7      => "dim7",
            QualityKind.Augmented        => "aug",
            _                            => "",
        };

    // -------------------------------------------------------------------
    // Chord symbol → root + quality. Conservative: the parser already
    // canonicalized the symbol, so we just classify it.
    // -------------------------------------------------------------------

    // Internal for unit-test coverage of the classifier directly (per multi-LLM
    // review of this PR — driving each canonical symbol through a [TestCase]
    // table catches ordering bugs that aren't visible from CanHandle alone).
    internal static string ExtractRoot(string chordSymbol)
    {
        if (string.IsNullOrEmpty(chordSymbol)) return "C";
        // Validate the first char is an actual chord-root letter. Parser
        // glitches that produce pathological symbols ("7", "#G", "b") would
        // otherwise yield garbage like "7 Ionian". Fall back to "C" so the
        // skill's no-intent branch can flag the error upstream.
        var first = chordSymbol[0];
        if (first < 'A' || first > 'G') return "C";
        // First char is the letter root; second char may be '#' or 'b'.
        if (chordSymbol.Length >= 2 && (chordSymbol[1] == '#' || chordSymbol[1] == 'b'))
            return chordSymbol[..2];
        return chordSymbol[..1];
    }

    internal static QualityClass InferQuality(string chordSymbol)
    {
        if (string.IsNullOrEmpty(chordSymbol)) return QualityClass.Unknown;
        if (chordSymbol[0] < 'A' || chordSymbol[0] > 'G') return QualityClass.Unknown;
        // Strip the root from the front so we look only at the quality suffix.
        var suffix = chordSymbol;
        if (suffix.Length >= 2 && (suffix[1] == '#' || suffix[1] == 'b'))
            suffix = suffix[2..];
        else if (suffix.Length >= 1)
            suffix = suffix[1..];

        // Strip parentheses so canonicalized forms like "7(b9)" / "m7(b5)" /
        // "(maj7)" still classify correctly. Without this, parenthesized
        // alterations fall through to less-specific buckets.
        var s = suffix.ToLowerInvariant().Replace("(", "", StringComparison.Ordinal).Replace(")", "", StringComparison.Ordinal);

        // Most-specific first. Order matters: mMaj7 must be tested BEFORE the
        // maj7 family (the lowercased "mmaj7" contains "maj7" and would
        // short-circuit otherwise).
        if (s.Contains("alt", StringComparison.Ordinal)) return QualityClass.Altered;
        if (s.Contains("m7b5", StringComparison.Ordinal) || s.Contains("ø", StringComparison.Ordinal)) return QualityClass.HalfDiminished;
        // Diminished family — order matters: check the 4-note (dim7 / °7 / o7)
        // form first, then fall to the 3-note triad. Pre-fix, `Cdim` (triad)
        // matched the `StartsWith("dim")` branch and returned Diminished7 — a
        // category error: the canonical scale for a dim TRIAD is Locrian,
        // while dim7 takes Whole-Half Diminished. Caught by multi-LLM review
        // 2026-05-17 (same family as the pre-#253 CmMaj7→major7 bug).
        if (s.Contains("dim7", StringComparison.Ordinal) || s.Contains("°7", StringComparison.Ordinal) || s.Contains("o7", StringComparison.Ordinal)) return QualityClass.Diminished7;
        if (s.StartsWith("dim", StringComparison.Ordinal) || s.StartsWith("°", StringComparison.Ordinal)) return QualityClass.Diminished;
        if (s.Contains("mmaj7", StringComparison.Ordinal) || s.Contains("minmaj7", StringComparison.Ordinal)) return QualityClass.MinorMajor7;
        if (s.Contains("maj7#11", StringComparison.Ordinal) || s.Contains("maj7+11", StringComparison.Ordinal)) return QualityClass.LydianMaj7;
        if (s.Contains("maj7", StringComparison.Ordinal) || s.Contains("maj9", StringComparison.Ordinal) || s.Contains("maj13", StringComparison.Ordinal) || s.Contains("δ", StringComparison.Ordinal)) return QualityClass.Major7;
        // aug7 / +7 / 7#5 — augmented dominant. Less common than plain aug; flag
        // as Augmented but the advisory note will steer toward whole tone.
        if (s.Contains("aug", StringComparison.Ordinal) || s.StartsWith("+", StringComparison.Ordinal) || s.Contains("7#5", StringComparison.Ordinal)) return QualityClass.Augmented;
        if (s.Contains("sus", StringComparison.Ordinal) && s.Contains("7", StringComparison.Ordinal)) return QualityClass.SuspendedDominant;
        if (s.Contains("7b9", StringComparison.Ordinal) || s.Contains("7#9", StringComparison.Ordinal) || s.Contains("7b5", StringComparison.Ordinal) || s.Contains("13b9", StringComparison.Ordinal)) return QualityClass.AlteredDominant;
        if (s.Contains("m6", StringComparison.Ordinal)) return QualityClass.MinorMajor7; // mel min territory
        if (s.StartsWith("m7", StringComparison.Ordinal) || s.StartsWith("min7", StringComparison.Ordinal) || s.StartsWith("-7", StringComparison.Ordinal)) return QualityClass.Minor7;
        if (s.StartsWith("m9", StringComparison.Ordinal) || s.StartsWith("m11", StringComparison.Ordinal) || s.StartsWith("m13", StringComparison.Ordinal)) return QualityClass.Minor7;
        if (s.StartsWith("m", StringComparison.Ordinal) || s.StartsWith("min", StringComparison.Ordinal) || s.StartsWith("-", StringComparison.Ordinal)) return QualityClass.Minor;
        if (s.StartsWith("7", StringComparison.Ordinal) || s.StartsWith("9", StringComparison.Ordinal) || s.StartsWith("11", StringComparison.Ordinal) || s.StartsWith("13", StringComparison.Ordinal)) return QualityClass.Dominant7;
        if (s.Length == 0 || s.StartsWith("6", StringComparison.Ordinal) || s.StartsWith("69", StringComparison.Ordinal) || s.StartsWith("maj", StringComparison.Ordinal)) return QualityClass.Major;
        return QualityClass.Unknown;
    }

    // -------------------------------------------------------------------
    // Chord quality → ranked scale candidates. Order is best/most-common
    // first. The "color" string is a one-line hint for the rendered output.
    // -------------------------------------------------------------------

    private static IReadOnlyList<Scale> ScalesFor(QualityClass q) => q.Kind switch
    {
        QualityKind.Major7 =>
        [
            new("Ionian (major)", "diatonic — the safe choice"),
            new("Lydian", "bright — emphasize the #11 for color"),
        ],
        QualityKind.LydianMaj7 =>
        [
            new("Lydian", "the home scale — #11 is the defining note"),
        ],
        QualityKind.Major =>
        [
            new("Ionian (major)", "diatonic, includes the 7 — careful on the IV chord"),
            new("Lydian", "raise the 4 to avoid the avoid-note"),
            new("Major Pentatonic", "remove tension notes for blues feel"),
        ],
        QualityKind.Dominant7 =>
        [
            new("Mixolydian", "diatonic dominant — natural choice for V chords"),
            new("Lydian Dominant", "raise the 4 (b7 + #11) for fusion / outside color"),
            new("Mixolydian b6", "from melodic minor — leans tense without going altered"),
        ],
        QualityKind.AlteredDominant =>
        [
            new("Altered (Super Locrian)", "b9 #9 #11 b13 — covers every alteration"),
            new("Half-Whole Diminished", "b9 #9 #11 13 — symmetric alternative"),
            new("Phrygian Dominant", "use over 7b9 chords with Phrygian color"),
        ],
        QualityKind.Altered =>
        [
            new("Altered (Super Locrian)", "every extension is altered — the canonical alt scale"),
        ],
        QualityKind.SuspendedDominant =>
        [
            new("Mixolydian", "diatonic — strong"),
            new("Phrygian", "darker color; works over sus7 with b9"),
        ],
        QualityKind.Minor7 =>
        [
            new("Dorian", "natural 6 — modern jazz default"),
            new("Aeolian (minor)", "b6 — darker, fits ii in minor keys"),
            new("Phrygian", "b2 — Spanish / modal flavor over static m7"),
        ],
        QualityKind.Minor =>
        [
            new("Aeolian (natural minor)", "diatonic minor — fits most i chords"),
            new("Dorian", "raise the 6 for a brighter minor color"),
            new("Minor Pentatonic", "subtract tension notes for blues / rock"),
        ],
        QualityKind.MinorMajor7 =>
        [
            new("Melodic Minor", "the home scale for mMaj7 / m6 chords"),
        ],
        QualityKind.HalfDiminished =>
        [
            new("Locrian", "diatonic — but b9 is an avoid note"),
            new("Locrian #2", "raise the 2 from melodic minor — better tonal target"),
        ],
        QualityKind.Diminished =>
        [
            new("Locrian", "the diatonic mode whose 1-b3-b5 triad IS the dim chord"),
            new("Half-Whole Diminished", "symmetric — works when the chord moves like a passing dim"),
        ],
        QualityKind.Diminished7 =>
        [
            new("Whole-Half Diminished", "symmetric scale that contains all chord tones"),
        ],
        QualityKind.Augmented =>
        [
            new("Whole Tone", "every step is a whole step — covers the #5 / b13"),
            new("Lydian Augmented", "from melodic minor — for major7#5 contexts"),
        ],
        _ =>
        [
            new("Major scale of the chord root", "fallback — chord quality not classified"),
        ],
    };

    // -------------------------------------------------------------------
    // Same regex pair as ChordVoicingsSkill (PR #251). Case-sensitive root,
    // accidental/quality or a valid chord-extension digit (5,6,7,9,11,13)
    // required (strict) or " major/minor/dim/aug/sus" word-form (spaced).
    // The bare-digit branch is restricted to real extensions so non-chord
    // letter+number tokens like "B12" / "A4" no longer parse as chords (ga#261).
    // -------------------------------------------------------------------

    [GeneratedRegex(@"\b[A-G][#b]?(?:maj|min|m|M|dim|aug|sus|add|alt|°|Δ|11|13|5|6|7|9)\w*\b")]
    private static partial Regex ChordSuffixRegex();

    [GeneratedRegex(@"\b[A-G][#b]?\s+(?:[Mm]ajor|[Mm]inor|[Mm]aj|[Mm]in|[Dd]im|[Aa]ug|[Ss]us)\b")]
    private static partial Regex ChordWithSpacedQualityRegex();

    // Progression tokenizer (v2). Captures each chord symbol in a run, longest
    // quality alternation first so "Cmaj7" is one token, not "C" + "maj7". The
    // quality group is OPTIONAL so a bare triad in a run ("F", "C", "G" in
    // "Am F C G") still matches — the >=2 run-length gate in ExecuteAsync keeps
    // a lone capital letter from being read as a one-chord "progression".
    // Case-sensitive root: a lowercase "a"/"e" is an English article, never a
    // chord. Known edge case: "the E string and A string" yields [E, A]; the
    // improv-intent gate in CanHandle makes that combination rare in practice.
    [GeneratedRegex(@"\b[A-G][#b]?(?:maj7#11|maj7\+11|maj7|maj9|maj13|maj|mmaj7|minmaj7|m7b5|m7#5|m7|m9|m11|m13|m6|min7|min|m|dim7|dim|aug|°7|°|ø|sus2|sus4|sus|add9|add11|7alt|alt|7b9|7#9|7b5|7#11|13b9|13|11|9|7|69|6|5|\+|Δ|-7|-)?\b")]
    private static partial Regex ChordTokenRegex();

    internal enum QualityKind
    {
        Unknown,
        Major,
        Major7,
        LydianMaj7,
        Dominant7,
        AlteredDominant,
        Altered,
        SuspendedDominant,
        Minor,
        Minor7,
        MinorMajor7,
        HalfDiminished,
        Diminished,
        Diminished7,
        Augmented,
    }

    internal readonly record struct QualityClass(QualityKind Kind, string Display, string AdvisoryNote)
    {
        public static readonly QualityClass Unknown = new(QualityKind.Unknown, "unknown",
            "I couldn't classify the chord quality; defaulting to the chord-root major scale.");
        public static readonly QualityClass Major = new(QualityKind.Major, "major triad",
            "On a IV chord, prefer Lydian to dodge the avoid-note F over C.");
        public static readonly QualityClass Major7 = new(QualityKind.Major7, "major 7",
            "Lydian is the modern / modal choice; Ionian is the classical choice.");
        public static readonly QualityClass LydianMaj7 = new(QualityKind.LydianMaj7, "major 7#11",
            "Lydian is the home scale — the #11 is what makes this chord sound this way.");
        public static readonly QualityClass Dominant7 = new(QualityKind.Dominant7, "dominant 7",
            "Mixolydian for diatonic V chords; Lydian Dominant for static (non-resolving) dominants.");
        public static readonly QualityClass AlteredDominant = new(QualityKind.AlteredDominant, "altered dominant",
            "Altered scale (7th mode of melodic minor) is the workhorse for V chords with b9/#9/#11/b13.");
        public static readonly QualityClass Altered = new(QualityKind.Altered, "altered",
            "Use Altered — every extension is naturally a chord tone.");
        public static readonly QualityClass SuspendedDominant = new(QualityKind.SuspendedDominant, "suspended dominant",
            "Mixolydian for plain 7sus; Phrygian when the chart has a b9.");
        public static readonly QualityClass Minor = new(QualityKind.Minor, "minor triad",
            "Aeolian for diatonic i chords in minor keys; Dorian when the underlying progression suggests a major key.");
        public static readonly QualityClass Minor7 = new(QualityKind.Minor7, "minor 7",
            "Dorian is the modern jazz default; Aeolian if you want a darker, more diatonic-to-minor sound.");
        public static readonly QualityClass MinorMajor7 = new(QualityKind.MinorMajor7, "minor major 7",
            "Melodic Minor is the home scale; m6 chords also live here.");
        public static readonly QualityClass HalfDiminished = new(QualityKind.HalfDiminished, "half-diminished (m7b5)",
            "Locrian #2 (from melodic minor) is more usable than vanilla Locrian — the b9 is an avoid note.");
        public static readonly QualityClass Diminished = new(QualityKind.Diminished, "diminished triad",
            "Locrian is the diatonic home; Half-Whole works for passing-dim contexts.");
        public static readonly QualityClass Diminished7 = new(QualityKind.Diminished7, "diminished 7",
            "Whole-Half Diminished is symmetric and contains every chord tone of a dim7.");
        public static readonly QualityClass Augmented = new(QualityKind.Augmented, "augmented",
            "Whole Tone is the canonical aug choice; Lydian Augmented if the chord is a major7#5.");
    }

    // Implicit cast from kind to QualityClass via the static fields. The
    // InferQuality method returns these directly.
    private readonly record struct Scale(string Name, string Color);
}
