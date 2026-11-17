// The provider's namespace (GA.Providers.Anthropic) shadows the SDK's global
// `Anthropic` namespace inside this file, so we alias to disambiguate.
namespace GA.Providers.Anthropic;

using System.Text.Json;
using global::Anthropic.Models.Messages;
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
/// shares that budget), sends <c>output_config.effort</c> (to models that accept it) from
/// <c>Anthropic:Effort</c>, marks the system prompt (and so the tool list
/// before it) for prompt caching, and never returns thinking blocks to the
/// caller.</para>
/// </remarks>
public static partial class AnthropicProvider
{
    public const string DefaultModel = "claude-sonnet-5-5";

    /// <summary>Configuration key for the chat model; <see cref="DefaultModel"/> when unset.</summary>
    public const string ModelConfigKey = "Anthropic:Model";

    /// <summary>Configuration key for the workspace id sent as <see cref="WorkspaceHeader"/>.</summary>
    public const string WorkspaceIdConfigKey = "Anthropic:WorkspaceId";

    /// <summary>Configuration key for the minimum <c>max_tokens</c> sent on every request.</summary>
    public const string MaxOutputTokensConfigKey = "Anthropic:MaxOutputTokens";

    /// <summary>
    /// Default minimum <c>max_tokens</c>. Haiku 5.5 runs adaptive thinking by
    /// default and the thinking counts inside <c>max_tokens</c>: at 300 the text
    /// came back empty, and at 2048 the agents' structured JSON was cut off
    /// mid-object (production, 2026-11-17). At $0.50/MTok output, 8192 costs at
    /// most $0.004 per call; the model stops when it is done.
    /// </summary>
    public const int DefaultMinOutputTokens = 8192;

    /// <summary>
    /// Configuration key for <c>output_config.effort</c> (<c>low</c>, <c>medium</c>,
    /// <c>high</c>); <c>low</c> when unset, which keeps the thinking short so the
    /// answer fits the budget.
    /// </summary>
    public const string EffortConfigKey = "Anthropic:Effort";

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
        var effort = ResolveEffort(configuration);
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
            // Innermost, so every request of the tool loop is shaped.
            .Use(inner => new RequestShapingChatClient(inner, model, minOutputTokens, effort))
            .Build();
    }

    /// <summary>
    /// Whether <paramref name="model"/> accepts <c>output_config.effort</c>: Opus 4.5
    /// and later, Sonnet 4.6 and later, Haiku 5 and later, and every Fable or Mythos
    /// model. Haiku 4.5 and Sonnet 4.5 reject the field with a 400, and an unknown
    /// id gets no effort rather than a rejected request. Platform prefixes
    /// (<c>us.anthropic.</c>) and date suffixes (<c>-20251001</c>) are ignored.
    /// </summary>
    internal static bool SupportsEffort(string model)
    {
        var match = ModelVersionPattern().Match(model);
        if (!match.Success)
        {
            return false;
        }

        var major = int.Parse(match.Groups["major"].Value, System.Globalization.CultureInfo.InvariantCulture);
        var minor = match.Groups["minor"].Success
            ? int.Parse(match.Groups["minor"].Value, System.Globalization.CultureInfo.InvariantCulture)
            : 0;
        var version = (major, minor);
        return match.Groups["family"].Value switch
        {
            "fable" or "mythos" => true,
            "opus" => version.CompareTo((4, 5)) >= 0,
            "sonnet" => version.CompareTo((4, 6)) >= 0,
            "haiku" => major >= 5,
            _ => false,
        };
    }

    // A one- or two-digit minor; an eight-digit date suffix is not a minor version.
    [System.Text.RegularExpressions.GeneratedRegex(@"claude-(?<family>opus|sonnet|haiku|fable|mythos)-(?<major>\d+)(?:-(?<minor>\d{1,2})(?!\d))?")]
    private static partial System.Text.RegularExpressions.Regex ModelVersionPattern();

    private static Effort ResolveEffort(IConfiguration configuration)
    {
        var configured = configuration[EffortConfigKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Effort.Low;
        }

        return configured.Trim().ToLowerInvariant() switch
        {
            "low" => Effort.Low,
            "medium" => Effort.Medium,
            "high" => Effort.High,
            _ => throw new InvalidOperationException(
                $"Invalid {EffortConfigKey} '{configured}'. Valid values: low, medium, high."),
        };
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
    /// Sets the Anthropic-only request fields the MEAI adapter cannot express
    /// through <see cref="ChatOptions"/>, via <see cref="ChatOptions.RawRepresentationFactory"/>
    /// (the binding the SDK's <c>IChatClient</c> honours, SDK 12.8.0
    /// <c>AnthropicClientExtensions.GetMessageCreateParams</c>):
    /// <c>output_config.effort</c>, and the system prompt as text blocks whose
    /// last one carries <c>cache_control</c>. The tool list precedes the system
    /// prompt in the cache prefix, so that one breakpoint caches both. When the
    /// factory supplies the params the adapter no longer fills <c>model</c> and
    /// <c>max_tokens</c>, so they are set here too. A caller-supplied factory is
    /// left untouched.
    /// </summary>
    private sealed class RequestShapingChatClient(
        IChatClient inner,
        string defaultModel,
        int minOutputTokens,
        Effort effort) : DelegatingChatClient(inner)
    {
        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var (shapedMessages, shapedOptions) = Shape(messages, options);
            return base.GetResponseAsync(shapedMessages, shapedOptions, cancellationToken);
        }

        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var (shapedMessages, shapedOptions) = Shape(messages, options);
            return base.GetStreamingResponseAsync(shapedMessages, shapedOptions, cancellationToken);
        }

        private (List<ChatMessage> Messages, ChatOptions Options) Shape(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options)
        {
            var shaped = options?.Clone() ?? new ChatOptions();
            if (shaped.RawRepresentationFactory is not null)
            {
                return ([.. messages], shaped);
            }

            List<TextBlockParam> system = [];
            List<ChatMessage> conversation = [];
            foreach (var message in messages)
            {
                if (message.Role != ChatRole.System)
                {
                    conversation.Add(message);
                    continue;
                }

                // The adapter keeps only the text of system messages; do the same.
                foreach (var text in message.Contents.OfType<TextContent>())
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        system.Add(new TextBlockParam { Text = text.Text });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(shaped.Instructions))
            {
                system.Add(new TextBlockParam { Text = shaped.Instructions });
                shaped.Instructions = null;
            }

            if (system.Count > 0)
            {
                system[^1] = system[^1] with { CacheControl = new CacheControlEphemeral() };
            }

            var model = shaped.ModelId ?? defaultModel;
            var maxTokens = shaped.MaxOutputTokens ?? minOutputTokens;
            shaped.RawRepresentationFactory = _ =>
            {
                var request = new MessageCreateParams
                {
                    Model = model,
                    MaxTokens = maxTokens,
                    Messages = [],
                };
                if (SupportsEffort(model))
                {
                    request = request with { OutputConfig = new OutputConfig { Effort = effort } };
                }

                return system.Count > 0 ? request with { System = system.ToList() } : request;
            };

            return (conversation, shaped);
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
