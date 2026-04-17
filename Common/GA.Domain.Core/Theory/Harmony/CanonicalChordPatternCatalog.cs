namespace GA.Domain.Core.Theory.Harmony;

/// <summary>
///     Canonical catalog of chord interval patterns, hand-curated and content-enumerated.
///     Replaces the mode-enumerated factory-generated templates for PC-set recognition.
///     <para>
///     Each pattern is a root-relative interval set (mod 12). Patterns are ordered by priority —
///     lower priority wins when multiple patterns match. Recognition code should score candidates
///     by (missing + extra) intervals first, then priority, then root-commonness prior.
///     </para>
///     <para>
///     Legal: all names here are standard music-theory terminology, not coinage. No content is
///     reproduced from copyrighted sources. Interval assignments derive from first principles.
///     </para>
/// </summary>
public static class CanonicalChordPatternCatalog
{
    /// <summary>
    ///     All patterns in priority order (lowest first).
    ///     Priority conventions:
    ///         0-9   = core triads and 7ths (most common, preferred when matching)
    ///         10-29 = 6ths, extended chords (9, 11, 13)
    ///         30-49 = altered dominants
    ///         50-69 = add chords, sus variants
    ///         70-89 = quartal, quintal, clusters
    ///         90-99 = edge cases (special chords, polychord fragments)
    /// </summary>
    public static readonly IReadOnlyList<ChordIntervalPattern> All = BuildCatalog();

    /// <summary>
    ///     Patterns grouped by cardinality for efficient lookup.
    ///     Cardinality 2 = dyads (handled separately in recognizer).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, IReadOnlyList<ChordIntervalPattern>> ByCardinality
        = All.GroupBy(p => p.Cardinality)
             .ToDictionary(g => g.Key, g => (IReadOnlyList<ChordIntervalPattern>)g.ToArray());

    private static IReadOnlyList<ChordIntervalPattern> BuildCatalog()
    {
        // NOTE on interval conventions:
        //   - Intervals are mod 12, sorted ascending, root=0 always present.
        //   - Alterations listed in "short form": "b5", "#5", "b9", "#9", "#11", "b13".
        //   - Sus chords have no 3rd by definition (2nd or 4th replaces).
        //   - Add chords have no 7th by definition (9, 11, or 6 added to triad).

        return
        [
            // ===== TRIADS (priority 0-9) =====
            new("major-triad",         "major",      "triad",  [],          [0, 4, 7],    0),
            new("minor-triad",         "minor",      "triad",  [],          [0, 3, 7],    1),
            new("diminished-triad",    "diminished", "triad",  [],          [0, 3, 6],    2),
            new("augmented-triad",     "augmented",  "triad",  [],          [0, 4, 8],    3),
            new("sus2",                "suspended",  "triad",  ["sus2"],    [0, 2, 7],    8),
            new("sus4",                "suspended",  "triad",  ["sus4"],    [0, 5, 7],    8),

            // ===== SIXTHS (priority 10-14) =====
            new("major-6",             "major",      "6th",    [],          [0, 4, 7, 9],   10),
            new("minor-6",             "minor",      "6th",    [],          [0, 3, 7, 9],   11),
            new("major-6-add-9",       "major",      "6th",    ["add9"],    [0, 2, 4, 7, 9], 12),
            new("minor-6-add-9",       "minor",      "6th",    ["add9"],    [0, 2, 3, 7, 9], 13),

            // ===== SEVENTH CHORDS (priority 5-9) =====
            new("dominant-7",          "dominant",   "7th",    [],          [0, 4, 7, 10],  5),
            new("major-7",             "major",      "7th",    [],          [0, 4, 7, 11],  5),
            new("minor-7",             "minor",      "7th",    [],          [0, 3, 7, 10],  5),
            new("minor-major-7",       "minor",      "7th",    ["maj7"],    [0, 3, 7, 11],  6),
            new("half-diminished-7",   "diminished", "7th",    ["b5"],      [0, 3, 6, 10],  6),
            new("diminished-7",        "diminished", "7th",    [],          [0, 3, 6, 9],   7),
            new("augmented-major-7",   "augmented",  "7th",    ["maj7"],    [0, 4, 8, 11],  8),
            new("augmented-7",         "augmented",  "7th",    [],          [0, 4, 8, 10],  8),
            new("7-sus4",              "suspended",  "7th",    ["sus4"],    [0, 5, 7, 10],  9),
            new("7-sus2",              "suspended",  "7th",    ["sus2"],    [0, 2, 7, 10],  9),

            // ===== NINTH CHORDS (priority 15-19) =====
            new("dominant-9",          "dominant",   "9th",    [],          [0, 2, 4, 7, 10],   15),
            new("major-9",             "major",      "9th",    [],          [0, 2, 4, 7, 11],   15),
            new("minor-9",             "minor",      "9th",    [],          [0, 2, 3, 7, 10],   15),
            new("minor-major-9",       "minor",      "9th",    ["maj9"],    [0, 2, 3, 7, 11],   16),
            new("half-diminished-9",   "diminished", "9th",    ["b5"],      [0, 2, 3, 6, 10],   17),
            new("9-sus4",              "suspended",  "9th",    ["sus4"],    [0, 2, 5, 7, 10],   18),

            // ===== ELEVENTH CHORDS (priority 20-24) =====
            // Conventional voicings often omit the 3rd on major 11 to avoid b9 clash with 11
            new("dominant-11",         "dominant",   "11th",   [],          [0, 2, 5, 7, 10],   20),
            new("major-11",            "major",      "11th",   [],          [0, 2, 5, 7, 11],   20),
            new("minor-11",            "minor",      "11th",   [],          [0, 2, 3, 5, 7, 10], 20),
            new("dominant-11-full",    "dominant",   "11th",   [],          [0, 2, 4, 5, 7, 10], 22),

            // ===== THIRTEENTH CHORDS (priority 25-29) =====
            new("dominant-13",         "dominant",   "13th",   [],          [0, 2, 4, 7, 9, 10],  25),
            new("major-13",            "major",      "13th",   [],          [0, 2, 4, 7, 9, 11],  25),
            new("minor-13",            "minor",      "13th",   [],          [0, 2, 3, 7, 9, 10],  26),
            new("13-sus4",             "suspended",  "13th",   ["sus4"],    [0, 2, 5, 7, 9, 10],  28),

            // ===== ALTERED DOMINANTS (priority 30-49) =====
            new("dominant-7-b5",       "altered-dominant",  "7th", ["b5"],            [0, 4, 6, 10],           30),
            new("dominant-7-sharp-5",  "altered-dominant",  "7th", ["#5"],            [0, 4, 8, 10],           30),
            new("dominant-7-b9",       "altered-dominant",  "7th", ["b9"],            [0, 1, 4, 7, 10],        32),
            new("dominant-7-sharp-9",  "altered-dominant",  "7th", ["#9"],            [0, 3, 4, 7, 10],        32),
            new("dominant-7-sharp-11", "altered-dominant",  "7th", ["#11"],           [0, 4, 6, 7, 10],        34),
            new("dominant-7-b13",      "altered-dominant",  "7th", ["b13"],           [0, 4, 7, 8, 10],        34),
            new("dominant-7-b9-sharp-9", "altered-dominant","7th", ["b9", "#9"],      [0, 1, 3, 4, 7, 10],     36),
            new("dominant-7-b9-sharp-11", "altered-dominant","7th",["b9", "#11"],     [0, 1, 4, 6, 7, 10],     38),
            new("dominant-9-sharp-11", "altered-dominant",  "9th", ["#11"],           [0, 2, 4, 6, 7, 10],     40),
            new("dominant-13-b9",      "altered-dominant",  "13th",["b9"],            [0, 1, 4, 7, 9, 10],     42),
            new("dominant-7-alt",      "altered-dominant",  "7th", ["b9", "#9", "b13"],[0, 1, 3, 4, 8, 10],    44),

            // ===== ADD CHORDS (priority 50-59) =====
            new("add-9",               "major", "add", ["add9"],  [0, 2, 4, 7],   50),
            new("minor-add-9",         "minor", "add", ["add9"],  [0, 2, 3, 7],   51),
            new("add-11",              "major", "add", ["add11"], [0, 4, 5, 7],   52),
            new("minor-add-11",        "minor", "add", ["add11"], [0, 3, 5, 7],   53),
            new("6-9",                 "major", "add", ["add9"],  [0, 2, 4, 7, 9], 54),
            new("minor-6-9",           "minor", "add", ["add9"],  [0, 2, 3, 7, 9], 55),

            // ===== QUARTAL / QUINTAL / QUARTAL-SUSPENDED (priority 70-79) =====
            // Quartal chords are stacked 4ths. "Quartal-3" = 3-note stack: root, P4, m7.
            new("quartal-3",           "quartal", "triad", [],    [0, 5, 10],       70),
            new("quartal-4",           "quartal", "7th",   [],    [0, 3, 5, 10],    71),
            new("quartal-5",           "quartal", "9th",   [],    [0, 3, 5, 8, 10], 72),

            // ===== SYMMETRIC DIVISIONS (priority 80-89) =====
            // Diminished whole-step / half-whole clusters handled by Forte fallback normally,
            // but common named versions deserve their own entries.
            new("whole-tone-hexachord", "set-class", null, [], [0, 2, 4, 6, 8, 10], 80),
            new("augmented-hexachord",  "set-class", null, [], [0, 3, 4, 7, 8, 11], 81),
            new("octatonic-half-whole", "set-class", null, [], [0, 1, 3, 4, 6, 7, 9, 10], 82),
            new("octatonic-whole-half", "set-class", null, [], [0, 2, 3, 5, 6, 8, 9, 11], 82),

            // ===== POWER & SHELL VOICINGS (priority 90-99) =====
            // Two-note "chords" that are idiomatic enough to deserve their own name
            // (matched only when cardinality=2 and pattern exactly fits).
            new("power-chord",         "power", "dyad", [],    [0, 7],  90),
            new("shell-7",             "shell", "7th", [],    [0, 4, 10], 91),  // root, 3, b7 (no 5)
            new("shell-major-7",       "shell", "7th", [],    [0, 4, 11], 91),
            new("shell-minor-7",       "shell", "7th", [],    [0, 3, 10], 91),
        ];
    }

    /// <summary>
    ///     Finds all patterns whose interval set is consistent with the given intervals-from-root.
    ///     Returns matches ordered by (distance ASC, priority ASC).
    /// </summary>
    public static IEnumerable<MatchResult> FindMatches(
        IReadOnlyCollection<int> intervalsFromRoot,
        int maxMissing = 1,
        int maxExtra = 1)
    {
        ArgumentNullException.ThrowIfNull(intervalsFromRoot);

        var results = new List<MatchResult>();
        foreach (var pattern in All)
        {
            var match = pattern.TryMatch(intervalsFromRoot, maxMissing, maxExtra);
            if (match.HasValue)
                results.Add(match.Value);
        }

        return results
            .OrderBy(m => m.Distance)
            .ThenBy(m => m.Pattern.Priority);
    }

    /// <summary>
    ///     Tries to find the best exact match (no missing, no extra intervals).
    ///     Used when we want to accept only patterns that fully describe the voicing.
    /// </summary>
    public static ChordIntervalPattern? TryFindExact(IReadOnlyCollection<int> intervalsFromRoot)
    {
        ArgumentNullException.ThrowIfNull(intervalsFromRoot);

        foreach (var pattern in All.OrderBy(p => p.Priority))
        {
            var match = pattern.TryMatch(intervalsFromRoot, maxMissing: 0, maxExtra: 0);
            if (match is { IsExact: true })
                return pattern;
        }

        return null;
    }
}
