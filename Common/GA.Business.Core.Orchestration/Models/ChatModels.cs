namespace GA.Business.Core.Orchestration.Models;

using GA.Domain.Core.Theory.Harmony.Progressions;

/// <summary>
/// A user request to the chatbot orchestrator.
/// </summary>
/// <param name="Message">The raw natural language query.</param>
/// <param name="SessionId">Optional session identifier for history.</param>
/// <param name="KeyContext">Optional key context (e.g., "C Major") to ground phases/functions.</param>
public sealed record ChatRequest(
    string Message,
    string? SessionId = null,
    string? KeyContext = null
);

/// <summary>
/// Routing decision made by SemanticRouter for the current request.
/// <c>null</c> means routing was not performed (e.g., fallback path was taken directly).
/// </summary>
public sealed record AgentRoutingMetadata(
    string AgentId,
    float Confidence,
    string RoutingMethod
);

/// <summary>
/// Structured search constraints extracted from the user query via LLM.
/// </summary>
public sealed class QueryFilters
{
    public string? Intent { get; set; }
    public string? Quality { get; set; }
    public string? Extension { get; set; }
    public string? StackingType { get; set; }
    public int? NoteCount { get; set; }

    /// <summary>
    /// Musical key identified in the query, e.g. "G major" or "B minor".
    /// Populated when the user asks about diatonic chords or key-specific theory.
    /// </summary>
    public string? Key { get; set; }
}

/// <summary>
/// Self-contained explanation of a voicing — mirrors VoicingExplanation from GA.Business.ML
/// without taking a direct dependency on the ML layer.
/// Map from VoicingExplanation at the Orchestration service boundary.
/// </summary>
public sealed record VoicingExplanationDto(
    string Summary,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Techniques,
    IReadOnlyList<string> Styles,
    double? SpectralCentroid
);

/// <summary>
/// A single candidate voicing retrieved from OPTIC-K.
/// </summary>
public sealed record CandidateVoicing(
    string Id,
    string DisplayName,
    string Shape,
    double Score,
    VoicingExplanationDto ExplanationFacts,
    string ExplanationText
);

/// <summary>
/// The final response from a chatbot orchestrator.
/// </summary>
/// <param name="NaturalLanguageAnswer">The LLM-generated narrative.</param>
/// <param name="Candidates">The grounded candidates used in the answer.</param>
/// <param name="Progression">Optional chord progression extracted from the response.</param>
/// <param name="Routing">Agent routing decision; null if routing was not invoked.</param>
/// <param name="QueryFilters">Extracted query constraints; null if filter extraction was not run.</param>
/// <param name="DebugParams">Optional debug info (weights used, filter logic).</param>
public sealed record ChatResponse(
    string NaturalLanguageAnswer,
    IReadOnlyList<CandidateVoicing> Candidates,
    Progression? Progression = null,
    AgentRoutingMetadata? Routing = null,
    QueryFilters? QueryFilters = null,
    object? DebugParams = null
);
