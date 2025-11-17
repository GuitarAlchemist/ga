namespace GA.Business.Core.Atonal;

using Fretboard.Shapes.Geometry;

/// <summary>
///     OPTIC-inspired geometric utilities for <see cref="SetClass"/>.
/// </summary>
/// <remarks>
///     Provides distances and nearest-neighbor queries among set classes
///     using Tymoczko/Callender–Quinn–Tymoczko-style quotient spaces.
///
///     References and links:
///     - Tymoczko, D. (2011). A Geometry of Music. Oxford University Press. ISBN 978-0195336672.
///     - Callender, C., Quinn, I., & Tymoczko, D. (2008). Generalized Voice-Leading Spaces. Music Theory Online 14(3).
///       Open access: http://mtosmt.org/issues/mto.08.14.3/mto.08.14.3.callender_quinn_tymoczko.html
///
///     Acknowledgements: Implementation follows the OPTIC framework (Octave, Permutation, Transposition,
///     Inversion, Cardinality) articulated by Callender–Quinn–Tymoczko.
/// </remarks>
[PublicAPI]
public static class SetClassOpticIndex
{
    // Simple static cache for mapping SetClass prime-form IDs to their double[] vectors.
    // Safe for reuse across calls; vectors are small (<=12) and immutable by convention.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, double[]> _vectorCache = new();

    private static byte ToToggleMask(VoiceLeadingOptions o)
    {
        byte m = 0;
        if (o.OctaveEquivalence) m |= 1 << 0;
        if (o.PermutationEquivalence) m |= 1 << 1;
        if (o.TranspositionEquivalence) m |= 1 << 2;
        if (o.InversionEquivalence) m |= 1 << 3;
        return m;
    }

    /// <summary>
    ///     Compute OPTIC-style distance between two set classes.
    /// </summary>
    /// <param name="a">First set class</param>
    /// <param name="b">Second set class</param>
    /// <param name="options">Space options; if null, defaults to OPTI with octave/permutation on</param>
    /// <returns>Minimal voice-leading distance under the chosen equivalences</returns>
    /// <example>
    ///     <code>
    ///     var options = new VoiceLeadingOptions
    ///     {
    ///         OctaveEquivalence = true,
    ///         PermutationEquivalence = true,
    ///         TranspositionEquivalence = true,
    ///         InversionEquivalence = false // OPT by default
    ///     };
    ///     var d = SetClassOpticIndex.Distance(scA, scB, options);
    ///     </code>
    /// </example>
    public static double Distance(SetClass a, SetClass b, VoiceLeadingOptions? options = null)
    {
        options ??= VoiceLeadingOptions.Default;

        // If cardinalities differ, we can embed into max(n_a, n_b) by padding with repeated tones.
        // Simple approach: if unequal, fall back to circular-EMD-like approximation via greedy pairing
        // after duplicating elements of the smaller set to match sizes (musically: allow doublings).
        var n = Math.Max(a.Cardinality.Value, b.Cardinality.Value);

        var va = ExpandToCardinality(ToVectorCached(a), n);
        var vb = ExpandToCardinality(ToVectorCached(b), n);

        var space = new VoiceLeadingSpace(
            voices: n,
            octaveEquivalence: options.OctaveEquivalence,
            permutationEquivalence: options.PermutationEquivalence,
            transpositionEquivalence: options.TranspositionEquivalence,
            inversionEquivalence: options.InversionEquivalence);

        return space.Distance(va, vb);
    }

    /// <summary>
    ///     Get k nearest set classes to the provided set class under the OPTIC distance.
    /// </summary>
    public static IReadOnlyList<(SetClass setClass, double distance)> GetNearestByOptic(
        SetClass source,
        int k = 10,
        VoiceLeadingOptions? options = null)
    {
        options ??= VoiceLeadingOptions.Default;
        var list = new List<(SetClass setClass, double distance)>();

        // Per-call memoization for distances to avoid duplicate work when UI rebinds.
        var memo = new Dictionary<(int a, int b, byte mask), double>();
        var mask = ToToggleMask(options);

        // Precompute expanded vector for source once for performance.
        // Note: expansion size depends on target; for simplicity, we compute per pair below.

        foreach (var sc in SetClass.Items)
        {
            if (ReferenceEquals(sc, source))
            {
                continue;
            }
            var key = (a: source.PrimeForm.Id.Value, b: sc.PrimeForm.Id.Value, mask);
            if (!memo.TryGetValue(key, out var d))
            {
                d = Distance(source, sc, options);
                memo[key] = d;
            }
            list.Add((sc, d));
        }

        return [.. list
            .OrderBy(t => t.distance)
            .Take(k)];
    }

    private static double[] ToVector(SetClass sc)
    {
        // Use the prime form’s pitch classes, mapped to [0,12)
        // The particular ordering is not critical when permutationEquivalence is enabled.
        return [.. sc.PrimeForm.Select(pc => (double)((pc.Value % 12 + 12) % 12))];
    }

    private static double[] ToVectorCached(SetClass sc)
    {
        var id = sc.PrimeForm.Id.Value;
        return _vectorCache.GetOrAdd(id, _ => ToVector(sc));
    }

    private static double[] ExpandToCardinality(double[] v, int target)
    {
        if (v.Length == target)
        {
            return v;
        }
        if (v.Length == 0)
        {
            return new double[target];
        }

        // Evenly duplicate elements to reach the target size (naive but effective for distance calc)
        var result = new double[target];
        for (var i = 0; i < target; i++)
        {
            result[i] = v[i % v.Length];
        }
        return result;
    }
}

/// <summary>
///     Options for OPTIC-style voice-leading computations.
/// </summary>
[PublicAPI]
public sealed class VoiceLeadingOptions
{
    public int? Voices { get; init; }
    public bool OctaveEquivalence { get; init; } = true;
    public bool PermutationEquivalence { get; init; } = true;
    public bool TranspositionEquivalence { get; init; } = true; // default to OPT rather than just OP
    public bool InversionEquivalence { get; init; } = false; // configurable
    public double[]? VoiceWeights { get; init; }

    public static VoiceLeadingOptions Default { get; } = new();
}
