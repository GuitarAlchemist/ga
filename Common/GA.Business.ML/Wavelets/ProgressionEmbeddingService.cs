namespace GA.Business.ML.Wavelets;

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// Service for generating fixed-length embeddings for musical progressions.
/// Uses Wavelet Transform features to capture harmonic motion and structure.
/// </summary>
public class ProgressionEmbeddingService
{
    private readonly ProgressionSignalService _signalService;
    private readonly WaveletTransformService _waveletService;

    public ProgressionEmbeddingService(
        ProgressionSignalService signalService,
        WaveletTransformService waveletService)
    {
        _signalService = signalService;
        _waveletService = waveletService;
    }

    /// <summary>
    /// Generates a 80-dimensional embedding for a progression.
    /// (16 features per signal * 5 signals: Stability, Tension, Entropy, Velocity, TonalDrift).
    /// </summary>
    public double[] GenerateEmbedding(IEnumerable<VoicingDocument> progression)
    {
        var signals = _signalService.ExtractSignals(progression);
        
        var features = new List<double>();

        // Process each signal through Wavelet Transform
        features.AddRange(ExtractSignalFeatures(signals.Stability));
        features.AddRange(ExtractSignalFeatures(signals.Tension));
        features.AddRange(ExtractSignalFeatures(signals.Entropy));
        features.AddRange(ExtractSignalFeatures(signals.Velocity));
        features.AddRange(ExtractSignalFeatures(signals.TonalDrift));

        return features.ToArray();
    }

    private double[] ExtractSignalFeatures(double[] signal)
    {
        if (signal.Length == 0) return new double[16];

        // 1. Decompose
        var decomp = _waveletService.Decompose(signal);

        // 2. Extract stats features (fixed 16-dim vector)
        return _waveletService.ExtractFeatures(decomp);
    }
}
