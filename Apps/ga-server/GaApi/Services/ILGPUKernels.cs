namespace GaApi.Services;

using ILGPU;
using ILGPU.Runtime;
using System.Numerics;

/// <summary>
/// ILGPU kernels for GPU-accelerated vector operations
/// Following ILGPU documentation: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/
/// </summary>
public static class ILGPUKernels
{
    /// <summary>
    /// GPU kernel for calculating cosine similarity between a query vector and multiple chord embeddings
    /// </summary>
    /// <param name="index">Thread index</param>
    /// <param name="queryVector">Query embedding vector (shared across all threads)</param>
    /// <param name="chordEmbeddings">All chord embeddings flattened in memory</param>
    /// <param name="similarities">Output similarities array</param>
    /// <param name="embeddingDim">Dimension of each embedding</param>
    /// <param name="numChords">Total number of chords</param>
    public static void CosineSimilarityKernel(
        Index1D index,
        ArrayView<double> queryVector,
        ArrayView<double> chordEmbeddings,
        ArrayView<double> similarities,
        int embeddingDim,
        int numChords)
    {
        if (index >= numChords)
            return;

        // Calculate offset for this chord's embedding
        var chordOffset = index * embeddingDim;

        // Calculate dot product, query norm, and chord norm
        double dotProduct = 0.0;
        double queryNorm = 0.0;
        double chordNorm = 0.0;

        for (int i = 0; i < embeddingDim; i++)
        {
            var queryVal = queryVector[i];
            var chordVal = chordEmbeddings[chordOffset + i];

            dotProduct += queryVal * chordVal;
            queryNorm += queryVal * queryVal;
            chordNorm += chordVal * chordVal;
        }

        // Calculate cosine similarity
        var magnitude = XMath.Sqrt(queryNorm) * XMath.Sqrt(chordNorm);
        similarities[index] = magnitude > 0 ? dotProduct / magnitude : 0.0;
    }

    /// <summary>
    /// GPU kernel for filtered cosine similarity (only compute for allowed chord indices)
    /// </summary>
    public static void FilteredCosineSimilarityKernel(
        Index1D index,
        ArrayView<double> queryVector,
        ArrayView<double> chordEmbeddings,
        ArrayView<int> allowedIndices,
        ArrayView<double> similarities,
        int embeddingDim,
        int numAllowedChords)
    {
        if (index >= numAllowedChords)
            return;

        // Get the actual chord index from the allowed indices array
        var chordIndex = allowedIndices[index];
        var chordOffset = chordIndex * embeddingDim;

        // Calculate dot product, query norm, and chord norm
        double dotProduct = 0.0;
        double queryNorm = 0.0;
        double chordNorm = 0.0;

        for (int i = 0; i < embeddingDim; i++)
        {
            var queryVal = queryVector[i];
            var chordVal = chordEmbeddings[chordOffset + i];

            dotProduct += queryVal * chordVal;
            queryNorm += queryVal * queryVal;
            chordNorm += chordVal * chordVal;
        }

        // Calculate cosine similarity
        var magnitude = XMath.Sqrt(queryNorm) * XMath.Sqrt(chordNorm);
        similarities[index] = magnitude > 0 ? dotProduct / magnitude : 0.0;
    }

    /// <summary>
    /// GPU kernel for batch cosine similarity with multiple query vectors
    /// </summary>
    public static void BatchCosineSimilarityKernel(
        Index2D index,
        ArrayView<double> queryVectors,
        ArrayView<double> chordEmbeddings,
        ArrayView<double> similarities,
        int embeddingDim,
        int numChords,
        int numQueries)
    {
        var queryIdx = index.X;
        var chordIdx = index.Y;

        if (queryIdx >= numQueries || chordIdx >= numChords)
            return;

        // Calculate offsets
        var queryOffset = queryIdx * embeddingDim;
        var chordOffset = chordIdx * embeddingDim;

        // Calculate dot product, query norm, and chord norm
        double dotProduct = 0.0;
        double queryNorm = 0.0;
        double chordNorm = 0.0;

        for (int i = 0; i < embeddingDim; i++)
        {
            var queryVal = queryVectors[queryOffset + i];
            var chordVal = chordEmbeddings[chordOffset + i];

            dotProduct += queryVal * chordVal;
            queryNorm += queryVal * queryVal;
            chordNorm += chordVal * chordVal;
        }

        // Calculate cosine similarity
        var magnitude = XMath.Sqrt(queryNorm) * XMath.Sqrt(chordNorm);
        var resultIdx = queryIdx * numChords + chordIdx;
        similarities[resultIdx] = magnitude > 0 ? dotProduct / magnitude : 0.0;
    }

    /// <summary>
    /// GPU kernel for Euclidean distance calculation
    /// </summary>
    public static void EuclideanDistanceKernel(
        Index1D index,
        ArrayView<double> queryVector,
        ArrayView<double> chordEmbeddings,
        ArrayView<double> distances,
        int embeddingDim,
        int numChords)
    {
        if (index >= numChords)
            return;

        var chordOffset = index * embeddingDim;
        double sumSquaredDiff = 0.0;

        for (int i = 0; i < embeddingDim; i++)
        {
            var diff = queryVector[i] - chordEmbeddings[chordOffset + i];
            sumSquaredDiff += diff * diff;
        }

        distances[index] = XMath.Sqrt(sumSquaredDiff);
    }
}

