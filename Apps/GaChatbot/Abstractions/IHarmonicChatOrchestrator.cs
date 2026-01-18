using GaChatbot.Models;

namespace GaChatbot.Abstractions;

/// <summary>
/// Orchestrates the Spectral RAG pipeline:
/// 1. Parse intent
/// 2. Retrieve candidates (OPTIC-K)
/// 3. Re-rank (Phase Sphere)
/// 4. Explain (Deterministic)
/// 5. Narrate (LLM)
/// </summary>
public interface IHarmonicChatOrchestrator
{
    /// <summary>
    /// Processes a natural language query and returns a grounded, narrated response.
    /// </summary>
    Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default);
}
