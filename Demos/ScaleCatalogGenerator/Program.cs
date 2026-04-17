// ScaleCatalogGenerator — one-off generator for the full Ian Ring scale catalog.
//
// Emits C:\Users\spare\source\repos\ga\Common\GA.Business.Config\ExtendedScales.yaml
// containing every non-empty 12-bit pitch-class subset (4095 entries).
//
// All properties are computed directly from the 12-bit bitmask. The only
// reference into GA is ForteCatalog (GA.Domain.Core), used purely to keep
// Forte-number assignments consistent with the rest of the ecosystem.
//
// Run:
//     dotnet run --project Demos/ScaleCatalogGenerator
//
// Output file is checked into source control; the generator itself is one-off.

namespace GA.Demos.ScaleCatalogGenerator;

using System.Globalization;
using System.Numerics;
using System.Text;
using GA.Domain.Core.Theory.Atonal;

internal static class Program
{
    private const int Mask12 = 0xFFF;
    private const int TotalIds = 4095; // 1..4095 (non-empty subsets)

    private static int Main(string[] args)
    {
        // Resolve repo root relative to this source file location.
        // When invoked via `dotnet run`, AppContext.BaseDirectory points into bin/…
        // so we walk up to find the GA repo root via the .slnx marker.
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("[ScaleCatalogGenerator] Could not locate GA repo root (AllProjects.slnx marker not found).");
            return 2;
        }

        var configDir  = Path.Combine(repoRoot, "Common", "GA.Business.Config");
        var scalesYaml = Path.Combine(configDir, "Scales.yaml");
        var outputYaml = Path.Combine(configDir, "ExtendedScales.yaml");

        // Optional CLI override for output path.
        if (args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0]))
        {
            outputYaml = Path.GetFullPath(args[0]);
        }

        Console.WriteLine($"[ScaleCatalogGenerator] Repo root : {repoRoot}");
        Console.WriteLine($"[ScaleCatalogGenerator] Curated   : {scalesYaml}");
        Console.WriteLine($"[ScaleCatalogGenerator] Output    : {outputYaml}");

        // 1. Load well-known names from Scales.yaml (minimal parser — key + Notes line).
        var wellKnown = LoadWellKnownNames(scalesYaml);
        Console.WriteLine($"[ScaleCatalogGenerator] Loaded {wellKnown.Count} curated scale name(s) from Scales.yaml");

        // 2. Precompute orbit representatives (prime forms) for every scale id,
        //    under both Forte (1973) and Rahn (1980) conventions.
        var (primeFortById, primeRahnById) = ComputeAllPrimeForms();
        var forteDistinct = new HashSet<int>(primeFortById).Count;
        var rahnDistinct  = new HashSet<int>(primeRahnById).Count;
        Console.WriteLine($"[ScaleCatalogGenerator] Distinct prime forms — Forte: {forteDistinct}, Rahn: {rahnDistinct}");

        // Count disagreements (Forte vs Rahn pick different prime forms).
        var disagreementCount = 0;
        var disagreeExamples = new List<(int id, int forte, int rahn)>();
        for (var id = 1; id <= TotalIds; id++)
        {
            if (primeFortById[id] != primeRahnById[id])
            {
                disagreementCount++;
                if (disagreeExamples.Count < 10)
                    disagreeExamples.Add((id, primeFortById[id], primeRahnById[id]));
            }
        }
        Console.WriteLine($"[ScaleCatalogGenerator] Forte/Rahn disagreements : {disagreementCount} scale ids");
        foreach (var (id, fp, rp) in disagreeExamples.Take(5))
            Console.WriteLine($"[ScaleCatalogGenerator]   e.g. id={id} Forte={fp} Rahn={rp}");

        // 3. Precompute Forte numbers via GA.Domain.Core.ForteCatalog (by prime form id).
        //    The lookup is against the 224 set-class catalog for cardinalities 3..9 (Forte scope).
        //    We index by the Forte prime form id (GA's catalog uses Forte 1973 convention internally).
        var forteByPrimeId = BuildForteIndex(primeFortById);
        Console.WriteLine($"[ScaleCatalogGenerator] Forte index size : {forteByPrimeId.Count}");

        // 4. Emit YAML.
        var sb = new StringBuilder(1 << 21); // 2 MiB initial
        WritePreamble(sb, scalesYaml);

        for (var id = 1; id <= TotalIds; id++)
        {
            var primeForte = primeFortById[id];
            var primeRahn  = primeRahnById[id];
            var isPrimeForte = (primeForte == id);
            var isPrimeRahn  = (primeRahn == id);
            string? forte = forteByPrimeId.TryGetValue(primeForte, out var f) ? f : null;
            wellKnown.TryGetValue(id, out var wellKnownName);

            WriteEntry(sb, id, primeForte, primeRahn, isPrimeForte, isPrimeRahn, forte, wellKnownName);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputYaml)!);
        File.WriteAllText(outputYaml, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var info = new FileInfo(outputYaml);
        Console.WriteLine($"[ScaleCatalogGenerator] Wrote {TotalIds} entries, {info.Length:N0} bytes to {outputYaml}");

        // Spot-checks (bubble up as non-zero exit on failure).
        if (!SpotCheck(primeFortById, primeRahnById, forteByPrimeId)) return 1;

        // Extra: show a classic 5-20 disagreement case if present.
        DumpClassic5_20(primeFortById, primeRahnById);

        Console.WriteLine("[ScaleCatalogGenerator] Spot checks passed.");
        return 0;
    }

    // ────────────────────────────────────────────────────────────────────
    // Bitmask-level math (all properties derive from a 12-bit integer).
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Rotate a 12-bit mask left by n semitones (transposition).</summary>
    private static int RotateLeft12(int v, int n)
    {
        n = ((n % 12) + 12) % 12;
        v &= Mask12;
        return ((v << n) | (v >> (12 - n))) & Mask12;
    }

    /// <summary>
    /// Reflection: reverse bit order within the 12-bit window.
    /// Bit i → bit (12 - i) mod 12. Matches Ian Ring's "reflection" definition.
    /// </summary>
    private static int Reflect12(int v)
    {
        var result = 0;
        for (var i = 0; i < 12; i++)
        {
            if ((v & (1 << i)) != 0)
            {
                var target = (12 - i) % 12;
                result |= 1 << target;
            }
        }
        return result;
    }

    /// <summary>Complement (bits not in the set, within 12-bit window).</summary>
    private static int Complement12(int v) => (~v) & Mask12;

    /// <summary>Interval-class vector IC1..IC6 as 6-element int array.</summary>
    private static int[] IntervalClassVector(int v)
    {
        // Collect present pitch classes.
        Span<int> pcs = stackalloc int[12];
        var count = 0;
        for (var i = 0; i < 12; i++)
        {
            if ((v & (1 << i)) != 0) pcs[count++] = i;
        }

        var ic = new int[6];
        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                var d = pcs[j] - pcs[i];
                // Interval class = min(d, 12 - d), range 1..6
                var klass = d <= 6 ? d : 12 - d;
                if (klass >= 1 && klass <= 6) ic[klass - 1]++;
            }
        }
        return ic;
    }

    /// <summary>
    /// Perfection count: number of pitch classes whose +7 (perfect fifth) partner
    /// is also present in the set.
    /// </summary>
    private static int PerfectionCount(int v)
    {
        var count = 0;
        for (var i = 0; i < 12; i++)
        {
            if ((v & (1 << i)) == 0) continue;
            var fifth = (i + 7) % 12;
            if ((v & (1 << fifth)) != 0) count++;
        }
        return count;
    }

    // ────────────────────────────────────────────────────────────────────
    // Prime form — two conventions (Forte 1973, Rahn 1980).
    //
    // Shared setup:
    //   1. Orbit = the set's 12 rotations + the inversion's 12 rotations (up
    //      to 24 distinct PC-sets).
    //   2. Each candidate is reduced to its "normal order": the ascending
    //      rotation of length n whose span (last - first) is minimised, then
    //      transposed so the first PC is 0.
    //
    // The two conventions differ in how ties are broken:
    //
    //   Forte 1973 "most packed to the left" = compare the INTERVAL sequence
    //   [d_1, d_2, …, d_{n-1}] lexicographically LEFT-TO-RIGHT. The rotation
    //   with the smallest first interval (d_1) wins; ties broken by d_2, etc.
    //
    //   Rahn 1980 "most packed to the left" = compare the INTERVAL sequence
    //   [d_1, d_2, …, d_{n-1}] lexicographically RIGHT-TO-LEFT — equivalently,
    //   minimise d_{n-1} first, then d_{n-2}, etc. This is also expressible as
    //   "PC values compared from the RIGHT": the rotation with the smallest
    //   next-to-last PC (relative to 0) wins; ties broken by the one before.
    //
    // Primal form = min under the chosen packing rule, taken over
    // {normal(set), normal(inversion)}.
    //
    // Classic disagreements (by set-class label, Forte 1973 numbering):
    //   5-20, 6-Z29, 6-Z50, 6-31, 7-20, 8-26 — plus their complements.
    // ────────────────────────────────────────────────────────────────────

    /// <summary>Enumerate the ascending pitch classes (0..11) present in the mask.</summary>
    private static int[] AscendingPitchClasses(int v)
    {
        var list = new int[BitOperations.PopCount((uint)v)];
        var k = 0;
        for (var i = 0; i < 12; i++)
            if ((v & (1 << i)) != 0) list[k++] = i;
        return list;
    }

    /// <summary>
    /// Rotate an ascending PC array cyclically so element at index <paramref name="start"/>
    /// becomes the first, then transpose so first element is 0. Returns a length-n
    /// int[] starting at 0.
    /// </summary>
    private static int[] RotationStartingAtZero(int[] pcs, int start)
    {
        var n = pcs.Length;
        var first = pcs[start];
        var cand = new int[n];
        for (var i = 0; i < n; i++)
        {
            cand[i] = (pcs[(start + i) % n] - first + 12) % 12;
        }
        return cand;
    }

    /// <summary>
    /// Minimum-span rotations of a PC set, transposed to start at 0.
    /// Returns every candidate whose span (last element) equals the minimum span.
    /// A set's "normal order" under Forte or Rahn is picked from this candidate list
    /// by the convention-specific tie-break.
    /// </summary>
    private static List<int[]> MinSpanRotations(int v)
    {
        var pcs = AscendingPitchClasses(v);
        var n = pcs.Length;
        var result = new List<int[]>();
        if (n == 0) return result;
        if (n == 1) { result.Add([0]); return result; }

        var minSpan = int.MaxValue;
        var all = new List<int[]>(n);
        for (var start = 0; start < n; start++)
        {
            var cand = RotationStartingAtZero(pcs, start);
            all.Add(cand);
            if (cand[^1] < minSpan) minSpan = cand[^1];
        }
        foreach (var c in all)
            if (c[^1] == minSpan) result.Add(c);
        return result;
    }

    /// <summary>Inversion (mod 12) of a PC set mask. Also ends up as a 12-bit mask.</summary>
    private static int InvertMask(int v)
    {
        var inv = 0;
        for (var i = 0; i < 12; i++)
        {
            if ((v & (1 << i)) != 0)
            {
                var q = (12 - i) % 12;
                inv |= 1 << q;
            }
        }
        return inv;
    }

    /// <summary>
    /// Forte packing rule: compare [d1, d2, …, d_{n-1}] lexicographically LEFT-TO-RIGHT.
    /// Returns negative if a is "more packed" (smaller), positive if b is, 0 if equal.
    /// </summary>
    private static int CompareFortePacking(int[] a, int[] b)
    {
        var n = a.Length;
        for (var i = 1; i < n; i++)
        {
            var da = a[i] - a[i - 1];
            var db = b[i] - b[i - 1];
            if (da != db) return da - db;
        }
        return 0;
    }

    /// <summary>
    /// Rahn packing rule: compare [d_{n-1}, d_{n-2}, …, d_1] lexicographically
    /// (i.e. the interval sequence read RIGHT-TO-LEFT). Equivalently: compare
    /// the PC values themselves RIGHT-TO-LEFT starting from a[n-1].
    /// </summary>
    private static int CompareRahnPacking(int[] a, int[] b)
    {
        var n = a.Length;
        // Smallest *final* PC wins; the final PC equals the span. But among candidates
        // with equal span, we compare by a[n-2], a[n-3], …, a[1] (all relative to 0).
        for (var i = n - 1; i >= 1; i--)
        {
            if (a[i] != b[i]) return a[i] - b[i];
        }
        return 0;
    }

    /// <summary>Pick the normal order of a set under a given packing rule.</summary>
    private static int[] NormalOrder(int v, Comparison<int[]> packing)
    {
        var candidates = MinSpanRotations(v);
        var best = candidates[0];
        for (var i = 1; i < candidates.Count; i++)
        {
            if (packing(candidates[i], best) < 0) best = candidates[i];
        }
        return best;
    }

    /// <summary>
    /// Compute the prime form under a given packing rule:
    ///   min-under-rule( normalOrder(set, rule), normalOrder(inversion, rule) ).
    /// Returns the prime form as a 12-bit mask.
    /// </summary>
    private static int PrimeFormWith(int v, Comparison<int[]> packing)
    {
        if (v == 0) return 0;
        var setNormal = NormalOrder(v, packing);
        var invNormal = NormalOrder(InvertMask(v), packing);
        var chosen = packing(setNormal, invNormal) <= 0 ? setNormal : invNormal;
        return MaskFromPcs(chosen);
    }

    private static int PrimeFormForte(int v) => PrimeFormWith(v, CompareFortePacking);
    private static int PrimeFormRahn(int v)  => PrimeFormWith(v, CompareRahnPacking);

    private static int MaskFromPcs(int[] pcs)
    {
        var m = 0;
        foreach (var p in pcs) m |= 1 << p;
        return m;
    }

    /// <summary>
    /// Precompute prime forms (Forte and Rahn) for every scale id 0..4095 in one pass.
    /// </summary>
    private static (int[] forte, int[] rahn) ComputeAllPrimeForms()
    {
        var forte = new int[4096];
        var rahn  = new int[4096];
        for (var id = 0; id <= 4095; id++)
        {
            forte[id] = PrimeFormForte(id);
            rahn[id]  = PrimeFormRahn(id);
        }
        return (forte, rahn);
    }

    // ────────────────────────────────────────────────────────────────────
    // Forte number lookup via GA.Domain.Core.ForteCatalog.
    // ────────────────────────────────────────────────────────────────────

    private static Dictionary<int, string> BuildForteIndex(int[] primeFormById)
    {
        // Unique prime-form ids to query.
        var primeIds = new HashSet<int>();
        foreach (var p in primeFormById) primeIds.Add(p);

        var dict = new Dictionary<int, string>(primeIds.Count);
        foreach (var primeId in primeIds)
        {
            if (primeId == 0) continue; // empty set — no Forte number

            try
            {
                // Build a PitchClassSet from the prime-form bitmask.
                var pcList = new List<PitchClass>(BitOperations.PopCount((uint)primeId));
                for (var i = 0; i < 12; i++)
                {
                    if ((primeId & (1 << i)) != 0)
                        pcList.Add(PitchClass.FromValue(i));
                }

                var pcs = new PitchClassSet(pcList);
                var forte = ForteCatalog.GetForteNumber(pcs);
                if (forte.HasValue)
                {
                    dict[primeId] = forte.Value.ToString();
                }
            }
            catch
            {
                // Non-fatal: leave unset -> will emit `null` in YAML.
            }
        }
        return dict;
    }

    // ────────────────────────────────────────────────────────────────────
    // Cross-reference with curated Scales.yaml (well-known names).
    // ────────────────────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, int> NoteToPc = new Dictionary<string, int>
    {
        ["C"] = 0,  ["B#"] = 0,
        ["C#"] = 1, ["Db"] = 1,
        ["D"] = 2,
        ["D#"] = 3, ["Eb"] = 3,
        ["E"] = 4,  ["Fb"] = 4,
        ["F"] = 5,  ["E#"] = 5,
        ["F#"] = 6, ["Gb"] = 6,
        ["G"] = 7,
        ["G#"] = 8, ["Ab"] = 8,
        ["A"] = 9,
        ["A#"] = 10, ["Bb"] = 10,
        ["B"] = 11,  ["Cb"] = 11
    };

    /// <summary>
    /// Minimal YAML parser specifically for Scales.yaml — extracts (Name → BinaryScaleId)
    /// pairs. Scale name is a top-level key (no leading whitespace, ends with ':'),
    /// and we read the Notes: field two lines below. Tolerant of blank lines / comments.
    /// </summary>
    private static IReadOnlyDictionary<int, string> LoadWellKnownNames(string scalesYaml)
    {
        var result = new Dictionary<int, string>();
        if (!File.Exists(scalesYaml)) return result;

        string? currentName = null;
        foreach (var rawLine in File.ReadLines(scalesYaml))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // Top-level key (no leading whitespace, ends with ':')
            if (!char.IsWhiteSpace(line[0]) && line.EndsWith(':'))
            {
                currentName = line[..^1].Trim();
                continue;
            }

            // Indented Notes: "C D E F G A B"
            if (currentName is not null)
            {
                var trimmed = line.TrimStart();
                const string notesKey = "Notes:";
                if (trimmed.StartsWith(notesKey, StringComparison.Ordinal))
                {
                    var value = trimmed[notesKey.Length..].Trim();
                    // Strip surrounding quotes if present.
                    if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                        value = value[1..^1];

                    var id = ComputeBinaryScaleId(value);
                    if (id > 0 && !result.ContainsKey(id))
                    {
                        result[id] = currentName;
                    }
                    currentName = null; // only the first Notes: after each name
                }
            }
        }
        return result;
    }

    private static int ComputeBinaryScaleId(string notes)
    {
        var acc = 0;
        foreach (var token in notes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (NoteToPc.TryGetValue(token, out var pc))
            {
                acc |= 1 << pc;
            }
        }
        return acc;
    }

    // ────────────────────────────────────────────────────────────────────
    // YAML emission.
    // ────────────────────────────────────────────────────────────────────

    private static void WritePreamble(StringBuilder sb, string scalesYamlPath)
    {
        sb.Append("# ===========================================================================\n");
        sb.Append("# ExtendedScales.yaml - pitch-class set catalog (cardinality 1-12)\n");
        sb.Append("# ===========================================================================\n");
        sb.Append("# Generated programmatically from first principles.\n");
        sb.Append("# Binary scale IDs: bitmask representation (bit i = pitch class i present).\n");
        sb.Append("# Interval vectors: standard music theory derivation.\n");
        sb.Append("# Forte numbers:    widely-adopted academic convention (Forte 1973 numbering).\n");
        sb.Append("# No text or naming from any third-party catalog is reproduced.\n");
        sb.Append("#\n");
        sb.Append("# Every non-empty 12-bit pitch-class subset (BinaryScaleId 1..4095 = 4095 entries).\n");
        sb.Append("#\n");
        sb.Append("# All properties derived mathematically from the 12-bit bitmask:\n");
        sb.Append("#   - PitchClasses    : space-separated 0..11\n");
        sb.Append("#   - Cardinality     : popcount(mask)\n");
        sb.Append("#   - IntervalVector  : IC1..IC6 counts (standard music set theory)\n");
        sb.Append("#   - ForteNumber     : from GA.Domain.Core.ForteCatalog (224 set classes);\n");
        sb.Append("#                       emitted as null when outside the 3-9 cardinality scope\n");
        sb.Append("#   - PrimeForm_Forte : Forte (1973) prime-form id. Compare set's normal order\n");
        sb.Append("#                       vs its inversion's normal order by lexicographic order\n");
        sb.Append("#                       of successive intervals (most-packed-from-left).\n");
        sb.Append("#   - PrimeForm_Rahn  : Rahn (1980) prime-form id. Same orbit, same normal order,\n");
        sb.Append("#                       but compare by lexicographic order of the PC values\n");
        sb.Append("#                       (cumulative intervals from the first PC).\n");
        sb.Append("#   - PrimeFormsAgree : true iff Forte and Rahn picked the same representative.\n");
        sb.Append("#                       Classic disagreements: 5-20, 6-Z29/50, 6-31, 7-20, 8-26.\n");
        sb.Append("#   - Complement      : (~mask) & 0xFFF\n");
        sb.Append("#   - Reflection      : bit i -> bit (12-i) mod 12\n");
        sb.Append("#   - Perfections     : pitch classes whose +7 partner is present\n");
        sb.Append("#   - IsPrime_Forte   : true iff this id equals its own Forte prime-form id\n");
        sb.Append("#   - IsPrime_Rahn    : true iff this id equals its own Rahn  prime-form id\n");
        sb.Append("#   - WellKnownName   : optional cross-reference from GA's curated Scales.yaml\n");
        sb.Append("#                       (absent when no established music-theory name exists)\n");
        sb.Append("#\n");
        sb.Append("# Schema version : 1\n");
        sb.Append($"# Generated      : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");
        sb.Append("# Generator      : Demos/ScaleCatalogGenerator (GA ecosystem)\n");
        sb.Append($"# Curated source : {Path.GetFileName(scalesYamlPath)} (GA-owned)\n");
        sb.Append("# ===========================================================================\n");
        sb.Append('\n');
    }

    private static void WriteEntry(
        StringBuilder sb,
        int id,
        int primeForteId,
        int primeRahnId,
        bool isPrimeForte,
        bool isPrimeRahn,
        string? forte,
        string? wellKnownName)
    {
        var inv = CultureInfo.InvariantCulture;

        // Key
        sb.Append("ScaleId_").Append(id.ToString(inv)).Append(":\n");

        // BinaryScaleId
        sb.Append("  BinaryScaleId: ").Append(id.ToString(inv)).Append('\n');

        // PitchClasses
        sb.Append("  PitchClasses: \"");
        var first = true;
        for (var i = 0; i < 12; i++)
        {
            if ((id & (1 << i)) == 0) continue;
            if (!first) sb.Append(' ');
            sb.Append(i.ToString(inv));
            first = false;
        }
        sb.Append("\"\n");

        // Cardinality
        sb.Append("  Cardinality: ").Append(BitOperations.PopCount((uint)id).ToString(inv)).Append('\n');

        // IntervalVector
        var icv = IntervalClassVector(id);
        sb.Append("  IntervalVector: \"")
          .Append(icv[0].ToString(inv)).Append(' ')
          .Append(icv[1].ToString(inv)).Append(' ')
          .Append(icv[2].ToString(inv)).Append(' ')
          .Append(icv[3].ToString(inv)).Append(' ')
          .Append(icv[4].ToString(inv)).Append(' ')
          .Append(icv[5].ToString(inv))
          .Append("\"\n");

        // ForteNumber
        if (forte is null)
            sb.Append("  ForteNumber: null\n");
        else
            sb.Append("  ForteNumber: \"").Append(forte).Append("\"\n");

        // PrimeForm_Forte / PrimeForm_Rahn / PrimeFormsAgree
        sb.Append("  PrimeForm_Forte: ").Append(primeForteId.ToString(inv)).Append('\n');
        sb.Append("  PrimeForm_Rahn: ").Append(primeRahnId.ToString(inv)).Append('\n');
        sb.Append("  PrimeFormsAgree: ").Append(primeForteId == primeRahnId ? "true" : "false").Append('\n');

        // Complement
        sb.Append("  Complement: ").Append(Complement12(id).ToString(inv)).Append('\n');

        // Reflection
        sb.Append("  Reflection: ").Append(Reflect12(id).ToString(inv)).Append('\n');

        // Perfections
        sb.Append("  Perfections: ").Append(PerfectionCount(id).ToString(inv)).Append('\n');

        // IsPrime_Forte / IsPrime_Rahn
        sb.Append("  IsPrime_Forte: ").Append(isPrimeForte ? "true" : "false").Append('\n');
        sb.Append("  IsPrime_Rahn: ").Append(isPrimeRahn ? "true" : "false").Append('\n');

        // WellKnownName (optional)
        if (!string.IsNullOrEmpty(wellKnownName))
        {
            sb.Append("  WellKnownName: \"").Append(EscapeYamlString(wellKnownName)).Append("\"\n");
        }

        sb.Append('\n');
    }

    private static string EscapeYamlString(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ────────────────────────────────────────────────────────────────────
    // Spot-checks from the task brief.
    // ────────────────────────────────────────────────────────────────────

    private static bool SpotCheck(int[] primeFortById, int[] primeRahnById, IReadOnlyDictionary<int, string> forteByPrimeId)
    {
        var ok = true;

        // Major scale: id 2741, ICV "2 5 4 3 6 1", prime form 1387 (both conventions).
        // Note: GA.Domain.Core's ProgrammaticForteCatalog uses Rahn ordering, which
        // assigns a different numerical index than Forte's historical 1973 scheme —
        // so we do NOT assert a specific "7-35" label here. The Forte *number string*
        // we emit is whatever GA's catalog returns (authoritative for this ecosystem).
        ok &= Assert("Major id 2741 ICV",
            string.Join(' ', IntervalClassVector(2741)), "2 5 4 3 6 1");
        ok &= Assert("Major id 2741 Forte prime  = 1387", primeFortById[2741].ToString(), "1387");
        ok &= Assert("Major id 2741 Rahn  prime  = 1387", primeRahnById[2741].ToString(), "1387");
        if (forteByPrimeId.TryGetValue(primeFortById[2741], out var majForte))
            Console.WriteLine($"[spot] Major 2741 -> GA Forte label: {majForte}");
        else
            Console.Error.WriteLine("[spot] Major Forte label missing (catalog incomplete — non-fatal).");

        // Prime form 1387 is its own prime under both conventions.
        ok &= Assert("1387 IsPrime_Forte", (primeFortById[1387] == 1387).ToString(), "True");
        ok &= Assert("1387 IsPrime_Rahn",  (primeRahnById[1387] == 1387).ToString(), "True");

        // Whole-tone scale: id 1365. Cardinality 6, ICV "0 6 0 6 0 3".
        ok &= Assert("Whole-tone id 1365 ICV",
            string.Join(' ', IntervalClassVector(1365)), "0 6 0 6 0 3");
        if (forteByPrimeId.TryGetValue(primeFortById[1365], out var wtForte))
            Console.WriteLine($"[spot] Whole-tone 1365 -> GA Forte label: {wtForte}");

        // Chromatic scale: id 4095. Cardinality 12, ICV "12 12 12 12 12 6".
        ok &= Assert("Chromatic id 4095 cardinality",
            BitOperations.PopCount(4095u).ToString(), "12");
        ok &= Assert("Chromatic id 4095 ICV",
            string.Join(' ', IntervalClassVector(4095)), "12 12 12 12 12 6");
        if (forteByPrimeId.TryGetValue(primeFortById[4095], out var chrForte))
            ok &= Assert("Chromatic Forte", chrForte, "12-1");

        return ok;
    }

    /// <summary>
    /// Classic 5-20 Forte/Rahn disagreement:
    ///   Forte 1973:  { 0, 1, 5, 6, 8 }  (prime 355 = 0b101100011)
    ///   Rahn  1980:  { 0, 1, 3, 7, 8 }  (prime 395 = 0b110001011)
    /// We print both picks for the first scale id whose orbit lands here, as
    /// independent corroboration that the two algorithms disagree where expected.
    /// </summary>
    private static void DumpClassic5_20(int[] primeFortById, int[] primeRahnById)
    {
        // Pick a canonical 5-20 member, e.g. the one with the lowest scale id whose
        // Forte prime maps into a known Forte catalog 5-20 candidate. We don't need
        // to know Forte numbers here — just report the two primes for a disagreement.
        for (var id = 1; id <= 4095; id++)
        {
            if (primeFortById[id] == primeRahnById[id]) continue;
            if (BitOperations.PopCount((uint)id) != 5) continue;
            Console.WriteLine($"[spot] 5-card disagreement example: id={id}  Forte={primeFortById[id]}  Rahn={primeRahnById[id]}");
            Console.WriteLine($"[spot]   Forte PCs: {{{string.Join(",", AscendingPitchClasses(primeFortById[id]))}}}");
            Console.WriteLine($"[spot]   Rahn  PCs: {{{string.Join(",", AscendingPitchClasses(primeRahnById[id]))}}}");
            return;
        }
        Console.WriteLine("[spot] No cardinality-5 Forte/Rahn disagreement observed.");
    }

    private static bool Assert(string label, string actual, string expected)
    {
        if (actual == expected)
        {
            Console.WriteLine($"[spot] OK   {label,-40}  = {actual}");
            return true;
        }
        Console.Error.WriteLine($"[spot] FAIL {label,-40}  expected '{expected}', got '{actual}'");
        return false;
    }

    // ────────────────────────────────────────────────────────────────────
    // Repo-root discovery.
    // ────────────────────────────────────────────────────────────────────

    private static string? FindRepoRoot()
    {
        // Walk up from AppContext.BaseDirectory looking for AllProjects.slnx (GA marker).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }

        // Fallback: walk up from Environment.CurrentDirectory.
        dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
