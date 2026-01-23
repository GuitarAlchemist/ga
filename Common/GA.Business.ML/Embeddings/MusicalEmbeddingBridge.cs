namespace GA.Business.ML.Embeddings;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using Microsoft.Extensions.AI;

/// <summary>
/// Bridges Guitar Alchemist's domain-specific <see cref="MusicalEmbeddingGenerator"/> to MEAI's
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> interface.
/// </summary>
/// <remarks>
/// <para>
/// This adapter enables the OPTIC-K embedding system to be used with any MEAI-compatible
/// infrastructure, including Semantic Kernel plugins and standard vector store abstractions.
/// </para>
/// <para>
/// The musical embeddings are domain-specific (computed from pitch classes, intervals, morphology)
/// rather than text-based, so this bridge wraps <see cref="VoicingDocument"/> as the input type.
/// </para>
/// </remarks>
public sealed class MusicalEmbeddingBridge : IEmbeddingGenerator<VoicingDocument, Embedding<float>>
{
    private readonly MusicalEmbeddingGenerator _generator;
    private readonly EmbeddingGeneratorMetadata _metadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicalEmbeddingBridge"/> class.
    /// </summary>
    /// <param name="generator">The underlying OPTIC-K embedding generator.</param>
    public MusicalEmbeddingBridge(MusicalEmbeddingGenerator generator)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        // EmbeddingGeneratorMetadata(providerName, providerUri, modelId)
        _metadata = new EmbeddingGeneratorMetadata(
            "GuitarAlchemist",
            null,
            $"OPTIC-K-v{EmbeddingSchema.Version}");
    }

    /// <summary>
    /// Gets metadata describing the embedding generator.
    /// </summary>
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <summary>
    /// Gets the embedding dimension.
    /// </summary>
    public int Dimension => _generator.Dimension;

    /// <summary>
    /// Generates embeddings for the provided voicing documents.
    /// </summary>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<VoicingDocument> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var documents = values.ToList();
        var embeddings = new List<Embedding<float>>(documents.Count);

        foreach (var doc in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generate domain-specific OPTIC-K embedding
            var doubleVector = await _generator.GenerateEmbeddingAsync(doc);

            // Convert double[] to float[] for MEAI compatibility
            var floatVector = Array.ConvertAll(doubleVector, v => (float)v);

            embeddings.Add(new Embedding<float>(floatVector));
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings)
        {
            Usage = new UsageDetails
            {
                InputTokenCount = documents.Count,
                TotalTokenCount = documents.Count
            }
        };
    }

    /// <summary>
    /// Generates a single embedding for a voicing document.
    /// </summary>
    /// <remarks>Convenience method that wraps the batch API.</remarks>
    public async Task<Embedding<float>> GenerateSingleAsync(
        VoicingDocument document,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateAsync([document], cancellationToken: cancellationToken);
        return result.First();
    }

    /// <summary>
    /// Releases resources used by the bridge.
    /// </summary>
    public void Dispose()
    {
        // No unmanaged resources to dispose.
    }

    /// <summary>
    /// Gets a service of the specified type from this embedding generator.
    /// </summary>
    public object? GetService(Type serviceType, object? key = null)
    {
        if (serviceType == typeof(MusicalEmbeddingGenerator))
        {
            return _generator;
        }
        if (serviceType == typeof(MusicalEmbeddingBridge))
        {
            return this;
        }
        return null;
    }
}
