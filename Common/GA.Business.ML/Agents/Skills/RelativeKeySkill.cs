namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Domain-backed relative-key / parallel-key / key-signature skill. Answers
/// questions like:
/// <list type="bullet">
/// <item><b>"Relative minor of G major"</b> → E minor (down a minor 3rd, same key signature)</item>
/// <item><b>"Relative major of A minor"</b> → C major (up a minor 3rd)</item>
/// <item><b>"Parallel minor of C major"</b> → C minor (same root, flip quality)</item>
/// <item><b>"Parallel major of A minor"</b> → A major</item>
/// <item><b>"How many sharps in D major"</b> → 2 sharps (F#, C#)</item>
/// </list>
/// Zero LLM calls — pure pitch-class arithmetic. Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-13 to close the corpus failure
/// <c>"What is the parallel minor of C major" → missing required substring "C minor"</c>.
/// Tier 1 candidate per <c>docs/plans/2026-05-13-skills-domain-backed-refactor-plan.md</c>.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("RelativeKeySkill", "key")]
public sealed class RelativeKeySkill(ILogger<RelativeKeySkill> logger) : IOrchestratorSkill
{
    public string Name => "RelativeKey";
    public string Description =>
        "Answers relative-key, parallel-key, and key-signature questions: " +
        "relative minor/major of a given key (down/up a minor 3rd, same key " +
        "signature), parallel minor/major (same root, flip quality, different " +
        "key signature), and sharps/flats count for any major or minor key. " +
        "Pure pitch-class math — no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What is the relative minor of G major",
        "Relative minor of D major",
        "Relative major of A minor",
        "What's the relative major of E minor",
        "Parallel minor of C major",
        "What is the parallel minor of F major",
        "Parallel major of D minor",
        "What's the parallel major of A minor",
        "How many sharps in D major",
        "How many flats in F major",
        "What's the key signature of E major",
        "Key signature of B minor",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    private static readonly Regex RelativeMinorPattern =
        new(@"\brelative\s+min(?:or)?\s+of\s+(?<key>[A-Ga-g][b#♭♯]?)\s*(?<quality>maj(?:or)?|min(?:or)?)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RelativeMajorPattern =
        new(@"\brelative\s+maj(?:or)?\s+of\s+(?<key>[A-Ga-g][b#♭♯]?)\s*(?<quality>maj(?:or)?|min(?:or)?)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParallelMinorPattern =
        new(@"\bparallel\s+min(?:or)?\s+of\s+(?<key>[A-Ga-g][b#♭♯]?)\s*(?<quality>maj(?:or)?|min(?:or)?)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParallelMajorPattern =
        new(@"\bparallel\s+maj(?:or)?\s+of\s+(?<key>[A-Ga-g][b#♭♯]?)\s*(?<quality>maj(?:or)?|min(?:or)?)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeySignaturePattern =
        new(@"\b(?:how\s+many\s+(?<acc>sharps|flats|accidentals)|key\s+signature)\s+(?:in|of|for)?\s+(?<key>[A-Ga-g][b#♭♯]?)\s*(?<quality>maj(?:or)?|min(?:or)?)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Map pitch-letter spellings → semitone PC (0..11)
    private static readonly Dictionary<string, int> RootPc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"]  = 0,  ["C#"] = 1, ["Db"] = 1, ["C♯"] = 1, ["D♭"] = 1,
        ["D"]  = 2,  ["D#"] = 3, ["Eb"] = 3, ["D♯"] = 3, ["E♭"] = 3,
        ["E"]  = 4,
        ["F"]  = 5,  ["F#"] = 6, ["Gb"] = 6, ["F♯"] = 6, ["G♭"] = 6,
        ["G"]  = 7,  ["G#"] = 8, ["Ab"] = 8, ["G♯"] = 8, ["A♭"] = 8,
        ["A"]  = 9,  ["A#"] = 10, ["Bb"] = 10, ["A♯"] = 10, ["B♭"] = 10,
        ["B"]  = 11,
    };

    // Circle-of-fifths position → key letter for major keys (sharps positive, flats negative)
    private static readonly string[] MajorByFifth =
        // -7   -6   -5   -4   -3   -2  -1  0  +1  +2  +3  +4  +5   +6   +7
        ["Cb", "Gb", "Db", "Ab", "Eb", "Bb", "F", "C", "G", "D", "A", "E", "B", "F#", "C#"];

    private static readonly string[] MinorByFifth =
        // -7    -6    -5   -4   -3   -2  -1  0   +1  +2  +3   +4   +5   +6   +7
        ["Abm", "Ebm", "Bbm", "Fm", "Cm", "Gm", "Dm", "Am", "Em", "Bm", "F#m", "C#m", "G#m", "D#m", "A#m"];

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;

        // Order matters — match the more-specific patterns first.
        if (RelativeMinorPattern.Match(msg) is { Success: true } m1)
            return Task.FromResult(AnswerRelativeMinor(NormalizeKey(m1.Groups["key"].Value)));
        if (RelativeMajorPattern.Match(msg) is { Success: true } m2)
            return Task.FromResult(AnswerRelativeMajor(NormalizeKey(m2.Groups["key"].Value)));
        if (ParallelMinorPattern.Match(msg) is { Success: true } m3)
            return Task.FromResult(AnswerParallelMinor(NormalizeKey(m3.Groups["key"].Value)));
        if (ParallelMajorPattern.Match(msg) is { Success: true } m4)
            return Task.FromResult(AnswerParallelMajor(NormalizeKey(m4.Groups["key"].Value)));
        if (KeySignaturePattern.Match(msg) is { Success: true } m5)
        {
            var key = NormalizeKey(m5.Groups["key"].Value);
            var quality = m5.Groups["quality"].Value;
            var isMinor = quality.StartsWith("min", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(AnswerKeySignature(key, isMinor));
        }

        return Task.FromResult(CannotHandle());
    }

    private AgentResponse AnswerRelativeMinor(string majorKey)
    {
        // Index-by-fifths lookup picks the enharmonic that matches the user's
        // input spelling. The previous LabelByFifth(pc, isMinor) approach
        // always returned flat-side spellings because the parallel arrays
        // place flats at low indices — F# major incorrectly returned "Ebm"
        // instead of "D#m". Caught by the 2026-05-13 multi-LLM correctness
        // review (PR #210). Now: relative minor shares the key signature, so
        // its index in MinorByFifth equals the major's index in MajorByFifth.
        var fifthsIndex = TryMajorIndex(majorKey);
        if (fifthsIndex is null)
            return CannotParse(majorKey);
        var relMinor = MinorByFifth[fifthsIndex.Value];
        var sharps = fifthsIndex.Value - 7;
        var sb = new StringBuilder();
        sb.AppendLine($"The relative minor of **{majorKey} major** is **{relMinor}**.");
        sb.AppendLine();
        sb.AppendLine($"Both share the same key signature ({KeySignatureBlurb(sharps)}). Same notes, different tonal center — the relative minor starts on the 6th degree of the major scale.");
        return Result(sb.ToString(), $"relative-minor({majorKey}→{relMinor})");
    }

    private AgentResponse AnswerRelativeMajor(string minorKey)
    {
        // Same correction as AnswerRelativeMinor — pick enharmonic from the
        // user's input spelling rather than a fixed flat-preference. G# minor
        // now correctly returns "B major" (5 sharps) instead of "Cb major"
        // (7 flats).
        var fifthsIndex = TryMinorIndex(minorKey);
        if (fifthsIndex is null)
            return CannotParse(minorKey);
        var relMajor = MajorByFifth[fifthsIndex.Value];
        var sharps = fifthsIndex.Value - 7;
        var sb = new StringBuilder();
        sb.AppendLine($"The relative major of **{minorKey} minor** is **{relMajor} major**.");
        sb.AppendLine();
        sb.AppendLine($"Both share the same key signature ({KeySignatureBlurb(sharps)}). Same notes, different tonal center — the relative major starts on the 3rd degree of the minor scale.");
        return Result(sb.ToString(), $"relative-major({minorKey}→{relMajor})");
    }

    private AgentResponse AnswerParallelMinor(string majorKey)
    {
        if (!RootPc.TryGetValue(majorKey, out _))
            return CannotParse(majorKey);
        var sharpsMaj = MajorSharpsFlats(majorKey);
        var sharpsMin = sharpsMaj - 3;  // parallel minor sits 3 positions counter-clockwise on the circle of fifths
        var sb = new StringBuilder();
        sb.AppendLine($"The parallel minor of **{majorKey} major** is **{majorKey} minor**.");
        sb.AppendLine();
        sb.AppendLine($"Same root note (**{majorKey}**) but different scales — the parallel minor lowers the 3rd, 6th, and 7th degrees. {majorKey} major has {KeySignatureBlurb(sharpsMaj)}; {majorKey} minor has {KeySignatureBlurb(sharpsMin)} (three positions counter-clockwise on the circle of fifths).");
        return Result(sb.ToString(), $"parallel-minor({majorKey})");
    }

    private AgentResponse AnswerParallelMajor(string minorKey)
    {
        if (!RootPc.TryGetValue(minorKey, out _))
            return CannotParse(minorKey);
        var sb = new StringBuilder();
        sb.AppendLine($"The parallel major of **{minorKey} minor** is **{minorKey} major**.");
        sb.AppendLine();
        sb.AppendLine($"Same root note (**{minorKey}**) but different scales — the parallel major raises the 3rd, 6th, and 7th degrees. The parallel major sits three positions clockwise on the circle of fifths.");
        return Result(sb.ToString(), $"parallel-major({minorKey})");
    }

    private AgentResponse AnswerKeySignature(string key, bool isMinor)
    {
        if (!RootPc.TryGetValue(key, out _))
            return CannotParse(key);
        var sharps = isMinor ? MinorSharpsFlats(key) : MajorSharpsFlats(key);
        var qualityWord = isMinor ? "minor" : "major";
        return Result(
            $"**{key} {qualityWord}** has {KeySignatureBlurb(sharps)}.",
            $"key-signature({key} {qualityWord}={sharps})");
    }

    private static int? TryMajorIndex(string majorKey)
    {
        for (var i = 0; i < MajorByFifth.Length; i++)
            if (string.Equals(MajorByFifth[i], majorKey, StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    private static int? TryMinorIndex(string minorKey)
    {
        var withM = minorKey.EndsWith("m", StringComparison.OrdinalIgnoreCase) ? minorKey : minorKey + "m";
        for (var i = 0; i < MinorByFifth.Length; i++)
            if (string.Equals(MinorByFifth[i], withM, StringComparison.OrdinalIgnoreCase))
                return i;
        return null;
    }

    private static int MajorSharpsFlats(string majorKey) =>
        TryMajorIndex(majorKey) is { } i ? i - 7 : 0;

    private static int MinorSharpsFlats(string minorKey) =>
        TryMinorIndex(minorKey) is { } i ? i - 7 : 0;

    private static string KeySignatureBlurb(int sharpsFlats) =>
        sharpsFlats switch
        {
            0  => "no sharps or flats",
            1  => "1 sharp",
            -1 => "1 flat",
            > 0 => $"{sharpsFlats} sharps",
            _   => $"{-sharpsFlats} flats",
        };

    private static string NormalizeKey(string raw) =>
        // Capitalize letter, normalize unicode accidentals to ASCII
        raw.Replace("♯", "#").Replace("♭", "b") switch
        {
            { Length: 1 } single => single.ToUpperInvariant(),
            { Length: 2 } pair  => char.ToUpperInvariant(pair[0]) + pair[1].ToString().ToLowerInvariant(),
            var other            => other,
        };

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("RelativeKeySkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = [$"Source: RelativeKeySkill (pure pitch-class arithmetic)", evidence],
        };
    }

    private static AgentResponse CannotParse(string key) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't identify '{key}' as a key. Try a single pitch letter optionally followed by # or b (e.g. C, G, F#, Bb).",
        Confidence = 0.3f,
        Evidence   = [$"RelativeKeySkill: unparseable key token '{key}'"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask about the relative or parallel key of a given major/minor key, or how many sharps/flats a key has.",
        Confidence = 0.1f,
        Evidence   = ["RelativeKeySkill: no recognised pattern in query"],
    };
}
