namespace GaMcpServer.Tools;

using GA.Business.Config;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using ModelContextProtocol.Server;

using GaReg = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;

/// <summary>
/// MCP tools that bridge tonal chord symbols to post-tonal set theory:
/// pitch-class sets, interval-class vectors, prime forms, Forte numbers,
/// modal families, polychords, and T/I-equivalent substitutions.
/// </summary>
[McpServerToolType]
public static class ChordAtonalTool
{
    // ── Static tables ─────────────────────────────────────────────────────────

    private static readonly int[] MajorScaleSemitones = [0, 2, 4, 5, 7, 9, 11];

    private static readonly IReadOnlyDictionary<string, int> NoteToSemitone =
        new Dictionary<string, int>
        { ["C"]=0,["D"]=2,["E"]=4,["F"]=5,["G"]=7,["A"]=9,["B"]=11 };

    // Standard 12-root vocabulary — semitone offsets for each quality archetype
    private static readonly (string Suffix, int[] Intervals)[] Vocabulary =
    [
        ("",       [0, 4, 7]),          // major triad
        ("m",      [0, 3, 7]),          // minor triad
        ("dim",    [0, 3, 6]),          // diminished triad
        ("aug",    [0, 4, 8]),          // augmented triad
        ("7",      [0, 4, 7, 10]),      // dominant 7th
        ("maj7",   [0, 4, 7, 11]),      // major 7th
        ("m7",     [0, 3, 7, 10]),      // minor 7th
        ("dim7",   [0, 3, 6, 9]),       // fully-diminished 7th
        ("m7b5",   [0, 3, 6, 10]),      // half-diminished (ø7)
        ("maj9",   [0, 4, 7, 11, 14]),  // major 9th
        ("9",      [0, 4, 7, 10, 14]),  // dominant 9th
        ("m9",     [0, 3, 7, 10, 14]),  // minor 9th
    ];

    private static readonly string[] NoteNames =
        ["C","C#","D","Eb","E","F","F#","G","Ab","A","Bb","B"];

    private static readonly string[] RootNames =
        ["C","C#","D","Eb","E","F","F#","G","Ab","A","Bb","B"];

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ParseRootPc(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return 0;
        var letter = symbol[0].ToString().ToUpperInvariant();
        if (!NoteToSemitone.TryGetValue(letter, out var s)) return 0;
        var acc = symbol.Length > 2 && symbol[1..3] is "##" or "bb" ? symbol[1..3]
                : symbol.Length > 1 ? symbol[1..2] : "";
        return ((s + acc switch { "#" => 1, "b" => -1, "##" => 2, "bb" => -2, _ => 0 }) % 12 + 12) % 12;
    }

    /// <summary>
    /// Semitones of an interval name returned by <c>domain.chordIntervals</c>, from its quality
    /// and degree: P5 → 7, d5 → 6, d7 → 9, m9 → 13, A11 → 18. Also accepts the legacy "TT".
    /// </summary>
    private static int? IntervalSemitones(string name)
    {
        if (name == "TT") return 6;
        if (name.Length < 2 || !int.TryParse(name.AsSpan(1), out var degree) || degree < 1) return null;
        var natural = MajorScaleSemitones[(degree - 1) % 7] + 12 * ((degree - 1) / 7);
        var perfect = (degree - 1) % 7 is 0 or 3 or 4;
        return (name[0], perfect) switch
        {
            ('P', true) or ('M', false) => natural,
            ('m', false) or ('d', true) => natural - 1,
            ('d', false) => natural - 2,
            ('A', _) => natural + 1,
            _ => null
        };
    }

    private static async Task<string[]> GetIntervalNamesAsync(string symbol)
    {
        var map = MapModule.OfSeq(new[] { Tuple.Create("symbol", (object)symbol) });
        var result = await FSharpAsync.StartAsTask(
            GaReg.Global.Invoke("domain.chordIntervals", map),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);
        if (!result.IsOk) return [];
        return result.ResultValue switch
        {
            string[] arr => arr,
            object[] arr => arr.Select(x => x?.ToString() ?? "").ToArray(),
            _ => []
        };
    }

    private static async Task<int[]> GetPitchClassesAsync(string symbol)
    {
        var rootPc = ParseRootPc(symbol);
        var intervals = await GetIntervalNamesAsync(symbol);
        return [.. intervals
            .Select(iv => IntervalSemitones(iv) is { } s ? (int?)(rootPc + s) % 12 : null)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .Distinct()
            .Order()];
    }

    private static PitchClassSet ToPitchClassSet(IEnumerable<int> pcs) =>
        new(pcs.Select(PitchClass.FromValue));

    private static string FormatPcs(IEnumerable<int> pcs) =>
        "{" + string.Join(", ", pcs.Select(pc => NoteNames[pc])) + "}";

    /// <summary>
    /// Name of a Modes.yaml entry whose notes, transposed so that their first note lands on
    /// <paramref name="rootPc"/>, are exactly <paramref name="pcs"/>. Without a root, a name is
    /// returned only when a single entry is a transposition of the set.
    /// A lookup by interval vector alone is not enough: inversions and Z-related sets share a vector,
    /// and Modes.yaml files seventh chords of different vectors under one family vector, which
    /// labelled Am "Major Triad" and Cm7b5 "Major Seventh".
    /// </summary>
    private static string? TranspositionName(int[] pcs, IntervalClassVector icv, int? rootPc)
    {
        var target = pcs.Aggregate(0, (mask, pc) => mask | 1 << pc);
        var names = ModesConfig.GetAllModes()
            .Where(m => string.Equals(m.IntervalClassVector, icv.ToString(), StringComparison.OrdinalIgnoreCase))
            .Where(m =>
            {
                var modePcs = m.Notes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ParseRootPc).ToArray();
                return modePcs.Length > 0 && Enumerable.Range(0, 12).Any(t =>
                    (rootPc is null || (modePcs[0] + t) % 12 == rootPc) &&
                    modePcs.Aggregate(0, (mask, pc) => mask | 1 << (pc + t) % 12) == target);
            })
            .Select(m => m.Name)
            .Distinct()
            .ToList();
        return rootPc is not null || names.Count == 1 ? names.FirstOrDefault() : null;
    }

    private static string AtonalCard(string label, int[] pcs, int? rootPc)
    {
        var set = ToPitchClassSet(pcs);
        var icv = set.IntervalClassVector;
        var prime = set.PrimeForm;
        var forte = prime != null ? ForteCatalog.GetForteNumber(prime) : null;
        var family = set.ModalFamily;
        var modeName = TranspositionName(pcs, icv, rootPc);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{label,-12}{FormatPcs(pcs)} ({pcs.Length} tones)");
        sb.AppendLine($"  ICV:        {icv}");
        if (forte.HasValue)  sb.AppendLine($"  Forte:      {forte.Value}");
        if (prime != null)   sb.AppendLine($"  Prime form: {prime}");
        if (family != null)
            sb.AppendLine($"  Modal fam.: {family.NoteCount}-note / {family.Modes.Count} modes" +
                          $" (mode {set.ModeIndex + 1})");
        if (modeName != null) sb.AppendLine($"  Scale:      {modeName}");
        return sb.ToString().TrimEnd();
    }

    // ── Tools ─────────────────────────────────────────────────────────────────

    [McpServerTool]
    [Description(
        "Return the full post-tonal identity of a chord: pitch-class set, interval-class vector (ICV), " +
        "prime form, Forte number, modal family position, and matching scale name if any. " +
        "Bridges tonal chord naming to set theory — e.g. Am7 and Cmaj6 share the same prime form (T/I equivalents).")]
    public static async Task<string> GaChordToSet(
        [Description("Chord symbol, e.g. 'Am7', 'Cmaj9', 'G7b9'")] string symbol)
    {
        var pcs = await GetPitchClassesAsync(symbol);
        if (pcs.Length == 0) return $"Error: could not parse chord '{symbol}'";

        var header = $"Chord: {symbol}";
        var card = AtonalCard("Pitch set:", pcs, ParseRootPc(symbol));
        var opticNote =
            "OPTIC-K layer: STRUCTURE (dims 6–29, w=0.45) — ICV drives structural similarity";
        return $"{header}\n{card}\n{opticNote}";
    }

    [McpServerTool]
    [Description(
        "Find all standard chords (triads, 7ths, 9ths) that are set-class equivalent (T/I) to the input chord — " +
        "same prime form under transposition or inversion. These are the deepest substitutions: " +
        "same interval content regardless of root. " +
        "Example: Am and C are equivalent (A-C-E is an inversion of C-E-G, set class 3-11), " +
        "and so are Am and Em (a transposition).")]
    public static async Task<string> GaSetClassSubs(
        [Description("Chord symbol, e.g. 'Am', 'Cmaj7', 'G7'")] string symbol)
    {
        var pcs = await GetPitchClassesAsync(symbol);
        if (pcs.Length == 0) return $"Error: could not parse chord '{symbol}'";

        var targetSet = ToPitchClassSet(pcs);
        var targetPrime = targetSet.PrimeForm;
        if (targetPrime == null) return $"No prime form computed for {symbol}";
        var targetIcv = targetSet.IntervalClassVector;
        var forte = ForteCatalog.GetForteNumber(targetPrime);

        // Build vocabulary using hardcoded intervals (no async calls for speed)
        var equivalents = new List<(string Suffix, string Chord)>();
        foreach (var root in RootNames)
        {
            var rootPc = ParseRootPc(root);
            foreach (var (suffix, intervals) in Vocabulary)
            {
                var candidate = root + suffix;
                if (candidate == symbol) continue;
                var cpcs = intervals.Select(i => (rootPc + i) % 12).Distinct().Order().ToArray();
                if (cpcs.Length != pcs.Length) continue;
                var cPrime = ToPitchClassSet(cpcs).PrimeForm;
                if (cPrime != null && cPrime.Equals(targetPrime))
                    equivalents.Add((suffix, candidate));
            }
        }

        var header = $"T/I equivalents of {symbol} (Forte {forte}, ICV {targetIcv}, prime {targetPrime}):";
        if (equivalents.Count == 0)
            return header + "\n  (none in standard vocabulary — unique set class)";

        // Group by the quality each chord was built from (a suffix test such as EndsWith("")
        // would match every chord)
        var grouped = equivalents
            .GroupBy(e => e.Suffix)
            .Select(g => $"  [{(g.Key == "" ? "maj" : g.Key)}] {string.Join("  ", g.Select(e => e.Chord))}")
            .ToList();
        return header + "\n" + string.Join("\n", grouped);
    }

    [McpServerTool]
    [Description(
        "Stack two chords into a polychord and analyze the combined pitch-class set: merged tones, " +
        "ICV, prime form, Forte number, modal family, and any matching scale name. " +
        "Example: D triad over C triad → {C,D,E,F#,G,A} (6-33), C Lydian without its seventh.")]
    public static async Task<string> GaPolychord(
        [Description("Bottom chord, e.g. 'C'")] string chord1,
        [Description("Top chord, e.g. 'D'")] string chord2)
    {
        var pcs1Task = GetPitchClassesAsync(chord1);
        var pcs2Task = GetPitchClassesAsync(chord2);
        var pcs1 = await pcs1Task;
        var pcs2 = await pcs2Task;
        if (pcs1.Length == 0) return $"Error: could not parse '{chord1}'";
        if (pcs2.Length == 0) return $"Error: could not parse '{chord2}'";

        var merged = pcs1.Union(pcs2).Order().ToArray();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Polychord: {chord2}/{chord1}");
        sb.AppendLine(AtonalCard("Merged:", merged, ParseRootPc(chord1)));
        sb.AppendLine($"  from {chord1}: {FormatPcs(pcs1)}");
        sb.Append($"  from {chord2}: {FormatPcs(pcs2)}");
        return sb.ToString();
    }

    [McpServerTool]
    [Description(
        "Find pitch-class sets near a chord in the Grothendieck ICV space — sets with the smallest " +
        "signed interval-content change (L1 norm on the ICV delta). These are the harmonically closest " +
        "scales and chords at the atonal level, regardless of tonal function. " +
        "Useful for smooth voice-leading or finding unexpected but structurally close substitutions.")]
    public static async Task<string> GaIcvNeighbors(
        [Description("Chord symbol, e.g. 'Am7', 'G7'")] string symbol,
        [Description("Max L1 distance between interval-class vectors (1–4, default 1). " +
                     "Sets of the same size are at least 2 apart; distance 0 is a Z-related set.")] int maxDistance = 1)
    {
        var pcs = await GetPitchClassesAsync(symbol);
        if (pcs.Length == 0) return $"Error: could not parse chord '{symbol}'";

        var sourceSet = ToPitchClassSet(pcs);
        var sourceIcv = sourceSet.IntervalClassVector;
        var sourcePrime = sourceSet.PrimeForm;
        var distance = Math.Clamp(maxDistance, 1, 4);
        // FindNearby scans all 4096 sets and reports those sharing the source's vector at
        // distance 1 (GrothendieckDelta.FromIcVs turns a zero delta into Ic1 = 1), so the 24 major
        // and minor triads came back as C's neighbours. Use the actual L1 distance, skip the
        // chord's own set class and list each set class once.
        var neighbors = new GrothendieckService().FindNearby(sourceSet, distance)
            .Select(r => (r.Set, Distance: IcvDistance(sourceIcv, r.Set.IntervalClassVector)))
            .Where(n => n.Distance <= distance && !Equals(n.Set.PrimeForm, sourcePrime))
            .GroupBy(n => n.Set.PrimeForm?.ToString() ?? n.Set.ToString())
            .Select(g => g.First())
            .OrderBy(n => n.Distance)
            .Take(12)
            .ToList();

        if (neighbors.Count == 0)
            return $"No other set class within distance {distance} of {symbol} " +
                   "(sets of the same size are at least 2 apart)";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ICV neighbors of {symbol} (ICV {sourceIcv}, dist ≤ {distance}):");
        foreach (var (neighbor, neighborDistance) in neighbors)
        {
            var forte = ForteCatalog.GetForteNumber(neighbor.PrimeForm ?? neighbor);
            var neighborName = TranspositionName([.. neighbor.Select(pc => pc.Value)], neighbor.IntervalClassVector, rootPc: null);
            var nameStr = neighborName != null ? $" [{neighborName}]" : "";
            sb.AppendLine($"  {neighbor.IntervalClassVector}  Δ={neighborDistance}  Forte:{forte}{nameStr}");
        }
        return sb.ToString().TrimEnd();
    }

    private static int IcvDistance(IntervalClassVector a, IntervalClassVector b) =>
        Enumerable.Range(1, 6).Sum(ic => Math.Abs(a[IntervalClass.FromValue(ic)] - b[IntervalClass.FromValue(ic)]));
}
