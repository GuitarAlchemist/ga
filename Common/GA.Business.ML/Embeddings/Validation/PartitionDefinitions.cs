namespace GA.Business.ML.Embeddings.Validation;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines an embedding partition with its offset, dimension, and weight.
/// </summary>
/// <param name="Name">Human-readable name of the partition.</param>
/// <param name="Offset">Starting index in the embedding vector.</param>
/// <param name="Dimension">Number of dimensions in this partition.</param>
/// <param name="Weight">Similarity weight (0 means excluded from similarity).</param>
public record EmbeddingPartition(string Name, int Offset, int Dimension, double Weight)
{
    /// <summary>
    /// Ending index (exclusive).
    /// </summary>
    public int End => Offset + Dimension;

    /// <summary>
    /// Extracts this partition's values from a full embedding vector.
    /// </summary>
    public float[] Extract(float[] embedding)
    {
        if (embedding.Length < End)
            throw new ArgumentException($"Embedding too short for partition {Name}: need {End}, got {embedding.Length}");
        
        var result = new float[Dimension];
        Array.Copy(embedding, Offset, result, 0, Dimension);
        return result;
    }

    /// <summary>
    /// Calculates cosine similarity for this partition only.
    /// </summary>
    public double CosineSimilarity(float[] a, float[] b)
    {
        var partA = Extract(a);
        var partB = Extract(b);
        return VectorMath.CosineSimilarity(partA, partB);
    }
}

/// <summary>
/// All OPTIC-K partitions as defined in the embedding schema.
/// </summary>
public static class OpticKPartitions
{
    public static readonly EmbeddingPartition Identity = new("Identity", EmbeddingSchema.IdentityOffset, EmbeddingSchema.IdentityDim, 0.0);
    public static readonly EmbeddingPartition Structure = new("Structure", EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureDim, EmbeddingSchema.StructureWeight);
    public static readonly EmbeddingPartition Morphology = new("Morphology", EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim, EmbeddingSchema.MorphologyWeight);
    public static readonly EmbeddingPartition Context = new("Context", EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextDim, EmbeddingSchema.ContextWeight);
    public static readonly EmbeddingPartition Symbolic = new("Symbolic", EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim, EmbeddingSchema.SymbolicWeight);
    public static readonly EmbeddingPartition Extensions = new("Extensions", EmbeddingSchema.ExtensionsOffset, EmbeddingSchema.ExtensionsDim, 0.0);
    public static readonly EmbeddingPartition Spectral = new("Spectral", EmbeddingSchema.SpectralOffset, EmbeddingSchema.SpectralDim, 0.0);
    public static readonly EmbeddingPartition Modal = new("Modal", EmbeddingSchema.ModalOffset, EmbeddingSchema.ModalDim, 0.0);
    public static readonly EmbeddingPartition Hierarchy = new("Hierarchy", EmbeddingSchema.HierarchyOffset, EmbeddingSchema.HierarchyDim, 0.0);

    /// <summary>
    /// All partitions in order.
    /// </summary>
    public static IReadOnlyList<EmbeddingPartition> All => new[]
    {
        Identity, Structure, Morphology, Context, Symbolic, Extensions, Spectral, Modal, Hierarchy
    };

    /// <summary>
    /// Only partitions used for similarity scoring.
    /// </summary>
    public static IReadOnlyList<EmbeddingPartition> SimilarityPartitions => new[]
    {
        Structure, Morphology, Context, Symbolic
    };
}

/// <summary>
/// Vector math utilities.
/// </summary>
public static class VectorMath
{
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Vector lengths don't match: {a.Length} vs {b.Length}");
        
        double dot = 0, magA = 0, magB = 0;
        bool identical = true;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
            if (a[i] != b[i]) identical = false;
        }

        if (identical) return 1.0;
        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    public static double EuclideanDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Vector lengths don't match: {a.Length} vs {b.Length}");

        double sum = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }

    public static float[] Normalize(float[] v)
    {
        double mag = 0;
        foreach (var x in v) mag += x * x;
        mag = Math.Sqrt(mag);
        
        if (mag == 0) return v;
        
        var result = new float[v.Length];
        for (var i = 0; i < v.Length; i++)
            result[i] = (float)(v[i] / mag);
        return result;
    }
}
