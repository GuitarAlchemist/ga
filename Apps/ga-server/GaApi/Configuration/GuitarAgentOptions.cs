namespace GaApi.Configuration;

/// <summary>
///     Configuration options for the Microsoft Agent Framework powered guitar agents.
/// </summary>
public sealed class GuitarAgentOptions
{
    public const string SectionName = "GuitarAgents";

    /// <summary>
    ///     Optional base URL for the agent chat client (defaults to Ollama:BaseUrl if not specified).
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    ///     Optional model identifier (defaults to Ollama:ChatModel if not provided).
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    ///     Sampling temperature used when invoking the agent.
    /// </summary>
    public float Temperature { get; set; } = 0.65f;

    /// <summary>
    ///     TopP nucleus sampling parameter.
    /// </summary>
    public float TopP { get; set; } = 0.9f;

    /// <summary>
    ///     Maximum number of tokens the agent should emit per response.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 600;

    /// <summary>
    ///     Timeout for agent calls, in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>
    ///     When true, exposes the raw agent text alongside the structured payload.
    /// </summary>
    public bool IncludeRawOutput { get; set; } = true;

    /// <summary>
    ///     Flag used to toggle the post-response quality pass.
    /// </summary>
    public bool EnableQualityPass { get; set; } = true;
}
