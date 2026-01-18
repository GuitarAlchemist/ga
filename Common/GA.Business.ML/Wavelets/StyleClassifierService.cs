namespace GA.Business.ML.Wavelets;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// Classifies the musical style of a harmonic progression using wavelet-based features.
/// Part of Phase 6.3.2.
/// </summary>
public class StyleClassifierService
{
    private readonly ProgressionEmbeddingService _embeddingService;

    public StyleClassifierService(ProgressionEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public record StylePrediction(string PredictedStyle, double Confidence, Dictionary<string, double> Probabilities);

    /// <summary>
    /// Predicts the style of a given progression.
    /// </summary>
    public StylePrediction PredictStyle(List<VoicingDocument> progression)
    {
        if (progression == null || progression.Count < 4) 
            return new StylePrediction("Too Short", 0, new());

        // 1. Extract Wavelet Features (Temporal Harmonic Signature)
        var features = _embeddingService.GenerateEmbedding(progression);

        // 2. Perform Classification (Mocked for Spike)
        // In real implementation, this would load an ML.NET / ONNX multiclass classifier.
        // We'll use a simple heuristic-based prediction for now to demonstrate the flow.
        
        var (style, confidence) = PerformHeuristicInference(features);

        return new StylePrediction(style, confidence, new Dictionary<string, double> { { style, confidence } });
    }

    private (string Style, double Confidence) PerformHeuristicInference(double[] features)
    {
        // Simple heuristic: 
        // Tension Variance (Feature index 1?) and Velocity Mean
        // Features are [StabMean, StabVar, TenMean, TenVar, EntMean, EntVar, VelMean, VelVar, ... Wavelets]
        
        double tensionVariance = features[3];
        double velocityMean = features[6];

        if (tensionVariance > 0.3) return ("Jazz", 0.85); // High tension variance = Jazz/Complex
        if (velocityMean > 0.4) return ("Blues", 0.75);   // High movement = Blues/Rock
        
        return ("Rock/Pop", 0.60);
    }
}
