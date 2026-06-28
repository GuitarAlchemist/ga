namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;

/// <summary>
/// Domain-backed ICV shortest-path skill. Given two chords (or PC-sets),
/// computes the shortest path of pitch-class sets connecting them through
/// small harmonic steps — answers "how do I get from X to Y harmonically,
/// one small move at a time?" via the BFS in
/// <see cref="IGrothendieckService.FindShortestPath"/>.
/// Surface forms:
/// <list type="bullet">
/// <item><b>"Shortest harmonic path from Cmaj7 to G7"</b></item>
/// <item><b>"Path from C major to F major"</b></item>
/// <item><b>"How do I get from Am to D7 harmonically"</b></item>
/// </list>
/// Zero LLM calls — pure BFS. Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14, stolen-from-demo per user request.
/// <para>
/// <b>Naming note</b>: this is shortest path over the <i>pitch-class-set</i>
/// graph (ICV-L1 distance metric), <b>not</b> over the <i>fretboard-shape</i>
/// graph. The two graphs use the same Grothendieck framework but answer
/// different questions:
/// </para>
/// <list type="bullet">
/// <item>This skill: "how do I move from one harmonic set to another in
/// small ICV steps" — appropriate for composers and harmony analysis.</item>
/// <item>A future <c>FretboardShortestPathSkill</c> would: "what fingering
/// sequence gets me from one voicing to another with smooth physical
/// motion" — uses diagness + ergonomics + cost, exposed by the
/// <c>/api/grothendieck/shortest-path</c> endpoint when a fretboard tuning
/// is supplied.</item>
/// </list>
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("IcvShortestPathSkill", "atonal")]
public sealed class IcvShortestPathSkill(
    IGrothendieckService grothendieck,
    ILogger<IcvShortestPathSkill> logger) : IOrchestratorSkill
{
    public string Name => "IcvShortestPath";
    public string Description =>
        "Finds the shortest harmonic path through pitch-class-set space " +
        "between two chords or pitch-class sets — the chain of intermediate " +
        "PC-sets that connects them via small ICV-L1 steps. Use to answer " +
        "'shortest harmonic path from X to Y' or 'how do I get from C major " +
        "to F major in small harmonic steps'. Pure BFS over the PC-set " +
        "graph — no LLM call.";

    // This skill returns the SEQUENCE of intermediate chords connecting two
    // chords. Anchors emphasise path/route/connect/chain/stepping-stones (the
    // discriminator vs GrothendieckDeltaSkill, which returns a single distance
    // NUMBER between the same pair), and drop "BFS"/"ICV path" jargon —
    // per the 2026-06-16 routing-ambiguity diagnostic.
    // This skill returns the SEQUENCE of chords connecting two chords. The
    // distinctive signal is "SHORTEST PATH/ROUTE" — that is what separates it
    // from GrothendieckDeltaSkill (a single distance NUMBER over the same pair).
    // The 2026-06-16 curation first tried "connect/chain/voice-leading" framings
    // and REGRESSED this skill (it collided with skill.voiceleading and read as
    // find-chords); leaning hard on "shortest … path/route" is the fix. "BFS"/
    // "ICV path" jargon stays dropped.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        "shortest harmonic path from Cmaj7 to G7",
        "shortest path from C major to F major",
        "shortest harmonic route from Am to D7",
        "shortest path from Cmaj7 to Bm7b5",
        "step-by-step harmonic route from C to G",
        "shortest chord path from Dm7 to Gmaj7",
        "shortest route from C to A minor",
        "harmonic stepping stones from Cmaj7 to Fmaj7",
        "shortest path from Gmaj7 to Em",
        "shortest harmonic path of chords from C to F",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    private const int DefaultMaxSteps = 5;

    // "shortest path / harmonic path / route from A to B"
    private static readonly Regex PathPattern =
        new(@"\b(?:shortest(?:[\s-]*harmonic)?[\s-]*(?:path|route)|harmonic[\s-]*(?:path|route)|BFS\s+path|step[\s-]*by[\s-]*step|PC[\s-]*set\s+path|ICV\s+path|harmonic\s+route)\b[^.?!]*?\b(?:from\s+)?(?<a>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\s+(?:to|→|->)\s+(?<b>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fallback — "how do I get from X to Y harmonically"
    private static readonly Regex HowDoIGetPattern =
        new(@"\bhow\s+do\s+i\s+get\s+from\s+(?<a>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\s+to\s+(?<b>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b[^.?!]*?\bharmonic",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        Match? hit = PathPattern.Match(msg) is { Success: true } m1 ? m1
                   : HowDoIGetPattern.Match(msg) is { Success: true } m2 ? m2
                   : null;
        if (hit is null)
            return Task.FromResult(CannotHandle());

        var aToken = hit.Groups["a"].Value;
        var bToken = hit.Groups["b"].Value;

        var setA = TryBuildPcSet(aToken);
        if (setA is null) return Task.FromResult(CannotParse(aToken));
        var setB = TryBuildPcSet(bToken);
        if (setB is null) return Task.FromResult(CannotParse(bToken));

        return Task.FromResult(AnswerPath(aToken, setA, bToken, setB));
    }

    private AgentResponse AnswerPath(string aLabel, PitchClassSet a, string bLabel, PitchClassSet b)
    {
        var path = grothendieck.FindShortestPath(a, b, DefaultMaxSteps).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"**Shortest harmonic path {aLabel} → {bLabel}** (max {DefaultMaxSteps} steps):");
        sb.AppendLine();

        if (path.Count == 0)
        {
            sb.AppendLine($"No path found within {DefaultMaxSteps} steps. The two pitch-class sets may be too far apart in ICV-L1 space, or the cardinality constraint (BFS holds set size constant) blocked the search.");
            return Result(sb.ToString(), $"icv-shortest-path({aLabel}→{bLabel}, no path)");
        }

        sb.AppendLine($"- **{path.Count} step{(path.Count == 1 ? "" : "s")}** total ({path.Count - 1} intermediate move{(path.Count - 1 == 1 ? "" : "s")}).");
        sb.AppendLine();
        sb.AppendLine("| Step | PC set | ICV |");
        sb.AppendLine("|------|--------|-----|");
        for (var i = 0; i < path.Count; i++)
        {
            var set = path[i];
            var pcsString = "{" + string.Join(",", set.Select(pc => (int)pc.Value)) + "}";
            var label = i == 0 ? $"0 ({aLabel})"
                      : i == path.Count - 1 ? $"{i} ({bLabel})"
                      : i.ToString();
            sb.AppendLine($"| {label} | `{pcsString}` | `{set.IntervalClassVector}` |");
        }

        sb.AppendLine();
        sb.AppendLine(
            "Each step is a small ICV-L1 move (radius ≤ 2) to a pitch-class set of " +
            "the same cardinality. The path is the shortest BFS route — useful for " +
            "exploring modulation routes, common-tone bridges, and modal interchange " +
            "between distant harmonic regions.");

        return Result(sb.ToString(), $"icv-shortest-path({aLabel}→{bLabel}, {path.Count} steps)");
    }

    private static PitchClassSet? TryBuildPcSet(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var t = token.Replace("♯", "#").Replace("♭", "b").Trim();

        var rootLen = t.Length >= 2 && (t[1] == '#' || t[1] == 'b') ? 2 : 1;
        if (rootLen > t.Length) return null;
        var rootStr = char.ToUpperInvariant(t[0]) + (rootLen == 2 ? t[1].ToString().ToLowerInvariant() : "");
        if (!RootPc.TryGetValue(rootStr, out var root)) return null;

        var quality = t.Length > rootLen ? t[rootLen..] : string.Empty;
        var intervals = QualityIntervals(quality);
        var pcs = intervals.Select(i => PitchClass.FromValue((root + i) % 12)).ToList();
        return new PitchClassSet(pcs);
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
        logger.LogDebug("IcvShortestPathSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: IcvShortestPathSkill (in-process GrothendieckService.FindShortestPath BFS)", evidence],
        };
    }

    private static AgentResponse CannotParse(string chord) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't parse '{chord}' as a chord. Try C, Am, G7, Cmaj7, Dm7, F#m7b5, B°, sus2/sus4, m9/maj9, etc.",
        Confidence = 0.3f,
        Evidence   = [$"IcvShortestPathSkill: unparseable chord token '{chord}'"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask for the shortest harmonic path between two chords, e.g. \"shortest path from Cmaj7 to G7\" or \"how do I get from C to F harmonically\".",
        Confidence = 0.1f,
        Evidence   = ["IcvShortestPathSkill: no path pattern in query"],
    };
}
