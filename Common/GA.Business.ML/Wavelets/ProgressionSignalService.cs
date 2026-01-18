namespace GA.Business.ML.Wavelets;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Fretboard.Voicings.Search;
using Embeddings;

using Embeddings.Services;

/// <summary>
/// Service for extracting time-series signals from musical progressions.
/// These signals serve as input for wavelet transform analysis.
/// </summary>
public class ProgressionSignalService
{
    private readonly PhaseSphereService _phaseSphereService;

    public ProgressionSignalService(PhaseSphereService phaseSphereService)
    {
        _phaseSphereService = phaseSphereService;
    }

    public ProgressionSignalService() : this(new PhaseSphereService()) { }

    /// <summary>
    /// Extracts scalar signals from a sequence of voicing documents.
    /// </summary>
    public ProgressionSignals ExtractSignals(IEnumerable<VoicingDocument> progression)
    {
        var docs = progression.ToList();
        if (docs.Count == 0) return new ProgressionSignals();

        int n = docs.Count;
        var stability = new double[n];
        var tension = new double[n];
        var entropy = new double[n];
        var velocity = new double[n];
        var tonalDrift = new double[n];

        // 1. Compute Global Spectral Barycenter (Key Center of the progression)
        // We use this as the reference point for Tonal Drift.
        var pcSets = docs.Select(d => d.PitchClasses).ToList();
        var barycenter = _phaseSphereService.ComputeSpectralBarycenter(pcSets);

        for (int i = 0; i < n; i++)
        {
            var doc = docs[i];
            
            // 1. Stability (Consonance)
            stability[i] = doc.Consonance;
            
            // 2. Tension (Inverse of Stability)
            tension[i] = 1.0 - doc.Consonance;
            
            // 3. Entropy (Spectral peakiness - Index 108 in Schema v1.4)
            if (doc.Embedding != null && doc.Embedding.Length > EmbeddingSchema.SpectralEntropy)
            {
                entropy[i] = doc.Embedding[EmbeddingSchema.SpectralEntropy];
            }

            // 4. Velocity (Distance from previous chord)
            if (i > 0)
            {
                velocity[i] = CalculateDistance(docs[i - 1].Embedding, doc.Embedding);
            }
            else
            {
                velocity[i] = 0.0; // Initial velocity is zero
            }

            // 5. Tonal Drift (Phase distance on the Circle of Fifths from Barycenter)
            // Use k=5 (index 4) for Fifth Cycle.
            var spec = _phaseSphereService.ComputeSpectralVector(doc.PitchClasses);
            var normSpec = _phaseSphereService.NormalizeToSphere(spec);
            
            // Relative phase angle at k=5
            var relPhases = _phaseSphereService.ComputeRelativePhases(normSpec, barycenter);
            
            // Unwrapped phase or absolute distance?
            // Absolute distance from center (0 to PI).
            // This represents "how far away" we are from the average key.
            tonalDrift[i] = Math.Abs(relPhases[4]); 
        }

        return new ProgressionSignals
        {
            Stability = stability,
            Tension = tension,
            Entropy = entropy,
            Velocity = velocity,
            TonalDrift = tonalDrift
        };
    }

    private double CalculateDistance(double[]? a, double[]? b)
    {
        if (a == null || b == null || a.Length != b.Length) return 0.0;
        
        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            double diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
}

/// <summary>
/// Container for time-series signals extracted from a progression.
/// </summary>
public record ProgressionSignals
{
    public double[] Stability { get; init; } = Array.Empty<double>();
    public double[] Tension { get; init; } = Array.Empty<double>();
    public double[] Entropy { get; init; } = Array.Empty<double>();
    public double[] Velocity { get; init; } = Array.Empty<double>();
    public double[] TonalDrift { get; init; } = Array.Empty<double>();
}
