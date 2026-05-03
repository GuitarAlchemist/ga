namespace GA.Business.ML.Extensions;

using System.Collections.Concurrent;
using GA.Providers.Anthropic;
using Microsoft.Extensions.AI;

/// <summary>
/// Default <see cref="IChatClientFactory"/> implementation. Routes purposes to
/// either the DI-registered <see cref="IChatClient"/> (for <c>default</c> and
/// <c>fast-local</c>) or to <see cref="AnthropicProvider"/> (for <c>skill-md</c>
/// and <c>qa-architect</c>) based on configuration keys.
/// </summary>
/// <remarks>
/// <para>Backed clients are cached per purpose for the lifetime of the factory —
/// constructing a new <see cref="IChatClient"/> per call would lose the
/// connection-pool and function-invocation state baked into the underlying SDKs.</para>
/// <para>This factory does NOT itself reference any provider SDK type beyond the
/// <see cref="AnthropicProvider"/> indirection; provider DTOs must not leak across
/// the <see cref="IChatClientFactory"/> boundary.</para>
/// </remarks>
public sealed class DefaultChatClientFactory(
    IConfiguration configuration,
    IChatClient defaultClient) : IChatClientFactory
{
    private readonly ConcurrentDictionary<string, IChatClient> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IChatClient Create(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return _cache.GetOrAdd(purpose, NewClient);
    }

    private IChatClient NewClient(string purpose) => purpose.ToLowerInvariant() switch
    {
        // The default chatbot conversation client and the latency-sensitive local
        // path both use the IChatClient registered via AddGuitarAlchemistChatClient
        // — typically Ollama or Docker Model Runner in dev.
        "default"     => defaultClient,
        "fast-local"  => defaultClient,

        // Tool-calling skills backed by SKILL.md need a frontier model. Resolve
        // model name from AnthropicSkills:Model first, falling back to the shared
        // Anthropic:Model setting and finally the provider's hard-coded default.
        "skill-md"    => AnthropicProvider.CreateChatClient(
            configuration,
            configuration["AnthropicSkills:Model"]
                ?? configuration["Anthropic:Model"]
                ?? AnthropicProvider.DefaultModel),

        // QA Architect Tribunal currently shares the SKILL.md / Anthropic path.
        // Split out into its own model selection once Phase 5 lands.
        "qa-architect" => AnthropicProvider.CreateChatClient(
            configuration,
            configuration["QaArchitect:Model"]
                ?? configuration["Anthropic:Model"]
                ?? AnthropicProvider.DefaultModel),

        _ => throw new ArgumentException(
            $"Unknown chat-client purpose '{purpose}'. " +
            "Valid purposes: default, skill-md, qa-architect, fast-local.",
            nameof(purpose)),
    };
}
