// The provider's namespace (GA.Providers.Anthropic) shadows the SDK's global
// `Anthropic` namespace inside this file, so we alias to disambiguate.
namespace GA.Providers.Anthropic;

using System.Text.Json;
using AnthropicSdk = global::Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Provider adapter that builds an <see cref="IChatClient"/> backed by the
/// official Anthropic C# SDK. Anthropic-specific types are kept inside this
/// project so callers can depend on <see cref="IChatClient"/> alone.
/// </summary>
/// <remarks>
/// <para>The Anthropic C# SDK is documented as official-but-beta — pin the
/// package version in <c>GA.Providers.Anthropic.csproj</c>. If a minor SDK break
/// ripples through, it must be contained here, not in skill or orchestrator
/// code (per <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>
/// §"Anthropic SDK guidance").</para>
/// <para>This is the ONLY place in the GA codebase that should reference
/// <c>Anthropic.AnthropicClient</c>. The factory boundary in
/// <c>GA.Business.ML.Extensions.IChatClientFactory</c> enforces this in code;
/// the project graph (no <c>Anthropic</c> package reference outside this csproj)
/// enforces it at build time.</para>
/// <para>Every client this class builds sends <c>anthropic-workspace-id</c>
/// when <c>Anthropic:WorkspaceId</c> is set (a key that is not scoped to a
/// workspace gets 400 on every request without it), raises
/// <c>max_tokens</c> to at least <c>Anthropic:MaxOutputTokens</c> (thinking
/// shares that budget), and never returns thinking blocks to the caller.</para>
/// </remarks>
public static class AnthropicProvider
{
    public const string DefaultModel = "claude-sonnet-5-5";

    /// <summary>Configuration key for the chat model; <see cref="DefaultModel"/> when unset.</summary>
    public const string ModelConfigKey = "Anthropic:Model";

    /// <summary>Configuration key for the workspace id sent as <see cref="WorkspaceHeader"/>.</summary>
    public const string WorkspaceIdConfigKey = "Anthropic:WorkspaceId";

    /// <summary>Configuration key for the minimum <c>max_tokens</c> sent on every request.</summary>
    public const string MaxOutputTokensConfigKey = "Anthropic:MaxOutputTokens";

    /// <summary>
    /// Default minimum <c>max_tokens</c>. Haiku 5.5 emits a thinking block before
    /// the text; at 300 the thinking used the whole budget and the text came back
    /// empty, and typical answers used 600–930 output tokens (measured 2026-11-17).
    /// </summary>
    public const int DefaultMinOutputTokens = 2048;

    /// <summary>Header that selects the workspace for a key not scoped to one.</summary>
    public const string WorkspaceHeader = "anthropic-workspace-id";

    private const string ApiVersion = "2023-06-01";

    // limit=1000 is the API maximum, so one page covers the whole model list.
    private static readonly Uri ModelsUri = new("https://api.anthropic.com/v1/models?limit=1000");

    /// <summary>
    /// Returns true if an Anthropic API key is reachable from configuration or
    /// the standard <c>ANTHROPIC_API_KEY</c> environment variable. Used by the
    /// factory to decide whether the Anthropic-backed purposes are viable.
    /// </summary>
    public static bool IsAvailable(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(ResolveApiKey(configuration));

    /// <summary>
    /// Builds an <see cref="IChatClient"/> with function-invocation enabled.
    /// </summary>
    /// <param name="configuration">Application configuration; resolves the API key
    /// from <c>Anthropic:ApiKey</c> first, then <c>ANTHROPIC_API_KEY</c>, and the
    /// optional <c>Anthropic:WorkspaceId</c> and <c>Anthropic:MaxOutputTokens</c>.</param>
    /// <param name="model">Anthropic model name (e.g. <c>claude-sonnet-5-5</c>).
    /// Falls back to <see cref="DefaultModel"/> when null/empty.</param>
    /// <param name="timeout">HTTP timeout for chat requests. Default is 100s
    /// (Anthropic SDK default). Long-output tasks (curator, summarizer with
    /// high <c>MaxOutputTokens</c>, etc.) should pass a longer value — 32k
    /// tokens of Sonnet output is typically 60–150 s wall-clock.
    /// Discovered necessary by the memory-curator end-to-end smoke 2026-05-11.
    /// <para>
    /// <b>Lifetime caveat (one-way door if violated):</b> when this parameter
    /// is set, or when <c>Anthropic:WorkspaceId</c> is configured, the method
    /// allocates a fresh <see cref="HttpClient"/> per call and the SDK reuses it.
    /// The current intended use is CLI / app-lifetime singleton consumers
    /// (e.g. <c>GaMemoryCli</c>, the DI-registered host chat client, the
    /// per-purpose cache in <c>DefaultChatClientFactory</c>). Calling this on
    /// every request from a request-scoped DI container would leak sockets and
    /// pin DNS — see PR #170 review (H1) for the rationale. If you need
    /// request-scoped clients, refactor this method to accept
    /// <c>IHttpClientFactory</c> first.
    /// </para></param>
    /// <exception cref="InvalidOperationException">Thrown when no API key is
    /// reachable. The message is safe to surface to operators (no secrets).</exception>
    public static IChatClient CreateChatClient(
        IConfiguration configuration,
        string? model = null,
        TimeSpan? timeout = null) =>
        Build(configuration, string.IsNullOrWhiteSpace(model) ? DefaultModel : model, timeout, handler: null);

    /// <summary>
    /// Resolves the chat model from <c>Anthropic:Model</c>, falling back to
    /// <see cref="DefaultModel"/> when it is unset or blank.
    /// </summary>
    public static string ResolveModel(IConfiguration configuration)
    {
        var configured = configuration[ModelConfigKey];
        return string.IsNullOrWhiteSpace(configured) ? DefaultModel : configured;
    }

    /// <summary>
    /// Builds the host's main <see cref="IChatClient"/> from configuration: key
    /// from <c>Anthropic:ApiKey</c> / <c>ANTHROPIC_API_KEY</c>, model from
    /// <c>Anthropic:Model</c> (default <see cref="DefaultModel"/>). Used when
    /// <c>AI:ChatProvider</c> or <c>AI:CascadeProvider</c> is <c>anthropic</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No API key is configured.</exception>
    public static IChatClient CreateChatClientFromConfig(IConfiguration configuration) =>
        Build(configuration, ResolveModel(configuration), timeout: null, handler: null);

    /// <summary>Test seam: same client as <see cref="CreateChatClient(IConfiguration, string?, TimeSpan?)"/> over <paramref name="handler"/>.</summary>
    internal static IChatClient CreateChatClient(
        IConfiguration configuration,
        string? model,
        TimeSpan? timeout,
        HttpMessageHandler handler) =>
        Build(configuration, string.IsNullOrWhiteSpace(model) ? DefaultModel : model, timeout, handler);

    /// <summary>Test seam: same client as <see cref="CreateChatClientFromConfig(IConfiguration)"/> over <paramref name="handler"/>.</summary>
    internal static IChatClient CreateChatClientFromConfig(IConfiguration configuration, HttpMessageHandler handler) =>
        Build(configuration, ResolveModel(configuration), timeout: null, handler);

    /// <summary>
    /// Builds <c>GET /v1/models</c> with the same authentication and workspace
    /// headers chat requests carry. A cheap readiness check: it costs no inference.
    /// </summary>
    /// <exception cref="InvalidOperationException">No API key is configured.</exception>
    public static HttpRequestMessage CreateModelsRequest(IConfiguration configuration)
    {
        var apiKey = RequireApiKey(configuration);
        var request = new HttpRequestMessage(HttpMethod.Get, ModelsUri);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);
        if (ResolveWorkspaceId(configuration) is { } workspaceId)
        {
            request.Headers.Add(WorkspaceHeader, workspaceId);
        }

        return request;
    }

    /// <summary>
    /// Sends <see cref="CreateModelsRequest"/> through <paramref name="httpClient"/>
    /// and returns the status code, the listed model ids and, on failure, the
    /// API's error message. Network failures propagate to the caller.
    /// </summary>
    public static async Task<AnthropicModelsListing> ListModelsAsync(
        IConfiguration configuration,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateModelsRequest(configuration);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new AnthropicModelsListing((int)response.StatusCode, ParseModelIds(body), null)
            : new AnthropicModelsListing((int)response.StatusCode, [], ParseErrorMessage(body));
    }

    private static IChatClient Build(
        IConfiguration configuration,
        string model,
        TimeSpan? timeout,
        HttpMessageHandler? handler)
    {
        var apiKey = RequireApiKey(configuration);
        var workspaceId = ResolveWorkspaceId(configuration);

        // The Anthropic SDK auto-creates an HttpClient with the .NET default
        // 100 s timeout if you don't supply one. AnthropicClient.Timeout
        // alone is not enough — that's the SDK-level timeout, but the
        // underlying HttpClient.Timeout fires first. Supply a custom client
        // with the longer timeout baked in.
        //
        // PR #170 review H2: AnthropicClient.Timeout is NOT set explicitly —
        // it was dead code in the original fix (the comment above admits the
        // SDK-level Timeout doesn't bind), and a future SDK rev that DOES
        // honor it could cancel requests before the longer HttpClient.Timeout,
        // masking the bug this PR fixes. HttpClient.Timeout is the single
        // source of truth. The workspace header rides on the same client as a
        // default request header, so every SDK request carries it.
        AnthropicSdk.AnthropicClient anthropicClient;
        if (timeout is not null || workspaceId is not null || handler is not null)
        {
            var http = handler is null ? new HttpClient() : new HttpClient(handler);
            if (timeout is { } t)
            {
                http.Timeout = t;
            }

            if (workspaceId is not null)
            {
                http.DefaultRequestHeaders.Add(WorkspaceHeader, workspaceId);
            }

            anthropicClient = new AnthropicSdk.AnthropicClient { ApiKey = apiKey, HttpClient = http };
        }
        else
        {
            anthropicClient = new AnthropicSdk.AnthropicClient { ApiKey = apiKey };
        }

        var minOutputTokens = ResolveMinOutputTokens(configuration);
        return anthropicClient
            .AsIChatClient(model)
            .AsBuilder()
            // Outermost, so the function-invocation loop below still sees the
            // thinking blocks the API needs echoed back on tool turns.
            .Use(inner => new TextOnlyChatClient(inner))
            .ConfigureOptions(options =>
            {
                if (options.MaxOutputTokens is not { } requested || requested < minOutputTokens)
                {
                    options.MaxOutputTokens = minOutputTokens;
                }
            })
            .UseFunctionInvocation()
            .Build();
    }

    private static string RequireApiKey(IConfiguration configuration)
    {
        var apiKey = ResolveApiKey(configuration);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Anthropic chat client requested but no API key is configured. " +
                "Set ANTHROPIC_API_KEY (environment) or Anthropic:ApiKey (configuration).");
        }

        return apiKey;
    }

    private static string? ResolveApiKey(IConfiguration configuration) =>
        configuration["Anthropic:ApiKey"]
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private static string? ResolveWorkspaceId(IConfiguration configuration)
    {
        var configured = configuration[WorkspaceIdConfigKey];
        return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    private static int ResolveMinOutputTokens(IConfiguration configuration) =>
        int.TryParse(configuration[MaxOutputTokensConfigKey], out var configured) && configured > 0
            ? configured
            : DefaultMinOutputTokens;

    private static IReadOnlyList<string> ParseModelIds(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. data.EnumerateArray()
                .Select(m => m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
                .OfType<string>()];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ParseErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("error", out var error)
                   && error.TryGetProperty("message", out var message)
                   && message.ValueKind == JsonValueKind.String
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Drops thinking content (<see cref="TextReasoningContent"/>) from responses
    /// and streaming updates, so only the model's text blocks reach callers.
    /// </summary>
    private sealed class TextOnlyChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            foreach (var message in response.Messages)
            {
                RemoveReasoning(message.Contents);
            }

            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                RemoveReasoning(update.Contents);
                yield return update;
            }
        }

        private static void RemoveReasoning(IList<AIContent> contents)
        {
            for (var i = contents.Count - 1; i >= 0; i--)
            {
                if (contents[i] is TextReasoningContent)
                {
                    contents.RemoveAt(i);
                }
            }
        }
    }
}

/// <summary>Outcome of <c>GET /v1/models</c>.</summary>
/// <param name="StatusCode">HTTP status code returned by the API.</param>
/// <param name="ModelIds">Listed model ids (empty on failure).</param>
/// <param name="ErrorMessage">The API's error message on failure, if it sent one.</param>
public sealed record AnthropicModelsListing(int StatusCode, IReadOnlyList<string> ModelIds, string? ErrorMessage)
{
    /// <summary>True for a 2xx response.</summary>
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}
