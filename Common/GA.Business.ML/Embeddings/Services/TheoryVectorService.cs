namespace GA.Business.ML.Embeddings.Services;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generates embeddings for pure music theory concepts (Pitch, Interval, Function).
/// Corresponds to dimensions 6-29 of the standard musical vector (STRUCTURE partition).
/// Implements OPTIC-K Schema v1.3.1.
/// </summary>
public class TheoryVectorService
{
    public const int Dimension = 24;

    /// <summary>
    /// Computes the Structure portion of the embedding (OPTIC/K invariants).
    /// </summary>
    public double[] ComputeEmbedding(
        IEnumerable<int> pitchClasses,
        int? rootPitchClass = null,
        string? intervalClassVector = null,
        double consonance = 0.0,
        double brightness = 0.0,
        double complementarity = 0.0)
    {
        var v = new double[Dimension];
        var pcs = pitchClasses.Distinct().ToList();

        // 00-11: Pitch Class Chroma (Presence) - Covers O and P
        foreach (var pc in pcs)
        {
            if (pc is >= 0 and < 12)
                v[pc] = 1.0;
        }

        // Boost Root (if known) - useful for tonal recognition
        if (rootPitchClass.HasValue)
        {
            var r = rootPitchClass.Value % 12;
            v[r] += 1.0;
        }

        // 12: Cardinality (C) - High weight for structural identity
        v[12] = (pcs.Count / 12.0) * 2.0;

        // 13-18: Interval Class Vector (Structural Content) - Covers T and I
        if (!string.IsNullOrEmpty(intervalClassVector))
        {
            for(var i=0; i<Math.Min(6, intervalClassVector.Length); i++)
            {
                if (char.IsDigit(intervalClassVector[i]))
                    v[13 + i] = (intervalClassVector[i] - '0') * 1.0; // High weight for ICV
            }
        }

        // 19: Complementarity (K)
        v[19] = complementarity;

        // 20-23: Functional Tonal Props
        // 20: Consonance
        v[20] = consonance;

        // 21: Brightness
        v[21] = brightness;

        // 22: Tonal Stability (Proxy: Root strength)
        if (rootPitchClass.HasValue) v[22] = 1.0;

        // 23: Reserved

        return v;
    }
}
