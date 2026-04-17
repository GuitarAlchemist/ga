// AtonalModalFamiliesGenerator — one-off generator for AtonalModalFamilies.yaml.
//
// Bridges two independent catalogs in the GA ecosystem:
//
//   1. GA.Domain.Core.Theory.Atonal.ModalFamily.Items (~224 families)
//      Auto-generated from exhaustive pitch-class enumeration. Keyed by
//      IntervalClassVector. Contains prime form, mode members, PitchClassSetIds.
//      Purely atonal: no musician-facing names.
//
//   2. GA.Business.Config/Modes.yaml (31 named families)
//      Curated, musician-facing. Each family has a human name ("Major Scale Family"),
//      per-mode names ("Ionian", "Dorian", …), aliases, and notes in C.
//
// Output: AtonalModalFamilies.yaml — one entry per atonal ModalFamily, with
// tonal names imported from Modes.yaml where the member PC sets line up.
//
// Legal guardrails:
//   - NO coined names. Only tonal analogs that already appear in Modes.yaml
//     (centuries-old: Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian,
//     Locrian, plus the well-established harmonic/melodic minor mode names).
//   - Unnamed families: FamilyName = "Family-{ForteNumber}", positional mode
//     names ("Mode 0", "Mode 1", …).
//   - Matching is done by PitchClassSetId → (FamilyName, ModeName) lookup,
//     derived from Modes.yaml's Notes field (tolerant to ICV drift).
//
// Run:
//     dotnet run --project Demos/AtonalModalFamiliesGenerator
//
// Output file is checked into source control; the generator itself is one-off.

namespace GA.Demos.AtonalModalFamiliesGenerator;

using System.Globalization;
using System.Text;
using GA.Domain.Core.Theory.Atonal;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class Program
{
    private const int Mask12 = 0xFFF;

    private static int Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            Console.Error.WriteLine("[AtonalModalFamiliesGenerator] Could not locate GA repo root (AllProjects.slnx marker not found).");
            return 2;
        }

        var configDir  = Path.Combine(repoRoot, "Common", "GA.Business.Config");
        var modesYaml  = Path.Combine(configDir, "Modes.yaml");
        var outputYaml = Path.Combine(configDir, "AtonalModalFamilies.yaml");

        if (args.Length >= 1 && !string.IsNullOrWhiteSpace(args[0]))
        {
            outputYaml = Path.GetFullPath(args[0]);
        }

        Console.WriteLine($"[AtonalModalFamiliesGenerator] Repo root : {repoRoot}");
        Console.WriteLine($"[AtonalModalFamiliesGenerator] Modes.yaml: {modesYaml}");
        Console.WriteLine($"[AtonalModalFamiliesGenerator] Output    : {outputYaml}");

        if (!File.Exists(modesYaml))
        {
            Console.Error.WriteLine($"[AtonalModalFamiliesGenerator] Modes.yaml not found at {modesYaml}");
            return 2;
        }

        // 1. Load Modes.yaml and build PitchClassSetId -> (family name, mode name, aliases).
        var modesYamlData = LoadModesYaml(modesYaml);
        Console.WriteLine($"[AtonalModalFamiliesGenerator] Modes.yaml families : {modesYamlData.Families.Count}");
        Console.WriteLine($"[AtonalModalFamiliesGenerator] Modes.yaml mode names mapped to PC-set IDs : {modesYamlData.ModeLookup.Count}");

        // 2. Enumerate GA.Domain.Core ModalFamily.Items (~224 entries).
        var allFamilies = ModalFamily.Items
            .OrderBy(f => f.NoteCount)
            .ThenBy(f => f.IntervalClassVector.Id.Value)
            .ToList();
        Console.WriteLine($"[AtonalModalFamiliesGenerator] Atonal modal families : {allFamilies.Count}");

        // 3. Build enriched records.
        var enriched = new List<EnrichedFamily>(allFamilies.Count);
        var namedCount = 0;
        var symmetricCount = 0;
        var zPairCount = 0;

        var unmatchedModesYamlFamilies = new HashSet<string>(
            modesYamlData.Families.Select(f => f.Name),
            StringComparer.Ordinal);

        foreach (var family in allFamilies)
        {
            var record = BuildEnrichedFamily(family, modesYamlData);
            enriched.Add(record);

            if (record.IsFamilyNamed) namedCount++;
            if (record.IsSymmetric) symmetricCount++;
            if (record.ForteNumbers.Count > 1) zPairCount++;

            // Mark every Modes.yaml family that supplied at least one mode as matched,
            // not just the dominant-vote winner (captures Harmonic Minor + Harmonic Major
            // both resolving into the same atonal family).
            foreach (var sub in record.TonalSubfamilies)
            {
                unmatchedModesYamlFamilies.Remove(sub);
            }
        }

        // 4. Emit YAML.
        var sb = new StringBuilder(1 << 20);
        WritePreamble(sb, modesYaml);

        sb.Append("Families:\n");
        foreach (var rec in enriched)
        {
            WriteFamilyEntry(sb, rec);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputYaml)!);
        File.WriteAllText(outputYaml, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var info = new FileInfo(outputYaml);
        Console.WriteLine($"[AtonalModalFamiliesGenerator] Wrote {enriched.Count} families, {info.Length:N0} bytes to {outputYaml}");

        // 5. Summary counters.
        Console.WriteLine();
        Console.WriteLine("[summary] -------------------------------------------------------------");
        Console.WriteLine($"[summary] Total families         : {enriched.Count}");
        Console.WriteLine($"[summary] Named (tonal analog)   : {namedCount}");
        Console.WriteLine($"[summary] Unnamed (positional)   : {enriched.Count - namedCount}");
        Console.WriteLine($"[summary] Symmetric (single mode): {symmetricCount}");
        Console.WriteLine($"[summary] Z-related (2+ Forte)   : {zPairCount}");
        Console.WriteLine();

        if (unmatchedModesYamlFamilies.Count > 0)
        {
            Console.WriteLine("[summary] Modes.yaml families WITHOUT an atonal counterpart:");
            foreach (var n in unmatchedModesYamlFamilies.OrderBy(s => s, StringComparer.Ordinal))
                Console.WriteLine($"[summary]   - {n}");
            Console.WriteLine();
        }

        // 6. Spot-checks.
        if (!SpotCheck(enriched)) return 1;

        Console.WriteLine("[AtonalModalFamiliesGenerator] Spot checks passed.");
        return 0;
    }

    // ────────────────────────────────────────────────────────────────────
    // Enriched family record.
    // ────────────────────────────────────────────────────────────────────

    private sealed record EnrichedFamily(
        string IntervalClassVector,
        string FamilyName,
        bool IsFamilyNamed,
        IReadOnlyList<string> ForteNumbers,
        IReadOnlyList<string> TonalSubfamilies,
        int NoteCount,
        int DistinctModeCount,
        bool IsSymmetric,
        int PrimeModeId,
        IReadOnlyList<EnrichedMode> Modes);

    private sealed record EnrichedMode(
        int Position,
        string PitchClasses,
        int PitchClassSetId,
        string? TonalAnalog,
        IReadOnlyList<string> Aliases);

    private static EnrichedFamily BuildEnrichedFamily(ModalFamily family, ModesYamlData yamlData)
    {
        // Forte numbers: collect distinct labels from member prime forms.
        // Hexachord Z-pairs have two members with the same ICV but different prime forms,
        // so ForteNumbers.Count > 1 is the Z-pair signature.
        var forteNumbers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var mode in family.Modes)
        {
            var forte = ProgrammaticForteCatalog.GetForteNumber(mode);
            if (forte.HasValue) forteNumbers.Add(forte.Value.ToString());
        }

        // Try to find a tonal name by looking up any member PC-set ID in Modes.yaml.
        // Most families will either match 0 mode IDs (unnamed) or all of them (named).
        // A partial match (e.g., 7 modes in the family, only 3 in Modes.yaml) still
        // assigns the family name from whichever Modes.yaml family dominates.
        var familyNameVotes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var mode in family.Modes)
        {
            if (yamlData.ModeLookup.TryGetValue(mode.Id.Value, out var entry))
            {
                familyNameVotes[entry.FamilyName] = familyNameVotes.GetValueOrDefault(entry.FamilyName, 0) + 1;
            }
        }

        string familyName;
        bool isFamilyNamed;
        if (familyNameVotes.Count > 0)
        {
            // Pick the family name with the most mode hits (ties broken by ordinal order).
            familyName = familyNameVotes
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .First().Key;
            isFamilyNamed = true;
        }
        else
        {
            // Unnamed — synthesize a positional family label from the first Forte number.
            var firstForte = forteNumbers.FirstOrDefault();
            familyName = firstForte is null ? $"Family-ICV-{family.IntervalClassVector.Id.Value}" : $"Family-{firstForte}";
            isFamilyNamed = false;
        }

        // Build mode list with tonal analogs + aliases (from Modes.yaml) where available.
        // We do NOT gate by the winning family name: two distinct Modes.yaml families can
        // share one atonal ICV (e.g. Harmonic Minor + Harmonic Major both have ICV
        // <3 3 5 4 4 2>). Keep every mode's own tonal analog; the atonal family simply
        // unions them under one primary FamilyName (the dominant vote) plus a
        // TonalSubfamilies list capturing everything the modes trace back to.
        var modes = new List<EnrichedMode>(family.Modes.Count);
        var tonalSubfamilies = new SortedSet<string>(StringComparer.Ordinal);
        for (var pos = 0; pos < family.Modes.Count; pos++)
        {
            var mode = family.Modes[pos];
            string pitchClasses = FormatPitchClasses(mode.Id.Value);

            string? tonalAnalog = null;
            IReadOnlyList<string> aliases = Array.Empty<string>();
            if (yamlData.ModeLookup.TryGetValue(mode.Id.Value, out var entry))
            {
                tonalAnalog = entry.ModeName;
                aliases = entry.Aliases;
                tonalSubfamilies.Add(entry.FamilyName);
            }

            modes.Add(new EnrichedMode(
                Position: pos,
                PitchClasses: pitchClasses,
                PitchClassSetId: mode.Id.Value,
                TonalAnalog: tonalAnalog,
                Aliases: aliases));
        }

        // Symmetric = only one distinct PC-set ID in the family (e.g., Whole Tone).
        // But ModalFamily already deduplicates transpositions: Modes.Count is the
        // number of distinct members. Truly symmetric sets yield Modes.Count == 1.
        var distinctModeCount = family.Modes.Count;
        var isSymmetric = distinctModeCount == 1;

        return new EnrichedFamily(
            IntervalClassVector: family.IntervalClassVector.ToString(),
            FamilyName: familyName,
            IsFamilyNamed: isFamilyNamed,
            ForteNumbers: forteNumbers.ToList(),
            TonalSubfamilies: tonalSubfamilies.ToList(),
            NoteCount: family.NoteCount,
            DistinctModeCount: distinctModeCount,
            IsSymmetric: isSymmetric,
            PrimeModeId: family.PrimeMode.Id.Value,
            Modes: modes);
    }

    private static string FormatPitchClasses(int id)
    {
        var sb = new StringBuilder(24);
        var first = true;
        for (var i = 0; i < 12; i++)
        {
            if ((id & (1 << i)) == 0) continue;
            if (!first) sb.Append(' ');
            sb.Append(i.ToString(CultureInfo.InvariantCulture));
            first = false;
        }
        return sb.ToString();
    }

    // ────────────────────────────────────────────────────────────────────
    // Modes.yaml loader.
    // ────────────────────────────────────────────────────────────────────

    private sealed record ModesYamlEntry(
        string FamilyName,
        string ModeName,
        IReadOnlyList<string> Aliases);

    private sealed record ModesYamlFamily(string Name, IReadOnlyList<string> ModeNames);

    private sealed record ModesYamlData(
        IReadOnlyList<ModesYamlFamily> Families,
        IReadOnlyDictionary<int, ModesYamlEntry> ModeLookup);

    [YamlDotNet.Serialization.YamlSerializable]
    private sealed class YFile
    {
        public List<YFamily>? ModalFamilies { get; set; }
    }

    private sealed class YFamily
    {
        public string? Name { get; set; }
        public string? IntervalClassVector { get; set; }
        public List<YMode>? Modes { get; set; }
    }

    private sealed class YMode
    {
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public List<string>? AlternateNames { get; set; }
    }

    private static ModesYamlData LoadModesYaml(string path)
    {
        var text = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var file = deserializer.Deserialize<YFile>(text);

        var families = new List<ModesYamlFamily>();
        var lookup = new Dictionary<int, ModesYamlEntry>();

        if (file?.ModalFamilies is null) return new ModesYamlData(families, lookup);

        foreach (var fam in file.ModalFamilies)
        {
            if (string.IsNullOrWhiteSpace(fam.Name)) continue;
            var modeNames = new List<string>();

            if (fam.Modes is not null)
            {
                foreach (var mode in fam.Modes)
                {
                    if (string.IsNullOrWhiteSpace(mode.Name)) continue;
                    modeNames.Add(mode.Name);

                    // Convert Notes ("C D Eb F G Ab B") to PC-set mask → id.
                    if (string.IsNullOrWhiteSpace(mode.Notes)) continue;
                    var id = NotesToPitchClassSetId(mode.Notes);
                    if (id <= 0) continue;

                    var aliases = mode.AlternateNames is null
                        ? (IReadOnlyList<string>)Array.Empty<string>()
                        : mode.AlternateNames.ToList();

                    // First writer wins — keeps Major Scale Family's Ionian even if the
                    // same PC set appears in a later curated family (shouldn't happen but safe).
                    if (!lookup.ContainsKey(id))
                    {
                        lookup[id] = new ModesYamlEntry(fam.Name, mode.Name, aliases);
                    }
                }
            }

            families.Add(new ModesYamlFamily(fam.Name, modeNames));
        }

        return new ModesYamlData(families, lookup);
    }

    // Note-token → pitch-class lookup (same table as ScaleCatalogGenerator).
    private static readonly IReadOnlyDictionary<string, int> NoteToPc = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["C"] = 0,   ["B#"] = 0,   ["Dbb"] = 0,
        ["C#"] = 1,  ["Db"] = 1,
        ["D"] = 2,   ["Ebb"] = 2,  ["C##"] = 2,
        ["D#"] = 3,  ["Eb"] = 3,
        ["E"] = 4,   ["Fb"] = 4,   ["D##"] = 4,
        ["F"] = 5,   ["E#"] = 5,   ["Gbb"] = 5,
        ["F#"] = 6,  ["Gb"] = 6,
        ["G"] = 7,   ["Abb"] = 7,  ["F##"] = 7,
        ["G#"] = 8,  ["Ab"] = 8,
        ["A"] = 9,   ["Bbb"] = 9,  ["G##"] = 9,
        ["A#"] = 10, ["Bb"] = 10,  ["Cbb"] = 10,
        ["B"] = 11,  ["Cb"] = 11,  ["A##"] = 11,
    };

    private static int NotesToPitchClassSetId(string notes)
    {
        var acc = 0;
        foreach (var raw in notes.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;
            if (NoteToPc.TryGetValue(token, out var pc))
            {
                acc |= 1 << pc;
            }
            // Unknown tokens are skipped silently — curated data may drift over time.
        }
        return acc & Mask12;
    }

    // ────────────────────────────────────────────────────────────────────
    // YAML emission.
    // ────────────────────────────────────────────────────────────────────

    private static void WritePreamble(StringBuilder sb, string modesYamlPath)
    {
        sb.Append("# ===========================================================================\n");
        sb.Append("# AtonalModalFamilies.yaml - bridge between atonal ModalFamily and tonal Modes\n");
        sb.Append("# ===========================================================================\n");
        sb.Append("# Generated from GA.Domain.Core.Theory.Atonal.ModalFamily.Items (~224 families)\n");
        sb.Append("# cross-referenced against GA.Business.Config/Modes.yaml for tonal analogs.\n");
        sb.Append("#\n");
        sb.Append("# - Families are keyed by IntervalClassVector (atonal invariant).\n");
        sb.Append("# - Forte numbers come from ProgrammaticForteCatalog (Rahn ordering).\n");
        sb.Append("# - Tonal analogs are imported from Modes.yaml via PitchClassSetId matching\n");
        sb.Append("#   (resilient to ICV string drift in the curated data).\n");
        sb.Append("# - Unnamed families use positional modes (no coined names).\n");
        sb.Append("#\n");
        sb.Append("# Schema version : 1\n");
        sb.Append($"# Generated      : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");
        sb.Append("# Generator      : Demos/AtonalModalFamiliesGenerator (GA ecosystem)\n");
        sb.Append($"# Curated source : {Path.GetFileName(modesYamlPath)} (GA-owned, human-authored)\n");
        sb.Append("# Atonal source  : GA.Domain.Core.Theory.Atonal.ModalFamily (auto-enumerated)\n");
        sb.Append("# ===========================================================================\n");
        sb.Append('\n');
    }

    private static void WriteFamilyEntry(StringBuilder sb, EnrichedFamily rec)
    {
        var inv = CultureInfo.InvariantCulture;

        sb.Append("  - IntervalClassVector: \"").Append(rec.IntervalClassVector).Append("\"\n");
        sb.Append("    FamilyName: \"").Append(EscapeYaml(rec.FamilyName)).Append("\"\n");

        sb.Append("    ForteNumbers: [");
        for (var i = 0; i < rec.ForteNumbers.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(rec.ForteNumbers[i]).Append('"');
        }
        sb.Append("]\n");

        sb.Append("    TonalSubfamilies: [");
        for (var i = 0; i < rec.TonalSubfamilies.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(EscapeYaml(rec.TonalSubfamilies[i])).Append('"');
        }
        sb.Append("]\n");

        sb.Append("    NoteCount: ").Append(rec.NoteCount.ToString(inv)).Append('\n');
        sb.Append("    DistinctModeCount: ").Append(rec.DistinctModeCount.ToString(inv)).Append('\n');
        sb.Append("    IsSymmetric: ").Append(rec.IsSymmetric ? "true" : "false").Append('\n');
        sb.Append("    PrimeModeId: ").Append(rec.PrimeModeId.ToString(inv)).Append('\n');
        sb.Append("    Modes:\n");

        foreach (var m in rec.Modes)
        {
            sb.Append("      - Position: ").Append(m.Position.ToString(inv)).Append('\n');
            sb.Append("        PitchClasses: \"").Append(m.PitchClasses).Append("\"\n");
            sb.Append("        PitchClassSetId: ").Append(m.PitchClassSetId.ToString(inv)).Append('\n');
            if (m.TonalAnalog is null)
                sb.Append("        TonalAnalog: null\n");
            else
                sb.Append("        TonalAnalog: \"").Append(EscapeYaml(m.TonalAnalog)).Append("\"\n");
            sb.Append("        Aliases: [");
            for (var i = 0; i < m.Aliases.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(EscapeYaml(m.Aliases[i])).Append('"');
            }
            sb.Append("]\n");
        }

        sb.Append('\n');
    }

    private static string EscapeYaml(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ────────────────────────────────────────────────────────────────────
    // Spot-checks from the task brief.
    // ────────────────────────────────────────────────────────────────────

    private static bool SpotCheck(IReadOnlyList<EnrichedFamily> enriched)
    {
        var ok = true;

        // Major scale family: ICV <2 5 4 3 6 1>, 7 modes, all named.
        var major = enriched.FirstOrDefault(f => f.IntervalClassVector == "<2 5 4 3 6 1>");
        ok &= Assert("Major family found", major is not null ? "yes" : "no", "yes");
        if (major is not null)
        {
            ok &= Assert("Major family mode count", major.DistinctModeCount.ToString(), "7");
            ok &= Assert("Major family name", major.FamilyName, "Major Scale Family");
            var namedModes = major.Modes.Count(m => m.TonalAnalog is not null);
            ok &= Assert("Major family named modes", namedModes.ToString(), "7");
            // Ionian + Dorian at positions 0 and 1 (by mode ID ordering from ModalFamily).
            var analogs = string.Join(",", major.Modes.Select(m => m.TonalAnalog ?? "null"));
            Console.WriteLine($"[spot] Major analogs: {analogs}");
        }

        // Whole tone: ICV <0 6 0 0 6 0>  (from Modes.yaml entry), 1 distinct mode, symmetric.
        // The ModalFamily class dedupes transpositions, so whole-tone ends up as a single-mode
        // entry. Double-check by ICV pattern (Tritonia=3 but ICV is <0 6 0 6 0 3> in theory?).
        // The ICV for whole-tone (0,2,4,6,8,10) is: IC1=0, IC2=6, IC3=0, IC4=6, IC5=0, IC6=3.
        var wholeTone = enriched.FirstOrDefault(f => f.IntervalClassVector == "<0 6 0 6 0 3>");
        ok &= Assert("Whole-tone family found (canonical ICV)", wholeTone is not null ? "yes" : "no", "yes");
        if (wholeTone is not null)
        {
            ok &= Assert("Whole-tone IsSymmetric", wholeTone.IsSymmetric.ToString(), "True");
            ok &= Assert("Whole-tone distinct mode count", wholeTone.DistinctModeCount.ToString(), "1");
        }

        // Octatonic (0,1,3,4,6,7,9,10) — ICV <4 4 8 4 4 4>? Let's calculate:
        //   PCs: 0,1,3,4,6,7,9,10 → pairs give a balanced spread.
        // Known ICV for the octatonic collection (8-28): <4 4 8 4 4 4>. Look for it.
        var octatonic = enriched.FirstOrDefault(f => f.IntervalClassVector == "<4 4 8 4 4 4>");
        if (octatonic is not null)
        {
            Console.WriteLine($"[spot] Octatonic found: {octatonic.FamilyName}, modes={octatonic.DistinctModeCount}, symmetric={octatonic.IsSymmetric}");
            // Octatonic has two distinct rotations (half-whole vs whole-half).
        }
        else
        {
            Console.WriteLine("[spot] Octatonic ICV <4 4 8 4 4 4> not present (informational only).");
        }

        return ok;
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
    // Repo-root discovery (same as ScaleCatalogGenerator).
    // ────────────────────────────────────────────────────────────────────

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
