namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Domain-backed capo skill. A capo raises every string by N semitones, so the
/// guitarist plays SHAPE = SOUNDING - N (transposed down N semitones) and the
/// listener hears SOUNDING = SHAPE + N (transposed up N semitones).
/// Three surface forms:
/// <list type="bullet">
/// <item><b>"Song is in E, what shape do I play with capo 4"</b> → C shape (E − 4st = C)</item>
/// <item><b>"I play a C shape with capo 3, what does it sound like"</b> → Eb (C + 3st)</item>
/// <item><b>"Capo on 5, song in A"</b> → E shape (A − 5st = E)</item>
/// </list>
/// Zero LLM calls — pure pitch-class arithmetic. Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14 to close BACKLOG dealbreaker #3 (capo). Tier-1 deterministic
/// per <c>docs/plans/2026-05-13-skills-domain-backed-refactor-plan.md</c>.
/// Spelling preference: pick the enharmonic that matches the user's input
/// side (sharps stay sharp, flats stay flat) — see <see cref="SpellPc"/>.
/// </remarks>
public sealed class CapoSkill(ILogger<CapoSkill> logger) : IOrchestratorSkill
{
    public string Name => "Capo";
    public string Description =>
        "Answers capo questions: given a sounding key and capo fret, what shape " +
        "does the guitarist play? Given a played shape and capo fret, what does " +
        "it sound like? Pure semitone arithmetic — no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What shape do I play in E with capo 4",
        "Song is in E, what chord shape with capo on 4",
        "Capo on 3, song in G, what shape",
        "I play a C shape with capo 3 — what does it sound like",
        "What does a G shape sound like with capo 2",
        "Capo on 5, song in A",
        "If I capo on 2 and play an Em shape, what's the sounding chord",
        "What's the sounding key if I play in G with capo on 5",
        "Capo 7, D shape — sounding chord",
        "What chord shape for B major with capo 4",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Pattern A: "<sounding>, capo <N>" or "capo <N>, <sounding>" → what SHAPE
    // The "shape" keyword (or sounding-key + capo without a shape token) implies
    // the user wants the played-side answer.
    private static readonly Regex SoundingToShape =
        new(@"(?:song\s+(?:is\s+)?in\s+|key\s+(?:is\s+|of\s+)?|in\s+)(?<key>[A-Ga-g][b#♭♯]?)(?:\s+(?<qual>major|minor|maj|min))?\b[^.?!]*?\bcapo\s+(?:on\s+|fret\s+|at\s+)?(?<n>\d{1,2})\b" +
            @"|" +
            @"\bcapo\s+(?:on\s+|fret\s+|at\s+)?(?<n2>\d{1,2})\b[^.?!]*?(?:song\s+(?:is\s+)?in\s+|key\s+(?:is\s+|of\s+)?|in\s+)(?<key2>[A-Ga-g][b#♭♯]?)(?:\s+(?<qual2>major|minor|maj|min))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pattern B: "<shape> shape with capo <N>" → SOUNDING
    // Shape-side anchor: "<note> shape" or "play a <note> shape" or "<note>m shape"
    private static readonly Regex ShapeToSounding =
        new(@"\b(?:play(?:ing)?\s+(?:a\s+|an\s+)?)?(?<shape>[A-Ga-g][b#♭♯]?m?)\s+shape\b[^.?!]*?\bcapo\s+(?:on\s+|fret\s+|at\s+)?(?<n>\d{1,2})\b" +
            @"|" +
            @"\bcapo\s+(?:on\s+|fret\s+|at\s+)?(?<n2>\d{1,2})\b[^.?!]*?\b(?:play(?:ing)?\s+(?:a\s+|an\s+)?)?(?<shape2>[A-Ga-g][b#♭♯]?m?)\s+shape\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pitch-letter → semitone PC (0..11)
    private static readonly Dictionary<string, int> RootPc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"]  = 0,  ["C#"] = 1, ["Db"] = 1,
        ["D"]  = 2,  ["D#"] = 3, ["Eb"] = 3,
        ["E"]  = 4,
        ["F"]  = 5,  ["F#"] = 6, ["Gb"] = 6,
        ["G"]  = 7,  ["G#"] = 8, ["Ab"] = 8,
        ["A"]  = 9,  ["A#"] = 10, ["Bb"] = 10,
        ["B"]  = 11,
    };

    private static readonly string[] SharpNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly string[] FlatNames =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;

        // Shape→sounding first: more specific (contains "shape" keyword)
        if (ShapeToSounding.Match(msg) is { Success: true } mShape)
        {
            var shapeRaw = (mShape.Groups["shape"].Success ? mShape.Groups["shape"].Value : mShape.Groups["shape2"].Value).Trim();
            var nRaw = mShape.Groups["n"].Success ? mShape.Groups["n"].Value : mShape.Groups["n2"].Value;
            return Task.FromResult(AnswerShapeToSounding(shapeRaw, ParseFret(nRaw), preferFlats: ShapeIsFlat(shapeRaw, msg)));
        }
        if (SoundingToShape.Match(msg) is { Success: true } mSound)
        {
            var keyRaw = (mSound.Groups["key"].Success ? mSound.Groups["key"].Value : mSound.Groups["key2"].Value).Trim();
            var nRaw = mSound.Groups["n"].Success ? mSound.Groups["n"].Value : mSound.Groups["n2"].Value;
            var qualRaw = mSound.Groups["qual"].Success ? mSound.Groups["qual"].Value
                        : mSound.Groups["qual2"].Success ? mSound.Groups["qual2"].Value
                        : string.Empty;
            var isMinor = qualRaw.StartsWith("min", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(AnswerSoundingToShape(NormalizeKey(keyRaw), ParseFret(nRaw), isMinor, preferFlats: KeyIsFlat(keyRaw)));
        }

        return Task.FromResult(CannotHandle());
    }

    private AgentResponse AnswerSoundingToShape(string soundingKey, int capoFret, bool isMinor, bool preferFlats)
    {
        if (!TryParseRoot(soundingKey, out var soundingPc, out _))
            return CannotParse(soundingKey);
        if (capoFret is < 0 or > 12)
            return CannotParseFret(capoFret);

        var shapePc = ((soundingPc - capoFret) % 12 + 12) % 12;
        var shape = SpellPc(shapePc, preferFlats);
        // Match the shape's quality to the sounding-key's quality: if the
        // user said "B minor", the shape that produces B-minor at capo N is
        // the (B−N)-minor shape — e.g. capo 4 in B minor = G-minor shape, not
        // G-major shape. Caught by the 2026-05-14 correctness review.
        var qualityWord = isMinor ? "minor" : "major";
        var shapeLabel = isMinor ? shape + "m" : shape;
        var sb = new StringBuilder();
        sb.AppendLine($"With a capo on fret **{capoFret}**, play a **{shapeLabel} shape** to sound in **{soundingKey} {qualityWord}**.");
        sb.AppendLine();
        // Educational example: how does an Am shape (PC=9) sound at this capo?
        var amSoundingPc = (9 + capoFret) % 12;
        sb.AppendLine($"The capo raises every string by {capoFret} semitone{(capoFret == 1 ? "" : "s")}, so the shape you finger ({shapeLabel}) sounds {capoFret} fret{(capoFret == 1 ? "" : "s")} higher (= {soundingKey} {qualityWord}). All chord shapes shift the same way: an Am shape at capo {capoFret} would sound as {SpellPc(amSoundingPc, preferFlats)}m.");
        return Result(sb.ToString(), $"capo({soundingKey} {qualityWord} sounding, fret {capoFret} → {shapeLabel} shape)");
    }

    private AgentResponse AnswerShapeToSounding(string shapeRaw, int capoFret, bool preferFlats)
    {
        var minorShape = shapeRaw.EndsWith("m", StringComparison.OrdinalIgnoreCase)
                         && shapeRaw.Length > 1
                         && !shapeRaw.EndsWith("aj", StringComparison.OrdinalIgnoreCase);
        var rootToken = minorShape ? shapeRaw[..^1] : shapeRaw;
        if (!TryParseRoot(rootToken, out var shapePc, out _))
            return CannotParse(shapeRaw);
        if (capoFret is < 0 or > 12)
            return CannotParseFret(capoFret);

        var soundingPc = (shapePc + capoFret) % 12;
        var soundingRoot = SpellPc(soundingPc, preferFlats);
        var soundingChord = minorShape ? soundingRoot + "m" : soundingRoot;
        var qualityWord = minorShape ? "minor" : "major";

        var sb = new StringBuilder();
        sb.AppendLine($"A **{shapeRaw} shape** with a capo on fret **{capoFret}** sounds as **{soundingChord}** ({soundingRoot} {qualityWord}).");
        sb.AppendLine();
        sb.AppendLine($"The capo raises every fingered note by {capoFret} semitone{(capoFret == 1 ? "" : "s")}: {NormalizeKey(rootToken)} + {capoFret} = {soundingRoot}. Same operation applies to every chord in the song you're playing.");
        return Result(sb.ToString(), $"capo({shapeRaw} shape, fret {capoFret} → {soundingChord} sounding)");
    }

    private static int ParseFret(string raw) =>
        int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : -1;

    private static bool TryParseRoot(string raw, out int pc, out string normalized)
    {
        normalized = NormalizeKey(raw);
        return RootPc.TryGetValue(normalized, out pc);
    }

    private static string NormalizeKey(string raw)
    {
        var ascii = (raw ?? string.Empty).Replace("♯", "#").Replace("♭", "b");
        return ascii switch
        {
            { Length: 1 } single => single.ToUpperInvariant(),
            { Length: 2 } pair  => char.ToUpperInvariant(pair[0]) + pair[1].ToString().ToLowerInvariant(),
            { Length: 3 } triple when (triple[2] == 'm' || triple[2] == 'M') =>
                char.ToUpperInvariant(triple[0]) + triple[1].ToString().ToLowerInvariant() + "m",
            var other => other,
        };
    }

    /// <summary>
    /// Pick the enharmonic spelling that matches the user's preferred side.
    /// If the user said "Eb major" → flats. If "F# major" → sharps.
    /// Default: sharps (guitarist convention).
    /// </summary>
    private static string SpellPc(int pc, bool preferFlats) =>
        preferFlats ? FlatNames[((pc % 12) + 12) % 12] : SharpNames[((pc % 12) + 12) % 12];

    private static bool KeyIsFlat(string keyRaw) =>
        keyRaw.IndexOf('b') >= 0 || keyRaw.IndexOf('♭') >= 0;

    private static bool ShapeIsFlat(string shapeRaw, string fullMessage) =>
        shapeRaw.IndexOf('b') >= 0 || shapeRaw.IndexOf('♭') >= 0
        || fullMessage.Contains("flat", StringComparison.OrdinalIgnoreCase);

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("CapoSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: CapoSkill (pure semitone arithmetic)", evidence],
        };
    }

    private static AgentResponse CannotParse(string key) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't identify '{key}' as a key or chord shape. Try a single pitch letter optionally followed by # or b (e.g. C, G, F#, Bb) — and `m` for minor shapes (e.g. Em).",
        Confidence = 0.3f,
        Evidence   = [$"CapoSkill: unparseable root token '{key}'"],
    };

    private static AgentResponse CannotParseFret(int fret) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"A capo fret of {fret} isn't valid — try a fret number between 0 (open) and 12 (octave).",
        Confidence = 0.3f,
        Evidence   = [$"CapoSkill: out-of-range fret {fret}"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask about a capo + sounding key (\"what shape do I play in E with capo 4\") or a capo + played shape (\"I play a C shape with capo 3, what does it sound like\").",
        Confidence = 0.1f,
        Evidence   = ["CapoSkill: no recognised capo pattern in query"],
    };
}
