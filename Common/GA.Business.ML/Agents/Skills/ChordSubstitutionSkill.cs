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
/// Registered at the <b>orchestrator level</b>. Parses the first chord symbol found in
/// the message, builds its <see cref="PitchClassSet"/>, calls
/// <see cref="IGrothendieckService.FindNearby"/> (radius 3), ranks results by harmonic
/// cost, and names them via a pre-computed bitmask lookup table.
/// </remarks>
public sealed class ChordSubstitutionSkill(
    IGrothendieckService grothendieck,
    ILogger<ChordSubstitutionSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ChordSubstitution";
    public string Description => "Finds harmonically-close chord substitutions via Grothendieck ICV distance";

    // ── Trigger ───────────────────────────────────────────────────────────────

    private static readonly Regex SubstituteTrigger =
        new(@"\b(substitut|reharmoni|instead\s+of|alternative\s+chord|swap\s+chord|replace\s+chord)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ChordSymbolPattern =
        new(@"\b(?<root>[A-G])(?<acc>[#b]?)(?<qual>m|min|dim|aug|\+)?(?!\w)",
            RegexOptions.Compiled);

    public bool CanHandle(string message) =>
        SubstituteTrigger.IsMatch(message) && ChordSymbolPattern.IsMatch(message);

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

    // quality string → semitone offsets from root
    private static readonly FrozenDictionary<string, int[]> QualityIntervals =
        new Dictionary<string, int[]>
        {
            [""]    = [0, 4, 7],   // major
            ["m"]   = [0, 3, 7],   // minor
            ["min"] = [0, 3, 7],
            ["dim"] = [0, 3, 6],   // diminished
            ["aug"] = [0, 4, 8],   // augmented
            ["+"]   = [0, 4, 8]
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // quality suffix → chord name suffix
    private static readonly (int[] Intervals, string Suffix)[] ChordTemplates =
    [
        ([0, 4, 7], ""),
        ([0, 3, 7], "m"),
        ([0, 3, 6], "dim"),
        ([0, 4, 8], "aug")
    ];

    // Pre-computed bitmask → chord name (e.g., 0b000010010001 → "C")
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
            var name = SetName(set);
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
                "Triads only — add extensions (7th, 9th) to taste"
            ]
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string Name, PitchClassSet Set)? ParseChord(string message)
    {
        var match = ChordSymbolPattern.Match(message);
        if (!match.Success) return null;

        var rootStr = match.Groups["root"].Value + match.Groups["acc"].Value;
        if (!RootPcMap.TryGetValue(rootStr, out var rootPc)) return null;

        var qualStr = match.Groups["qual"].Value;
        if (!QualityIntervals.TryGetValue(qualStr, out var intervals))
            intervals = QualityIntervals[""];   // default to major

        var pcs = intervals.Select(offset => PitchClass.FromValue((rootPc + offset) % 12));
        var set = new PitchClassSet(pcs);

        var suffix = qualStr switch { "m" or "min" => "m", "dim" => "dim", "aug" or "+" => "aug", _ => "" };
        return ($"{rootStr}{suffix}", set);
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
