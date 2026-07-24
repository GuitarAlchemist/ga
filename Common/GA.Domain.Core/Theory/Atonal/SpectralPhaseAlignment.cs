namespace GA.Domain.Core.Theory.Atonal;

using System.Numerics;

/// <summary>
///     Transposition/inversion-aligned similarity via DFT phase correlation on Z₁₂
///     (the shift theorem + Lewin's lemma + phase correlation; Amiot,
///     <em>Music Through Fourier Space</em>, 2016; Quinn 2006–07; Lewin 1959).
///     <para>
///     Fixes the two blind spots of the interval-class vector, which — being the
///     autocorrelation of the chroma — determines exactly the DFT magnitudes and
///     nothing more (<see cref="SetClass.GetMagnitudeSpectrum" />). Magnitude/ICV
///     similarity therefore scores as identical (a) the 23 homometric
///     <em>Z-related</em> set-class pairs and (b) a major triad vs its minor
///     (chirality). The <em>phase</em> spectrum, transformed predictably under
///     transposition, separates both — in closed form, at query time.
///     </para>
///     Spec + numerical oracle: <c>docs/research/2026-07-04-optick-spectral-phase-alignment.md</c>.
/// </summary>
public static class SpectralPhaseAlignment
{
    private const int PitchClassSpaceSize = 12;
    private const double Epsilon = 1e-9;

    /// <summary>Outcome of a phase-aligned comparison of two pitch-class sets.</summary>
    /// <param name="Similarity">
    ///     <c>S ∈ [-1, 1]</c>. <c>S = 1</c> iff the sets are transposition-aligned
    ///     (one is a transposition of the other, or its inversion on the TnI path).
    /// </param>
    /// <param name="AligningTranspositions">
    ///     Every <c>t</c> achieving the maximum (<c>t*</c>) — the transposition(s)
    ///     that align <c>b</c> onto <c>a</c>. Multiple values occur for sets fixed by
    ///     some transposition (augmented triad, diminished 7th, whole-tone …); empty
    ///     only in the disjoint-support degenerate case.
    /// </param>
    /// <param name="Inverted">True iff the winning alignment used <c>b</c>'s inversion (TnI path).</param>
    public readonly record struct Alignment(
        double Similarity,
        IReadOnlyList<int> AligningTranspositions,
        bool Inverted);

    /// <summary>
    ///     Theorem 3 — transposition-aligned similarity <c>S(a, b)</c>, chirality
    ///     preserving (major ≠ minor). Maximises magnitude-weighted phase agreement
    ///     over the 12 transpositions.
    /// </summary>
    /// <param name="weights">Optional per-coefficient weights for k = 1..6 (Quinn quality
    ///     semantics); uniform when null.</param>
    public static Alignment Similarity(PitchClassSet a, PitchClassSet b, double[]? weights = null)
        => Align(Coefficients(a), Coefficients(b), weights, inverted: false);

    /// <summary>
    ///     Theorem 4 — TnI-aligned similarity, <c>max(S(a, b), S(a, inv b))</c>. Inversion
    ///     conjugates the DFT, so set-class (TnI) matching is available on demand while
    ///     plain <see cref="Similarity" /> keeps chirality. Separates all 23 Z-pairs.
    /// </summary>
    public static Alignment SimilarityTnI(PitchClassSet a, PitchClassSet b, double[]? weights = null)
    {
        var fa = Coefficients(a);
        var fb = Coefficients(b);

        var direct = Align(fa, fb, weights, inverted: false);

        // Inversion I: n ↦ −n conjugates every coefficient (X_k ↦ conj X_k).
        var fbInv = new Complex[fb.Length];
        for (var k = 1; k <= 6; k++)
        {
            fbInv[k] = Complex.Conjugate(fb[k]);
        }

        var inverted = Align(fa, fbInv, weights, inverted: true);
        return inverted.Similarity > direct.Similarity ? inverted : direct;
    }

    /// <summary>Fourier coefficients <c>X_k, k = 0..6</c> of the set's ACTUAL chroma
    /// (not the prime form — phases carry the transposition, which is the point).</summary>
    private static Complex[] Coefficients(PitchClassSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var chroma = set.ToBinaryVector(PitchClassSpaceSize);
        var x = new Complex[7];
        for (var k = 0; k <= 6; k++)
        {
            var sum = Complex.Zero;
            for (var n = 0; n < PitchClassSpaceSize; n++)
            {
                if (chroma[n] == 0.0)
                {
                    continue;
                }

                var angle = -2.0 * Math.PI * k * n / PitchClassSpaceSize;
                sum += Complex.Exp(new Complex(0.0, angle));
            }

            x[k] = sum;
        }

        return x;
    }

    private static Alignment Align(Complex[] fa, Complex[] fb, double[]? weights, bool inverted)
    {
        // denom = Σ_{k=1..6} w_k |X_k(a)| |X_k(b)|
        var denom = 0.0;
        for (var k = 1; k <= 6; k++)
        {
            denom += Weight(weights, k) * fa[k].Magnitude * fb[k].Magnitude;
        }

        if (denom < Epsilon)
        {
            // Zero-denominator convention (Codex review, ga#513):
            //  - a or b null on k=1..6 (empty set / chromatic aggregate): transpositionally
            //    trivial, fixed by every T_t → S := 1 with every t aligning.
            //  - otherwise both non-trivial with disjoint periodicity support → S := 0, no t*.
            return AllNull(fa, weights) || AllNull(fb, weights)
                ? new Alignment(1.0, AllTranspositions(), inverted)
                : new Alignment(0.0, [], inverted);
        }

        // numerator(t) = Σ_k w_k · Re[ X_k(a) · conj(X_k(b)) · e^{i·2πkt/12} ]
        //             = Σ_k w_k · m_k(a) m_k(b) · cos( φ_k(a) − φ_k(b) + 2πkt/12 )
        var cross = new Complex[7];
        for (var k = 1; k <= 6; k++)
        {
            cross[k] = fa[k] * Complex.Conjugate(fb[k]);
        }

        var best = double.NegativeInfinity;
        var argmax = new List<int>();
        for (var t = 0; t < PitchClassSpaceSize; t++)
        {
            var num = 0.0;
            for (var k = 1; k <= 6; k++)
            {
                var rot = Complex.Exp(new Complex(0.0, 2.0 * Math.PI * k * t / PitchClassSpaceSize));
                num += Weight(weights, k) * (cross[k] * rot).Real;
            }

            var s = num / denom;
            if (s > best + Epsilon)
            {
                best = s;
                argmax.Clear();
                argmax.Add(t);
            }
            else if (Math.Abs(s - best) <= Epsilon)
            {
                argmax.Add(t);
            }
        }

        return new Alignment(best, argmax, inverted);
    }

    private static double Weight(double[]? weights, int k)
        => weights is null ? 1.0 : weights[k - 1];

    private static bool AllNull(Complex[] f, double[]? weights)
    {
        for (var k = 1; k <= 6; k++)
        {
            if (Weight(weights, k) > 0.0 && f[k].Magnitude >= Epsilon)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<int> AllTranspositions()
    {
        var all = new int[PitchClassSpaceSize];
        for (var t = 0; t < PitchClassSpaceSize; t++)
        {
            all[t] = t;
        }

        return all;
    }
}
