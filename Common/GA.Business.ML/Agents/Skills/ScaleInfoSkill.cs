namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Answers "what notes are in X major/minor?" queries using <see cref="Key.Items"/> —
/// zero LLM calls, pure domain computation.
/// </summary>
/// <remarks>
/// Registered at the <b>orchestrator level</b>. Returns the 7 scale notes and the
/// relative key as structured evidence without touching the LLM pipeline.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("ScaleInfo", "scale")]
public sealed class ScaleInfoSkill(ILogger<ScaleInfoSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ScaleInfo";
    public string Description =>
        "Returns the notes of a major or minor key (e.g. C major has C D E F G A B). " +
        "Pure domain computation, zero LLM calls.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What notes are in C major?",
        "Show me the F# minor scale",
        "List the notes in Bb major",
        // Bare "What is X major/minor?" pattern — common phrasing that
        // production was dropping into the LLM fallback because no example
        // prompt was structurally close enough to it. The 0.65 cosine
        // threshold + "What is a C major chord?" gap on the ChordInfo side
        // pushed both candidates below threshold for the bare query.
        // See docs/plans/2026-05-03-skill-routing-quality-fix.md for the
        // diagnosis trail.
        "What is C major?",
        "What is A minor?",
        "What is D minor?",
        "What is F# major?",
        "Tell me the notes of E major",
        // "What's in the [X] major/minor scale" pattern — was losing to
        // ModesSkill because "scale" appeared adjacent to a key letter
        // and the modes embedding was a slightly better cosine match.
        // Added 2026-05-12 to close si-4 misroute in the 2026-05-11 corpus.
        "What's in the G major scale?",
        "What's in the D minor scale?",
        "What's in the F major scale?",
        // v0.5 corpus expansion (2026-05-12): "formula for [scale]"
        // pattern — was misrouting to chordinfo on the abstract
        // "formula" word. Scale formulas are scale knowledge.
        "What's the formula for harmonic minor",
        "Formula for melodic minor scale",
        "Degrees of the A major scale",
        // "Show me the notes in [key]" family — bare key, no "scale"/"chord"
        // word. "Show me the notes in C major" was losing to ChordInfoSkill
        // because "notes in" + "C major" sat closer to the chord examples
        // ("What notes are in a Cmaj7?") than to any scaleinfo anchor, which
        // all carried either "scale" or the "What notes are in…?" framing.
        // Added 2026-06-19 to close the scales-keys misroute surfaced by the
        // /auto-optimize loop (skill.scaleinfo expected, skill.chordinfo seen).
        "Show me the notes in C major",
        "Show me the notes in A minor",
        "Show me the notes in G major",

        // Scale-side defence for the chord-quality anchors added to
        // ChordInfoSkill for issue #555 (2026-07-20). Adding chord-noun
        // anchors there pulls "what notes are in <root> <quality>" queries
        // toward chordinfo generally — measured: it broke "what notes are in
        // the A minor scale" (0.890 scaleinfo -> 0.900 chordinfo) even though
        // the query says "scale" outright. These re-anchor the explicit-scale
        // and bare-key forms so the two skills separate on the trailing noun
        // rather than on the root-plus-quality prefix they share.
        //
        // Do not add a bare "<root> <quality>" anchor beyond D major here: a
        // sweep showed "what notes are in F minor" drags "F sharp minor triad"
        // back across to this skill (19/20 -> 18/20).
        "what notes are in the A minor scale",
        "what notes are in the G major scale",
        "what notes are in D major",
        "what notes are in the B flat major scale",
        // Terse bare-key forms. An offline 3-intent cosine simulation said the
        // chord anchors above were safe for these; the LIVE router disagreed —
        // "notes in c major" went from skill.scaleinfo 0.93 to
        // fallback-direct 0.00 (chordinfo wins the embedding race, then
        // ChordInfoSkill cannot parse "c major" as a chord symbol, so the whole
        // query falls through to the LLM). The simulation was not a valid proxy
        // because production ranks many more intents than the three modelled.
        // Verified by A/B against a rebuilt baseline binary, not by simulation.
        "notes in c major",
        "notes in a minor",
        "notes in g major",
    ];

    // Matches: "notes in C major", "what is Bb minor scale", "D# minor notes", etc.
    private static readonly Regex KeyPattern =
        new(@"\b([A-G][#b]?)\s*(major|minor|maj|min)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Scales spelled from a key's own degrees: "A minor pentatonic", "A pentatonic minor",
    // "E blues", "C major blues", "G# harmonic minor", "D melodic minor". "A minor pentatonic"
    // also matches KeyPattern ("A minor"), so these must be resolved first or the answer is
    // the 7-note A minor scale.
    private static readonly Regex VariantPattern =
        new(@"\b(?<root>[A-G][#b]?)\s+(?:(?<quality>major|minor|maj|min)\s+)?" +
            @"(?<variant>pentatonic|blues|harmonic\s+minor|melodic\s+minor)(?:\s+(?<quality2>major|minor))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Other non-diatonic scales and modes. The key's 7 notes would be a wrong answer for
    // "C major bebop" or "A minor dorian", so the skill declines them.
    private static readonly Regex UnsupportedScalePattern =
        new(@"\b(whole[\s-]?tone|diminished|octatonic|chromatic|bebop|augmented|altered|hexatonic|" +
            @"dorian|phrygian|lydian|mixolydian|locrian|hungarian|neapolitan|enigmatic|byzantine|" +
            @"hirajoshi|prometheus|persian|gypsy|harmonic\s+major|double\s+harmonic)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // (degree, alteration) pairs over the parallel major or natural-minor key.
    private sealed record ScaleVariant(string Name, bool FromMinorKey, (int Degree, int Alter)[] Degrees, string Formula);

    private static readonly ScaleVariant MajorPentatonic = new("major pentatonic", false,
        [(1, 0), (2, 0), (3, 0), (5, 0), (6, 0)], "1 2 3 5 6");
    private static readonly ScaleVariant MinorPentatonic = new("minor pentatonic", true,
        [(1, 0), (3, 0), (4, 0), (5, 0), (7, 0)], "1 b3 4 5 b7");
    private static readonly ScaleVariant MinorBlues = new("blues", true,
        [(1, 0), (3, 0), (4, 0), (5, -1), (5, 0), (7, 0)], "1 b3 4 b5 5 b7");
    private static readonly ScaleVariant MajorBlues = new("major blues", false,
        [(1, 0), (2, 0), (3, -1), (3, 0), (5, 0), (6, 0)], "1 2 b3 3 5 6");
    private static readonly ScaleVariant HarmonicMinor = new("harmonic minor", true,
        [(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 0), (7, 1)], "1 2 b3 4 5 b6 7");
    private static readonly ScaleVariant MelodicMinor = new("melodic minor", true,
        [(1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 1), (7, 1)], "1 2 b3 4 5 6 7");

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        var q = message.ToLowerInvariant();

        // Yield to ChordInfoSkill when the prompt is clearly about a chord rather
        // than a scale — "what notes are in a C major chord?" otherwise matches
        // both because "C major" + "note" satisfies our pattern.
        if (q.Contains("chord")) return false;

        // Yield to RelativeKeySkill: "what is the relative minor of C major?"
        // names a key but asks for a related key, not for its notes.
        if (RelativeKeySkill.IsKeyRelationQuestion(message)) return false;

        if (UnsupportedScalePattern.IsMatch(message)) return false;

        return (KeyPattern.IsMatch(message) || VariantPattern.IsMatch(message)) &&
               (q.Contains("note") || q.Contains("scale") || q.Contains("what is") ||
                q.Contains("what's in") || q.Contains("tell me") || q.Contains("show me") ||
                q.Contains("list") || q.Contains("play"));
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        if (UnsupportedScalePattern.IsMatch(message))
            return Task.FromResult(Decline("the question names a scale other than major, minor, pentatonic, blues, harmonic or melodic minor"));

        var variantMatch = VariantPattern.Match(message);
        if (variantMatch.Success)
            return Task.FromResult(DescribeVariant(variantMatch));

        var match = KeyPattern.Match(message);
        if (!match.Success)
            return Task.FromResult(CannotHelp("Could not parse a key name from your question."));

        var rootStr  = match.Groups[1].Value;
        var modeStr  = match.Groups[2].Value.ToLowerInvariant();
        var isMinor  = modeStr is "minor" or "min";

        // Find the matching domain key
        var key = KeyNaming.ResolveKey(rootStr, isMinor);

        if (key is null)
            return Task.FromResult(CannotHelp(
                $"I don't recognise \"{rootStr} {modeStr}\" as a standard key. " +
                "Try a key like C major, F# minor, or Bb major."));

        var notes       = key.Notes.ToList();
        var noteNames   = notes.Select(n => n.ToString()).ToList();
        var keyName     = $"{key.Root} {(isMinor ? "minor" : "major")}";
        var relativeKey = key.KeyMode == KeyMode.Major
            ? $"Relative minor: {KeyNaming.RelativeKeyName(key)}"
            : $"Relative major: {KeyNaming.RelativeKeyName(key)}";

        logger.LogDebug("ScaleInfoSkill: resolved {Key} → [{Notes}]", keyName, string.Join(", ", noteNames));

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = $"The {keyName} scale has 7 notes: **{string.Join(" – ", noteNames)}**. {relativeKey}.",
            Confidence = 1.0f,
            Evidence   =
            [
                $"Key: {keyName}",
                $"Notes: {string.Join(", ", noteNames)}",
                $"Key signature: {KeyNaming.DescribeKeySignature(key)}",
                relativeKey
            ],
            Assumptions = []
        });
    }

    private AgentResponse DescribeVariant(Match match)
    {
        var rootStr = match.Groups["root"].Value;
        var quality = (match.Groups["quality"].Success ? match.Groups["quality"] : match.Groups["quality2"]).Value
            .ToLowerInvariant();
        var variantWord = Regex.Replace(match.Groups["variant"].Value.ToLowerInvariant(), @"\s+", " ");

        var variant = variantWord switch
        {
            "pentatonic" when quality is "major" or "maj" => MajorPentatonic,
            "pentatonic" when quality is "minor" or "min" => MinorPentatonic,
            "pentatonic"                                  => null, // major or minor? let the agent path ask
            "blues" when quality is "major" or "maj"      => MajorBlues,
            "blues"                                       => MinorBlues,
            "harmonic minor"                              => HarmonicMinor,
            _                                             => MelodicMinor,
        };
        if (variant is null)
            return Decline("a pentatonic scale was named without major or minor");

        var key = KeyNaming.ResolveKey(rootStr, variant.FromMinorKey);
        if (key is null)
            return CannotHelp(
                $"I don't recognise \"{rootStr} {(variant.FromMinorKey ? "minor" : "major")}\" as a standard key, " +
                $"so I can't spell its {variant.Name} scale. Try a key like C major, F# minor, or Bb major.");

        var keyNotes  = key.Notes.ToList();
        var noteNames = variant.Degrees.Select(d => Spell(keyNotes[d.Degree - 1], d.Alter)).ToList();
        var scaleName = $"{key.Root} {variant.Name}";

        logger.LogDebug("ScaleInfoSkill: resolved {Scale} → [{Notes}]", scaleName, string.Join(", ", noteNames));

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = $"The {scaleName} scale has {noteNames.Count} notes: **{string.Join(" – ", noteNames)}** (formula {variant.Formula}).",
            Confidence = 1.0f,
            Evidence   =
            [
                $"Scale: {scaleName}",
                $"Notes: {string.Join(", ", noteNames)}",
                $"Formula: {variant.Formula}",
                $"Spelled from the degrees of {key.Root} {(variant.FromMinorKey ? "minor" : "major")}"
            ],
            Assumptions = []
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>A key note raised or lowered by <paramref name="alter"/> semitones, keeping its letter.</summary>
    private static string Spell(Note.KeyNote note, int alter)
    {
        if (alter == 0) return note.ToString();
        return ((note.Accidental?.Value ?? 0) + alter) switch
        {
            0  => $"{note.NaturalNote}",
            1  => $"{note.NaturalNote}#",
            2  => $"{note.NaturalNote}x",
            -1 => $"{note.NaturalNote}b",
            -2 => $"{note.NaturalNote}bb",
            var v => throw new InvalidOperationException($"Cannot spell {note} altered by {alter} ({v})")
        };
    }

    private static AgentResponse Decline(string reason) => new()
    {
        Declined    = true,
        AgentId     = AgentIds.Theory,
        Result      = "Ask for the notes of a major or minor key, or of its pentatonic, blues, harmonic minor or melodic minor scale (e.g. \"notes in the A minor pentatonic scale\").",
        Confidence  = 0.1f,
        Evidence    = [$"ScaleInfoSkill: declined because {reason}"],
        Assumptions = []
    };

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Theory,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };
}
