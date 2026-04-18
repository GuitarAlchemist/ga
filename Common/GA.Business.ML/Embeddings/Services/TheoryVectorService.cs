namespace GA.Business.ML.Embeddings.Services;

/// <summary>
///     Generates embeddings for pure music theory concepts (Pitch, Interval, Function).
///     Corresponds to dimensions 6-29 of the standard musical vector (STRUCTURE partition).
///     Implements OPTIC-K Schema v1.3.1.
///
///     <para>
///         STRUCTURE is octave-invariant by contract — dims 20 (consonance) and 21 (brightness)
///         are derived from the ICV, not from perceptual qualities of the realized voicing.
///         Invariant #32 (cross-octave cosine == 1.0) depends on this.
///     </para>
/// </summary>
public class TheoryVectorService
{
    public const int Dimension = 24;

    /// <summary>
    ///     Computes the Structure portion of the embedding (OPTIC/K invariants).
    /// </summary>
    /// <remarks>
    ///     The <paramref name="consonance"/> and <paramref name="brightness"/> parameters are
    ///     accepted for backwards compatibility with older callers but are IGNORED. Their
    ///     replacements (dims 20-21) are derived from the ICV to preserve octave invariance.
    ///     See <see cref="IcvConsonance"/> and <see cref="IcvBrightness"/>.
    /// </remarks>
    public double[] ComputeEmbedding(
        IEnumerable<int> pitchClasses,
        int? rootPitchClass = null,
        string? intervalClassVector = null,
        double consonance = 0.0,
        double brightness = 0.0,
        double complementarity = 0.0)
    {
        _ = consonance;
        _ = brightness;

        var v = new double[Dimension];
        var pcs = pitchClasses.Distinct().ToList();

        // 00-11: Pitch Class Chroma (Presence) - Covers O and P
        foreach (var pc in pcs)
        {
            if (pc is >= 0 and < 12)
            {
                v[pc] = 1.0;
            }
        }

        // Boost Root (if known) - useful for tonal recognition
        if (rootPitchClass.HasValue)
        {
            var r = rootPitchClass.Value % 12;
            v[r] += 1.0;
        }

        // 12: Cardinality (C) - High weight for structural identity
        v[12] = pcs.Count / 12.0 * 2.0;

        // 13-18: Interval Class Vector (Structural Content) - Covers T and I
        var icv = ParseIcv(intervalClassVector);
        for (var i = 0; i < 6; i++)
        {
            v[13 + i] = icv[i];
        }

        // 19: Complementarity (K)
        v[19] = complementarity;

        // 20: Consonance — ICV-derived, octave-invariant
        v[20] = IcvConsonance(icv);

        // 21: Brightness — ICV-derived, octave-invariant
        v[21] = IcvBrightness(icv);

        // 22: Tonal Stability (Proxy: Root strength)
        if (rootPitchClass.HasValue)
        {
            v[22] = 1.0;
        }

        // 23: Reserved

        return v;
    }

    /// <summary>
    ///     Parses an ICV string (e.g. "001110") into six interval-class counts.
    ///     Returns zeros on null/empty/malformed input.
    /// </summary>
    private static int[] ParseIcv(string? icv)
    {
        var counts = new int[6];
        if (string.IsNullOrEmpty(icv)) return counts;
        for (var i = 0; i < Math.Min(6, icv.Length); i++)
        {
            if (char.IsDigit(icv[i])) counts[i] = icv[i] - '0';
        }
        return counts;
    }

    /// <summary>
    ///     Consonance proxy: share of consonant interval classes (minor third, major third,
    ///     perfect fourth/fifth) in the ICV. Ranges [0, 1]. Octave-invariant.
    /// </summary>
    internal static double IcvConsonance(int[] icv)
    {
        var total = 0;
        for (var i = 0; i < 6; i++) total += icv[i];
        if (total == 0) return 0.0;
        // ic3 (minor 3rd), ic4 (major 3rd), ic5 (perfect 4th/5th) are consonant.
        var consonant = icv[2] + icv[3] + icv[4];
        return (double)consonant / total;
    }

    /// <summary>
    ///     Brightness proxy: bright-to-dark interval balance from the ICV, normalised to [0, 1].
    ///     Major-3rd + perfect-5th lean bright; minor-2nd + tritone lean dark. Octave-invariant.
    /// </summary>
    internal static double IcvBrightness(int[] icv)
    {
        var total = 0;
        for (var i = 0; i < 6; i++) total += icv[i];
        if (total == 0) return 0.5;
        var bright = icv[3] + icv[4]; // ic4 + ic5
        var dark = icv[0] + icv[5];   // ic1 + ic6
        var net = (double)(bright - dark) / total; // range ~[-1, 1]
        return (net + 1.0) / 2.0;                  // map to [0, 1]
    }
}
