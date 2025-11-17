namespace GA.Business.Core.Fretboard.Invariants;

using System.Collections.Immutable;
using System.Numerics;

[PublicAPI]
public static class ChordPatternEquivalenceFactory
{
    /// <summary>
    /// Creates a chord pattern translation equivalence collection for a standard 6-string guitar.
    /// For MVP, we seed with a single prime-form self-equivalence; equivalence checks rely on PatternId normalization.
    /// </summary>
    public static ChordPatternEquivalenceCollection CreateGuitarChordEquivalences()
    {
        // Seed with a canonical E-major shape in prime form as a reference equivalence
        var eMajorPrime = PatternId.FromPattern(new[] { 0, 2, 2, 1, 0, 0 });
        var seed = new ChordPatternEquivalence(
            eMajorPrime,
            eMajorPrime,
            0,
            BigInteger.Zero,
            BigInteger.Zero);

        return new ChordPatternEquivalenceCollection([seed]);
    }

    /// <summary>
    /// Analyze a set of chord invariants to compute compression and grouping stats by PatternId.
    /// </summary>
    public static ChordPatternAnalysisResult AnalyzeChordDatabase(IEnumerable<ChordInvariant> chords)
    {
        ArgumentNullException.ThrowIfNull(chords);
        var list = chords as IReadOnlyCollection<ChordInvariant> ?? [.. chords];
        var total = list.Count;

        var groups = list
            .GroupBy(c => c.PatternId)
            .ToDictionary(g => g.Key, g => g.ToImmutableList())
            .ToImmutableDictionary();

        var unique = groups.Count;
        var compression = total == 0 ? 0.0 : (double)unique / total;
        var avgTranspositions = groups.Count == 0 ? 0.0 : groups.Average(g => (double)g.Value.Count);

        return new ChordPatternAnalysisResult(
            total,
            unique,
            compression,
            avgTranspositions,
            groups);
    }
}
