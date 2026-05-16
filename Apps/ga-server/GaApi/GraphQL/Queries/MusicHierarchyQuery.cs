namespace GaApi.GraphQL.Queries;

using HotChocolate.Types;
using GA.Domain.Core.Theory.Atonal;
using GA.Business.Config;
using Microsoft.FSharp.Core;

// ── GraphQL DTOs ─────────────────────────────────────────────────────────────
// Shape matches ReactComponents/ga-react-components/src/types/musicHierarchy.ts.
// Six levels (atonal → tonal hierarchy):
//   SetClass → ForteNumber → PrimeForm → Chord → ChordVoicing
//   Scale (parallel branch from theory taxonomy)
//
// Wired 2026-05-16 after the navigator went live without a backend resolver
// and the frontend defaulted to a stale https://localhost:7001 endpoint. Chord
// and ChordVoicing levels return empty lists for v1; backed by MongoDB in a
// follow-up (task tracked separately).

public enum MusicHierarchyLevel
{
    SetClass,
    ForteNumber,
    PrimeForm,
    Chord,
    ChordVoicing,
    Scale,
}

public record MusicHierarchyLevelInfo(
    MusicHierarchyLevel Level,
    string DisplayName,
    string Description,
    int TotalItems,
    string PrimaryMetric,
    IReadOnlyList<string> Highlights);

public record MusicHierarchyItem(
    string Id,
    string Name,
    MusicHierarchyLevel Level,
    string Category,
    string? Description,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Metadata);

[ExtendObjectType("Query")]
public class MusicHierarchyQuery
{
    /// <summary>
    /// Lists the six levels of the GA music-hierarchy taxonomy with counts
    /// derived from the live domain catalogs. Cheap; no I/O.
    /// </summary>
    public IReadOnlyList<MusicHierarchyLevelInfo> MusicHierarchyLevels()
    {
        var setClassCount   = SetClass.Items.Count;
        var forteCount      = ForteNumber.Items.Count;
        var primeFormCount  = PitchClassSet.Items.Count(p => p.IsPrimeForm);
        var scaleCount      = ScalesConfig.GetAllScales().Count();

        return new[]
        {
            new MusicHierarchyLevelInfo(
                MusicHierarchyLevel.SetClass,
                "Set Classes",
                "Equivalence classes of pitch-class sets under transposition and inversion.",
                setClassCount,
                "cardinality",
                ["Z-relation surfaces hidden symmetry", "Equivalence under T/I", "Forte: ~220 classes incl. trivial"]),

            new MusicHierarchyLevelInfo(
                MusicHierarchyLevel.ForteNumber,
                "Forte Numbers",
                "Allen Forte's canonical naming convention for atonal pitch-class sets.",
                forteCount,
                "cardinality-ordinal",
                ["Format: <cardinality>-<ordinal>", "7-35 is the major scale", "8-Z29 is the only Z-pair across cardinalities"]),

            new MusicHierarchyLevelInfo(
                MusicHierarchyLevel.PrimeForm,
                "Prime Forms",
                "The minimum-bit-value representative of each transposition / inversion class.",
                primeFormCount,
                "binary",
                ["Smallest int across orbit", "One per Forte class", "Direct mapping to interval-class vector"]),

            new MusicHierarchyLevelInfo(
                MusicHierarchyLevel.Chord,
                "Chords",
                "Named chord types (MongoDB-backed; follow-up wiring pending).",
                0,
                "voicing-count",
                ["Placeholder; not yet wired to ChordRepository"]),

            new MusicHierarchyLevelInfo(
                MusicHierarchyLevel.ChordVoicing,
                "Chord Voicings",
                "Specific fretboard / instrument realizations of a chord (MongoDB-backed; follow-up wiring pending).",
                0,
                "fingering-difficulty",
                ["Placeholder; not yet wired to VectorSearch index"]),

            new MusicHierarchyLevelInfo(
                MusicHierarchyLevel.Scale,
                "Scales",
                "Named scale catalog from Scales.yaml — 12-bit pitch-class bitmasks with Forte cross-references where present.",
                scaleCount,
                "modal-rotation",
                ["Major, Minor, Modal, Symmetric, Exotic families", "BinaryScaleId enables prime-form join", "AlternateNames for jazz / world idioms"]),
        };
    }

    /// <summary>
    /// Returns the items at the requested level with optional search +
    /// pagination. parentId is honored where the hierarchy supports it
    /// (e.g. a SetClass parent filters PrimeForms to its orbit).
    /// </summary>
    public IReadOnlyList<MusicHierarchyItem> MusicHierarchyItems(
        MusicHierarchyLevel level,
        string? parentId = null,
        int? take = null,
        string? search = null)
    {
        var pageSize = take is > 0 ? Math.Min(take.Value, 500) : 50;
        var needle = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        return level switch
        {
            MusicHierarchyLevel.SetClass => SetClass.Items
                .Where(sc => MatchesSearch(sc.ToString(), needle))
                .Take(pageSize)
                .Select(sc => new MusicHierarchyItem(
                    Id: sc.PrimeForm.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Name: sc.ToString(),
                    Level: MusicHierarchyLevel.SetClass,
                    Category: $"cardinality-{sc.Cardinality.Value}",
                    Description: $"Set class of cardinality {sc.Cardinality.Value}",
                    Tags: sc.IsModal ? new[] { "modal" } : Array.Empty<string>(),
                    Metadata: new Dictionary<string, string>
                    {
                        ["primeFormId"] = sc.PrimeForm.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["cardinality"] = sc.Cardinality.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["isModal"]     = sc.IsModal.ToString(),
                        ["icv"]         = sc.IntervalClassVector.ToString() ?? string.Empty,
                    }))
                .ToList(),

            MusicHierarchyLevel.ForteNumber => ForteNumber.Items
                .Where(fn => MatchesSearch(fn.ToString(), needle))
                .Take(pageSize)
                .Select(fn => new MusicHierarchyItem(
                    Id: fn.ToString(),
                    Name: fn.ToString(),
                    Level: MusicHierarchyLevel.ForteNumber,
                    Category: $"cardinality-{fn.Cardinality.Value}",
                    Description: $"Forte number {fn} (cardinality {fn.Cardinality.Value})",
                    Tags: Array.Empty<string>(),
                    Metadata: new Dictionary<string, string>
                    {
                        ["cardinality"] = fn.Cardinality.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["index"]       = fn.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    }))
                .ToList(),

            MusicHierarchyLevel.PrimeForm => PitchClassSet.Items
                .Where(p => p.IsPrimeForm)
                .Where(p => MatchesSearch(p.ToString() ?? string.Empty, needle))
                .Take(pageSize)
                .Select(p =>
                {
                    var hasForte = ForteCatalog.TryGetForteNumber(p, out var forte);
                    var forteLabel = hasForte ? forte.ToString() : string.Empty;
                    return new MusicHierarchyItem(
                        Id: p.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Name: p.ToString() ?? $"Prime({p.Id.Value})",
                        Level: MusicHierarchyLevel.PrimeForm,
                        Category: $"cardinality-{p.Count}",
                        Description: forteLabel.Length > 0 ? $"Prime form for Forte {forteLabel}" : "Prime form",
                        Tags: forteLabel.Length > 0 ? new[] { forteLabel } : Array.Empty<string>(),
                        Metadata: new Dictionary<string, string>
                        {
                            ["id"]    = p.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["count"] = p.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ["forte"] = forteLabel,
                        });
                })
                .ToList(),

            MusicHierarchyLevel.Scale => ScalesConfig.GetAllScales()
                .Where(s => MatchesSearch(s.Name, needle))
                .Take(pageSize)
                .Select(s => new MusicHierarchyItem(
                    Id: s.BinaryScaleId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Name: s.Name,
                    Level: MusicHierarchyLevel.Scale,
                    Category: OptionToString(s.Category, s.Common ? "common" : "extended"),
                    Description: OptionToNullable(s.Description),
                    Tags: BuildScaleTags(s),
                    Metadata: new Dictionary<string, string>
                    {
                        ["binaryScaleId"] = s.BinaryScaleId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["notes"]         = s.Notes,
                        ["forte"]         = OptionToString(s.ForteNumber, string.Empty),
                        ["common"]        = s.Common.ToString(),
                    }))
                .ToList(),

            // Chord + ChordVoicing return empty until MongoDB wiring lands.
            // The frontend shows the empty state with the level-info description
            // explaining the gap, so users see WHY rather than an error.
            MusicHierarchyLevel.Chord       => Array.Empty<MusicHierarchyItem>(),
            MusicHierarchyLevel.ChordVoicing => Array.Empty<MusicHierarchyItem>(),

            _ => Array.Empty<MusicHierarchyItem>(),
        };

        static bool MatchesSearch(string candidate, string? needle) =>
            needle is null || candidate.Contains(needle, StringComparison.OrdinalIgnoreCase);

        static string OptionToString(FSharpOption<string> opt, string fallback) =>
            FSharpOption<string>.get_IsSome(opt) ? opt.Value : fallback;

        static string? OptionToNullable(FSharpOption<string> opt) =>
            FSharpOption<string>.get_IsSome(opt) ? opt.Value : null;

        static IReadOnlyList<string> BuildScaleTags(ScalesConfig.ScaleInfo scale)
        {
            var tags = new List<string>();
            if (scale.Common) tags.Add("common");
            if (scale.AlternateNames is { Count: > 0 } alts)
            {
                foreach (var alt in alts) tags.Add(alt);
            }
            return tags;
        }
    }
}
