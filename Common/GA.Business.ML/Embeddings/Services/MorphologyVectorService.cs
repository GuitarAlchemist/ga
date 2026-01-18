namespace GA.Business.ML.Embeddings.Services;

using System;

/// <summary>
/// Generates the MORPHOLOGY partition of the musical embedding (Fretboard layout, span, difficulty).
/// Corresponds to dimensions 30-53 of the standard musical vector.
/// Implements OPTIC-K Schema v1.3.1 (Indices 30-53 unchanged since v1.1).
/// </summary>
public class MorphologyVectorService
{
    public const int Dimension = 24;

    /// <summary>
    /// Computes the Morphology portion of the embedding.
    /// </summary>
    public double[] ComputeEmbedding(
        int? bassPitchClass = null,
        int? melodyPitchClass = null,
        double normalizedSpan = 0.0,
        double normalizedNoteCount = 0.0,
        bool isRootless = false,
        double averageFret = 0.0,
        bool barreRequired = false)
    {
        var v = new double[Dimension];

        // 00-11: Bass Pitch Class (One-hot)
        if (bassPitchClass.HasValue)
        {
            var bass = bassPitchClass.Value % 12;
            v[bass] = 1.0;
        }

        // 12-23: Shape/Difficulty Features

        // 12: Span
        v[12] = normalizedSpan;

        // 13: Note Count
        v[13] = normalizedNoteCount;

        // 14: IsRootless
        v[14] = isRootless ? 1.0 : 0.0;

        // 15-16: Melody (Circular Encoding)
        if (melodyPitchClass.HasValue)
        {
            var angle = (melodyPitchClass.Value % 12) * (Math.PI * 2 / 12.0);
            v[15] = Math.Sin(angle);
            v[16] = Math.Cos(angle);
        }

        // 17: Average Fret (Position) - Higher weight to distinguish positions
        v[17] = (averageFret / 12.0) * 2.0;

        // 18: Barre Required - Technical change
        v[18] = barreRequired ? 2.0 : 0.0;

        return v;
    }
}
