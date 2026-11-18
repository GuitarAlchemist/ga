namespace GA.Business.ML.Agents.Skills;

using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;

/// <summary>
/// Returns harmonically-close chord substitutions using Grothendieck ICV distance —
/// zero LLM calls, pure domain math.
/// </summary>
/// <remarks>
/// Supports two modes:
/// <list type="bullet">
/// <item><b>Single-chord</b>: finds nearby chords in ICV space via <see cref="IGrothendieckService.FindNearby"/>.</item>
/// <item><b>Two-chord comparison</b>: classifies the relationship type (tritone sub, secondary dominant, backdoor dominant, set-class equivalent, ICV neighbor).</item>
/// </list>
/// Both modes are pure pitch-class arithmetic — <c>Confidence = 1.0</c>.
/// </remarks>
[GuitarAlchemist.Registry.GaSkill("ChordSubstitution", "chord")]
public sealed class ChordSubstitutionSkill(
    IGrothendieckService grothendieck,
    ILogger<ChordSubstitutionSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ChordSubstitution";
    public string Description =>
        "Suggests chord substitutions and reharmonizations: tritone subs " +
        "(G7 → Db7), backdoor dominants, secondary dominants, ICV-neighbor " +
        "swaps, set-class equivalents, alternative replacement chords for " +
        "any given chord, and classification of the relationship between " +
        "two named chords.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Tritone substitution for G7",
        "What can I substitute for Cmaj7 in a ii-V-I?",
        "Reharmonize Dm7 in a jazz context",
        "Alternative chord for F major in C",
        "What's the secondary dominant of Am?",
        "Show me a backdoor dominant for C major",
        // Bare "alternative chord for [chord]" without "in [key]" suffix —
        // was losing to DiatonicChordsSkill because the in-key version
        // pulled examples toward diatonic territory. Added 2026-05-12.
        "Alternative chord for Cmaj7",
        "What can replace Dm7?",
        "Swap chord for G7",
        // "Modal interchange" pattern — was losing to TransposeSkill
        // (modal-interchange semantically near "modal" + the "for C major"
        // tail was reading as a target key). Added 2026-05-12.
        "Modal interchange substitutes for C major",
        "Borrow a chord from parallel minor",
        "Modal interchange options in F major",
    ];

    // ── Triggers ──────────────────────────────────────────────────────────────

    private static readonly Regex SubstituteTrigger =
        new(@"\b(substitut|reharmoni|instead\s+of|alternative\s+chord|swap\s+chord|replace\s+chord)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Additional comparison keywords for the two-chord path
    private static readonly Regex TwoChordTrigger =
        new(@"\b(?:same|related|equivalent|tritone)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Extended chord symbol pattern — matches triads AND 7th chords (longer alternations first).
    // Case-sensitive: "M7" is major 7, "m7" minor 7. A bare "M" is left out so prose such as
    // "I AM" does not read as an A major chord.
    private static readonly Regex ExtendedChordSymbol =
        new(@"\b(?<root>[A-G])(?<acc>[b#]?)(?<qual>m7b5|dim7|maj7|M7|m7|7|min|m|dim|aug|\+)?(?!\w)",
            RegexOptions.Compiled);

    // A root named after "in" or "key of" is the key of the question ("in C major", "in the key
    // of G"), not a chord to substitute. A key is major or minor, so a symbol with any other
    // quality ("in G7") still names a chord.
    private static readonly Regex KeyContextBefore =
        new(@"\b(?:in|key\s+of)\s+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsKeyContext(string message, Match m) =>
        m.Groups["qual"].Value is "" or "m" or "min" && KeyContextBefore.IsMatch(message.AsSpan(0, m.Index));

    private static List<Match> ChordMatches(string message) =>
        [.. ExtendedChordSymbol.Matches(message).Where(m => !IsKeyContext(message, m))];

    public bool CanHandle(string message) =>
        ExtendedChordSymbol.IsMatch(message) &&
        (SubstituteTrigger.IsMatch(message) || TwoChordTrigger.IsMatch(message));

    // ── Domain data ───────────────────────────────────────────────────────────

    // Canonical root names (prefer flats except F# and C#)
    private static readonly string[] RootNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // Dominant 7th intervals used in tritone/backdoor checks
    private static readonly int[] Dom7 = [0, 4, 7, 10];

    // Template → name suffix (for bitmask → chord name lookup)
    private static readonly (int[] Intervals, string Suffix)[] ChordTemplates =
    [
        ([0, 4, 7],       ""),
        ([0, 3, 7],       "m"),
        ([0, 3, 6],       "dim"),
        ([0, 4, 8],       "aug"),
        ([0, 4, 7, 10],   "7"),
        ([0, 4, 7, 11],   "maj7"),
        ([0, 3, 7, 10],   "m7"),
        ([0, 3, 6, 10],   "m7b5"),
        ([0, 3, 6, 9],    "dim7"),
    ];

    // Pre-computed bitmask → chord name
    private static readonly FrozenDictionary<int, string> MaskToChord = BuildMaskToChord();

    private static FrozenDictionary<int, string> BuildMaskToChord()
    {
        var map = new Dictionary<int, string>();
        for (var root = 0; root < 12; root++)
        {
            foreach (var (intervals, suffix) in ChordTemplates)
            {
                var mask = intervals.Aggregate(0, (acc, i) => acc | (1 << ((root + i) % 12)));
                map.TryAdd(mask, $"{RootNames[root]}{suffix}");
            }
        }
        return map.ToFrozenDictionary();
    }

    // ── Execute ───────────────────────────────────────────────────────────────

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        // Try to extract up to two chord symbols (extended, includes 7th chords)
        var chordMatches = ChordMatches(message);
        var chords = chordMatches
            .Select(TryParseChordMatch)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .Take(2)
            .ToList();

        if (chords.Count == 2)
            return Task.FromResult(ExecuteComparison(chords[0], chords[1]));

        // Single-chord path — original behaviour
        var parsed = chordMatches.Count == 0 ? null : ParseChord(chordMatches[0]);
        if (parsed is null)
            return Task.FromResult(AgentResponse.Decline(AgentIds.Composer, "Could not identify a chord symbol in your message."));

        var (chordName, sourceSet) = parsed.Value;

        // Exclude the source from its own substitution list. The previous
        // ReferenceEquals check only worked if the catalog interned the EXACT
        // instance built locally — it doesn't, so the source could leak into
        // its own results. Compare by pitch-class mask instead (PR #85 fix).
        var sourceMask = sourceSet.PitchClassMask;
        var nearby = grothendieck.FindNearby(sourceSet, maxDistance: 3)
            .Where(r => r.Set.PitchClassMask != sourceMask
                        && r.Set.Cardinality == sourceSet.Cardinality)
            .OrderBy(r => r.Cost)
            .Take(5)
            .ToList();

        if (nearby.Count == 0)
            return Task.FromResult(CannotHelp($"No substitutions found within harmonic distance 3 for {chordName}."));

        logger.LogDebug("ChordSubstitutionSkill: {Chord} → {Count} substitutions", chordName, nearby.Count);

        var result = new StringBuilder();
        result.AppendLine($"Harmonic substitutions for **{chordName}** (ranked by ICV distance):");
        result.AppendLine();

        var evidence = new List<string> { $"Source chord: {chordName} (bitmask {sourceSet.PitchClassMask})" };

        foreach (var (set, delta, cost) in nearby)
        {
            var name    = SetName(set);
            var costStr = $"{cost:F2}";
            result.AppendLine($"- **{name}** — harmonic cost {costStr} (Δ L1={delta.L1Norm})");
            evidence.Add($"{name}: cost={costStr}, L1={delta.L1Norm}");
        }

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Composer,
            Result     = result.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   = [.. evidence],
            Assumptions =
            [
                "Substitutions computed using Grothendieck ICV distance (L1 norm, radius 3)",
                "Triads and 7th chords supported — add extensions to taste"
            ]
        });
    }

    // ── Two-chord comparison ───────────────────────────────────────────────────

    private AgentResponse ExecuteComparison(
        (string Name, int Root, int[] Intervals) a,
        (string Name, int Root, int[] Intervals) b)
    {
        var rels = ClassifySubstitution(a.Name, a.Root, a.Intervals, b.Name, b.Root, b.Intervals);

        var sb = new StringBuilder();
        sb.AppendLine($"**{a.Name}** → **{b.Name}** relationship analysis:");
        sb.AppendLine();

        foreach (var rel in rels)
        {
            var stars = rel.Type.Contains("Tritone", StringComparison.Ordinal) ? "★★★" :
                        rel.Type.StartsWith("ICV", StringComparison.Ordinal)   ? "★"   : "★★";
            sb.AppendLine($"{stars} **{rel.Type}**");
            sb.AppendLine($"  {rel.Explanation}");
            sb.AppendLine();
        }

        sb.AppendLine("*Confidence: 100% (deterministic pitch-class arithmetic)*");

        logger.LogDebug(
            "ChordSubstitutionSkill comparison: {A} → {B} = [{Rels}]",
            a.Name, b.Name, string.Join(", ", rels.Select(r => r.Type)));

        return new AgentResponse
        {
            AgentId    = AgentIds.Composer,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   = rels.Select(r => $"{r.Type}: {r.Explanation}").ToArray(),
            Assumptions = ["Pure pitch-class arithmetic — no LLM call"]
        };
    }

    private IReadOnlyList<SubstitutionRelationship> ClassifySubstitution(
        string nameA, int rootA, int[] intervalsA,
        string nameB, int rootB, int[] intervalsB)
    {
        var results = new List<SubstitutionRelationship>();
        var ab = (rootB - rootA + 12) % 12;   // semitones from A up to B
        var ba = (rootA - rootB + 12) % 12;   // semitones from B up to A

        // Tritone substitution: roots 6 semitones apart + both dominant 7ths
        if (ab == 6 && intervalsA.SequenceEqual(Dom7) && intervalsB.SequenceEqual(Dom7))
            results.Add(new("Tritone Substitution",
                $"Roots are 6 semitones (tritone) apart; both are dominant 7ths. " +
                $"The M3 of {nameA} equals the m7 of {nameB} and vice versa — guide tones are shared by inversion. " +
                $"Classic bebop move: both chords resolve to the same target by half-step."));

        // Secondary dominant: A is a P5 above B → A functions as V of B
        if (ba == 7)
            results.Add(new("Secondary Dominant",
                $"{nameA} is a perfect 5th above {nameB} — {nameA} functions as V (dominant) of {nameB}."));

        // Backdoor dominant: A is bVII7 of B (resolves up a whole step).
        // bVII = 10 semitones above the tonic, so going from B (tonic) UP to
        // A (bVII) is 10 semitones — that's `ba == 10`. Was previously `ab == 10`,
        // which actually detects "B is bVII of A", the opposite of the comment.
        // Fixed in PR #85 alongside the MCP-tool port.
        if (ba == 10 && intervalsA.SequenceEqual(Dom7))
            results.Add(new("Backdoor Dominant",
                $"{nameA} is bVII7 relative to {nameB} — the backdoor dominant resolves up by a whole step to the tonic."));

        // Build PitchClassSets for structural comparisons
        var setA = new PitchClassSet(intervalsA.Select(i => PitchClass.FromValue((rootA + i) % 12)));
        var setB = new PitchClassSet(intervalsB.Select(i => PitchClass.FromValue((rootB + i) % 12)));

        // Set-class equivalent: same prime form under T/I equivalence
        var pfA = setA.PrimeForm?.PitchClassMask;
        var pfB = setB.PrimeForm?.PitchClassMask;
        if (pfA.HasValue && pfA == pfB)
            results.Add(new("Set-Class Equivalent",
                $"Both chords share the same prime form under T/I equivalence — they belong to the same set class (e.g. major triads are T/I equivalent to minor triads)."));

        // ICV neighbor: Grothendieck L1 distance ≤ 2
        var delta = grothendieck.ComputeDelta(setA.IntervalClassVector, setB.IntervalClassVector);
        if (delta.L1Norm <= 2)
            results.Add(new($"ICV Neighbor (L1 = {delta.L1Norm})",
                $"{nameA} and {nameB} are {delta.L1Norm} step(s) apart in ICV space — harmonically proximate by Grothendieck measure."));

        // Fallback: report raw distance
        if (results.Count == 0)
            results.Add(new("Harmonic Distance",
                $"No standard substitution relationship detected. ICV distance = {delta.L1Norm} (L1 norm)."));

        return results;
    }

    /// <summary>Named relationship type with a human-readable explanation.</summary>
    public sealed record SubstitutionRelationship(string Type, string Explanation);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string Name, int Root, int[] Intervals)? TryParseChordMatch(Match m)
    {
        var rootStr = m.Groups["root"].Value + m.Groups["acc"].Value;
        var qualStr = m.Groups["qual"].Value;
        if (!ChordVocabulary.TryGetPitchClassesStrict(rootStr + qualStr, out var pcs)) return null;

        var rootPc = pcs[0];
        int[] intervals = [.. pcs.Select(pc => (pc - rootPc + 12) % 12)];

        var suffix = qualStr switch
        {
            "m" or "min"   => "m",
            "dim"          => "dim",
            "aug" or "+"   => "aug",
            "7"            => "7",
            "maj7" or "M7" => "maj7",
            "m7"           => "m7",
            "m7b5"         => "m7b5",
            "dim7"         => "dim7",
            _              => ""
        };

        return ($"{rootStr}{suffix}", rootPc, intervals);
    }

    private static (string Name, PitchClassSet Set)? ParseChord(Match match)
    {
        var parsed = TryParseChordMatch(match);
        if (parsed is null) return null;

        var pcs = parsed.Value.Intervals.Select(offset => PitchClass.FromValue((parsed.Value.Root + offset) % 12));
        return (parsed.Value.Name, new PitchClassSet(pcs));
    }

    private static string SetName(PitchClassSet set)
    {
        var mask = set.Aggregate(0, (acc, pc) => acc | (1 << pc.Value));
        return MaskToChord.TryGetValue(mask, out var name) ? name : set.Name;
    }

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Composer,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };
}
