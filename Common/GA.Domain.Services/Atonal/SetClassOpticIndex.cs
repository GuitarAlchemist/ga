namespace GA.Domain.Services.Atonal;

using System.Collections.Concurrent;
using JetBrains.Annotations;
using GA.Domain.Core.Theory.Atonal;


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
    private static readonly ConcurrentDictionary<int, double[]> _vectorCache = new();

    private static double Mod12(double x)
    {
        var r = x % 12.0;
        return r < 0 ? r + 12.0 : r;
    }

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
    public static double Distance(SetClass a, SetClass b, VoiceLeadingOptions? options = null)
    {
        options ??= VoiceLeadingOptions.Default;

        var va = ToVectorCached(a);
        var vb = ToVectorCached(b);

        if (va.Length == vb.Length)
        {
            if (options.PermutationEquivalence)
            {
                // With permutation equivalence, we can use the nearest-neighbor sum approach (approximation)
                // or strict best-permutation (expensive). The doubling logic provides a reasonable
                // Hausdorff-like distance that effectively ignores order.
                return DistanceWithDoublings(va, vb, options);
            }
            
            // Strict element-wise comparison (respecting order)
            return DistanceElementWise(va, vb, options);
        }

        // For unequal cardinalities, allow doublings in the smaller set
        return DistanceWithDoublings(va, vb, options);
    }

    private static double DistanceElementWise(double[] a, double[] b, VoiceLeadingOptions options)
    {
        var steps = options.TranspositionEquivalence ? 12 : 1;
        var inversions = options.InversionEquivalence ? 2 : 1;
        var best = double.MaxValue;

        for (var t = 0; t < steps; t++)
        {
            for (var inv = 0; inv < inversions; inv++)
            {
                // Apply transformation to b
                var transformedB = TransformVector(b, t, inv == 1, options.OctaveEquivalence);
                
                var currentCost = 0.0;
                for (var i = 0; i < a.Length; i++)
                {
                    currentCost += MovementCost(a[i], transformedB[i], options.OctaveEquivalence);
                }

                if (currentCost < best)
                {
                    best = currentCost;
                }
            }
        }
        return best;
    }

    private static double[] TransformVector(double[] v, int transposition, bool invert, bool octaveEquivalence)
    {
        var result = new double[v.Length];
        for (var i = 0; i < v.Length; i++)
        {
            var val = v[i];
            if (invert) val = -val;
            val += transposition;
            result[i] = octaveEquivalence ? Mod12(val) : val;
        }
        return result;
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

        // Distance handles cardinality differences internally; compute per pair.

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

    private static double[] ToVector(SetClass sc) =>
        // Use the prime form’s pitch classes, mapped to [0,12)
        // The particular ordering is not critical when permutationEquivalence is enabled.
        [.. sc.PrimeForm.Select(pc => (double)((pc.Value % 12 + 12) % 12))];

    private static double[] ToVectorCached(SetClass sc)
    {
        var id = sc.PrimeForm.Id.Value;
        return _vectorCache.GetOrAdd(id, _ => ToVector(sc));
    }

    private static double DistanceWithDoublings(double[] a, double[] b, VoiceLeadingOptions options)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return 0.0;
        }

        var larger = a.Length >= b.Length ? a : b;
        var smaller = a.Length >= b.Length ? b : a;

        if (smaller.Length == 0)
        {
            smaller = [0.0];
        }

        var steps = options.TranspositionEquivalence ? 12 : 1;
        var best = double.MaxValue;

        for (var k = 0; k < steps; k++)
        {
            var shifted = k == 0
                ? larger
                : ShiftBy(larger, k, options.OctaveEquivalence);

            var cost = SumNearestDistances(shifted, smaller, options.OctaveEquivalence);
            if (cost < best)
            {
                best = cost;
            }
        }

        return best == double.MaxValue ? 0.0 : best;
    }

    private static double SumNearestDistances(double[] larger, double[] smaller, bool octaveEquivalence)
    {
        var total = 0.0;

        foreach (var v in larger)
        {
            var best = double.MaxValue;
            foreach (var s in smaller)
            {
                var cost = MovementCost(v, s, octaveEquivalence);
                if (cost < best)
                {
                    best = cost;
                }
            }

            if (best != double.MaxValue)
            {
                total += best;
            }
        }

        return total;
    }

    private static double MovementCost(double from, double to, bool octaveEquivalence)
    {
        var movement = to - from;

        if (octaveEquivalence)
        {
            movement = (movement % 12 + 12) % 12;
            if (movement > 6)
            {
                movement -= 12;
            }
        }

        return Math.Abs(movement);
    }

    private static double[] ShiftBy(double[] v, int semitones, bool octaveEquivalence)
    {
        var result = new double[v.Length];
        for (var i = 0; i < v.Length; i++)
        {
            var shifted = v[i] + semitones;
            result[i] = octaveEquivalence ? Mod12(shifted) : shifted;
        }

        return result;
    }
}
