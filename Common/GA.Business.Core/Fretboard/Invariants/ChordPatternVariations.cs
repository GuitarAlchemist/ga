namespace GA.Business.Core.Fretboard.Invariants;

using System.Numerics;
using GA.Core.Combinatorics;
using Fretboard;
using Primitives;

/// <summary>
/// Helper to create and work with chord pattern variations built from relative frets.
/// </summary>
[PublicAPI]
public sealed class ChordPatternVariations
{
    /// <summary>
    /// Creates a variation from an absolute fret array. Negative values are clamped to 0.
    /// </summary>
    public Variation<RelativeFret> FromFretArray(int[] frets)
    {
        ArgumentNullException.ThrowIfNull(frets);
        if (frets.Length != 6)
        {
            throw new ArgumentException("Expected 6 fret values", nameof(frets));
        }

        var rel = new RelativeFret[frets.Length];
        for (var i = 0; i < frets.Length; i++)
        {
            var v = frets[i];
            if (v < 0) v = 0; // clamp negatives for RelativeFret domain
            rel[i] = RelativeFret.FromValue(v);
        }

        // Use a simple index of zero here; callers typically don't need it.
        return new Variation<RelativeFret>(BigInteger.Zero, rel);
    }
}

public static class ChordPatternVariationExtensions
{
    /// <summary>
    /// Convert a variation of relative frets to a normalized PatternId.
    /// </summary>
    public static PatternId ToPatternId(this Variation<RelativeFret> variation)
    {
        var arr = new int[variation.Count];
        for (var i = 0; i < variation.Count; i++) arr[i] = variation[i].Value;
        return PatternId.FromPattern(arr);
    }

    /// <summary>
    /// Convert a variation of relative frets to a ChordInvariant assuming default tuning.
    /// </summary>
    public static ChordInvariant ToChordInvariant(this Variation<RelativeFret> variation)
    {
        var arr = new int[variation.Count];
        for (var i = 0; i < variation.Count; i++) arr[i] = variation[i].Value;
        return ChordInvariant.FromFrets(arr, Tuning.Default);
    }
}
