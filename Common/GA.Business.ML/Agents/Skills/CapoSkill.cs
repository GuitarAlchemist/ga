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
[GuitarAlchemist.Registry.GaSkill("Capo", "fretboard")]
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

    /// <summary>"And a C shape?" after a capo question is answered from the history.</summary>
    public bool ResolvesFollowUps => true;

    // Pattern A: "<sounding>, capo <N>" or "capo <N>, <sounding>" → what SHAPE
    // The "shape" keyword (or sounding-key + capo without a shape token) implies
    // the user wants the played-side answer.
    private static readonly Regex SoundingToShape =
        new(@"(?:song\s+(?:is\s+)?in\s+|key\s+(?:is\s+|of\s+)?|in\s+)(?<key>[A-Ga-g][b#♭♯]?)(?:\s+(?<qual>major|minor|maj|min))?\b[^.?!]*?" + CapoFret("n") +
            @"|" +
            CapoFret("n2") + @"[^.?!]*?(?:song\s+(?:is\s+)?in\s+|key\s+(?:is\s+|of\s+)?|in\s+)(?<key2>[A-Ga-g][b#♭♯]?)(?:\s+(?<qual2>major|minor|maj|min))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pattern B: "<shape> shape with capo <N>" → SOUNDING
    // Shape-side anchor: "<note> shape" or "play a <note> shape" or "<note>m shape"
    private static readonly Regex ShapeToSounding =
        new(@"\b(?:play(?:ing)?\s+(?:a\s+|an\s+)?)?(?<shape>[A-Ga-g][b#♭♯]?m?)\s+shape\b[^.?!]*?" + CapoFret("n") +
            @"|" +
            CapoFret("n2") + @"[^.?!]*?\b(?:play(?:ing)?\s+(?:a\s+|an\s+)?)?(?<shape2>[A-Ga-g][b#♭♯]?m?)\s+shape\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // The capo fret as people write it: "capo 3", "capo on 3", "capo on fret 3", "capo at fret 5",
    // "capo on the 2nd fret" (issue #888: "capo on fret 3" used to read as no fret at all).
    private static string CapoFret(string group) =>
        @"\bcapo\s+(?:(?:on|at)\s+)?(?:the\s+)?(?:fret\s+)?(?<" + group + @">\d{1,2})(?:st|nd|rd|th)?\b";

    /// <summary>Which enharmonic spelling the user's own note names ask for, if any.</summary>
    private enum Spelling { KeySignature, Sharps, Flats }

    // A key named without an accidental preference gets the spelling of its key signature, the way
    // a chart names it: Bb major (2 flats) rather than A# major (10 sharps). F# keeps sharps (6 vs 6),
    // and the minor keys follow their own signatures (C#m, G#m, but Ebm and Bbm).
    private static readonly string[] MajorKeyNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly string[] MinorKeyNames =
        ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "G#", "A", "Bb", "B"];

    private static readonly string[] SharpNames =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly string[] FlatNames =
        ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default) =>
        Task.FromResult(Answer(message));

    /// <summary>
    /// Answers an elliptical follow-up ("And a C shape?", "What about capo 4?") by taking the
    /// argument it leaves out from the previous capo question in <paramref name="history"/>.
    /// A message that is neither a capo question nor such a follow-up still declines.
    /// </summary>
    public Task<AgentResponse> ExecuteAsync(
        string message,
        IReadOnlyList<ChatHistoryTurn> history,
        CancellationToken cancellationToken = default)
    {
        var direct = Answer(message);
        if (!direct.Declined) return Task.FromResult(direct);

        return Task.FromResult(
            LastCapoQuestion(history) is { } previous && Complete(message ?? string.Empty, previous) is { } completed
                ? Answer(completed)
                : direct);
    }

    // The latest complete capo question of the conversation. Earlier follow-ups are resolved in
    // order, so "What about capo 4?" then "And a C shape?" asks about a C shape on fret 4.
    private string? LastCapoQuestion(IReadOnlyList<ChatHistoryTurn> history)
    {
        string? question = null;
        foreach (var turn in history)
        {
            if (!string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Answer(turn.Content).Declined) question = turn.Content;
            else if (question is not null && Complete(turn.Content, question) is { } completed) question = completed;
        }

        return question;
    }

    // Rewrites a follow-up that names only a shape or only a capo fret as a complete question,
    // taking the other argument from the previous question. Null when it names neither or both,
    // or when it is a question of its own rather than a fragment.
    private static string? Complete(string followUp, string previous)
    {
        var fret = FretOnly.Match(followUp);
        var shape = ShapeOnly.Match(followUp);
        if (shape.Success == fret.Success) return null;
        if (!IsFragment(followUp, shape.Success ? shape : fret)) return null;

        if (shape.Success)
        {
            var previousFret = FretOnly.Match(previous);
            return previousFret.Success
                ? $"{shape.Groups["shape"].Value} shape with capo {previousFret.Groups["n"].Value}"
                : null;
        }

        return FretOnly.Replace(previous, $"capo {fret.Groups["n"].Value}", 1);
    }

    // An elliptical follow-up is the changed argument plus a few linking words ("And a C shape?",
    // "What about capo 4?"). With the argument removed, a question of its own still carries its
    // own predicate ("Can I use ... on a 12-string?", "How do I finger a ...?"), so it keeps more
    // words than a fragment does.
    private const int MaxFragmentWords = 4;

    private static bool IsFragment(string followUp, Match argument)
    {
        var rest = followUp.Remove(argument.Index, argument.Length);
        var words = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Count(word => word.Any(char.IsLetterOrDigit));
        return words <= MaxFragmentWords;
    }

    private static readonly Regex FretOnly = new(CapoFret("n"), RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ShapeOnly =
        new(@"\b(?<shape>[A-Ga-g][b#♭♯]?m?)\s+shape\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private AgentResponse Answer(string? message)
    {
        var msg = message ?? string.Empty;

        // Shape→sounding first: more specific (contains "shape" keyword)
        if (ShapeToSounding.Match(msg) is { Success: true } mShape)
        {
            var shapeRaw = (mShape.Groups["shape"].Success ? mShape.Groups["shape"].Value : mShape.Groups["shape2"].Value).Trim();
            var nRaw = mShape.Groups["n"].Success ? mShape.Groups["n"].Value : mShape.Groups["n2"].Value;
            return AnswerShapeToSounding(shapeRaw, ParseFret(nRaw), ShapeSpelling(shapeRaw, msg));
        }
        if (SoundingToShape.Match(msg) is { Success: true } mSound)
        {
            var keyRaw = (mSound.Groups["key"].Success ? mSound.Groups["key"].Value : mSound.Groups["key2"].Value).Trim();
            var nRaw = mSound.Groups["n"].Success ? mSound.Groups["n"].Value : mSound.Groups["n2"].Value;
            var qualRaw = mSound.Groups["qual"].Success ? mSound.Groups["qual"].Value
                        : mSound.Groups["qual2"].Success ? mSound.Groups["qual2"].Value
                        : string.Empty;
            var isMinor = qualRaw.StartsWith("min", StringComparison.OrdinalIgnoreCase);
            return AnswerSoundingToShape(NormalizeKey(keyRaw), ParseFret(nRaw), isMinor, KeySpelling(keyRaw));
        }

        return CannotHandle();
    }

    private AgentResponse AnswerSoundingToShape(string soundingKey, int capoFret, bool isMinor, Spelling spelling)
    {
        if (!TryParseRoot(soundingKey, out var soundingPc, out _))
            return CannotParse(soundingKey);
        if (capoFret is < 0 or > 12)
            return CannotParseFret(capoFret);

        var shapePc = ((soundingPc - capoFret) % 12 + 12) % 12;
        var shape = SpellPc(shapePc, spelling, isMinor);
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
        sb.AppendLine($"The capo raises every string by {capoFret} semitone{(capoFret == 1 ? "" : "s")}, so the shape you finger ({shapeLabel}) sounds {capoFret} fret{(capoFret == 1 ? "" : "s")} higher (= {soundingKey} {qualityWord}). All chord shapes shift the same way: an Am shape at capo {capoFret} would sound as {SpellPc(amSoundingPc, spelling, minor: true)}m.");
        return Result(sb.ToString(), $"capo({soundingKey} {qualityWord} sounding, fret {capoFret} → {shapeLabel} shape)");
    }

    private AgentResponse AnswerShapeToSounding(string shapeRaw, int capoFret, Spelling spelling)
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
        var soundingRoot = SpellPc(soundingPc, spelling, minorShape);
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
        // Shared root table: the edge spellings (B#, Cb, E#, Fb) read too — a capo is pure pitch-class arithmetic.
        normalized = NormalizeKey(raw);
        return ChordVocabulary.TryGetPitchClass(normalized, out pc);
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
    /// No preference: the key-signature spelling (Bb, not A#).
    /// </summary>
    private static string SpellPc(int pc, Spelling spelling, bool minor)
    {
        var index = ((pc % 12) + 12) % 12;
        return spelling switch
        {
            Spelling.Flats  => FlatNames[index],
            Spelling.Sharps => SharpNames[index],
            _               => minor ? MinorKeyNames[index] : MajorKeyNames[index],
        };
    }

    // The accidental follows the letter, so a lowercase "b" root ("b shape") is the note B, not a flat.
    private static Spelling KeySpelling(string keyRaw) =>
        keyRaw.Length < 2 ? Spelling.KeySignature
        : keyRaw[1..].IndexOfAny(['b', '♭']) >= 0 ? Spelling.Flats
        : keyRaw[1..].IndexOfAny(['#', '♯']) >= 0 ? Spelling.Sharps
        : Spelling.KeySignature;

    private static Spelling ShapeSpelling(string shapeRaw, string fullMessage) =>
        KeySpelling(shapeRaw) is var fromShape && fromShape != Spelling.KeySignature ? fromShape
        : fullMessage.Contains("flat", StringComparison.OrdinalIgnoreCase) ? Spelling.Flats
        : fullMessage.Contains("sharp", StringComparison.OrdinalIgnoreCase) ? Spelling.Sharps
        : Spelling.KeySignature;

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
        Declined   = true,
        AgentId    = AgentIds.Theory,
        Result     = "Ask about a capo + sounding key (\"what shape do I play in E with capo 4\") or a capo + played shape (\"I play a C shape with capo 3, what does it sound like\").",
        Confidence = 0.1f,
        Evidence   = ["CapoSkill: no recognised capo pattern in query"],
    };
}
