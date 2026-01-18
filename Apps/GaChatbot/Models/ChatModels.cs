namespace GaChatbot.Models;

/// <summary>
/// A user request to the spectral RAG chatbot.
/// </summary>
/// <param name="Message">The raw natural language query (e.g., "Give me a jazzier Dm7").</param>
/// <param name="SessionId">Optional session identifier for history.</param>
/// <param name="KeyContext">Optional key context (e.g., "C Major") to ground phases/functions.</param>
public sealed record ChatRequest(
    string Message, 
    string? SessionId = null,
    string? KeyContext = null
);

/// <summary>
/// A single candidate voicing retrieved from OPTIC-K.
/// </summary>
/// <param name="Id">Unique identifier of the voicing.</param>
/// <param name="DisplayName">Canonical name (e.g., "Dm7").</param>
/// <param name="Shape">Fretboard shape (e.g., "x-5-7-5-6-5").</param>
/// <param name="Score">Re-ranked geometric match score [0-1].</param>
/// <param name="ExplanationFacts">Structured reasoning facts from VoicingExplanationService.</param>
/// <param name="ExplanationText">Pre-generated human-readable summary.</param>
public sealed record CandidateVoicing(
    string Id,
    string DisplayName,
    string Shape,
    double Score,
    GA.Business.ML.Musical.Explanation.VoicingExplanation ExplanationFacts,
    string ExplanationText
);

/// <summary>
/// The final response from the chatbot orchestrator.
/// </summary>
/// <param name="NaturalLanguageAnswer">The LLM-generated narrative.</param>
/// <param name="Candidates">The list of grounded candidates used in the answer.</param>
/// <param name="DebugParams">Optional debug info (weights used, filter logic).</param>
public sealed record ChatResponse(
    string NaturalLanguageAnswer,
    IReadOnlyList<CandidateVoicing> Candidates,
    GA.Business.Core.Progressions.Progression? Progression = null,
    object? DebugParams = null
);
