namespace GA.Business.ML.Providers;

using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI;

// Disambiguate types that exist in both Microsoft.Extensions.AI and OpenAI SDK namespaces
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MeaiEmbeddingOptions = Microsoft.Extensions.AI.EmbeddingGenerationOptions;
using OaiChatClient = OpenAI.Chat.ChatClient;
using OaiEmbeddingClient = OpenAI.Embeddings.EmbeddingClient;

/// <summary>
/// Provider factory for Docker Model Runner — the OpenAI-compatible inference API
/// bundled with Docker Desktop (port 12434, <c>/v1</c> prefix).
/// </summary>
/// <remarks>
/// Use this provider when Docker Desktop's built-in model runner is preferred over a
/// separate Ollama install. Models available:
/// <list type="bullet">
///   <item><c>docker.io/ai/mxbai-embed-large</c> (1024-dim) — higher-quality embeddings than nomic-embed-text</item>
///   <item><c>docker.io/ai/llama3.2</c> — chat inference without a separate Ollama install</item>
/// </list>
/// Configuration keys: <c>DockerModelRunner:BaseUrl</c>, <c>DockerModelRunner:ChatModel</c>,
/// <c>DockerModelRunner:EmbeddingModel</c>.
/// </remarks>
public static class DockerModelRunnerProvider
{
    public const string DefaultBaseUrl = "http://localhost:12434/v1";
    public const string DefaultChatModel = "docker.io/ai/llama3.2:latest";
    public const string DefaultEmbeddingModel = "docker.io/ai/mxbai-embed-large:latest";

    /// <summary>Creates an <see cref="IChatClient"/> backed by Docker Model Runner.</summary>
    public static IChatClient CreateChatClient(
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null)
    {
        var url = baseUrl ?? DefaultBaseUrl;
        var modelId = model ?? DefaultChatModel;
        logger?.LogInformation("Creating Docker Model Runner chat client at {Url} for model: {Model}", url, modelId);
        return new DockerModelRunnerChatClient(url, modelId);
    }

    /// <summary>Creates a chat client from <c>DockerModelRunner:*</c> config keys.</summary>
    public static IChatClient CreateChatClientFromConfig(IConfiguration configuration, ILogger? logger = null)
    {
        var baseUrl = configuration.GetValue<string>("DockerModelRunner:BaseUrl") ?? DefaultBaseUrl;
        var model = configuration.GetValue<string>("DockerModelRunner:ChatModel") ?? DefaultChatModel;
        return CreateChatClient(baseUrl, model, logger);
    }

    /// <summary>Creates an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> backed by Docker Model Runner.</summary>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null)
    {
        var url = baseUrl ?? DefaultBaseUrl;
        var modelId = model ?? DefaultEmbeddingModel;
        logger?.LogInformation("Creating Docker Model Runner embedding generator at {Url} for model: {Model}", url, modelId);
        return new DockerModelRunnerEmbeddingGenerator(url, modelId);
    }

    /// <summary>Creates an embedding generator from <c>DockerModelRunner:*</c> config keys.</summary>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGeneratorFromConfig(
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var baseUrl = configuration.GetValue<string>("DockerModelRunner:BaseUrl") ?? DefaultBaseUrl;
        var model = configuration.GetValue<string>("DockerModelRunner:EmbeddingModel") ?? DefaultEmbeddingModel;
        return CreateEmbeddingGenerator(baseUrl, model, logger);
    }

    /// <summary>Returns true if the Docker Model Runner API responds on <c>/v1/models</c>.</summary>
    public static async Task<bool> IsAvailableAsync(
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync($"{baseUrl ?? DefaultBaseUrl}/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Private MEAI implementations ──────────────────────────────────────────

    /// <summary>
    /// MEAI <see cref="IChatClient"/> backed by the OpenAI SDK pointed at Docker Model Runner.
    /// </summary>
    private sealed class DockerModelRunnerChatClient : IChatClient
    {
        private readonly OaiChatClient _chatClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public DockerModelRunnerChatClient(string baseUrl, string model)
        {
            _baseUrl = baseUrl;
            _model = model;
            var openAiClient = new OpenAIClient(
                new ApiKeyCredential("no-key"),
                new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
            _chatClient = openAiClient.GetChatClient(model);
        }

        public ChatClientMetadata Metadata => new("DockerModelRunner", new Uri(_baseUrl), _model);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<MeaiChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _chatClient.CompleteChatAsync(
                [.. messages.Select(ToOpenAiMessage)],
                cancellationToken: cancellationToken);
            return new ChatResponse([new MeaiChatMessage(ChatRole.Assistant, result.Value.Content[0].Text)]);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<MeaiChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var streaming = _chatClient.CompleteChatStreamingAsync(
                [.. messages.Select(ToOpenAiMessage)],
                cancellationToken: cancellationToken);

            await foreach (var update in streaming)
            {
                foreach (var part in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                        yield return new ChatResponseUpdate(ChatRole.Assistant, part.Text);
                }
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }

        private static OpenAI.Chat.ChatMessage ToOpenAiMessage(MeaiChatMessage msg) =>
            msg.Role == ChatRole.System    ? new OpenAI.Chat.SystemChatMessage(msg.Text ?? "") :
            msg.Role == ChatRole.Assistant ? new OpenAI.Chat.AssistantChatMessage(msg.Text ?? "") :
                                             new OpenAI.Chat.UserChatMessage(msg.Text ?? "");
    }

    /// <summary>
    /// MEAI <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> backed by the OpenAI SDK
    /// pointed at Docker Model Runner's <c>/v1/embeddings</c> endpoint.
    /// </summary>
    private sealed class DockerModelRunnerEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly OaiEmbeddingClient _embeddingClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public DockerModelRunnerEmbeddingGenerator(string baseUrl, string model)
        {
            _baseUrl = baseUrl;
            _model = model;
            var openAiClient = new OpenAIClient(
                new ApiKeyCredential("no-key"),
                new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
            _embeddingClient = openAiClient.GetEmbeddingClient(model);
        }

        public EmbeddingGeneratorMetadata Metadata => new("DockerModelRunner", new Uri(_baseUrl), _model);

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            MeaiEmbeddingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var inputs = values.ToList();
            var result = await _embeddingClient.GenerateEmbeddingsAsync(inputs, cancellationToken: cancellationToken);
            return new GeneratedEmbeddings<Embedding<float>>(
                [.. result.Value.Select(e => new Embedding<float>(e.ToFloats().ToArray()))]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
