namespace GA.Business.ML.Embeddings;

using Microsoft.Extensions.AI;

/// <summary>
///     Bridges Guitar Alchemist's domain-specific <see cref="MusicalEmbeddingGenerator" /> to MEAI's
///     <see cref="Microsoft.Extensions.AI.IEmbeddingGenerator{TInput,TEmbedding}" /> interface.
/// </summary>
/// <remarks>
///     <para>
///         This adapter enables the OPTIC-K embedding system to be used with any MEAI-compatible
///         infrastructure, including Semantic Kernel plugins and standard vector store abstractions.
///     </para>
///     <para>
///         The musical embeddings are domain-specific (computed from pitch classes, intervals, morphology)
///         rather than text-based, so this bridge wraps <see cref="ChordVoicingRagDocument" /> as the input type.
///     </para>
/// </remarks>
public sealed class MusicalEmbeddingBridge(MusicalEmbeddingGenerator generator)
    : IEmbeddingGenerator<ChordVoicingRagDocument, Embedding<float>>
{
    private readonly MusicalEmbeddingGenerator _generator = generator ?? throw new ArgumentNullException(nameof(generator));

    /// <summary>
    ///     Gets metadata describing the embedding generator.
    /// </summary>
    public EmbeddingGeneratorMetadata Metadata { get; } = new(
        "GuitarAlchemist",
        null,
        $"OPTIC-K-v{EmbeddingSchema.Version}");


    /// <summary>
    ///     Gets the embedding dimension.
    /// </summary>
    public int Dimension => _generator.Dimension;

    /// <summary>
    ///     Generates embeddings for the provided voicing documents.
    /// </summary>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<ChordVoicingRagDocument> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var documents = values.ToList();
        var embeddings = new List<Embedding<float>>(documents.Count);

        foreach (var doc in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generate domain-specific OPTIC-K embedding
            var floatVector = await _generator.GenerateEmbeddingAsync(doc);

            embeddings.Add(new(floatVector));
        }

        return new(embeddings)
        {
            Usage = new()
            {
                InputTokenCount = documents.Count,
                TotalTokenCount = documents.Count
            }
        };
    }

    /// <summary>
    ///     Releases resources used by the bridge.
    /// </summary>
    public void Dispose()
    {
        // No unmanaged resources to dispose.
    }

    /// <summary>
    ///     Gets a service of the specified type from this embedding generator.
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

    /// <summary>
    ///     Generates a single embedding for a voicing document.
    /// </summary>
    /// <remarks>Convenience method that wraps the batch API.</remarks>
    public async Task<Embedding<float>> GenerateSingleAsync(
        ChordVoicingRagDocument document,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateAsync([document], cancellationToken: cancellationToken);
        return result.First();
    }
}
