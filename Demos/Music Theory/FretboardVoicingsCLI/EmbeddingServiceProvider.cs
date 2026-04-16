namespace FretboardVoicingsCLI;

using GA.Business.ML.Embeddings;
using GA.Business.ML.Extensions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Minimal DI container for CLI access to OPTIC-K embedding generation.
/// </summary>
internal static class EmbeddingServiceProvider
{
    /// <summary>
    /// Creates a <see cref="MusicalEmbeddingGenerator"/> with all 7 vector services wired via DI.
    /// </summary>
    public static MusicalEmbeddingGenerator CreateEmbeddingGenerator()
    {
        var services = new ServiceCollection();
        services.AddMusicalEmbeddings();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<MusicalEmbeddingGenerator>();
    }
}
