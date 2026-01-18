namespace GA.Business.ML.Embeddings.Services;

using System;
using System.Collections.Generic;

/// <summary>
/// Generates the CONTEXT partition of the musical embedding (Harmonic function, tension).
/// Corresponds to dimensions 54-65 of the standard musical vector.
/// Implements OPTIC-K Schema v1.3.1 (Indices 54-65 unchanged since v1.1).
/// </summary>
public class ContextVectorService
{
    private const int Dimension = 12;

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

        // 1. Harmonic Function (Indices 0-2)
        // [Tonic, Subdominant, Dominant]
                if (!string.IsNullOrEmpty(harmonicFunction))
                {
                    var function = Core.Tonal.HarmonicFunctionAnalyzer.Parse(harmonicFunction);
                    var primary = Core.Tonal.HarmonicFunctionAnalyzer.ToPrimaryCategory(function);
        
                    switch (primary)
                    {
                        case Core.Tonal.HarmonicFunctionCategory.Tonic: v[0] = 1.0; break;
                        case Core.Tonal.HarmonicFunctionCategory.Subdominant: v[1] = 1.0; break;
                        case Core.Tonal.HarmonicFunctionCategory.Dominant: v[2] = 1.0; break;
                    }
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
