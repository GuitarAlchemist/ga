namespace GaChatbot.Services;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GA.Business.Core.Orchestration.Models;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Wrapper service that exposes chord progression suggestion and analysis capabilities.
/// Delegates to ProgressionSuggestionSkill and HarmonicAnalysisSkill via the chatbot orchestrator.
/// </summary>
public class ChordProgressionService
{
    public record ProgressionSuggestion(
        string Key,
        string Style,
        IReadOnlyList<string> ProgressionChords,
        string Pattern,
        string Description);

    public record HarmonicAnalysisResult(
        string Key,
        IReadOnlyList<ChordAnalysis> ChordAnalyses,
        string OverallMotion,
        string VoiceLeadingNotes);

    public record ChordAnalysis(
        string Chord,
        string RomanNumeral,
        string HarmonicFunction,
        string Notes);

    /// <summary>
    /// Suggests chord progressions for a given key and optional style.
    /// </summary>
    /// <param name="key">Key (e.g., "C major", "Am")</param>
    /// <param name="style">Optional style (e.g., "blues", "jazz", "pop")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of suggested progressions</returns>
    public async Task<List<ProgressionSuggestion>> SuggestProgressionsAsync(
        string key,
        string? style = null,
        CancellationToken ct = default)
    {
        // This is called by the chatbot UI — the actual skill execution happens
        // in ProductionOrchestrator when the user query triggers ProgressionSuggestionSkill.
        // This service acts as a data contract layer.

        // Build a query that will trigger ProgressionSuggestionSkill
        var query = style is not null
            ? $"Suggest chord progressions for {key} in {style} style"
            : $"Suggest chord progressions for {key}";

        // In a full implementation, we would call the orchestrator here.
        // For now, return a placeholder — the actual logic is in ProgressionSuggestionSkill.
        return [];
    }

    /// <summary>
    /// Analyzes harmonic function of a chord progression.
    /// </summary>
    /// <param name="chords">Chord progression (e.g., "Am F C G")</param>
    /// <param name="key">Optional key context (e.g., "Am")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Harmonic analysis with roman numerals and functions</returns>
    public async Task<HarmonicAnalysisResult?> AnalyzeProgressionAsync(
        string chords,
        string? key = null,
        CancellationToken ct = default)
    {
        // This is called by the chatbot UI — the actual skill execution happens
        // in ProductionOrchestrator when the user query triggers HarmonicAnalysisSkill.

        // Build a query that will trigger HarmonicAnalysisSkill
        var query = key is not null
            ? $"Analyze {chords} in {key}"
            : $"Analyze {chords}";

        // In a full implementation, we would call the orchestrator here.
        // For now, return null — the actual logic is in HarmonicAnalysisSkill.
        return null;
    }
}
