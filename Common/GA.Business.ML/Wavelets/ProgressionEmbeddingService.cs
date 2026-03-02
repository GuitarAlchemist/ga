namespace GA.Business.ML.Wavelets;

/// <summary>
///     Service for generating fixed-length embeddings for musical progressions.
///     Uses Wavelet Transform features to capture harmonic motion and structure.
/// </summary>
public class ProgressionEmbeddingService(
    ProgressionSignalService signalService,
    WaveletTransformService waveletService)
{
    /// <summary>
    ///     Generates a 80-dimensional embedding for a progression.
    ///     (16 features per signal * 5 signals: Stability, Tension, Entropy, Velocity, TonalDrift).
    /// </summary>
    public double[] GenerateEmbedding(IEnumerable<ChordVoicingRagDocument> progression)
    {
        var signals = signalService.ExtractSignals(progression);

        var features = new List<double>();

        // Process each signal through Wavelet Transform
        features.AddRange(ExtractSignalFeatures(signals.Stability));
        features.AddRange(ExtractSignalFeatures(signals.Tension));
        features.AddRange(ExtractSignalFeatures(signals.Entropy));
        features.AddRange(ExtractSignalFeatures(signals.Velocity));
        features.AddRange(ExtractSignalFeatures(signals.TonalDrift));

        return [.. features];
    }

    private double[] ExtractSignalFeatures(double[] signal)
    {
        if (signal.Length == 0)
        {
            return new double[16];
        }

        // 1. Decompose
        var decomp = waveletService.Decompose(signal);

        // 2. Extract stats features (fixed 16-dim vector)
        return waveletService.ExtractFeatures(decomp);
    }
}
