namespace GA.Business.ML.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using Core.Fretboard.Voicings.Search;

/// <summary>
/// High-level service for retrieving musical objects using the Spectral RAG architecture.
/// Implements Weighted Partition Cosine Similarity based on the OPTIC-K schema.
/// </summary>
public class SpectralRetrievalService
{
    private readonly IVectorIndex _index;

    public SpectralRetrievalService(IVectorIndex index)
    {
        _index = index;
    }

    /// <summary>
    /// Performs a weighted similarity search across the vector partitions.
    /// </summary>
    public IEnumerable<(VoicingDocument Doc, double Score)> Search(
        double[] queryEmbedding, 
        int topK = 10,
        SearchPreset preset = SearchPreset.Tonal)
    {
        if (queryEmbedding.Length != EmbeddingSchema.TotalDimension)
        {
            // Fallback to basic search if dimension mismatch
            return _index.Search(queryEmbedding, topK);
        }

        var results = new List<(VoicingDocument Doc, double Score)>();
        
        // In a real production system, this would be optimized (e.g. vector DB with partition support)
        // Here we do a linear scan for correctness.
        foreach (var doc in _index.Documents)
        {
            if (doc.Embedding == null || doc.Embedding.Length != EmbeddingSchema.TotalDimension) continue;

            double score = CalculateWeightedSimilarity(queryEmbedding, doc.Embedding, preset);
            results.Add((doc, score));
        }

        return results
            .OrderByDescending(x => x.Score)
            .Take(topK);
    }

    /// <summary>
    /// Finds neighbors based purely on Spectral Geometry (harmonic smoothness/voice-leading).
    /// </summary>
    public IEnumerable<(VoicingDocument Doc, double Score)> FindSpectralNeighbors(
        double[] queryEmbedding,
        int topK = 10)
    {
        return Search(queryEmbedding, topK, SearchPreset.Spectral);
    }

    private static (double Structure, double Morphology, double Context, double Symbolic, double Spectral) GetWeights(SearchPreset preset)
    {
        return preset switch
        {
            SearchPreset.Tonal => (0.45, 0.25, 0.20, 0.10, 0.0),
            SearchPreset.Atonal => (0.80, 0.10, 0.05, 0.05, 0.0),
            SearchPreset.Guitar => (0.20, 0.70, 0.05, 0.05, 0.0),
            SearchPreset.Jazz => (0.30, 0.10, 0.40, 0.20, 0.0),
            
            // Spectral: Pure harmonic geometry. Used for voice-leading suggestions.
            SearchPreset.Spectral => (0.0, 0.0, 0.0, 0.0, 1.0),
            
            _ => (0.45, 0.25, 0.20, 0.10, 0.0)
        };
    }

    public static double CalculateWeightedSimilarity(double[] a, double[] b, SearchPreset preset)
    {
        var weights = GetWeights(preset);
        double score = 0;

        if (weights.Structure > 0)
            score += weights.Structure * CosineSimilarity(a, EmbeddingSchema.StructureOffset, b, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureDim);

        if (weights.Morphology > 0)
            score += weights.Morphology * CosineSimilarity(a, EmbeddingSchema.MorphologyOffset, b, EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim);

        if (weights.Context > 0)
            score += weights.Context * CosineSimilarity(a, EmbeddingSchema.ContextOffset, b, EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextDim);

        if (weights.Symbolic > 0)
            score += weights.Symbolic * CosineSimilarity(a, EmbeddingSchema.SymbolicOffset, b, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);

        // 5. SPECTRAL (Phase Sphere)
        if (weights.Spectral > 0)
        {
            score += weights.Spectral * CalculateSpectralSimilarity(a, b);
        }

        return score;
    }

    private static double CalculateSpectralSimilarity(double[] a, double[] b)
    {
        // Sub-partition 1: Magnitudes (96-101) - 6d
        // These are linear strengths, Cosine is fine.
        double magSim = CosineSimilarity(a, EmbeddingSchema.FourierMagK1, b, EmbeddingSchema.FourierMagK1, 6);

        // Sub-partition 2: Phases (102-107) - 6d
        // These are periodic [0, 1]. We must use angular distance.
        double phaseSim = 0;
        int activeComponents = 0;
        for (int k = 0; k < 6; k++)
        {
            double p1 = a[EmbeddingSchema.FourierPhaseK1 + k];
            double p2 = b[EmbeddingSchema.FourierPhaseK1 + k];
            double m1 = a[EmbeddingSchema.FourierMagK1 + k];
            double m2 = b[EmbeddingSchema.FourierMagK1 + k];

            // Only compare phases if both components have significant magnitude
            if (m1 > 0.1 && m2 > 0.1)
            {
                double diff = Math.Abs(p1 - p2);
                if (diff > 0.5) diff = 1.0 - diff; // Wrap around
                
                // Map distance [0, 0.5] to similarity [1, 0]
                phaseSim += (1.0 - (diff * 2.0));
                activeComponents++;
            }
        }
        
        double finalPhaseSim = activeComponents > 0 ? phaseSim / activeComponents : 0;

        // Weighted combination: Magnitude identity (0.4) + Phase alignment (0.6)
        return (magSim * 0.4) + (finalPhaseSim * 0.6);
    }

    private static double CosineSimilarity(double[] v1, int offset1, double[] v2, int offset2, int dim)
    {
        var span1 = v1.AsSpan(offset1, dim);
        var span2 = v2.AsSpan(offset2, dim);
        
        // TensorPrimitives.CosineSimilarity returns NaN if one of the vectors is zero-length (magnitude 0)
        // We want 0.0 in that case.
        double sim = TensorPrimitives.CosineSimilarity(span1, span2);
        return double.IsNaN(sim) ? 0.0 : sim;
    }

    public enum SearchPreset
    {
        Tonal,
        Atonal,
        Experimental,
        Guitar,
        Jazz,
        Spectral
    }
}
