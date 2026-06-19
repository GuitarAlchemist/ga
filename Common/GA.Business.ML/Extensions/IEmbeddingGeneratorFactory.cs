namespace GA.Business.ML.Extensions;

using System.Collections.Concurrent;
using GA.Business.ML.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Returns an <see cref="IEmbeddingGenerator{String,Embedding}"/> for a named
/// purpose, so different consumers can use different embedding models without a
/// global swap. The text-embedding analogue of <see cref="IChatClientFactory"/>.
/// </summary>
/// <remarks>
/// <para>Motivation (plan <c>docs/plans/2026-06-16-ml-text-embedder-evaluation-plan.md</c>):
/// routing and the persisted memory store historically shared one global
/// <see cref="IEmbeddingGenerator{String,Embedding}"/> singleton. A bake-off found
/// a stronger routing embedder (<c>bge-large</c>), but a global swap would force
/// re-embedding the memory store + break dimension-coupled call sites. This factory
/// lets ROUTING use a different model while the store keeps its own — a routing-only,
/// reversible change.</para>
/// <para>Defined purposes: <c>routing</c> (semantic intent routing) and
/// <c>memory</c> / <c>default</c> (persisted retrieval). A purpose with no
/// configured override transparently uses the default generator, so behaviour is
/// UNCHANGED until <c>AI:Embedding:&lt;purpose&gt;:Model</c> is set.</para>
/// </remarks>
public interface IEmbeddingGeneratorFactory
{
    /// <summary>
    /// Returns the embedding generator for <paramref name="purpose"/>: an
    /// override built from <c>AI:Embedding:&lt;purpose&gt;:Model</c> if configured,
    /// otherwise the global default generator (which may be <see langword="null"/>
    /// when no embedder is registered — callers already treat embeddings as
    /// optional). Override instances are cached per purpose.
    /// </summary>
    IEmbeddingGenerator<string, Embedding<float>>? Create(string purpose);
}

/// <summary>
/// Config-driven <see cref="IEmbeddingGeneratorFactory"/>. An override is read from
/// <c>AI:Embedding:&lt;purpose&gt;:Model</c> and built as an Ollama embedding
/// generator at <c>Ollama:BaseUrl</c>; absent an override the global default
/// generator is returned verbatim (zero behaviour change).
/// </summary>
public sealed class DefaultEmbeddingGeneratorFactory(
    IConfiguration configuration,
    IEmbeddingGenerator<string, Embedding<float>>? defaultGenerator,
    ILogger<DefaultEmbeddingGeneratorFactory>? logger = null) : IEmbeddingGeneratorFactory
{
    private readonly ILogger<DefaultEmbeddingGeneratorFactory> _logger =
        logger ?? NullLogger<DefaultEmbeddingGeneratorFactory>.Instance;

    // Only override generators are cached; the no-override path returns the shared
    // default singleton, so there is nothing to cache (and ConcurrentDictionary
    // cannot store the null-default case).
    private readonly ConcurrentDictionary<string, IEmbeddingGenerator<string, Embedding<float>>> _overrides = new();

    public IEmbeddingGenerator<string, Embedding<float>>? Create(string purpose)
    {
        // Config keys are case-insensitive, so the lowercase purpose matches
        // "AI:Embedding:Routing:Model" as authored.
        var model = configuration.GetValue<string>($"AI:Embedding:{purpose}:Model");
        if (string.IsNullOrWhiteSpace(model))
        {
            return defaultGenerator; // exact current behaviour
        }

        return _overrides.GetOrAdd(purpose, _ =>
        {
            var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? OllamaProvider.DefaultBaseUrl;
            _logger.LogInformation(
                "Embedding purpose '{Purpose}' uses override model '{Model}' at {BaseUrl} (decoupled from the default embedder).",
                purpose, model, baseUrl);
            return OllamaProvider.CreateEmbeddingGenerator(baseUrl, model);
        });
    }
}
