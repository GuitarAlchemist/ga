namespace GA.Business.Core.AI.Embeddings;

using System;
using System.Collections.Generic;

/// <summary>
/// Generates embeddings for Harmonic Context (v1.1).
/// Encodes relationship to Key and Harmonic Function.
/// Corresponds to dimensions 54-65 of the standard musical vector.
/// </summary>
public class ContextVectorService
{
    public const int Dimension = 12;

    /// <summary>
    /// Computes context vector.
    /// Note: For static indexing of voicings, this may be largely zero or generic,
    /// but for Query/Progression logic, it is fully populated.
    /// </summary>
    public double[] ComputeEmbedding(
        string? harmonicFunction = null, // Tonic, Subdominant, Dominant
        double stabilityDelta = 0.0,     // Change in stability from previous
        double tension = 0.0,            // Harmonic tension
        bool isResolution = false        // Is this a resolution point?
    )
    {
        var v = new double[Dimension];

        // 0-2: Harmonic Function (One-Hot-ish or Coords)
        // 0: Tonicness
        // 1: Subdominantness
        // 2: Dominantness
        if (!string.IsNullOrEmpty(harmonicFunction))
        {
            var hf = harmonicFunction.ToLowerInvariant();
            if (hf.Contains("tonic")) v[0] = 1.0;
            if (hf.Contains("subdominant") || hf.Contains("predominant")) v[1] = 1.0;
            if (hf.Contains("dominant")) v[2] = 1.0;
        }

        // 3: Stability Delta (Motion)
        v[3] = stabilityDelta;

        // 4: Absolute Tension
        v[4] = tension;

        // 5: Is Resolution
        v[5] = isResolution ? 1.0 : 0.0;

        // 6-11: Reserved for Key Relationship (Circle of Fifths distance, etc)

        return v;
    }
}
