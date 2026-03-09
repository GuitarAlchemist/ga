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
public sealed class ChordSubstitutionSkill(
    IGrothendieckService grothendieck,
    ILogger<ChordSubstitutionSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ChordSubstitution";
    public string Description => "Classifies two-chord relationships or finds nearby substitutions via Grothendieck ICV distance";

    // ── Triggers ──────────────────────────────────────────────────────────────

    private static readonly Regex SubstituteTrigger =
        new(@"\b(substitut|reharmoni|instead\s+of|alternative\s+chord|swap\s+chord|replace\s+chord)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Additional comparison keywords for the two-chord path
    private static readonly Regex TwoChordTrigger =
        new(@"\b(?:same|related|equivalent|tritone)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Extended chord symbol pattern — matches triads AND 7th chords (longer alternations first)
    private static readonly Regex ExtendedChordSymbol =
        new(@"\b(?<root>[A-G])(?<acc>[b#]?)(?<qual>m7b5|dim7|maj7|m7|7|min|m|dim|aug|\+)?(?!\w)",
            RegexOptions.Compiled);

    public bool CanHandle(string message) =>
        ExtendedChordSymbol.IsMatch(message) &&
        (SubstituteTrigger.IsMatch(message) || TwoChordTrigger.IsMatch(message));

    // ── Domain data ───────────────────────────────────────────────────────────

    private static readonly FrozenDictionary<string, int> RootPcMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4, ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8,
            ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // Canonical root names (prefer flats except F# and C#)
    private static readonly string[] RootNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // quality string → semitone offsets from root (triads and 7th chords)
    private static readonly FrozenDictionary<string, int[]> QualityIntervals =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            [""]     = [0, 4, 7],       // major triad
            ["m"]    = [0, 3, 7],       // minor triad
            ["min"]  = [0, 3, 7],
            ["dim"]  = [0, 3, 6],       // diminished triad
            ["aug"]  = [0, 4, 8],       // augmented triad
            ["+"]    = [0, 4, 8],
            ["7"]    = [0, 4, 7, 10],   // dominant 7th
            ["maj7"] = [0, 4, 7, 11],   // major 7th
            ["m7"]   = [0, 3, 7, 10],   // minor 7th
            ["m7b5"] = [0, 3, 6, 10],   // half-diminished
            ["dim7"] = [0, 3, 6, 9],    // diminished 7th
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

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
        var chords = ExtendedChordSymbol.Matches(message)
            .Select(TryParseChordMatch)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .Take(2)
            .ToList();

        if (chords.Count == 2)
            return Task.FromResult(ExecuteComparison(chords[0], chords[1]));

        // Single-chord path — original behaviour
        var parsed = ParseChord(message);
        if (parsed is null)
            return Task.FromResult(CannotHelp("Could not identify a chord symbol in your message."));

        var (chordName, sourceSet) = parsed.Value;

        var nearby = grothendieck.FindNearby(sourceSet, maxDistance: 3)
            .Where(r => !ReferenceEquals(r.Set, sourceSet) && r.Set.Cardinality == sourceSet.Cardinality)
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
            var stars = rel.Type.Contains("Tritone") ? "★★★" :
                        rel.Type.StartsWith("ICV")   ? "★"   : "★★";
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

        // Backdoor dominant: A is bVII7 of B (resolves up by whole step to B as I)
        if (ab == 10 && intervalsA.SequenceEqual(Dom7))
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
        if (!RootPcMap.TryGetValue(rootStr, out var rootPc)) return null;

        var qualStr = m.Groups["qual"].Value;
        if (!QualityIntervals.TryGetValue(qualStr, out var intervals))
            intervals = QualityIntervals[""];   // default to major

        var suffix = qualStr switch
        {
            "m" or "min" => "m",
            "dim"        => "dim",
            "aug" or "+" => "aug",
            "7"          => "7",
            "maj7"       => "maj7",
            "m7"         => "m7",
            "m7b5"       => "m7b5",
            "dim7"       => "dim7",
            _            => ""
        };

        return ($"{rootStr}{suffix}", rootPc, intervals);
    }

    private static (string Name, PitchClassSet Set)? ParseChord(string message)
    {
        var match = ExtendedChordSymbol.Match(message);
        if (!match.Success) return null;

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
