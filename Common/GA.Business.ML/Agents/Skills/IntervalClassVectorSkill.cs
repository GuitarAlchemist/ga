namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;

/// <summary>
/// Domain-backed interval-class-vector skill. Computes the ICV (the 6-tuple
/// counting occurrences of each interval class) for a chord, scale, or
/// explicit pitch-class set. Returns the vector plus a per-component
/// interpretation.
/// Surface forms:
/// <list type="bullet">
/// <item><b>"What is the ICV of Cmaj7"</b></item>
/// <item><b>"Interval-class vector of {0,2,4,5,7,9,11}"</b></item>
/// <item><b>"Compute ICV for the major scale"</b></item>
/// <item><b>"ICV of Dm7"</b></item>
/// </list>
/// Zero LLM calls — calls <see cref="IGrothendieckService.ComputeIcv"/>
/// directly. Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14, stolen-from-demo per user request. The
/// <c>/test/grothendieck-dsl</c> demo exposes ICV computation as a numeric
/// output; this skill surfaces it conversationally with named interval-class
/// glosses (ic1 = semitone, ic2 = whole tone, ...).
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("IntervalClassVectorSkill", "atonal")]
public sealed class IntervalClassVectorSkill(
    IGrothendieckService grothendieck,
    ILogger<IntervalClassVectorSkill> logger) : IOrchestratorSkill
{
    public string Name => "IntervalClassVector";
    public string Description =>
        "Computes the interval-class vector (ICV) for a chord, scale, or " +
        "explicit pitch-class set. Returns the 6-tuple counting occurrences " +
        "of each interval class (ic1 semitone..ic6 tritone) plus a per-component " +
        "interpretation. Use to answer 'what is the ICV of Cmaj7' or " +
        "'compute the interval-class vector for {0,2,4,5,7,9,11}'.";

    // This skill OWNS the "ICV / interval-class vector OF a single set" intent,
    // so anchors keep the ICV vocabulary (the discriminator vs the neighbors /
    // delta / path skills, which were de-ICV'd in the 2026-06-16 routing-
    // ambiguity curation). Dropped the terse bare "ICV of X" duplicates that
    // collided most with IcvNeighborsSkill.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        "what is the interval-class vector of Cmaj7",
        "interval class vector of Dm7",
        "compute the ICV of the major scale",
        "interval-class vector of {0,2,4,5,7,9,11}",
        "what's the interval vector for G7",
        "how many tritones does Cmaj7 contain",
        "compute the interval-class vector of Fmaj7",
        "ICV of the dorian mode",
        "interval class vector for {0,1,4,8}",
        "what's the interval content of Am",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Chord-anchored: "ICV of <chord>" / "interval-class vector of <chord>"
    private static readonly Regex ChordPattern =
        new(@"\b(?:icv|interval[\s-]*class[\s-]*vector|interval[\s-]*vector)\s+(?:of\s+|for\s+)?(?<chord>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Pitch-class set anchored: "{0,1,4}" or "(0,1,4)" or "[0,1,4]"
    private static readonly Regex PcSetPattern =
        new(@"[\{\(\[]\s*(?<pcs>\d{1,2}(?:\s*,\s*\d{1,2}){0,11})\s*[\}\)\]]",
            RegexOptions.Compiled);

    // Mode/scale-name anchor — fall back to a small lookup
    private static readonly Regex ScaleNamePattern =
        new(@"\b(?:icv|interval[\s-]*class[\s-]*vector|interval[\s-]*vector)\b[^.?!]*?\b(?<scale>major(?:\s+scale)?|minor(?:\s+scale)?|natural\s+minor|harmonic\s+minor|melodic\s+minor|ionian|dorian|phrygian|lydian|mixolydian|aeolian|locrian|pentatonic|blues|chromatic|whole\s*tone)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, int[]> ScalePcs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["major"]          = [0, 2, 4, 5, 7, 9, 11],
        ["major scale"]    = [0, 2, 4, 5, 7, 9, 11],
        ["minor"]          = [0, 2, 3, 5, 7, 8, 10],
        ["minor scale"]    = [0, 2, 3, 5, 7, 8, 10],
        ["natural minor"]  = [0, 2, 3, 5, 7, 8, 10],
        ["harmonic minor"] = [0, 2, 3, 5, 7, 8, 11],
        ["melodic minor"]  = [0, 2, 3, 5, 7, 9, 11],
        ["ionian"]         = [0, 2, 4, 5, 7, 9, 11],
        ["dorian"]         = [0, 2, 3, 5, 7, 9, 10],
        ["phrygian"]       = [0, 1, 3, 5, 7, 8, 10],
        ["lydian"]         = [0, 2, 4, 6, 7, 9, 11],
        ["mixolydian"]     = [0, 2, 4, 5, 7, 9, 10],
        ["aeolian"]        = [0, 2, 3, 5, 7, 8, 10],
        ["locrian"]        = [0, 1, 3, 5, 6, 8, 10],
        ["pentatonic"]     = [0, 2, 4, 7, 9],
        ["blues"]          = [0, 3, 5, 6, 7, 10],
        ["chromatic"]      = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
        ["whole tone"]     = [0, 2, 4, 6, 8, 10],
        ["wholetone"]      = [0, 2, 4, 6, 8, 10],
    };

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

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;

        // PC-set form is most specific — try first.
        if (PcSetPattern.Match(msg) is { Success: true } pcSetMatch)
        {
            var rawPcs = pcSetMatch.Groups["pcs"].Value;
            var pcs = rawPcs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var n) ? n : -1)
                .Where(n => n is >= 0 and <= 11)
                .Distinct()
                .ToList();
            if (pcs.Count >= 2)
                return Task.FromResult(AnswerPcSet($"{{{string.Join(",", pcs)}}}", pcs));
        }

        if (ChordPattern.Match(msg) is { Success: true } chordMatch)
        {
            var chordToken = chordMatch.Groups["chord"].Value;
            var pcs = TryBuildChordPcs(chordToken);
            if (pcs is null) return Task.FromResult(CannotParse(chordToken));
            return Task.FromResult(AnswerPcSet(chordToken, pcs));
        }

        if (ScaleNamePattern.Match(msg) is { Success: true } scaleMatch)
        {
            var scaleName = scaleMatch.Groups["scale"].Value.ToLowerInvariant().Trim();
            if (ScalePcs.TryGetValue(scaleName, out var pcs))
                return Task.FromResult(AnswerPcSet($"the {scaleName}", [.. pcs]));
        }

        return Task.FromResult(CannotHandle());
    }

    private AgentResponse AnswerPcSet(string label, IReadOnlyList<int> pcs)
    {
        var icv = grothendieck.ComputeIcv(pcs);
        var ic1 = icv[IntervalClass.Hemitone];
        var ic2 = icv[IntervalClass.Tone];
        var ic3 = icv[IntervalClass.FromValue(3)];
        var ic4 = icv[IntervalClass.FromValue(4)];
        var ic5 = icv[IntervalClass.FromValue(5)];
        var ic6 = icv[IntervalClass.Tritone];

        var pcsStr = "{" + string.Join(", ", pcs.OrderBy(p => p)) + "}";
        var sb = new StringBuilder();
        sb.AppendLine($"**ICV of {label}**: `{icv}`  (`[{ic1}, {ic2}, {ic3}, {ic4}, {ic5}, {ic6}]`)");
        sb.AppendLine();
        sb.AppendLine($"- Pitch-class set: `{pcsStr}` ({pcs.Count} notes)");
        sb.AppendLine();
        sb.AppendLine("| ic | Interval | Count |");
        sb.AppendLine("|----|----------|-------|");
        sb.AppendLine($"| ic1 | semitone (m2 / M7) | {ic1} |");
        sb.AppendLine($"| ic2 | whole tone (M2 / m7) | {ic2} |");
        sb.AppendLine($"| ic3 | minor 3rd (m3 / M6) | {ic3} |");
        sb.AppendLine($"| ic4 | major 3rd (M3 / m6) | {ic4} |");
        sb.AppendLine($"| ic5 | perfect 4th (P4 / P5) | {ic5} |");
        sb.AppendLine($"| ic6 | tritone (TT) | {ic6} |");
        sb.AppendLine();
        sb.AppendLine(
            "The interval-class vector counts how many of each interval class " +
            "appear among all pairs of notes in the set (interval classes 1..6 " +
            "fold inversions: a P5 and a P4 both count as ic5). Modes of the " +
            "same scale share the same ICV — they're interval-content twins.");

        return Result(sb.ToString(), $"icv({label}={icv})");
    }

    private static int[]? TryBuildChordPcs(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var t = token.Replace("♯", "#").Replace("♭", "b").Trim();

        var rootLen = t.Length >= 2 && (t[1] == '#' || t[1] == 'b') ? 2 : 1;
        if (rootLen > t.Length) return null;
        var rootStr = char.ToUpperInvariant(t[0]) + (rootLen == 2 ? t[1].ToString().ToLowerInvariant() : "");
        if (!RootPc.TryGetValue(rootStr, out var root)) return null;

        var quality = t.Length > rootLen ? t[rootLen..] : string.Empty;
        var intervals = QualityIntervals(quality);
        return [.. intervals.Select(i => (root + i) % 12).Distinct()];
    }

    private static int[] QualityIntervals(string quality)
    {
        var q = quality.ToLowerInvariant().Trim();
        if (q == string.Empty || q == "maj" || q == "major") return [0, 4, 7];
        if (q is "m" or "min" or "minor" or "-") return [0, 3, 7];
        if (q is "dim" or "°" or "o") return [0, 3, 6];
        if (q is "aug" or "+") return [0, 4, 8];
        if (q is "7") return [0, 4, 7, 10];
        if (q is "m7" or "min7" or "-7") return [0, 3, 7, 10];
        if (q is "maj7" or "major7" or "M7") return [0, 4, 7, 11];
        if (q is "m7b5" or "min7b5" or "ø" or "ø7") return [0, 3, 6, 10];
        if (q is "dim7" or "°7" or "o7") return [0, 3, 6, 9];
        if (q is "sus2") return [0, 2, 7];
        if (q is "sus4" or "sus") return [0, 5, 7];
        if (q is "6") return [0, 4, 7, 9];
        if (q is "m6") return [0, 3, 7, 9];
        if (q is "9") return [0, 4, 7, 10, 2];
        if (q is "maj9") return [0, 4, 7, 11, 2];
        if (q is "m9") return [0, 3, 7, 10, 2];
        return [0, 4, 7];
    }

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("IntervalClassVectorSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: IntervalClassVectorSkill (in-process GrothendieckService.ComputeIcv)", evidence],
        };
    }

    private static AgentResponse CannotParse(string chord) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't parse '{chord}' as a chord. Try C, Am, G7, Cmaj7, Dm7, F#m7b5, B°, sus2/sus4, m9/maj9, etc.",
        Confidence = 0.3f,
        Evidence   = [$"IntervalClassVectorSkill: unparseable chord token '{chord}'"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask for an ICV of a chord, scale, or explicit pitch-class set, e.g. \"ICV of Cmaj7\", \"interval-class vector of {0,2,4,5,7,9,11}\", \"ICV of the dorian mode\".",
        Confidence = 0.1f,
        Evidence   = ["IntervalClassVectorSkill: no recognised target in query"],
    };
}
