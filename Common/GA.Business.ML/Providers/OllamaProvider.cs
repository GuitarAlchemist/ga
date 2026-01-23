namespace GA.Business.ML.Providers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provider factory for creating Ollama-based MEAI clients.
/// </summary>
/// <remarks>
/// <para>
/// Ollama provides local LLM inference for development and privacy-sensitive deployments.
/// This provider wraps Ollama's embedding capability for text (non-musical) content.
/// </para>
/// <para>
/// Common embedding models:
/// <list type="bullet">
///   <item>nomic-embed-text (default - 768 dimensions)</item>
///   <item>mxbai-embed-large (1024 dimensions)</item>
///   <item>all-minilm (384 dimensions)</item>
/// </list>
/// </para>
/// </remarks>
public static class OllamaProvider
{
    /// <summary>
    /// Default Ollama base URL.
    /// </summary>
    public const string DefaultBaseUrl = "http://localhost:11434";

    /// <summary>
    /// Default chat model.
    /// </summary>
    public const string DefaultChatModel = "llama3.2:3b";

    /// <summary>
    /// Default embedding model.
    /// </summary>
    public const string DefaultEmbeddingModel = "nomic-embed-text";

    /// <summary>
    /// Creates an <see cref="IChatClient"/> using Ollama.
    /// </summary>
    /// <param name="baseUrl">The Ollama base URL (default: http://localhost:11434).</param>
    /// <param name="model">The model to use (default: llama3.2:3b).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>An IChatClient configured for Ollama.</returns>
    public static IChatClient CreateChatClient(
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null)
    {
        var uri = new Uri(baseUrl ?? DefaultBaseUrl);
        var modelId = model ?? DefaultChatModel;

        logger?.LogInformation("Creating Ollama chat client at {Uri} for model: {Model}", uri, modelId);

        return new OllamaChatClient(uri, modelId);
    }

    /// <summary>
    /// Creates an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for text embeddings using Ollama.
    /// </summary>
    /// <param name="baseUrl">The Ollama base URL (default: http://localhost:11434).</param>
    /// <param name="model">The embedding model to use (default: nomic-embed-text).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>An embedding generator configured for Ollama.</returns>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null)
    {
        var uri = new Uri(baseUrl ?? DefaultBaseUrl);
        var modelId = model ?? DefaultEmbeddingModel;

        logger?.LogInformation("Creating Ollama embedding generator at {Uri} for model: {Model}", uri, modelId);

        return new OllamaEmbeddingGenerator(uri, modelId);
    }

    /// <summary>
    /// Creates a chat client from configuration.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>An IChatClient configured from the Ollama section.</returns>
    public static IChatClient CreateChatClientFromConfig(
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? DefaultBaseUrl;
        var model = configuration.GetValue<string>("Ollama:ChatModel") ?? DefaultChatModel;
        return CreateChatClient(baseUrl, model, logger);
    }

    /// <summary>
    /// Creates an embedding generator from configuration.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="logger">Optional logger.</param>
    /// <returns>An embedding generator configured from the Ollama section.</returns>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGeneratorFromConfig(
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var baseUrl = configuration.GetValue<string>("Ollama:BaseUrl") ?? DefaultBaseUrl;
        var model = configuration.GetValue<string>("Ollama:EmbeddingModel") ?? DefaultEmbeddingModel;
        return CreateEmbeddingGenerator(baseUrl, model, logger);
    }

    /// <summary>
    /// Checks if Ollama is available by attempting to connect.
    /// </summary>
    /// <param name="baseUrl">The Ollama base URL to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if Ollama is responding.</returns>
    public static async Task<bool> IsAvailableAsync(
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            var uri = new Uri(baseUrl ?? DefaultBaseUrl);
            var response = await httpClient.GetAsync(uri, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Provides combined embedding support using both musical (OPTIC-K) and text (Ollama) embeddings.
/// </summary>
/// <remarks>
/// <para>
/// This service intelligently routes embedding requests:
/// <list type="bullet">
///   <item>Musical content (VoicingDocument) → OPTIC-K embeddings via MusicalEmbeddingBridge</item>
///   <item>Text content (strings) → Ollama/GitHub Models text embeddings</item>
/// </list>
/// </para>
/// </remarks>
public sealed class HybridEmbeddingService : IDisposable
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _textEmbeddingGenerator;
    private readonly Embeddings.MusicalEmbeddingBridge? _musicalEmbeddingBridge;
    private readonly ILogger<HybridEmbeddingService>? _logger;

    /// <summary>
    /// Initializes a new hybrid embedding service.
    /// </summary>
    /// <param name="textEmbeddingGenerator">Generator for text embeddings.</param>
    /// <param name="musicalEmbeddingBridge">Optional generator for musical embeddings.</param>
    /// <param name="logger">Optional logger.</param>
    public HybridEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> textEmbeddingGenerator,
        Embeddings.MusicalEmbeddingBridge? musicalEmbeddingBridge = null,
        ILogger<HybridEmbeddingService>? logger = null)
    {
        _textEmbeddingGenerator = textEmbeddingGenerator ?? throw new ArgumentNullException(nameof(textEmbeddingGenerator));
        _musicalEmbeddingBridge = musicalEmbeddingBridge;
        _logger = logger;
    }

    /// <summary>
    /// Gets the text embedding generator.
    /// </summary>
    public IEmbeddingGenerator<string, Embedding<float>> TextEmbeddings => _textEmbeddingGenerator;

    /// <summary>
    /// Gets the musical embedding bridge (if available).
    /// </summary>
    public Embeddings.MusicalEmbeddingBridge? MusicalEmbeddings => _musicalEmbeddingBridge;

    /// <summary>
    /// Generates text embeddings for the provided strings.
    /// </summary>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateTextEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Generating text embeddings for {Count} texts", texts.Count());
        return await _textEmbeddingGenerator.GenerateAsync(texts, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Generates a single text embedding.
    /// </summary>
    public async Task<Embedding<float>> GenerateTextEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var results = await GenerateTextEmbeddingsAsync([text], cancellationToken);
        return results.First();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _musicalEmbeddingBridge?.Dispose();
        if (_textEmbeddingGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
