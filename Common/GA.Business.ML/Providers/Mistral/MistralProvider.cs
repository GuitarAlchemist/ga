namespace GA.Business.ML.Providers.Mistral;

using Microsoft.Extensions.AI;

/// <summary>
/// Factory for the Mistral-backed <see cref="IChatClient"/>. Mirrors the
/// Ollama/Docker/GitHub provider pattern in <see cref="GA.Business.ML.Providers"/>.
/// </summary>
public static class MistralProvider
{
    public const string DefaultBaseUrl = "https://api.mistral.ai";
    public const string DefaultChatModel = "mistral-medium-latest";
    public const string ApiKeyEnvVar = "MISTRAL_API_KEY";

    /// <summary>
    /// Resolves the Mistral API key from <c>Mistral:ApiKey</c> first, then the
    /// <c>MISTRAL_API_KEY</c> environment variable. Returns <c>null</c> if neither
    /// is set so callers can decide whether to fail loudly or skip cascade wiring.
    /// </summary>
    public static string? ResolveApiKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var fromConfig = configuration.GetValue<string>("Mistral:ApiKey");
        if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;

        var fromEnv = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }

    public static bool IsAvailable(IConfiguration configuration) =>
        ResolveApiKey(configuration) is not null;

    public static IChatClient CreateChatClientFromConfig(
        IConfiguration configuration,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = ResolveApiKey(configuration)
            ?? throw new InvalidOperationException(
                "Mistral cascade requires Mistral:ApiKey config or MISTRAL_API_KEY env var.");

        var baseUrl = configuration.GetValue<string>("Mistral:BaseUrl") ?? DefaultBaseUrl;
        var model   = configuration.GetValue<string>("Mistral:Model")   ?? DefaultChatModel;

        logger?.LogInformation("Creating Mistral chat client at {BaseUrl} for model: {Model}", baseUrl, model);

        return new MistralChatClient(apiKey, model, new Uri(baseUrl));
    }
}
