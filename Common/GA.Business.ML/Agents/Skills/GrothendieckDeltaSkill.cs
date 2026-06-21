namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;

/// <summary>
/// Domain-backed Grothendieck-delta skill. Given two chords (or two pitch-class
/// sets), computes the signed interval-class-vector difference, its L1 (Manhattan)
/// norm, and the harmonic cost via <see cref="IGrothendieckService.ComputeHarmonicCost"/>.
/// Surface forms:
/// <list type="bullet">
/// <item><b>"Harmonic distance from Cmaj7 to G7"</b></item>
/// <item><b>"Grothendieck delta C to F"</b></item>
/// <item><b>"How harmonically far is Am from D7"</b></item>
/// <item><b>"Compare the ICVs of Cmaj7 and Dm7"</b></item>
/// </list>
/// Zero LLM calls — calls <see cref="IGrothendieckService.ComputeDelta"/> directly.
/// Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14, stolen-from-demo per user request — adopts the same
/// <c>GrothendieckService</c> the <c>/test/grothendieck-dsl</c> demo uses,
/// but exposes it through the chatbot's conversational surface.
/// </remarks>
public sealed class GrothendieckDeltaSkill(
    IGrothendieckService grothendieck,
    ILogger<GrothendieckDeltaSkill> logger) : IOrchestratorSkill
{
    public string Name => "GrothendieckDelta";
    public string Description =>
        "Computes the Grothendieck delta between two chords or pitch-class " +
        "sets: signed interval-class-vector difference, L1 (Manhattan) norm, " +
        "and harmonic cost. Use to answer 'how harmonically far is X from Y' " +
        "or 'what is the ICV difference between Cmaj7 and G7'. Calls into " +
        "the GrothendieckService directly — no LLM, no backend HTTP hop.";

    // This skill answers "how harmonically FAR is X from Y" (a distance between
    // TWO chords). Anchors emphasise distance/gap/cost between a pair, and drop
    // the "compare the ICVs" / "ICV difference" framings that collided with
    // IntervalClassVectorSkill (single-set ICV) in the 2026-06-16 routing-
    // ambiguity diagnostic.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        "how harmonically far is Am from D7",
        "harmonic distance from Cmaj7 to G7",
        "how far apart are Cmaj7 and Dm7 harmonically",
        "harmonic cost to move from C to G",
        "how different are C major and F major harmonically",
        "harmonic distance between Am and Em",
        "how close are Cmaj7 and Fmaj7 harmonically",
        "L1 distance from Gmaj7 to Bm7b5",
        "Grothendieck delta from C to F",
        "measure the harmonic gap between Dm7 and G7",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Two-chord pattern shared with VoiceLeadingSkill — anchored on
    // "<chord A> to/and <chord B>" with a permissive chord token. Pre-anchored
    // by routing hint, so substring overlap with unrelated phrases is unlikely.
    private static readonly Regex TwoChordPattern =
        new(@"\b(?<a>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\s+(?:to|and|→|->|>)\s+(?<b>[A-Ga-g][b#♭♯]?(?:maj|min|m|dim|aug|sus|add|dom)?\d*(?:[b#♭♯]\d+)*°?)\b",
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
        if (TwoChordPattern.Match(msg) is not { Success: true } match)
            return Task.FromResult(CannotHandle());

        var aToken = match.Groups["a"].Value;
        var bToken = match.Groups["b"].Value;

        var setA = TryBuildPcSet(aToken);
        if (setA is null) return Task.FromResult(CannotParse(aToken));
        var setB = TryBuildPcSet(bToken);
        if (setB is null) return Task.FromResult(CannotParse(bToken));

        return Task.FromResult(AnswerDelta(aToken, setA, bToken, setB));
    }

    private AgentResponse AnswerDelta(string aLabel, PitchClassSet a, string bLabel, PitchClassSet b)
    {
        var icvA = a.IntervalClassVector;
        var icvB = b.IntervalClassVector;

        var delta = grothendieck.ComputeDelta(icvA, icvB);
        var cost = grothendieck.ComputeHarmonicCost(delta);

        var sb = new StringBuilder();
        sb.AppendLine($"**Grothendieck delta {aLabel} → {bLabel}**");
        sb.AppendLine();
        sb.AppendLine($"- **ICV {aLabel}**: `{icvA}`  (ic1..ic6)");
        sb.AppendLine($"- **ICV {bLabel}**: `{icvB}`");
        sb.AppendLine($"- **Δ (signed)**: `[{delta.Ic1:+#;-#;0}, {delta.Ic2:+#;-#;0}, {delta.Ic3:+#;-#;0}, {delta.Ic4:+#;-#;0}, {delta.Ic5:+#;-#;0}, {delta.Ic6:+#;-#;0}]`");
        sb.AppendLine($"- **L1 norm** (Manhattan): **{delta.L1Norm}**");
        sb.AppendLine($"- **L2 norm** (Euclidean): **{delta.L2Norm:F3}**");
        sb.AppendLine($"- **Harmonic cost**: **{cost:F2}**");

        var explained = delta.Explain();
        if (!string.IsNullOrWhiteSpace(explained))
        {
            sb.AppendLine();
            sb.AppendLine($"**Interpretation**: {explained}");
        }

        sb.AppendLine();
        sb.AppendLine(
            "The delta is the signed change in interval-class content from " +
            $"{aLabel} to {bLabel}: each component (ic1..ic6) says how many " +
            "more occurrences of that interval-class the target has than the " +
            "source. L1 norm is the total absolute interval-class movement. " +
            "Harmonic cost weights the L1 by 0.6 — a rough musical-distance scalar.");

        return Result(sb.ToString(), $"grothendieck-delta({aLabel}→{bLabel}, L1={delta.L1Norm}, cost={cost:F2})");
    }

    /// <summary>
    /// Parse a chord token to a <see cref="PitchClassSet"/> via the same
    /// quality table that <see cref="VoiceLeadingSkill"/> uses.
    /// </summary>
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
        return [0, 4, 7];  // unknown → major triad fallback
    }

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("GrothendieckDeltaSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: GrothendieckDeltaSkill (in-process GrothendieckService)", evidence],
        };
    }

    private static AgentResponse CannotParse(string chord) => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = $"I couldn't parse '{chord}' as a chord. Try C, Am, G7, Cmaj7, Dm7, F#m7b5, B°, sus2/sus4, m9/maj9, etc.",
        Confidence = 0.3f,
        Evidence   = [$"GrothendieckDeltaSkill: unparseable chord token '{chord}'"],
    };

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask about the Grothendieck delta or harmonic distance between two named chords, e.g. \"harmonic distance from Cmaj7 to G7\" or \"delta C to F\".",
        Confidence = 0.1f,
        Evidence   = ["GrothendieckDeltaSkill: no two-chord pattern in query"],
    };
}
