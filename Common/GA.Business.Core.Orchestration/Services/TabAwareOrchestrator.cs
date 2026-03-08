namespace GA.Business.Core.Orchestration.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;

/// <summary>
/// Orchestrator that checks for tablature input and routes to tab analysis,
/// falling back to the Spectral RAG orchestrator for knowledge queries.
/// </summary>
public class TabAwareOrchestrator(
    SpectralRagOrchestrator ragOrchestrator,
    TabTokenizer tabTokenizer,
    TabAnalysisService tabAnalyzer,
    TabPresentationService presenter,
    NextChordSuggestionService suggestionService,
    ModulationAnalyzer modulationAnalyzer) : IHarmonicChatOrchestrator
{
    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        if (IsTablature(req.Message))
            return await AnalyzeTabAsync(req.Message);

        return await ragOrchestrator.AnswerAsync(req, ct);
    }

    /// <inheritdoc />
    public async Task<ChatResponse> AnswerStreamingAsync(
        ChatRequest req,
        Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        // Delegate to the RAG orchestrator's streaming path when possible;
        // for tablature input we fall back to word-level simulation.
        if (IsTablature(req.Message))
        {
            var tabResponse = await AnalyzeTabAsync(req.Message);
            foreach (var word in tabResponse.NaturalLanguageAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                await onToken(word + " ");
            return tabResponse;
        }

        return await ragOrchestrator.AnswerStreamingAsync(req, onToken, ct);
    }

    private bool IsTablature(string message)
    {
        var blocks = tabTokenizer.Tokenize(message);
        return blocks.Any(b => b.Slices.Any(s => s.Notes.Any()));
    }

    private async Task<ChatResponse> AnalyzeTabAsync(string message)
    {
        var result = await tabAnalyzer.AnalyzeAsync(message);
        var progression = result.Events.Select(e => e.Document).ToList();

        List<NextChordSuggestionService.SuggestionResult> suggestions = [];
        if (result.Events.Count > 0)
        {
            var lastChord = result.Events.Last().Document;
            suggestions = await suggestionService.SuggestNextAsync(lastChord, topK: 3);
        }

        var modulations = modulationAnalyzer.IdentifyTargets(progression);
        return presenter.FormatAnalysis(result, suggestions, modulations);
    }
}
