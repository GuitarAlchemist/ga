namespace GA.Business.ML.Providers;

using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI;

// Disambiguate types that exist in both Microsoft.Extensions.AI and OpenAI SDK namespaces.
using MeaiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using OaiChatClient = OpenAI.Chat.ChatClient;

/// <summary>
/// Provider factory for Inception Labs' Mercury 2 — a diffusion-based LLM positioned for
/// the "subagent" tier: cheap, low-latency, structured-output tasks like query extraction,
/// routing, and tool selection. Reports ~5× speed vs Sonnet 4.6 at matched quality on the
/// 2026-05-13 benchmark for <see cref="GA.Business.ML.Search.LlmMusicalQueryExtractor"/>
/// (median 590 ms / p95 848 ms on 8 representative voicing queries).
/// </summary>
/// <remarks>
/// <para>
/// API is OpenAI-compatible — we reuse the OpenAI SDK and just point its endpoint at
/// <c>https://api.inceptionlabs.ai/v1</c>. Same pattern as
/// <see cref="DockerModelRunnerProvider"/> but with a real bearer token instead of
/// "no-key".
/// </para>
/// <para>
/// <b>Configuration keys:</b>
/// <list type="bullet">
///   <item><c>Inception:ApiKey</c> — bearer token. Read from config first; falls back to
///     the <c>INCEPTION_API_KEY</c> environment variable. Required; the factory throws if
///     neither is set.</item>
///   <item><c>Inception:BaseUrl</c> — overrides the default endpoint (used in tests).</item>
///   <item><c>Inception:ChatModel</c> — overrides the default model (used to pin a specific
///     version once Mercury 2.x ships).</item>
/// </list>
/// </para>
/// <para>
/// <b>Subagent-only scope.</b> This provider is intentionally NOT a drop-in for the main
/// chat loop — the article and our benchmark are about the utility-operations tier. Wire
/// it through a <em>keyed</em> service registration (key: <c>"subagent"</c>) so the
/// chatbot's primary <see cref="IChatClient"/> stays on Anthropic Haiku / Claude where
/// tool-use reliability matters more than per-call latency.
/// </para>
/// </remarks>
public static class InceptionProvider
{
    public const string DefaultBaseUrl = "https://api.inceptionlabs.ai/v1";
    public const string DefaultChatModel = "mercury-2";

    /// <summary>Service-collection key used for the subagent <see cref="IChatClient"/> registration.</summary>
    public const string SubagentServiceKey = "subagent";

    /// <summary>Configuration section name (e.g. <c>"Inception:ApiKey"</c>).</summary>
    public const string ConfigSection = "Inception";

    /// <summary>Environment variable consulted when <c>Inception:ApiKey</c> is unset in config.</summary>
    public const string ApiKeyEnvVar = "INCEPTION_API_KEY";

    /// <summary>
    /// Creates an <see cref="IChatClient"/> backed by Inception Labs Mercury 2.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no API key is found in config or the <c>INCEPTION_API_KEY</c> env var.
    /// Eager construction surfaces this at host startup so a missing key doesn't appear
    /// as an opaque 500 on the first user query — same lesson as PR #151's Anthropic fix.
    /// </exception>
    public static IChatClient CreateChatClient(
        string apiKey,
        string? baseUrl = null,
        string? model = null,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Inception API key is required. Set Inception:ApiKey in config or the " +
                $"{ApiKeyEnvVar} environment variable.");

        var url = baseUrl ?? DefaultBaseUrl;
        var modelId = model ?? DefaultChatModel;
        logger?.LogInformation("Creating Inception (Mercury) chat client at {Url} for model: {Model}", url, modelId);
        return new InceptionChatClient(apiKey, url, modelId);
    }

    /// <summary>
    /// Creates an <see cref="IChatClient"/> from <c>Inception:*</c> config keys, with
    /// <c>INCEPTION_API_KEY</c> env var as a fallback for the bearer token.
    /// </summary>
    public static IChatClient CreateChatClientFromConfig(IConfiguration configuration, ILogger? logger = null)
    {
        var apiKey = configuration.GetValue<string>($"{ConfigSection}:ApiKey")
                     ?? Environment.GetEnvironmentVariable(ApiKeyEnvVar)
                     ?? string.Empty;
        var baseUrl = configuration.GetValue<string>($"{ConfigSection}:BaseUrl") ?? DefaultBaseUrl;
        var model = configuration.GetValue<string>($"{ConfigSection}:ChatModel") ?? DefaultChatModel;
        return CreateChatClient(apiKey, baseUrl, model, logger);
    }

    /// <summary>
    /// Returns true if the configured API key is set (either in config or the env var).
    /// Used by DI registration to decide whether to wire the subagent provider at all —
    /// missing key means no keyed registration.
    /// </summary>
    public static bool IsConfigured(IConfiguration configuration)
    {
        var apiKey = configuration.GetValue<string>($"{ConfigSection}:ApiKey")
                     ?? Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    /// <summary>
    /// Returns true if the operator has explicitly opted-in to routing
    /// <see cref="GA.Business.ML.Search.LlmMusicalQueryExtractor"/> through Mercury for
    /// fuzzy-query extraction. Requires BOTH a configured API key (see
    /// <see cref="IsConfigured"/>) AND <c>Inception:EnableForQueryExtraction = true</c>.
    /// </summary>
    /// <remarks>
    /// Two-flag gate is deliberate: a present API key alone (e.g. from a stray env var or
    /// a copy-pasted config block) MUST NOT silently re-route the chatbot's query
    /// extraction. Operators flipping <c>EnableForQueryExtraction</c> are making an
    /// explicit, observable change — same shape as <c>AI:ChatProvider=claude</c>.
    /// </remarks>
    public static bool IsEnabledForQueryExtraction(IConfiguration configuration)
    {
        if (!IsConfigured(configuration)) return false;
        return configuration.GetValue<bool>($"{ConfigSection}:EnableForQueryExtraction");
    }

    // ── Private MEAI implementation ───────────────────────────────────────────

    /// <summary>
    /// MEAI <see cref="IChatClient"/> backed by the OpenAI SDK pointed at Inception's
    /// chat-completions endpoint. Honors <see cref="ChatOptions.Temperature"/> and
    /// <see cref="ChatOptions.MaxOutputTokens"/>; Mercury 2 publishes
    /// <c>supported_sampling_parameters: ["temperature", "stop"]</c> in <c>/v1/models</c>
    /// so the SDK passes those through and ignores other knobs.
    /// </summary>
    private sealed class InceptionChatClient : IChatClient
    {
        private readonly OaiChatClient _chatClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public InceptionChatClient(string apiKey, string baseUrl, string model)
        {
            _baseUrl = baseUrl;
            _model = model;
            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
            _chatClient = openAiClient.GetChatClient(model);
        }

        public ChatClientMetadata Metadata => new("Inception", new Uri(_baseUrl), _model);

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
}
