namespace GA.Business.Core.Fretboard.Invariants;

using System.Collections.Immutable;

/// <summary>
/// Collection and helpers for chord pattern translation equivalences.
/// </summary>
[PublicAPI]
public sealed class ChordPatternEquivalenceCollection
{
    private readonly ImmutableList<ChordPatternEquivalence> _equivalences;

    public ChordPatternEquivalenceCollection(IEnumerable<ChordPatternEquivalence> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _equivalences = [.. items];
    }

    public IReadOnlyCollection<ChordPatternEquivalence> Equivalences => _equivalences;

    /// <summary>
    /// Two patterns are equivalent if their normalized arrays are identical.
    /// </summary>
    public bool AreEquivalent(PatternId a, PatternId b)
    {
        var ap = a.ToPattern();
        var bp = b.ToPattern();
        if (ap.Length != bp.Length) return false;
        for (var i = 0; i < ap.Length; i++)
        {
            if (ap[i] != bp[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Returns the prime form (normalized) of a pattern. Always present for well-formed patterns.
    /// </summary>
    public PatternId? GetPrimeForm(PatternId pattern)
    {
        // PatternId is already normalized, so just return it.
        return pattern;
    }

    /// <summary>
    /// Returns all known equivalent patterns. With no precomputed catalog, returns the input itself.
    /// </summary>
    public IEnumerable<PatternId> FindEquivalentPatterns(PatternId pattern)
    {
        yield return pattern;
    }
}
