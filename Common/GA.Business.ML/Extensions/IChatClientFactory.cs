namespace GA.Business.ML.Extensions;

using Microsoft.Extensions.AI;

/// <summary>
/// Provider-neutral factory that returns an <see cref="IChatClient"/> for a
/// named purpose. Centralises model + provider selection so that
/// orchestrator skills and agents can depend on <see cref="IChatClient"/>
/// alone without referencing vendor SDK types.
/// </summary>
/// <remarks>
/// <para>Purposes are intentionally named by the role they serve, not by the
/// underlying model — that lets configuration switch a purpose between
/// Anthropic, OpenAI, Ollama, or local without touching call sites.</para>
/// <para>Defined purposes (one-way door — extending this taxonomy requires
/// updating <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>
/// §"Phase 1 — provider factory cleanup"):</para>
/// <list type="bullet">
///   <item><c>default</c> — top-level chatbot conversational client.</item>
///   <item><c>skill-md</c> — SKILL.md driven skills that need tool calling and
///         tend to use a frontier model.</item>
///   <item><c>qa-architect</c> — QA Architect Tribunal verdict reasoning.</item>
///   <item><c>fast-local</c> — short, latency-sensitive prompts that should
///         never leave the box.</item>
/// </list>
/// </remarks>
public interface IChatClientFactory
{
    /// <summary>
    /// Returns an <see cref="IChatClient"/> configured for the given purpose.
    /// Implementations should cache per purpose so repeat calls return the same
    /// instance for the lifetime of the factory.
    /// </summary>
    /// <param name="purpose">One of <c>default</c>, <c>skill-md</c>,
    /// <c>qa-architect</c>, or <c>fast-local</c>.</param>
    /// <exception cref="ArgumentException">Unknown purpose.</exception>
    /// <exception cref="InvalidOperationException">Backing provider is configured
    /// for this purpose but is unavailable (e.g. missing API key). The message is
    /// safe to surface to operators; secrets are never included.</exception>
    IChatClient Create(string purpose);
}
