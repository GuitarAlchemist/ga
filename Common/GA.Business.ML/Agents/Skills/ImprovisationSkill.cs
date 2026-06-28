namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.ML.Search;

/// <summary>
/// Answers "what scale can I use to solo over X" style prompts. Wraps the
/// typed chord parser to extract a chord symbol from the user query, infers
/// the chord quality, and returns matching modes / scales drawn from a
/// canonical chord-scale mapping. Pure domain compute; no LLM at the skill
/// layer. Single-chord queries only in v1 — progression handling (ii-V-I,
/// minor blues, etc.) is documented as v2 scope.
/// </summary>
[GuitarAlchemist.Registry.GaSkill("ImprovisationSkill", "scale")]
public sealed partial class ImprovisationSkill(
    ILogger<ImprovisationSkill> logger,
    IMusicalQueryExtractor extractor) : IOrchestratorSkill
{
    public string Name => "Improvisation";

    public string Description =>
        "Suggests scales and modes to improvise / solo over a given chord. " +
        "Parses the chord symbol from the user query (e.g. 'Cmaj7', 'G7', " +
        "'Am7b5') and returns canonical chord-scale candidates with their " +
        "note spellings and a one-line color note. Single-chord queries only.";

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
        var structured = await extractor.ExtractAsync(message, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(structured.ChordSymbol))
        {
            return new AgentResponse
            {
                AgentId = $"skill.{Name.ToLowerInvariant()}",
                Result =
                    "I couldn't find a chord name in your request. Try naming a single " +
                    "chord (e.g. 'Cmaj7', 'G7', 'Am7b5') and I'll suggest scales to " +
                    "improvise over it. Progression-level questions (ii-V-I, minor blues) " +
                    "aren't supported yet — name a specific chord.",
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
