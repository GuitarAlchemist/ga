namespace GA.Business.Core.Fretboard.Analysis;

using System.Collections.Generic;

/// <summary>
/// Interface for ML-based naturalness scoring of fingering transitions.
/// </summary>
public interface IMlNaturalnessRanker
{
    /// <summary>
    /// Predicts naturalness score (0-1) for a transition between two chord shapes.
    /// </summary>
    float PredictNaturalness(List<FretboardPosition> from, List<FretboardPosition> to);
}
