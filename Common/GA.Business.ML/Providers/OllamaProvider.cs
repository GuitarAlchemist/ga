namespace GA.Business.ML.Providers;

using Microsoft.Extensions.AI;

/// <summary>
///     Provider factory for creating Ollama-based MEAI clients.
/// </summary>
/// <remarks>
///     <para>
///         Ollama provides local LLM inference for development and privacy-sensitive deployments.
///         This provider wraps Ollama's embedding capability for text (non-musical) content.
///     </para>
///     <para>
///         Common embedding models:
///         <list type="bullet">
///             <item>nomic-embed-text (default - 768 dimensions)</item>
///             <item>mxbai-embed-large (1024 dimensions)</item>
///             <item>all-minilm (384 dimensions)</item>
///         </list>
///     </para>
/// </remarks>
public static class OllamaProvider
{
    /// <summary>
    ///     Default Ollama base URL.
    /// </summary>
    public const string DefaultBaseUrl = "http://localhost:11434";

    /// <summary>
    ///     Default chat model.
    /// </summary>
    public const string DefaultChatModel = "llama3.2:3b";

    /// <summary>
    ///     Default embedding model.
    /// </summary>
    public const string DefaultEmbeddingModel = "nomic-embed-text";

    /// <summary>
    ///     Creates an <see cref="IChatClient" /> using Ollama.
    /// </summary>
    /// <param name="baseUrl">The Ollama base URL (default: http://localhost:11434).</param>
    /// <param name="model">The model to use (default: llama3.2:3b).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="httpClient">Optional pre-configured <see cref="HttpClient" /> (e.g. carrying Cloudflare Access headers); a default client is used when null.</param>
    /// <returns>An IChatClient configured for Ollama.</returns>
    public static IChatClient CreateChatClient(
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null,
        HttpClient? httpClient = null)
    {
        var uri = new Uri(baseUrl ?? DefaultBaseUrl);
        var modelId = model ?? DefaultChatModel;

        logger?.LogInformation("Creating Ollama chat client at {Uri} for model: {Model}", uri, modelId);

        return httpClient is null
            ? new OllamaChatClient(uri, modelId)
            : new OllamaChatClient(uri, modelId, httpClient);
    }

    /// <summary>
    ///     Creates an <see cref="IEmbeddingGenerator{TInput, TEmbedding}" /> for text embeddings using Ollama.
    /// </summary>
    /// <param name="baseUrl">The Ollama base URL (default: http://localhost:11434).</param>
    /// <param name="model">The embedding model to use (default: nomic-embed-text).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="httpClient">Optional pre-configured <see cref="HttpClient" /> (e.g. carrying Cloudflare Access headers); a default client is used when null.</param>
    /// <returns>An embedding generator configured for Ollama.</returns>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null,
        HttpClient? httpClient = null)
    {
        var uri = new Uri(baseUrl ?? DefaultBaseUrl);
        var modelId = model ?? DefaultEmbeddingModel;

        logger?.LogInformation("Creating Ollama embedding generator at {Uri} for model: {Model}", uri, modelId);

        return httpClient is null
            ? new OllamaEmbeddingGenerator(uri, modelId)
            : new OllamaEmbeddingGenerator(uri, modelId, httpClient);
    }

    /// <summary>
    ///     Creates a chat client from configuration.
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
        return CreateChatClient(baseUrl, model, logger, BuildAccessHttpClient(configuration, baseUrl));
    }

    /// <summary>
    ///     Creates an embedding generator from configuration.
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
        return CreateEmbeddingGenerator(baseUrl, model, logger, BuildAccessHttpClient(configuration, baseUrl));
    }

    /// <summary>
    ///     Builds an <see cref="HttpClient" /> carrying Cloudflare Access service-token
    ///     headers when <c>Ollama:AccessClientId</c> / <c>Ollama:AccessClientSecret</c> are
    ///     configured AND the target endpoint is HTTPS (a remote Ollama fronted by Cloudflare
    ///     Access). Returns <c>null</c> when unset or non-HTTPS — so local/dev hits a
    ///     header-less localhost Ollama exactly as before and the service token is never
    ///     transmitted in cleartext. See docs/runbooks/chatbot-qa-ollama-ci-endpoint.md.
    /// </summary>
    public static HttpClient? BuildAccessHttpClient(IConfiguration configuration, string? baseUrl = null)
    {
        var (clientId, clientSecret) = ReadAccessCredentials(configuration);
        if (clientId is null || clientSecret is null) return null;

        var url = baseUrl ?? configuration.GetValue<string>("Ollama:BaseUrl") ?? DefaultBaseUrl;
        if (!IsHttps(url)) return null; // never transmit the service token over plaintext

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("CF-Access-Client-Id", clientId);
        httpClient.DefaultRequestHeaders.Add("CF-Access-Client-Secret", clientSecret);
        return httpClient;
    }

    /// <summary>
    ///     Attaches Cloudflare Access service-token headers to an already-configured named
    ///     <see cref="HttpClient" /> (the registered "ollama" / "Ollama" clients) when the
    ///     access credentials are set and the client's <see cref="HttpClient.BaseAddress" />
    ///     is HTTPS. No-op otherwise — keeps the header-less localhost path unchanged and
    ///     never leaks the token to a plaintext endpoint. Call after BaseAddress is set.
    /// </summary>
    public static void TryAddAccessHeaders(HttpClient client, IConfiguration configuration)
    {
        var (clientId, clientSecret) = ReadAccessCredentials(configuration);
        if (clientId is null || clientSecret is null) return;
        if (client.BaseAddress?.Scheme != Uri.UriSchemeHttps) return;

        client.DefaultRequestHeaders.Remove("CF-Access-Client-Id");
        client.DefaultRequestHeaders.Add("CF-Access-Client-Id", clientId);
        client.DefaultRequestHeaders.Remove("CF-Access-Client-Secret");
        client.DefaultRequestHeaders.Add("CF-Access-Client-Secret", clientSecret);
    }

    private static (string? Id, string? Secret) ReadAccessCredentials(IConfiguration configuration)
    {
        var clientId = configuration.GetValue<string>("Ollama:AccessClientId");
        var clientSecret = configuration.GetValue<string>("Ollama:AccessClientSecret");
        return string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)
            ? (null, null)
            : (clientId, clientSecret);
    }

    private static bool IsHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>
    ///     Checks if Ollama is available by attempting to connect.
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
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
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
///     Provides combined embedding support using both musical (OPTIC-K) and text (Ollama) embeddings.
/// </summary>
/// <remarks>
///     <para>
///         This service intelligently routes embedding requests:
///         <list type="bullet">
///             <item>Musical content (ChordVoicingRagDocument) → OPTIC-K embeddings via MusicalEmbeddingBridge</item>
///             <item>Text content (strings) → Ollama/GitHub Models text embeddings</item>
///         </list>
///     </para>
/// </remarks>
public sealed class HybridEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> textEmbeddingGenerator,
    MusicalEmbeddingBridge? musicalEmbeddingBridge = null,
    ILogger<HybridEmbeddingService>? logger = null) : IDisposable
{
    private readonly ILogger<HybridEmbeddingService>? _logger = logger;

    /// <summary>
    ///     Gets the text embedding generator.
    /// </summary>
    public IEmbeddingGenerator<string, Embedding<float>> TextEmbeddings { get; } = textEmbeddingGenerator ?? throw new ArgumentNullException(nameof(textEmbeddingGenerator));

    /// <summary>
    ///     Gets the musical embedding bridge (if available).
    /// </summary>
    public MusicalEmbeddingBridge? MusicalEmbeddings { get; } = musicalEmbeddingBridge;

    /// <inheritdoc />
    public void Dispose()
    {
        MusicalEmbeddings?.Dispose();
        if (TextEmbeddings is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    ///     Generates text embeddings for the provided strings.
    /// </summary>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateTextEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("Generating text embeddings for {Count} texts", texts.Count());
        return await TextEmbeddings.GenerateAsync(texts, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Generates a single text embedding.
    /// </summary>
    public async Task<Embedding<float>> GenerateTextEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var results = await GenerateTextEmbeddingsAsync([text], cancellationToken);
        return results.First();
    }
}
