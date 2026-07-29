namespace GA.Business.ML.Embeddings.Services;

using System;
using System.Numerics;
using Domain.Core.Theory.Tonal;

/// <summary>
///     Generates the CONTEXT partition of the musical embedding (Harmonic function, tension).
///     Corresponds to dimensions 54-65 of the standard musical vector.
///     Implements OPTIC-K Schema v1.3.1 (Indices 54-65 unchanged since v1.1).
/// </summary>
public class ContextVectorService
{
    private const int Dimension = 12;

    /// <summary>
    ///     Computes context vector.
    ///     Note: For static indexing of voicings, this may be largely zero or generic,
    ///     but for Query/Progression logic, it is fully populated.
    /// </summary>
    public static double[] ComputeEmbedding(
        int[]? midiNotes = null,
        string? harmonicFunction = null, // Tonic, Subdominant, Dominant
        double stabilityDelta = 0.0, // Change in stability from previous
        double tension = 0.0, // Harmonic tension
        bool isResolution = false // Is this a resolution point?
    )
    {
        var v = new double[Dimension];

        // 1. Harmonic Function (Indices 0-2)
        // [Tonic, Subdominant, Dominant]
        if (!string.IsNullOrEmpty(harmonicFunction))
        {
            var function = HarmonicFunctionAnalyzer.Parse(harmonicFunction);
            var primary = HarmonicFunctionAnalyzer.ToPrimaryCategory(function);

            switch (primary)
            {
                case HarmonicFunctionCategory.Tonic: v[0] = 1.0; break;
                case HarmonicFunctionCategory.Subdominant: v[1] = 1.0; break;
                case HarmonicFunctionCategory.Dominant: v[2] = 1.0; break;
            }
        }

        // 3: Stability Delta (Motion)
        v[3] = stabilityDelta;

        // 4: Absolute Tension
        v[4] = tension;

        // 5: Is Resolution
        v[5] = isResolution ? 1.0 : 0.0;

        // 6-11: Key Relationship (Circle of Fifths distance, etc)
        if (midiNotes != null && midiNotes.Length > 0)
        {
            var spec = PhaseSphereService.ComputeWeightedSpectralVector(midiNotes);
            var normalizedSpec = PhaseSphereService.NormalizeToSphere(spec);
            var k5 = normalizedSpec[4]; // k=5 component is index 4

            if (k5.Magnitude > 1e-10)
            {
                var phi5 = k5.Phase;
                for (var m = 0; m < 6; m++)
                {
                    var theta = m * Math.PI / 6.0;
                    v[6 + m] = k5.Magnitude * Math.Cos(phi5 - theta);
                }
            }
        }

        return v;
    }
}
