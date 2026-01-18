namespace GaChatbot.Services;

using System.Text.RegularExpressions;
using GA.Business.ML.Tabs;
using GaChatbot.Abstractions;
using GaChatbot.Models;
using GA.Business.ML.Embeddings; 
using GA.Business.ML.Retrieval;
using GA.Business.Core.Fretboard.Voicings.Search;

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
        // 1. Check if input contains a tab block
        if (IsTablature(req.Message))
        {
            return await AnalyzeTabAsync(req.Message);
        }

        // 2. Fallback to RAG
        return await ragOrchestrator.AnswerAsync(req, ct);
    }

    private bool IsTablature(string message)
    {
        // Use TabTokenizer to check for valid blocks with notes
        var blocks = tabTokenizer.Tokenize(message);
        return blocks.Any(b => b.Slices.Any(s => s.Notes.Any()));
    }

    private async Task<ChatResponse> AnalyzeTabAsync(string message)
    {
        var result = await tabAnalyzer.AnalyzeAsync(message);
        var progression = result.Events.Select(e => e.Document).ToList();

        // 1. Get Suggestions based on the last chord
        List<NextChordSuggestionService.SuggestionResult> suggestions = new();
        if (result.Events.Count > 0)
        {
            var lastChord = result.Events.Last().Document;
            suggestions = await suggestionService.SuggestNextAsync(lastChord, topK: 3);
        }

        // 2. Analyze Modulation
        var modulations = modulationAnalyzer.IdentifyTargets(progression);

        return presenter.FormatAnalysis(result, suggestions, modulations);
    }
}
