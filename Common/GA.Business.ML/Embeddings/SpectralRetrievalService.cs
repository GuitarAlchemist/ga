namespace GA.Business.ML.Embeddings;

using System.Numerics.Tensors;

/// <summary>
///     High-level service for retrieving musical objects using the Spectral RAG architecture.
///     Implements Weighted Partition Cosine Similarity based on the OPTIC-K schema.
/// </summary>
public class SpectralRetrievalService(IVectorIndex index) : ISpectralRetrievalService
{
    public enum SearchPreset
    {
        Tonal,
        Atonal,
        Experimental,
        Guitar,
        Jazz,
        Spectral
    }

    /// <summary>
    ///     Performs a weighted similarity search across the vector partitions with optional metadata filtering.
    /// </summary>
    public IEnumerable<(ChordVoicingRagDocument Doc, double Score)> Search(
        float[] queryEmbedding,
        int topK = 10,
        SearchPreset preset = SearchPreset.Tonal,
        string? quality = null,
        string? extension = null,
        string? stackingType = null,
        int? noteCount = null)
    {
        if (queryEmbedding.Length != EmbeddingSchema.TotalDimension)
        {
            // Fallback to basic search if dimension mismatch
            return index.Search(queryEmbedding, topK);
        }

        var results = new List<(ChordVoicingRagDocument Doc, double Score)>();

        // In a real production system, this would be optimized (e.g. vector DB with partition support)
        // Here we do a linear scan for correctness.
        foreach (var doc in index.Documents)
        {
            if (doc.Embedding is not { Length: EmbeddingSchema.TotalDimension })
            {
                continue;
            }

            // Apply metadata filters if provided
            if (quality != null && doc.SemanticTags != null &&
                !doc.SemanticTags.Contains(quality, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (extension != null && doc.SemanticTags != null &&
                !doc.SemanticTags.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (stackingType != null && doc.StackingType != null && !string.Equals(doc.StackingType, stackingType,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (noteCount.HasValue && doc.MidiNotes.Length != noteCount.Value)
            {
                continue;
            }

            var score = CalculateWeightedSimilarity(queryEmbedding, doc.Embedding, preset);
            results.Add((doc, score));
        }

        return results
            .OrderByDescending(x => x.Score)
            .Take(topK);
    }

    /// <summary>
    ///     Finds neighbors based purely on Spectral Geometry (harmonic smoothness/voice-leading).
    /// </summary>
    public IEnumerable<(ChordVoicingRagDocument Doc, double Score)> FindSpectralNeighbors(
        float[] queryEmbedding,
        int topK = 10) => Search(queryEmbedding, topK, SearchPreset.Spectral);

    private static (double Structure, double Morphology, double Context, double Symbolic, double Spectral)
        GetWeights(SearchPreset preset) => preset switch
    {
        SearchPreset.Tonal => (0.45, 0.25, 0.20, 0.10, 0.0),
        SearchPreset.Atonal => (0.80, 0.10, 0.05, 0.05, 0.0),
        SearchPreset.Guitar => (0.20, 0.70, 0.05, 0.05, 0.0),
        SearchPreset.Jazz => (0.30, 0.10, 0.40, 0.20, 0.0),

        // Spectral: Pure harmonic geometry. Used for voice-leading suggestions.
        SearchPreset.Spectral => (0.0, 0.0, 0.0, 0.0, 1.0),

        _ => (0.45, 0.25, 0.20, 0.10, 0.0)
    };

    public static double CalculateWeightedSimilarity(float[] a, float[] b, SearchPreset preset)
    {
        var weights = GetWeights(preset);
        double score = 0;

        if (weights.Structure > 0)
        {
            score += weights.Structure * CosineSimilarity(a, EmbeddingSchema.StructureOffset, b,
                EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureDim);
        }

        if (weights.Morphology > 0)
        {
            score += weights.Morphology * CosineSimilarity(a, EmbeddingSchema.MorphologyOffset, b,
                EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim);
        }

        if (weights.Context > 0)
        {
            score += weights.Context * CosineSimilarity(a, EmbeddingSchema.ContextOffset, b,
                EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextDim);
        }

        if (weights.Symbolic > 0)
        {
            score += weights.Symbolic * CosineSimilarity(a, EmbeddingSchema.SymbolicOffset, b,
                EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);
        }

        // 5. SPECTRAL (Phase Sphere)
        if (weights.Spectral > 0)
        {
            score += weights.Spectral * CalculateSpectralSimilarity(a, b);
        }

        return score;
    }

    private static double CalculateSpectralSimilarity(float[] a, float[] b)
    {
        // Sub-partition 1: Magnitudes (96-101) - 6d
        // These are linear strengths, Cosine is fine.
        var magSim = CosineSimilarity(a, EmbeddingSchema.FourierMagK1, b, EmbeddingSchema.FourierMagK1, 6);

        // Sub-partition 2: Phases (102-107) - 6d
        // These are periodic [0, 1]. We must use angular distance.
        double phaseSim = 0;
        var activeComponents = 0;
        for (var k = 0; k < 6; k++)
        {
            var p1 = a[EmbeddingSchema.FourierPhaseK1 + k];
            var p2 = b[EmbeddingSchema.FourierPhaseK1 + k];
            var m1 = a[EmbeddingSchema.FourierMagK1 + k];
            var m2 = b[EmbeddingSchema.FourierMagK1 + k];

            // Only compare phases if both components have significant magnitude
            if (m1 > 0.1f && m2 > 0.1f)
            {
                var diff = Math.Abs(p1 - p2);
                if (diff > 0.5f)
                {
                    diff = 1.0f - diff; // Wrap around
                }

                // Map distance [0, 0.5] to similarity [1, 0]
                phaseSim += 1.0 - (double)diff * 2.0;
                activeComponents++;
            }
        }

        var finalPhaseSim = activeComponents > 0 ? phaseSim / activeComponents : 0;

        // Weighted combination: Magnitude identity (0.4) + Phase alignment (0.6)
        return magSim * 0.4 + finalPhaseSim * 0.6;
    }

    private static double CosineSimilarity(float[] v1, int offset1, float[] v2, int offset2, int dim)
    {
        var span1 = v1.AsSpan(offset1, dim);
        var span2 = v2.AsSpan(offset2, dim);

        // TensorPrimitives.CosineSimilarity returns NaN if one of the vectors is zero-length (magnitude 0)
        // We want 0.0 in that case.
        var sim = TensorPrimitives.CosineSimilarity(span1, span2);
        return double.IsNaN(sim) ? 0.0 : sim;
    }
}
