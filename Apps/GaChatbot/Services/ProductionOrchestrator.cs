using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GA.Business.ML.Tabs;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Embeddings;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Tabs.Models;
using GaChatbot.Models;

namespace GaChatbot.Services;

/// <summary>
/// Unifies all intelligent spikes (RAG, Motion, Style, Generation) 
/// into a production-grade chat entry point.
/// Part of Phase 8.1.1.
/// </summary>
public class ProductionOrchestrator
{
    private readonly TabAwareOrchestrator _tabOrchestrator;
    private readonly TabAnalysisService _tabAnalyzer;
    private readonly NextChordSuggestionService _suggestionService;
    private readonly ModulationAnalyzer _modulationAnalyzer;
    private readonly TabPresentationService _presenter;
    private readonly MusicalEmbeddingGenerator _embeddingGenerator;
    private readonly AdvancedTabSolver _tabSolver;
    private readonly AlternativeFingeringService _altService;

    public ProductionOrchestrator(
        TabAwareOrchestrator tabOrchestrator,
        TabAnalysisService tabAnalyzer,
        NextChordSuggestionService suggestionService,
        ModulationAnalyzer modulationAnalyzer,
        TabPresentationService presenter,
        MusicalEmbeddingGenerator embeddingGenerator,
        AdvancedTabSolver tabSolver,
        AlternativeFingeringService altService)
    {
        _tabOrchestrator = tabOrchestrator;
        _tabAnalyzer = tabAnalyzer;
        _suggestionService = suggestionService;
        _modulationAnalyzer = modulationAnalyzer;
        _presenter = presenter;
        _embeddingGenerator = embeddingGenerator;
        _tabSolver = tabSolver;
        _altService = altService;
    }

    public async Task<ChatResponse> AnswerAsync(ChatRequest request)
    {
        // 1. Check if the user query contains Tablature
        if (IsTabQuery(request.Message))
        {
            if (IsAskingForOptimization(request.Message))
            {
                return await HandlePathOptimizationAsync(request.Message);
            }
            return await HandleTabAnalysisAsync(request.Message);
        }

        // 2. Delegate to standard TabAwareOrchestrator (which handles RAG/Knowledge queries)
        return await _tabOrchestrator.AnswerAsync(request);
    }

    private async Task<ChatResponse> HandlePathOptimizationAsync(string query)
    {
        // Extract raw tab from query
        // Check for diagram pattern first: x-3-2-0-1-0
        var diagramMatch = System.Text.RegularExpressions.Regex.Match(query, @"([x\d]{1,2}-){5}[x\d]{1,2}");
        string tabText;

        if (diagramMatch.Success && !query.Contains("|"))
        {
            // Convert vertical diagram (Low E -> High E) to Horizontal Tab (High E -> Low E lines)
            var parts = diagramMatch.Value.Split('-');
            if (parts.Length == 6)
            {
                var sb = new StringBuilder();
                // parts[0] is Low E (String 0) -> Line 5 (Bottom)
                // parts[5] is High E (String 5) -> Line 0 (Top)
                
                // We generate 6 lines. Line 0 matches parts[5]. Line 5 matches parts[0].
                for (int i = 5; i >= 0; i--)
                {
                    sb.AppendLine($"|--{parts[i]}--|");
                }
                tabText = sb.ToString();
            }
            else
            {
                // Fallback extraction
                var lines = query.Split('\n')
                    .Where(l => l.Contains('|') || l.Contains("--"))
                    .ToList();
                tabText = string.Join("\n", lines);
            }
        }
        else
        {
            var lines = query.Split('\n')
                .Where(l => l.Contains('|') || l.Contains("--"))
                .ToList();
            tabText = string.Join("\n", lines);
        }

        // 1. Parse into Chord IDs/Pitches
        var analysis = await _tabAnalyzer.AnalyzeAsync(tabText);
        if (analysis.Events.Count == 0) return new ChatResponse("I couldn't find a valid tab to optimize.", new List<CandidateVoicing>(), null, new { });

        // 2. Solve for Optimal (Smoothest) Path
        // AdvancedTabSolver uses Viterbi to minimize physical cost.
        var solution = await _tabSolver.SolveOptimalPathAsync(analysis.Events.Select(e => e.Document));
        
        // 2.5 Get Style-Based Alternatives
        var alternatives = await _altService.GetAlternativesAsync(analysis.Events.Select(e => e.Document));

        // 3. Format result
        var narrative = new StringBuilder();
        narrative.AppendLine("I've re-calculated the optimal path for that progression to minimize hand movement and transitions.");
        narrative.AppendLine();
        narrative.AppendLine("**Optimized Tab:**");
        narrative.AppendLine("```");
        narrative.AppendLine(solution.TabContent);
        narrative.AppendLine("```");
        narrative.AppendLine($"*Optimization Score: {solution.TotalPhysicalCost:F1} physical cost units.*");
        
        if (alternatives.Any())
        {
            narrative.AppendLine();
            narrative.AppendLine("### Alternative Styles");
            foreach(var alt in alternatives)
            {
                narrative.AppendLine($"**{alt.Label}** ({alt.Description})");
                narrative.AppendLine("```");
                // Convert list of chords to string. We need a helper for this ideally, 
                // but sticking to simple string generic format for now or just calling TabPresentationService?
                // For now, let's just assume we can print it simply.
                // Or better, recreate the Tab String from the alt.Tab list.
                // Assuming standard tuning E A D G B E
                var sb = new StringBuilder();
                // 6 strings, 0..5
                string[] stringNames = ["e", "B", "G", "D", "A", "E"];
                for(int s=0; s<6; s++) {
                    sb.Append(stringNames[s] + "|");
                    foreach(var chord in alt.Tab) {
                        var note = chord.FirstOrDefault(n => n.StringIndex.Value == (6 - s)); // StringIndex 6=LowE -> s=5??
                        // Wait, StringIndex 1=HighE, 6=LowE.
                        // So s=0 (HighE) matches StringIndex 1.
                        var stringIdx = s + 1; 
                        var pos = chord.FirstOrDefault(n => n.StringIndex.Value == stringIdx);
                        if (pos != null) sb.Append($"-{pos.Fret}-");
                        else sb.Append("-x-");
                    }
                    sb.AppendLine("|");
                }
                narrative.AppendLine(sb.ToString());
                narrative.AppendLine("```");
            }
        }

        return new ChatResponse(narrative.ToString(), new List<CandidateVoicing>(), null, new { Mode = "PathOptimization", Cost = solution.TotalPhysicalCost });
    }

    private bool IsAskingForOptimization(string query)
    {
        string q = query.ToLowerInvariant();
        return q.Contains("smooth") || q.Contains("easy") || q.Contains("optimize") || q.Contains("ergonomic") || q.Contains("better path");
    }

    private async Task<ChatResponse> HandleTabAnalysisAsync(string tab)
    {
        // 1. Extract events
        var result = await _tabAnalyzer.AnalyzeAsync(tab);
        if (result.Events.Count == 0)
            return new ChatResponse("I detected tab but couldn't parse any chords.", new List<CandidateVoicing>(), null, new { Status = "Empty" });

        // 2. Hydrate embeddings (Required for motion/suggestions)
        var events = result.Events.ToList();
        for (int i = 0; i < events.Count; i++)
        {
            var doc = events[i].Document;
            events[i] = events[i] with { Document = doc with { Embedding = await _embeddingGenerator.GenerateEmbeddingAsync(doc) } };
        }
        var progression = events.Select(e => e.Document).ToList();

        // 3. High-level Intelligence
        var targets = _modulationAnalyzer.IdentifyTargets(progression);
        
        // 4. Suggest where to go next
        var suggestions = await _suggestionService.SuggestNextAsync(progression.Last(), topK: 3);

        // 5. Present unified result
        return _presenter.FormatAnalysis(result, suggestions, targets);
    }

    private bool IsTabQuery(string query)
    {
        bool isTab = false;
        if (query.Contains("e|") || query.Contains("E|") || query.Contains("--")) isTab = true;
        // Check for diagrams like x-3-2-0-1-0 or 3-x-0-0-0-3
        else if (System.Text.RegularExpressions.Regex.IsMatch(query, @"[x\d]-(\d|x)-")) isTab = true;
        // Check for compact diagrams like x02210 or 3x0003
        else if (System.Text.RegularExpressions.Regex.IsMatch(query, @"\b[x\d]{6}\b")) isTab = true;
        
        return isTab;
    }
}
