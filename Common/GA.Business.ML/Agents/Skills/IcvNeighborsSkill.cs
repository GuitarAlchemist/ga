namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;

/// <summary>
/// Domain-backed ICV-neighbors skill. Given a single chord (or pitch-class
/// set), returns nearby pitch-class sets within a small harmonic radius —
/// pitch-class collections whose interval-class vector is within
/// <see cref="DefaultMaxDistance"/> L1 of the source's ICV. Answers questions
/// like "what chords are harmonically close to Cmaj7" — the same operation
/// the <c>/test/grothendieck-dsl</c> demo exposes via its nearby-shapes panel,
/// adapted to conversational form.
/// Surface forms:
/// <list type="bullet">
/// <item><b>"What chords are harmonically close to Cmaj7"</b></item>
/// <item><b>"Nearby pitch-class sets to C major"</b></item>
/// <item><b>"Find ICV neighbors of Dm7"</b></item>
/// <item><b>"Closest chord to G7 in ICV space"</b></item>
/// </list>
/// Zero LLM calls — calls <see cref="IGrothendieckService.FindNearby"/>
/// directly. Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14, stolen-from-demo per user request.
/// <para>
/// Note that <see cref="IGrothendieckService.FindNearby"/> enumerates over
/// <see cref="PitchClassSet.Items"/> (every possible PC set). Without a
/// distance cap this is 2^12 sets — fine performance-wise but the result
/// list would be enormous. We cap at <see cref="DefaultMaxDistance"/> = 2
/// and surface the top <see cref="MaxNeighborsToShow"/> = 8 by cost.
/// </para>
/// </remarks>
public sealed class IcvNeighborsSkill(
    IGrothendieckService grothendieck,
    ILogger<IcvNeighborsSkill> logger) : IOrchestratorSkill
{
    public string Name => "IcvNeighbors";
    public string Description =>
        "Finds pitch-class sets that are harmonically close to a given chord — " +
        "the nearby-shapes feature of the Grothendieck demo, exposed in the " +
        "chatbot. Returns the closest pitch-class sets by L1 distance over " +
        "their interval-class vectors, with the signed delta and harmonic " +
        "cost for each. Use to answer 'what chord/scale is harmonically near " +
        "Cmaj7' or 'find ICV neighbors of Dm7'.";

    // Routing anchors deliberately emphasise the user GOAL — "find similar /
    // nearby chords" — and avoid the bare "ICV" framing, which is owned by
    // IntervalClassVectorSkill (computing the vector of ONE chord). The
    // routing-ambiguity diagnostic (2026-06-16) measured this skill at the
    // worst silhouette (-0.05), colliding with intervalclassvector at 0.90
    // cosine precisely on shared "ICV … <chord>" phrasings. Leaning on
    // "similar/nearby/closest/neighbors" is the discriminator.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        "what chords are harmonically close to Cmaj7",
        "which chords are most similar to Dm7",
        "nearby chords to C major",
        "closest chords to G7 by interval content",
        "what's harmonically adjacent to F major",
        "find chords similar to Gmaj7",
        "harmonic neighbors of E minor",
        "chords with similar interval content to Bm7b5",
        "give me chords close to Am",
        "nearest chords to Fmaj7",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    private const int DefaultMaxDistance = 2;
    private const int MaxNeighborsToShow = 8;

    // Single-chord pattern — anchored on "neighbors/near/close/adjacent" + a
    // chord token. Word boundary protects against routing on prose that
    // happens to mention a single chord-letter.
    private static readonly Regex NeighborsPattern =
        new(@"\b(?:icv\s+neighbors?|neighbors?|nearby|close\s+to|near|adjacent|harmonic(?:ally)?\s+(?:close|near|adjacent))\s+(?:to\s+|of\s+)?(?<chord>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Reverse anchor: "<chord> ... (icv-)neighbors" / "<chord> ... close"
    private static readonly Regex NeighborsPatternReverse =
        new(@"\b(?<chord>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b[^.?!]*?\b(?:icv\s+neighbors?|neighbors?|harmonic(?:ally)?\s+(?:close|near|adjacent))\b",
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

        Match? hit = NeighborsPattern.Match(msg) is { Success: true } m1 ? m1
                   : NeighborsPatternReverse.Match(msg) is { Success: true } m2 ? m2
                   : null;
        if (hit is null)
            return Task.FromResult(CannotHandle());

        var chordToken = hit.Groups["chord"].Value;
        var pcSet = TryBuildPcSet(chordToken);
        if (pcSet is null)
            return Task.FromResult(CannotParse(chordToken));

        return Task.FromResult(AnswerNeighbors(chordToken, pcSet));
    }

    private AgentResponse AnswerNeighbors(string chordLabel, PitchClassSet source)
    {
        // We bypass the (n.Delta, n.Cost) values FindNearby returns because
        // GrothendieckDelta.FromIcVs injects a synthetic Ic1=+1 when the
        // true delta would be zero — see correctness-review 2026-05-14. That
        // heuristic helps differentiate same-ICV transpositions in OTHER
        // call sites, but it makes the neighbor table mis-label set-class
        // equivalents as "L1=1". For this skill the user expects honest
        // distances, so we recompute the per-component delta directly and
        // derive L1 from that.
        var sourceIcv = source.IntervalClassVector;
        var rawNeighbors = grothendieck.FindNearby(source, DefaultMaxDistance + 1).ToList();

        var neighbors = rawNeighbors
            .Select(n => (n.Set, Delta: ComputeTrueDelta(sourceIcv, n.Set.IntervalClassVector)))
            .Where(t => !ReferenceEquals(t.Set, source))           // skip the source itself
            .Where(t => t.Delta.l1 > 0)                            // skip exact ICV-identical (same set class)
            .Where(t => t.Delta.l1 <= DefaultMaxDistance)
            .OrderBy(t => t.Delta.l1)
            .Take(MaxNeighborsToShow)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"**ICV neighbors of {chordLabel}** (within L1 = {DefaultMaxDistance}):");
        sb.AppendLine();
        sb.AppendLine($"- Source ICV: `{source.IntervalClassVector}`");
        sb.AppendLine($"- {neighbors.Count} neighbor{(neighbors.Count == 1 ? "" : "s")} returned (top {MaxNeighborsToShow} by harmonic cost):");
        sb.AppendLine();

        if (neighbors.Count == 0)
        {
            sb.AppendLine($"No pitch-class sets within L1 = {DefaultMaxDistance}. Try a wider radius.");
            return Result(sb.ToString(), $"icv-neighbors({chordLabel}, 0 results)");
        }

        sb.AppendLine("| Neighbor (PC set) | ICV | L1 | Harmonic cost |");
        sb.AppendLine("|-------------------|-----|----|----|");
        foreach (var (set, delta) in neighbors)
        {
            var pcsString = "{" + string.Join(",", set.Select(pc => (int)pc.Value)) + "}";
            // Harmonic cost mirrors GrothendieckService.ComputeHarmonicCost
            // (L1 × 0.6 scaling factor). Apply the same scalar so the table
            // is comparable to the upstream service contract.
            var cost = delta.l1 * 0.6;
            sb.AppendLine($"| `{pcsString}` | `{set.IntervalClassVector}` | {delta.l1} | {cost:F2} |");
        }

        sb.AppendLine();
        sb.AppendLine(
            $"Each row is a pitch-class set whose interval-class content sits within " +
            $"{DefaultMaxDistance} L1 steps of {chordLabel}'s ICV. Sorted by harmonic " +
            $"cost (lower = more similar). The PC set is the literal collection of " +
            $"pitches (mod 12); the ICV is the unordered count of intervals it contains.");

        return Result(sb.ToString(), $"icv-neighbors({chordLabel}, {neighbors.Count} of L1≤{DefaultMaxDistance})");
    }

    /// <summary>
    /// Per-component honest delta: target − source, with L1 derived directly.
    /// Returns (Δic1..Δic6, L1Norm) as a tuple to avoid carrying the upstream
    /// GrothendieckDelta record (which would re-inject the +1 heuristic on
    /// equal ICVs).
    /// </summary>
    private static (int ic1, int ic2, int ic3, int ic4, int ic5, int ic6, int l1)
        ComputeTrueDelta(IntervalClassVector src, IntervalClassVector tgt)
    {
        var d1 = tgt[IntervalClass.Hemitone] - src[IntervalClass.Hemitone];
        var d2 = tgt[IntervalClass.Tone] - src[IntervalClass.Tone];
        var d3 = tgt[IntervalClass.FromValue(3)] - src[IntervalClass.FromValue(3)];
        var d4 = tgt[IntervalClass.FromValue(4)] - src[IntervalClass.FromValue(4)];
        var d5 = tgt[IntervalClass.FromValue(5)] - src[IntervalClass.FromValue(5)];
        var d6 = tgt[IntervalClass.Tritone] - src[IntervalClass.Tritone];
        var l1 = Math.Abs(d1) + Math.Abs(d2) + Math.Abs(d3) + Math.Abs(d4) + Math.Abs(d5) + Math.Abs(d6);
        return (d1, d2, d3, d4, d5, d6, l1);
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
        logger.LogDebug("IcvNeighborsSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: IcvNeighborsSkill (in-process GrothendieckService.FindNearby)", evidence],
        };
    }

    private AgentResponse CannotParse(string chord) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't parse '{chord}' as a chord. Try C, Am, G7, Cmaj7, Dm7, F#m7b5, B°, sus2/sus4, m9/maj9, etc.",
        Confidence = 0.3f,
        Evidence   = [$"IcvNeighborsSkill: unparseable chord token '{chord}'"],
    };

    private AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask about ICV-neighbor pitch-class sets near a chord, e.g. \"ICV neighbors of Cmaj7\" or \"what chords are harmonically close to Dm7\".",
        Confidence = 0.1f,
        Evidence   = ["IcvNeighborsSkill: no neighbor pattern in query"],
    };
}
