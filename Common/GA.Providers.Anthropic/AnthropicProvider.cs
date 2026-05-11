// The provider's namespace (GA.Providers.Anthropic) shadows the SDK's global
// `Anthropic` namespace inside this file, so we alias to disambiguate.
namespace GA.Providers.Anthropic;

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
/// </remarks>
public static class AnthropicProvider
{
    public const string DefaultModel = "claude-sonnet-4-6";

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
    /// from <c>Anthropic:ApiKey</c> first, then <c>ANTHROPIC_API_KEY</c>.</param>
    /// <param name="model">Anthropic model name (e.g. <c>claude-sonnet-4-6</c>).
    /// Falls back to <see cref="DefaultModel"/> when null/empty.</param>
    /// <param name="timeout">HTTP timeout for chat requests. Default is 100s
    /// (Anthropic SDK default). Long-output tasks (curator, summarizer with
    /// high <c>MaxOutputTokens</c>, etc.) should pass a longer value — 32k
    /// tokens of Sonnet output is typically 60–150 s wall-clock.
    /// Discovered necessary by the memory-curator end-to-end smoke 2026-05-11.</param>
    /// <exception cref="InvalidOperationException">Thrown when no API key is
    /// reachable. The message is safe to surface to operators (no secrets).</exception>
    public static IChatClient CreateChatClient(
        IConfiguration configuration,
        string? model = null,
        TimeSpan? timeout = null)
    {
        var apiKey = ResolveApiKey(configuration);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Anthropic chat client requested but no API key is configured. " +
                "Set ANTHROPIC_API_KEY (environment) or Anthropic:ApiKey (configuration).");
        }

        var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;

        // The Anthropic SDK auto-creates an HttpClient with the .NET default
        // 100 s timeout if you don't supply one. AnthropicClient.Timeout
        // alone is not enough — that's the SDK-level timeout, but the
        // underlying HttpClient.Timeout fires first. Supply a custom client
        // with the longer timeout baked in.
        AnthropicSdk.AnthropicClient anthropicClient;
        if (timeout is { } t)
        {
            var http = new HttpClient { Timeout = t };
            anthropicClient = new AnthropicSdk.AnthropicClient
            {
                ApiKey = apiKey,
                HttpClient = http,
                Timeout = t,
            };
        }
        else
        {
            anthropicClient = new AnthropicSdk.AnthropicClient { ApiKey = apiKey };
        }

        return anthropicClient
            .AsIChatClient(resolvedModel)
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
    }

    private static string? ResolveApiKey(IConfiguration configuration) =>
        configuration["Anthropic:ApiKey"]
        ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
}
