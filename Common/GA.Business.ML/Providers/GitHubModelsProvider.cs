namespace GA.Business.ML.Providers;

using Microsoft.Extensions.AI;

/// <summary>
///     Provider factory for creating MEAI-compatible clients using GitHub Models (Azure AI Inference).
/// </summary>
/// <remarks>
///     <para>
///         GitHub Models provides free access to various AI models through the Azure AI Inference API.
///         Authentication uses a GitHub Personal Access Token (PAT) via the GITHUB_TOKEN environment variable.
///     </para>
///     <para>
///         Endpoint: https://models.inference.ai.azure.com
///     </para>
///     <para>
///         <b>Status:</b> Currently deferred due to OpenAI SDK version mismatch with MEAI.OpenAI package.
///         Will be implemented when Microsoft.Extensions.AI.OpenAI stabilizes (post 9.4.0-preview).
///     </para>
///     <para>
///         Available models include:
///         <list type="bullet">
///             <item>gpt-4o-mini (default - fast and cost-effective)</item>
///             <item>gpt-4o (more capable)</item>
///             <item>Phi-3.5-mini-instruct</item>
///             <item>text-embedding-3-small (embeddings)</item>
///         </list>
///     </para>
/// </remarks>
public static class GitHubModelsProvider
{
    /// <summary>
    ///     GitHub Models inference endpoint (Azure AI Inference).
    /// </summary>
    public const string GitHubModelsEndpoint = "https://models.inference.ai.azure.com";

    /// <summary>
    ///     Default chat model for GitHub Models.
    /// </summary>
    public const string DefaultChatModel = "gpt-4o-mini";

    /// <summary>
    ///     Default embedding model for GitHub Models.
    /// </summary>
    public const string DefaultEmbeddingModel = "text-embedding-3-small";

    /// <summary>
    ///     Creates an <see cref="IChatClient" /> using GitHub Models.
    /// </summary>
    /// <remarks>
    ///     Currently deferred - OpenAI SDK version mismatch with MEAI.OpenAI package.
    ///     Use Ollama provider for local development instead.
    /// </remarks>
    public static IChatClient CreateChatClient(
        string? model = null,
        ILogger? logger = null) => throw new NotImplementedException(
        "GitHub Models integration is deferred until Microsoft.Extensions.AI.OpenAI package " +
        "version stabilizes. Use 'ollama' provider for local development. " +
        $"Endpoint: {GitHubModelsEndpoint}, Model: {model ?? DefaultChatModel}");

    /// <summary>
    ///     Creates an embedding generator using GitHub Models.
    /// </summary>
    /// <remarks>
    ///     Currently deferred - OpenAI SDK version mismatch with MEAI.OpenAI package.
    ///     Use Ollama provider for local development instead.
    /// </remarks>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        string? model = null,
        ILogger? logger = null) => throw new NotImplementedException(
        "GitHub Models embedding integration is deferred until Microsoft.Extensions.AI.OpenAI package " +
        "version stabilizes. Use 'ollama' provider for local development.");

    /// <summary>
    ///     Creates a chat client from configuration.
    /// </summary>
    public static IChatClient CreateChatClientFromConfig(
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var model = configuration.GetValue<string>("AI:GitHub:ChatModel") ?? DefaultChatModel;
        return CreateChatClient(model, logger);
    }

    /// <summary>
    ///     Creates an embedding generator from configuration.
    /// </summary>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGeneratorFromConfig(
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var model = configuration.GetValue<string>("AI:GitHub:EmbeddingModel") ?? DefaultEmbeddingModel;
        return CreateEmbeddingGenerator(model, logger);
    }

    /// <summary>
    ///     Checks if GitHub Models is available (GITHUB_TOKEN is set).
    /// </summary>
    /// <returns>True if GITHUB_TOKEN environment variable is configured.</returns>
    public static bool IsAvailable()
    {
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return !string.IsNullOrWhiteSpace(token);
    }
}
