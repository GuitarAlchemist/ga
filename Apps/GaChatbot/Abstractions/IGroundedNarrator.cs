namespace GaChatbot.Abstractions;

using System.Collections.Generic;
using System.Threading.Tasks;
using GaChatbot.Models;

/// <summary>
/// Interface for narrators that provide grounded natural language explanations.
/// </summary>
public interface IGroundedNarrator
{
    /// <summary>
    /// Generates a natural language narrative for the given query and candidates.
    /// </summary>
    Task<string> NarrateAsync(string query, List<CandidateVoicing> candidates);
}
