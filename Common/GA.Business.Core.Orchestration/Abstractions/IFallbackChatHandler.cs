namespace GA.Business.Core.Orchestration.Abstractions;

/// <summary>
/// Last-resort answerer used by
/// <see cref="GA.Business.Core.Orchestration.Services.FallbackChatApplicationService"/>
/// when the orchestrator returns low-confidence garbage AND no deterministic
/// tool indicated a hard failure (i.e. the orchestrator failed in a way the
/// fallback path is allowed to paper over).
/// </summary>
/// <remarks>
/// Intentionally narrow — string in, string out — so a host can plug in
/// any direct-chat path (Ollama, Claude, OpenAI, etc.) without inheriting
/// the full <see cref="IChatApplicationService"/> contract. The fallback
/// decorator wraps the returned text into a <c>ChatResponse</c> with
/// confidence 0 and routing method <c>fallback</c> so callers can never
/// mistake it for a grounded answer.
/// </remarks>
public interface IFallbackChatHandler
{
    /// <summary>
    /// Produce a plain-text answer. Should respect the cancellation token
    /// and complete within the fallback timeout configured on
    /// <see cref="FallbackOptions"/>.
    /// </summary>
    Task<string> AnswerAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IFallbackChatHandler"/> registered when no host has
/// supplied one. Returns a deterministic "no fallback configured" message
/// at confidence 0 so the absence of a real fallback is visible to users
/// rather than silently suppressed.
/// </summary>
public sealed class NoOpFallbackChatHandler : IFallbackChatHandler
{
    public Task<string> AnswerAsync(string message, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            "I couldn't answer that with the deterministic pipeline and no fallback handler is configured. " +
            "Please rephrase or contact an operator.");
}

/// <summary>
/// Configuration shape bound to the <c>Chatbot:Fallback</c> section.
/// </summary>
/// <remarks>
/// Default: <see cref="Enabled"/>=false. Codex CLI 2026-05-08 design
/// review explicit call: "Fallback should be config-gated off by default
/// in GaApi until tests prove deterministic-skill failures cannot be
/// papered over."
/// </remarks>
public sealed class FallbackOptions
{
    public const string SectionName = "Chatbot:Fallback";

    /// <summary>Master switch. Default false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Confidence threshold below which the orchestrator's response is
    /// considered low-confidence garbage and fallback fires. Default 0.25.
    /// </summary>
    public float MinConfidence { get; set; } = 0.25f;

    /// <summary>Hard ceiling on the fallback handler call. Default 15s.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
