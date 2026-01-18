namespace GA.Business.ML.Retrieval;

using System;
using System.Collections.Generic;
using System.Linq;
using Embeddings;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// Calculates stylistic prototypes (Centroids) from the vector index.
/// Used to bias generative realization toward specific stylistic physical patterns.
/// </summary>
public class StyleProfileService
{
    private readonly IVectorIndex _index;

    public StyleProfileService(IVectorIndex index)
    {
        _index = index;
    }

    /// <summary>
    /// Calculates the mean morphology vector for a given style tag.
    /// </summary>
    public virtual double[]? GetStyleCentroid(string styleTag)
    {
        var candidates = _index.Documents
            .Where(d => d.SemanticTags != null && d.SemanticTags.Contains(styleTag, StringComparer.OrdinalIgnoreCase))
            .Where(d => d.Embedding != null && d.Embedding.Length == EmbeddingSchema.TotalDimension)
            .ToList();

        if (candidates.Count == 0) return null;

        var centroid = new double[EmbeddingSchema.TotalDimension];
        foreach (var doc in candidates)
        {
            for (int i = 0; i < centroid.Length; i++)
            {
                centroid[i] += doc.Embedding![i];
            }
        }

        for (int i = 0; i < centroid.Length; i++)
        {
            centroid[i] /= candidates.Count;
        }

        return centroid;
    }

    /// <summary>
    /// Calculates how "Stylistically Natural" a potential realization is.
    /// Returns a score [0, 1] where 1 is perfect match to style prototype.
    /// </summary>
    public virtual double CalculateNaturalness(double[] realizationEmbedding, double[] styleCentroid)
    {
        // Use Weighted Euclidean Distance for Morphology
        // This ensures that absolute register (fret numbers) matters.
        
        double sumSq = 0;
        int count = 0;
        for (int i = EmbeddingSchema.MorphologyOffset; i < EmbeddingSchema.MorphologyEnd; i++)
        {
            double diff = realizationEmbedding[i] - styleCentroid[i];
            sumSq += diff * diff;
            count++;
        }

        double distance = Math.Sqrt(sumSq);
        // Map distance to [0, 1] score. Typical max distance approx 1.0 - 2.0
        return Math.Max(0, 1.0 - (distance / 2.0));
    }

    private static double CosineSimilarity(double[] v1, int offset1, double[] v2, int offset2, int dim)
    {
        double dot = 0, mag1 = 0, mag2 = 0;
        for (int i = 0; i < dim; i++)
        {
            double a = v1[offset1 + i];
            double b = v2[offset2 + i];
            dot += a * b;
            mag1 += a * a;
            mag2 += b * b;
        }
        if (mag1 == 0 || mag2 == 0) return 0;
        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}
