namespace GA.Business.Core.Fretboard.Invariants;

using Fretboard;

/// <summary>
/// Comprehensive CAGED recognition and utilities backed by PatternId normalization.
/// </summary>
[PublicAPI]
public static class CagedSystemIntegration
{
    #region Public enums / DTOs

    public enum CagedShape
    {
        Unknown = 0,
        C,
        A,
        G,
        E,
        D
    }

    public enum CagedQuality
    {
        Major,
        Minor,
        Dominant7,
        Major7,
        Minor7,
        Sus2,
        Sus4,
        Power
    }

    public readonly record struct CagedMatcherOptions(
        bool AllowEdgeStringFlex = true,
        double EdgePenalty = 0.25,
        double MismatchPenalty = 1.0,
        int MinCoreStrings = 4);

    public readonly record struct CagedShapeInfo(
        CagedShape Shape,
        CagedQuality Quality,
        int BaseFret,
        PatternId CanonicalId,
        PatternId MatchedId,
        string VariantLabel,
        double Confidence,
        bool Barre,
        int? BarreFret,
        int BarreSpan,
        ChordDifficulty Difficulty);

    #endregion

    #region Catalog (canonical prime-form patterns)

    private static class Catalog
    {
        // For quick exact matching
        public static readonly Dictionary<(CagedShape shape, CagedQuality quality), HashSet<PatternId>> Map = new();
        // For reverse lookup (PatternId -> first known (shape, quality, label))
        public static readonly Dictionary<PatternId, (CagedShape shape, CagedQuality quality, string label)> Reverse = new();

        static Catalog()
        {
            // Seed canonical open shapes for majors (existing MVP)
            Add(CagedShape.C, CagedQuality.Major, "C open",
                new[] { -1, 3, 2, 0, 1, 0 }); // canonical open C: [-1,3,2,0,1,0]

            Add(CagedShape.A, CagedQuality.Major, "A open",
                new[] { 0, 0, 2, 2, 2, 0 });

            Add(CagedShape.G, CagedQuality.Major, "G open",
                new[] { 3, 2, 0, 0, 0, 3 });

            Add(CagedShape.E, CagedQuality.Major, "E open",
                new[] { 0, 2, 2, 1, 0, 0 });

            Add(CagedShape.D, CagedQuality.Major, "D open",
                new[] { -1, -1, 0, 2, 3, 2 });

            // Common minor variants
            Add(CagedShape.E, CagedQuality.Minor, "Em open",
                new[] { 0, 2, 2, 0, 0, 0 });
            Add(CagedShape.A, CagedQuality.Minor, "Am open",
                new[] { 0, 0, 2, 2, 1, 0 });
            Add(CagedShape.D, CagedQuality.Minor, "Dm open",
                new[] { -1, -1, 0, 2, 3, 1 });

            // 7th family essentials
            Add(CagedShape.E, CagedQuality.Dominant7, "E7 open",
                new[] { 0, 2, 0, 1, 0, 0 });
            Add(CagedShape.A, CagedQuality.Dominant7, "A7 open",
                new[] { 0, 0, 2, 0, 2, 0 });
            Add(CagedShape.D, CagedQuality.Dominant7, "D7 open",
                new[] { -1, -1, 0, 2, 1, 2 });

            Add(CagedShape.E, CagedQuality.Major7, "Emaj7 open",
                new[] { 0, 2, 1, 1, 0, 0 });
            Add(CagedShape.A, CagedQuality.Major7, "Amaj7 open",
                new[] { 0, 0, 2, 1, 2, 0 });

            Add(CagedShape.E, CagedQuality.Minor7, "Em7 open",
                new[] { 0, 2, 0, 0, 0, 0 });
            Add(CagedShape.A, CagedQuality.Minor7, "Am7 open",
                new[] { 0, 0, 2, 0, 1, 0 });

            // Suspended
            Add(CagedShape.E, CagedQuality.Sus2, "Esus2 variant",
                new[] { 0, 2, 2, 4, 0, 0 }); // normalized relative form of a common Esus2 voicing
            Add(CagedShape.E, CagedQuality.Sus4, "Esus4 open",
                new[] { 0, 2, 2, 2, 0, 0 });
            Add(CagedShape.A, CagedQuality.Sus2, "Asus2 open",
                new[] { 0, 0, 2, 2, 0, 0 });
            Add(CagedShape.A, CagedQuality.Sus4, "Asus4 open",
                new[] { 0, 0, 2, 2, 3, 0 });

            // Power chord (use E and A string roots; represent as 3-note core across low strings)
            Add(CagedShape.E, CagedQuality.Power, "E5 (6th string root)",
                new[] { 0, 0, -1, -1, -1, -1 }); // normalized representation: E (root) + B (5th) doubles via same fret on A
            Add(CagedShape.A, CagedQuality.Power, "A5 (5th string root)",
                new[] { -1, 0, 2, -1, -1, -1 });

            // Add helper local to register items
            static void Add(CagedShape shape, CagedQuality quality, string label, int[] primePattern)
            {
                var id = PatternId.FromPattern(primePattern);
                var key = (shape, quality);
                if (!Map.TryGetValue(key, out var set))
                {
                    set = new HashSet<PatternId>();
                    Map[key] = set;
                }
                set.Add(id);
                // only store first label per id for quick reporting
                if (!Reverse.ContainsKey(id)) Reverse[id] = (shape, quality, label);
            }
        }
    }

    #endregion

    #region Public API - Identification

    /// <summary>
    /// Identify the CAGED shape for absolute frets using provided tuning (default guitar if not specified).
    /// Returns the shape only (quality-agnostic); for detailed info use TryIdentifyDetailed.
    /// </summary>
    public static CagedShape IdentifyCagedShape(int[] frets)
    {
        var invariant = ChordInvariant.FromFrets(frets, Tuning.Default);
        return IdentifyCagedShape(invariant.PatternId);
    }

    /// <summary>
    /// Identify the CAGED shape for a normalized pattern (major-quality bias). Returns Unknown if no major canonical matches.
    /// </summary>
    public static CagedShape IdentifyCagedShape(PatternId patternId)
    {
        // Prefer major sets for quick shape-only classification
        foreach (var shape in new[] { CagedShape.C, CagedShape.A, CagedShape.G, CagedShape.E, CagedShape.D })
        {
            if (Catalog.Map.TryGetValue((shape, CagedQuality.Major), out var set) && set.Contains(patternId))
            {
                return shape;
            }
        }

        // Fall back to any quality hit
        if (Catalog.Reverse.TryGetValue(patternId, out var entry)) return entry.shape;
        return CagedShape.Unknown;
    }

    /// <summary>
    /// Detailed identification with quality and confidence. Returns false if nothing plausible found.
    /// </summary>
    public static bool TryIdentifyDetailed(
        int[] frets,
        Tuning tuning,
        CagedMatcherOptions? options,
        out CagedShapeInfo info)
    {
        var inv = ChordInvariant.FromFrets(frets, tuning);
        return TryIdentifyDetailed(inv, options, out info);
    }

    /// <summary>
    /// Detailed identification for a prepared chord invariant (normalized already).
    /// </summary>
    public static bool TryIdentifyDetailed(
        ChordInvariant invariant,
        CagedMatcherOptions? options,
        out CagedShapeInfo info)
    {
        var opts = options ?? new CagedMatcherOptions();
        var id = invariant.PatternId;

        // 1) Exact fast-path via reverse catalog
        if (Catalog.Reverse.TryGetValue(id, out var hit))
        {
            var (shape, quality, label) = hit;
            var (barre, barreFret, barreSpan) = AnalyzeBarre(id);
            info = new CagedShapeInfo(
                shape,
                quality,
                invariant.BaseFret,
                GetAnyCanonical(shape, quality),
                id,
                label,
                Confidence: 1.0,
                Barre: barre,
                BarreFret: barreFret,
                BarreSpan: barreSpan,
                Difficulty: invariant.GetDifficulty());
            return true;
        }

        // 1b) Near-exact fast-path: allow only edge-string mute/unmute differences against any canonical
        // This captures open-shape variants like E major with muted high-E or low-E.
        var pattern = id.ToPattern();
        foreach (var kv in Catalog.Map)
        {
            foreach (var cand in kv.Value)
            {
                var candPattern = cand.ToPattern();
                if (EqualUpToEdgeMutes(pattern, candPattern))
                {
                    var meta = Catalog.Reverse[cand];
                    var (barre, barreFret, barreSpan) = AnalyzeBarre(cand);
                    info = new CagedShapeInfo(
                        meta.shape,
                        meta.quality,
                        invariant.BaseFret,
                        GetAnyCanonical(meta.shape, meta.quality),
                        cand,
                        meta.label + " (edge-muted)",
                        Confidence: 0.9,
                        Barre: barre,
                        BarreFret: barreFret,
                        BarreSpan: barreSpan,
                        Difficulty: invariant.GetDifficulty());
                    return true;
                }
            }
        }

        // 2) Relaxed structural match scan (inner-core matching with penalties)
        var best = (score: double.MaxValue, shape: CagedShape.Unknown, quality: CagedQuality.Major, canonical: default(PatternId), label: "");

        foreach (var kv in Catalog.Map)
        {
            foreach (var cand in kv.Value)
            {
                var s = StructuralDistance(pattern, cand.ToPattern(), opts);
                if (s < best.score)
                {
                    var meta = Catalog.Reverse[cand];
                    best = (s, meta.shape, meta.quality, cand, meta.label);
                }
            }
        }

        // Convert distance to confidence (simple mapping): 0 => 1.0, 1 => 0.8, 2 => 0.6, else <= 0.5
        if (best.shape != CagedShape.Unknown)
        {
            var conf = best.score <= 0.0 ? 1.0 : best.score <= 1.0 ? 0.8 : best.score <= 2.0 ? 0.6 : 0.5;
            var (barre, barreFret, barreSpan) = AnalyzeBarre(id);
            info = new CagedShapeInfo(
                best.shape,
                best.quality,
                invariant.BaseFret,
                best.canonical,
                id,
                best.label,
                conf,
                barre,
                barreFret,
                barreSpan,
                invariant.GetDifficulty());
            return true;
        }

        info = default;
        return false;
    }

    #endregion

    #region Public API - Canonicals and transpositions

    public static bool TryGetCanonicalPattern(CagedShape shape, CagedQuality quality, out PatternId patternId)
    {
        if (Catalog.Map.TryGetValue((shape, quality), out var set) && set.Count > 0)
        {
            patternId = set.First();
            return true;
        }

        patternId = default;
        return false;
    }

    /// <summary>
    /// Backward-compatible overload: returns major canonical pattern for the shape if present.
    /// </summary>
    public static bool TryGetCanonicalPattern(CagedShape shape, out PatternId patternId)
    {
        return TryGetCanonicalPattern(shape, CagedQuality.Major, out patternId);
    }

    public static IEnumerable<(int baseFret, int[] frets)> GetCagedTranspositions(CagedShape shape, CagedQuality quality, int maxFret)
    {
        if (!TryGetCanonicalPattern(shape, quality, out var id)) yield break;
        var norm = id.ToPattern();
        for (var k = 0; k <= maxFret; k++)
        {
            var arr = new int[norm.Length];
            for (var i = 0; i < norm.Length; i++)
            {
                var v = norm[i];
                arr[i] = v < 0 ? -1 : v + k;
            }
            yield return (k, arr);
        }
    }

    /// <summary>
    /// Backward-compatible overload for major quality.
    /// </summary>
    public static IEnumerable<(int baseFret, int[] frets)> GetCagedTranspositions(CagedShape shape, int maxFret)
    {
        return GetCagedTranspositions(shape, CagedQuality.Major, maxFret);
    }

    /// <summary>
    /// Suggest nearby positions (by base fret) for the same shape/quality family.
    /// </summary>
    public static IEnumerable<int[]> SuggestOtherPositions(ChordInvariant invariant, int radius = 2)
    {
        if (!TryIdentifyDetailed(invariant, null, out var info)) yield break;
        for (var delta = -radius; delta <= radius; delta++)
        {
            var baseFret = info.BaseFret + delta;
            if (baseFret < 0) continue;
            yield return invariant.ToAbsoluteFrets(baseFret);
        }
    }

    #endregion

    #region Internal helpers

    private static PatternId GetAnyCanonical(CagedShape shape, CagedQuality quality)
    {
        return TryGetCanonicalPattern(shape, quality, out var id) ? id : default;
    }

    private static (bool barre, int? barreFret, int barreSpan) AnalyzeBarre(PatternId id)
    {
        var p = id.ToPattern();
        // detect any fret value (>=0) repeated on >=2 adjacent strings
        int? bestFret = null; var bestSpan = 0;
        for (var i = 0; i < p.Length; i++)
        {
            if (p[i] < 0) continue;
            var fret = p[i];
            var span = 1;
            var j = i + 1;
            while (j < p.Length && p[j] == fret) { span++; j++; }
            if (span >= 2 && span > bestSpan)
            {
                bestSpan = span;
                bestFret = fret;
            }
        }

        return (bestSpan >= 2, bestFret, bestSpan);
    }

    private static double StructuralDistance(int[] a, int[] b, CagedMatcherOptions opts)
    {
        // Same length (6). Penalize mismatches ignoring -1 (muted). Allow flexible edges if enabled.
        double score = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            var ai = a[i];
            var bi = b[i];

            if (ai == bi) continue;

            // Edge flexibility: if outer strings differ only by -1 vs 0/same fret, apply small penalty
            var isEdge = i == 0 || i == a.Length - 1;
            if (opts.AllowEdgeStringFlex && isEdge)
            {
                if (ai < 0 && bi == 0 || bi < 0 && ai == 0)
                {
                    score += opts.EdgePenalty;
                    continue;
                }
            }

            // Muted vs fretted elsewhere -> treat as mismatch unless equal by duplication rule
            if (ai < 0 || bi < 0)
            {
                score += opts.MismatchPenalty;
                continue;
            }

            // Treat duplicates (same value appearing elsewhere) as zero penalty if rest matches; here we keep simple
            score += opts.MismatchPenalty;
        }

        // Ensure some minimum number of non-muted core strings
        var core = a.Count(v => v >= 0);
        if (core < opts.MinCoreStrings) score += 1.0; // penalize sparse grips
        return score;
    }

    /// <summary>
    /// Equality check that ignores differences limited to edge strings being muted/unmuted (0 vs -1) and exact elsewhere.
    /// </summary>
    private static bool EqualUpToEdgeMutes(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i]) continue;
            var isEdge = i == 0 || i == a.Length - 1;
            if (isEdge && ((a[i] < 0 && b[i] == 0) || (b[i] < 0 && a[i] == 0)))
            {
                continue;
            }
            return false;
        }
        return true;
    }

    #endregion
}
