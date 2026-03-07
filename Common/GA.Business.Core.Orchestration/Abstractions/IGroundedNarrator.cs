namespace GA.Business.Core.Orchestration.Abstractions;

using GA.Business.Core.Orchestration.Models;

/// <summary>
/// Interface for narrators that provide grounded natural language explanations.
/// </summary>
public interface IGroundedNarrator
{
    /// <summary>
    /// Generates a natural language narrative for the given query and candidates.
    /// </summary>
    Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates);
}
