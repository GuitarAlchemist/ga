namespace GA.Business.Core.Orchestration.Abstractions;

using GA.Business.Core.Orchestration.Models;

/// <summary>
/// Orchestrates the Spectral RAG pipeline:
/// 1. Parse intent
/// 2. Retrieve candidates (OPTIC-K)
/// 3. Re-rank (Phase Sphere)
/// 4. Explain (Deterministic)
/// 5. Narrate (LLM with anti-hallucination guardrails)
/// </summary>
public interface IHarmonicChatOrchestrator
{
    /// <summary>
    /// Processes a natural language query and returns a grounded, narrated response.
    /// </summary>
    Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default);

    /// <summary>
    /// Streams the LLM response token-by-token via <paramref name="onToken"/>,
    /// then returns the full <see cref="ChatResponse"/> with routing metadata when done.
    /// </summary>
    Task<ChatResponse> AnswerStreamingAsync(
        ChatRequest req,
        Func<string, Task> onToken,
        CancellationToken ct = default);
}
