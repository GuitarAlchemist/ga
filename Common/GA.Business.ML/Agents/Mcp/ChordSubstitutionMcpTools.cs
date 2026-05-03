namespace GA.Business.ML.Agents.Mcp;

using System.Collections.Frozen;
using System.ComponentModel;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tool surface for chord substitution analysis. Wraps the deterministic
/// pitch-class arithmetic and Grothendieck-ICV machinery that
/// <see cref="Skills.ChordSubstitutionSkill"/> also uses, so an LLM-driven
/// SKILL.md skill can compute substitution analysis rather than recalling
/// theory rules from training data.
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. Fifth tool in the MCP-
/// tool-exposure workstream — same template as <see cref="IntervalMcpTools"/>,
/// <see cref="ScaleMcpTools"/>, <see cref="ChordMcpTools"/>,
/// <see cref="FretSpanMcpTools"/>: length-guarded inputs, sanitized Error
/// echo via <see cref="McpEchoSanitizer"/>, structured result with
/// Error-branch invariant.
///
/// This class exposes TWO MCP methods (first MCP tool with multiple operations):
/// <list type="bullet">
///   <item><c>ga_chord_substitutions</c> — find chords harmonically close to a single source</item>
///   <item><c>ga_chord_compare</c> — classify the relationship between two chord symbols</item>
/// </list>
/// </remarks>
[McpServerToolType]
public sealed partial class ChordSubstitutionMcpTools(IGrothendieckService grothendieck)
{
    private const int MaxChordSymbolLength = 12;
    private const int MaxNearbyResults     = 5;
    private const int MaxIcvDistance       = 3;

    /// <summary>
    /// Finds harmonically-close chord substitutions for a single source chord,
    /// ranked by Grothendieck ICV distance.
    /// </summary>
    [McpServerTool(Name = "ga_chord_substitutions"), Description(
        "Find harmonic substitutions for a chord, ranked by Grothendieck ICV distance. " +
        "Use when a user asks for 'substitutions for X', 'reharmonize X', 'alternatives to X', etc. " +
        "Returns up to 5 nearby chords with their cost and L1 distance. Supports triads (major / m / dim / aug) " +
        "and 7th chords (7 / maj7 / m7 / m7b5 / dim7).")]
    public ChordSubstitutionsResult GetSubstitutions(
        [Description("The source chord symbol — root + optional quality suffix. Examples: 'Cmaj7', 'F#m', 'Bb7', 'Am7b5'.")]
        string chordSymbol)
    {
        if (string.IsNullOrEmpty(chordSymbol) || chordSymbol.Length > MaxChordSymbolLength)
            return ChordSubstitutionsResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordSymbol)}' as a chord symbol.");

        var parsed = TryParseFullChord(chordSymbol);
        if (parsed is null)
            return ChordSubstitutionsResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordSymbol)}' as a chord symbol. Try Cmaj7, F#m, Bb7, etc.");

        var (chordName, _, intervals) = parsed.Value;
        var sourceSet = BuildPitchClassSet(parsed.Value.Root, intervals);

        // Exclude the source from its own substitution list. The original C#
        // skill used ReferenceEquals which only works if the catalog interns
        // the exact instance we built — it doesn't, so the source can leak
        // through into its own results. Compare by pitch-class mask instead
        // (PR #84 fix).
        var sourceMask = sourceSet.PitchClassMask;
        var nearby = grothendieck
            .FindNearby(sourceSet, maxDistance: MaxIcvDistance)
            .Where(r => r.Set.PitchClassMask != sourceMask
                        && r.Set.Cardinality == sourceSet.Cardinality)
            .OrderBy(r => r.Cost)
            .Take(MaxNearbyResults)
            .Select(r => new SubstitutionCandidate
            {
                Name    = SetName(r.Set),
                Cost    = (float)r.Cost,
                L1Delta = r.Delta.L1Norm,
            })
            .ToArray();

        if (nearby.Length == 0)
            return ChordSubstitutionsResult.Failure($"No substitutions found within harmonic distance {MaxIcvDistance} for {chordName}.");

        return new ChordSubstitutionsResult
        {
            SourceChord  = chordName,
            Substitutions = nearby,
        };
    }

    /// <summary>
    /// Classifies the relationship between two chord symbols — tritone sub,
    /// secondary dominant, backdoor dominant, set-class equivalent, ICV neighbor.
    /// </summary>
    [McpServerTool(Name = "ga_chord_compare"), Description(
        "Classify the harmonic relationship between two chord symbols. " +
        "Detects tritone substitution, secondary dominant, backdoor dominant, set-class equivalence, " +
        "and ICV-neighbor proximity (Grothendieck distance ≤ 2). " +
        "Use when a user asks 'how are X and Y related', 'is X a tritone sub for Y', etc.")]
    public ChordComparisonResult CompareChords(
        [Description("The first chord (e.g. 'G7'). The 'A' chord in classifications like 'A is V of B'.")]
        string chordA,
        [Description("The second chord (e.g. 'Db7'). The 'B' chord in classifications.")]
        string chordB)
    {
        if (string.IsNullOrEmpty(chordA) || chordA.Length > MaxChordSymbolLength)
            return ChordComparisonResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordA)}' as the first chord.");
        if (string.IsNullOrEmpty(chordB) || chordB.Length > MaxChordSymbolLength)
            return ChordComparisonResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordB)}' as the second chord.");

        var a = TryParseFullChord(chordA);
        var b = TryParseFullChord(chordB);
        if (a is null) return ChordComparisonResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordA)}' as a chord symbol.");
        if (b is null) return ChordComparisonResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordB)}' as a chord symbol.");

        var (nameA, rootA, intervalsA) = a.Value;
        var (nameB, rootB, intervalsB) = b.Value;
        var ab = (rootB - rootA + 12) % 12;
        var ba = (rootA - rootB + 12) % 12;

        var setA = BuildPitchClassSet(rootA, intervalsA);
        var setB = BuildPitchClassSet(rootB, intervalsB);

        var rels = new List<ChordRelationship>();

        // Tritone substitution: roots 6 semitones apart + both dominant 7ths
        if (ab == 6 && intervalsA.SequenceEqual(Dom7) && intervalsB.SequenceEqual(Dom7))
            rels.Add(new("Tritone Substitution",
                $"Roots are 6 semitones (tritone) apart; both are dominant 7ths. " +
                $"M3 of {nameA} = m7 of {nameB} and vice versa — guide tones shared by inversion. Classic bebop move."));

        // Secondary dominant: A is a P5 above B → A functions as V of B
        if (ba == 7)
            rels.Add(new("Secondary Dominant",
                $"{nameA} is a perfect 5th above {nameB} — {nameA} functions as V (dominant) of {nameB}."));

        // Backdoor dominant: A is bVII7 of B (resolves up a whole step).
        // bVII = 10 semitones above the tonic, so going from B (tonic) UP to
        // A (bVII) is 10 semitones — that's `ba == 10`. The original C# skill
        // had `ab == 10` which actually detects "B is bVII of A", the opposite
        // of the comment. Fixed here per PR #84 review and the regression
        // surfaced by the Bb7→C test.
        if (ba == 10 && intervalsA.SequenceEqual(Dom7))
            rels.Add(new("Backdoor Dominant",
                $"{nameA} is bVII7 relative to {nameB} — backdoor dominant resolves up by a whole step to the tonic."));

        // Set-class equivalence under T/I
        var pfA = setA.PrimeForm?.PitchClassMask;
        var pfB = setB.PrimeForm?.PitchClassMask;
        if (pfA.HasValue && pfA == pfB)
            rels.Add(new("Set-Class Equivalent",
                "Both chords share the same prime form under T/I equivalence — they belong to the same set class."));

        // ICV neighbor: Grothendieck L1 distance ≤ 2
        var delta = grothendieck.ComputeDelta(setA.IntervalClassVector, setB.IntervalClassVector);
        if (delta.L1Norm <= 2)
            rels.Add(new($"ICV Neighbor (L1 = {delta.L1Norm})",
                $"{nameA} and {nameB} are {delta.L1Norm} step(s) apart in ICV space — harmonically proximate by Grothendieck measure."));

        // Fallback when no specific relationship triggered
        if (rels.Count == 0)
            rels.Add(new("Harmonic Distance",
                $"No standard substitution relationship detected. ICV distance = {delta.L1Norm} (L1 norm)."));

        return new ChordComparisonResult
        {
            ChordA          = nameA,
            ChordB          = nameB,
            Relationships   = [.. rels],
            IcvL1Distance   = delta.L1Norm,
        };
    }

    // ── Parsing / lookup helpers (mirror ChordSubstitutionSkill) ──────────────────

    private static readonly int[] Dom7 = [0, 4, 7, 10];

    private static readonly FrozenDictionary<string, int> RootPcMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0, ["C#"] = 1, ["Db"] = 1, ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4, ["F"] = 5, ["F#"] = 6, ["Gb"] = 6, ["G"] = 7, ["G#"] = 8,
            ["Ab"] = 8, ["A"] = 9, ["A#"] = 10, ["Bb"] = 10, ["B"] = 11,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] RootNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly FrozenDictionary<string, int[]> QualityIntervals =
        new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            [""]     = [0, 4, 7],
            ["m"]    = [0, 3, 7],
            ["min"]  = [0, 3, 7],
            ["dim"]  = [0, 3, 6],
            ["aug"]  = [0, 4, 8],
            ["+"]    = [0, 4, 8],
            ["7"]    = [0, 4, 7, 10],
            ["maj7"] = [0, 4, 7, 11],
            ["m7"]   = [0, 3, 7, 10],
            ["m7b5"] = [0, 3, 6, 10],
            ["dim7"] = [0, 3, 6, 9],
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly (int[] Intervals, string Suffix)[] ChordTemplates =
    [
        ([0, 4, 7],     ""),     ([0, 3, 7],     "m"),
        ([0, 3, 6],     "dim"),  ([0, 4, 8],     "aug"),
        ([0, 4, 7, 10], "7"),    ([0, 4, 7, 11], "maj7"),
        ([0, 3, 7, 10], "m7"),   ([0, 3, 6, 10], "m7b5"),
        ([0, 3, 6, 9],  "dim7"),
    ];

    private static readonly FrozenDictionary<int, string> MaskToChord = BuildMaskToChord();

    private static FrozenDictionary<int, string> BuildMaskToChord()
    {
        var map = new Dictionary<int, string>();
        for (var root = 0; root < 12; root++)
            foreach (var (intervals, suffix) in ChordTemplates)
            {
                var mask = intervals.Aggregate(0, (acc, i) => acc | (1 << ((root + i) % 12)));
                map.TryAdd(mask, $"{RootNames[root]}{suffix}");
            }
        return map.ToFrozenDictionary();
    }

    [GeneratedRegex(@"^(?<root>[A-Ga-g])(?<acc>[b#]?)(?<qual>m7b5|dim7|maj7|m7|7|min|m|dim|aug|\+)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex FullChordRegex();

    private static (string Name, int Root, int[] Intervals)? TryParseFullChord(string chordSymbol)
    {
        var match = FullChordRegex().Match(chordSymbol.Trim());
        if (!match.Success) return null;

        var rootStr = match.Groups["root"].Value.ToUpperInvariant() + match.Groups["acc"].Value;
        if (!RootPcMap.TryGetValue(rootStr, out var rootPc)) return null;

        var qualStr = match.Groups["qual"].Value.ToLowerInvariant();
        if (!QualityIntervals.TryGetValue(qualStr, out var intervals))
            intervals = QualityIntervals[""];

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
            _            => "",
        };

        return ($"{rootStr}{suffix}", rootPc, intervals);
    }

    private static PitchClassSet BuildPitchClassSet(int root, int[] intervals) =>
        new(intervals.Select(i => PitchClass.FromValue((root + i) % 12)));

    private static string SetName(PitchClassSet set)
    {
        var mask = set.Aggregate(0, (acc, pc) => acc | (1 << pc.Value));
        return MaskToChord.TryGetValue(mask, out var name) ? name : set.Name;
    }
}

/// <summary>One harmonically-close substitution candidate.</summary>
public sealed record SubstitutionCandidate
{
    public string Name    { get; init; } = string.Empty;
    /// <summary>Grothendieck ICV cost (lower is closer).</summary>
    public float  Cost    { get; init; }
    /// <summary>L1 norm of the ICV delta.</summary>
    public int    L1Delta { get; init; }
}

/// <summary>Result of <see cref="ChordSubstitutionMcpTools.GetSubstitutions"/>.</summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, <see cref="SourceChord"/>
/// is empty and <see cref="Substitutions"/> is empty.
/// </remarks>
public sealed record ChordSubstitutionsResult
{
    public string SourceChord { get; init; } = string.Empty;
    public SubstitutionCandidate[] Substitutions { get; init; } = [];
    public string? Error { get; init; }
    public static ChordSubstitutionsResult Failure(string message) => new() { Error = message };
}

/// <summary>One named relationship between two chords.</summary>
public sealed record ChordRelationship(string Type, string Explanation);

/// <summary>Result of <see cref="ChordSubstitutionMcpTools.CompareChords"/>.</summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, <see cref="ChordA"/> /
/// <see cref="ChordB"/> are empty and <see cref="Relationships"/> is empty.
/// </remarks>
public sealed record ChordComparisonResult
{
    public string ChordA { get; init; } = string.Empty;
    public string ChordB { get; init; } = string.Empty;
    public ChordRelationship[] Relationships { get; init; } = [];
    /// <summary>L1 distance between the two ICVs (always populated on success, even when no specific relationship triggered).</summary>
    public int IcvL1Distance { get; init; }
    public string? Error { get; init; }
    public static ChordComparisonResult Failure(string message) => new() { Error = message };
}
