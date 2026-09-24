namespace GA.Domain.Core.Theory.Harmony;

using System.Numerics;

/// <summary>
///     A canonical chord pattern expressed as intervals-from-root (mod 12, sorted ascending).
///     Part of the content-enumerated chord recognition architecture (replaces the
///     mode-enumerated <see cref="ChordTemplate" /> for PC-set recognition purposes).
/// </summary>
/// <param name="Name">Stable kebab-case identifier, e.g. "major-7", "dominant-7-sharp-9".</param>
/// <param name="Quality">Canonical quality family: "major", "minor", "diminished", "augmented",
///     "dominant", "altered-dominant", "suspended", "dyad", "quartal", "set-class".</param>
/// <param name="Extension">Extension label if any: "triad", "6th", "7th", "9th", "11th", "13th", null.</param>
/// <param name="Alterations">Non-canonical tones relative to the quality+extension,
///     e.g. ["#9"] for 7#9, ["b5"] for 7b5, empty for plain.</param>
/// <param name="Intervals">Interval pattern from root (mod 12), sorted ascending, unique.
///     Must be a valid pitch-class set.</param>
/// <param name="Priority">Lower = preferred when multiple patterns match.
///     Baseline triads/7ths: 0-10. Extended chords: 20-40. Altered dominants: 50-80.
///     Quartal/exotic: 90+. Fallbacks (dyad, Forte-only): 999.</param>
public readonly record struct ChordIntervalPattern(
    string Name,
    string Quality,
    string? Extension,
    string[] Alterations,
    int[] Intervals,
    int Priority)
{
    /// <summary>Number of pitch classes in the pattern (= cardinality).</summary>
    public int Cardinality => Intervals.Length;

    /// <summary>
    ///     Attempts to match the pattern against a set of intervals-from-root.
    ///     Returns null if the match is too poor (missing + extra > max tolerance).
    /// </summary>
    /// <param name="intervalsFromRoot">Intervals-from-root to match, mod 12, distinct.</param>
    /// <param name="maxMissing">Max pattern intervals absent from the voicing (default 1, e.g. no-5 voicings).</param>
    /// <param name="maxExtra">Max voicing intervals not in the pattern (default 0, strict match).</param>
    public MatchResult? TryMatch(
        IReadOnlyCollection<int> intervalsFromRoot,
        int maxMissing = 1,
        int maxExtra = 0)
    {
        ArgumentNullException.ThrowIfNull(intervalsFromRoot);

        // Intervals are pitch classes relative to the root, so each side is a 12-bit set and
        // missing, extra and overlap are popcounts of AND/AND-NOT. This runs once per pattern per
        // candidate root for every voicing analysed, and the set version allocated a HashSet here
        // plus one inside each of Except, Except and Intersect.
        if (TryMask(Intervals, out var patternMask) && TryMask(intervalsFromRoot, out var voicingMask))
        {
            var missingBits = BitOperations.PopCount((uint)(patternMask & ~voicingMask));
            var extraBits = BitOperations.PopCount((uint)(voicingMask & ~patternMask));

            if (missingBits > maxMissing || extraBits > maxExtra)
                return null;

            return new MatchResult(this, Overlap: BitOperations.PopCount((uint)(patternMask & voicingMask)), Missing: missingBits, Extra: extraBits);
        }

        // Values outside 0-11 do not fit the mask: keep the set semantics for them
        var patternSet = new HashSet<int>(Intervals);
        var voicingSet = intervalsFromRoot is HashSet<int> hs ? hs : [.. intervalsFromRoot];

        var missing = patternSet.Except(voicingSet).Count();
        var extra = voicingSet.Except(patternSet).Count();

        if (missing > maxMissing || extra > maxExtra)
            return null;

        return new MatchResult(this, Overlap: patternSet.Intersect(voicingSet).Count(), Missing: missing, Extra: extra);
    }

    /// <summary>
    ///     Folds intervals into a 12-bit mask (bit i set when interval i is present).
    ///     Returns false when a value lies outside 0-11 and cannot be represented.
    /// </summary>
    private static bool TryMask(IEnumerable<int> intervals, out int mask)
    {
        mask = 0;
        switch (intervals)
        {
            case int[] array:
                foreach (var interval in array)
                {
                    if ((uint)interval > 11) return false;
                    mask |= 1 << interval;
                }
                return true;
            case HashSet<int> set:
                foreach (var interval in set)
                {
                    if ((uint)interval > 11) return false;
                    mask |= 1 << interval;
                }
                return true;
            default:
                foreach (var interval in intervals)
                {
                    if ((uint)interval > 11) return false;
                    mask |= 1 << interval;
                }
                return true;
        }
    }
}

/// <summary>
///     Result of matching a <see cref="ChordIntervalPattern" /> against a voicing.
/// </summary>
/// <param name="Pattern">The matched pattern.</param>
/// <param name="Overlap">Count of intervals present in both pattern and voicing.</param>
/// <param name="Missing">Count of pattern intervals absent from the voicing (e.g. dropped 5th).</param>
/// <param name="Extra">Count of voicing intervals not in the pattern (e.g. added tension).</param>
public readonly record struct MatchResult(
    ChordIntervalPattern Pattern,
    int Overlap,
    int Missing,
    int Extra)
{
    /// <summary>True when the pattern and voicing share every interval.</summary>
    public bool IsExact => Missing == 0 && Extra == 0;

    /// <summary>Total edit distance between pattern and voicing (lower = better fit).</summary>
    public int Distance => Missing + Extra;
}
