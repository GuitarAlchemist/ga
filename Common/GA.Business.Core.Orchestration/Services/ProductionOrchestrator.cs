namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Agents;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;

/// <summary>
/// Top-level orchestrator that unifies RAG, tab analysis, path optimization,
/// and semantic routing into a single production-grade chat entry point.
/// </summary>
public class ProductionOrchestrator(
    TabAwareOrchestrator tabOrchestrator,
    TabAnalysisService tabAnalyzer,
    NextChordSuggestionService suggestionService,
    ModulationAnalyzer modulationAnalyzer,
    TabPresentationService presenter,
    MusicalEmbeddingGenerator embeddingGenerator,
    AdvancedTabSolver tabSolver,
    AlternativeFingeringService altService,
    SemanticRouter router,
    QueryUnderstandingService queryUnderstandingService) : IHarmonicChatOrchestrator
{
    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        var filters = await queryUnderstandingService.ExtractFiltersAsync(req.Message, ct);
        var routing = await router.RouteAsync(req.Message, ct);

        var routingMetadata = new AgentRoutingMetadata(
            routing.SelectedAgent.AgentId,
            routing.Confidence,
            routing.RoutingMethod);

        if (routing.SelectedAgent.AgentId == AgentIds.Tab ||
            (filters?.Intent is "OptimizePath" or "AnalyzeTab"))
        {
            if (filters?.Intent == "OptimizePath" || IsAskingForOptimization(req.Message))
            {
                var optimized = await HandlePathOptimizationAsync(req.Message, ct);
                return optimized with { Routing = routingMetadata, QueryFilters = filters };
            }

            var analyzed = await HandleTabAnalysisAsync(req.Message, ct);
            return analyzed with { Routing = routingMetadata, QueryFilters = filters };
        }

        var response = await tabOrchestrator.AnswerAsync(req, ct);
        return response with { Routing = routingMetadata, QueryFilters = filters };
    }

    private async Task<ChatResponse> HandlePathOptimizationAsync(string query, CancellationToken ct)
    {
        // Extract raw tab from query
        var diagramMatch = Regex.Match(query, @"([x\d]{1,2}-){5}[x\d]{1,2}");
        string tabText;

        if (diagramMatch.Success && !query.Contains('|'))
        {
            var parts = diagramMatch.Value.Split('-');
            if (parts.Length == 6)
            {
                var sb = new StringBuilder();
                for (int i = 5; i >= 0; i--)
                    sb.AppendLine($"|--{parts[i]}--|");
                tabText = sb.ToString();
            }
            else
            {
                tabText = ExtractTabLines(query);
            }
        }
        else
        {
            tabText = ExtractTabLines(query);
        }

        var analysis = await tabAnalyzer.AnalyzeAsync(tabText);
        if (analysis.Events.Count == 0)
            return new ChatResponse("I couldn't find a valid tab to optimize.", []);

        var solution = await tabSolver.SolveOptimalPathAsync(analysis.Events.Select(e => e.Document));
        var alternatives = await altService.GetAlternativesAsync(analysis.Events.Select(e => e.Document));

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
            string[] stringNames = ["e", "B", "G", "D", "A", "E"];

            foreach (var alt in alternatives)
            {
                narrative.AppendLine($"**{alt.Label}** ({alt.Description})");
                narrative.AppendLine("```");
                var sb = new StringBuilder();
                for (int s = 0; s < 6; s++)
                {
                    sb.Append(stringNames[s] + "|");
                    var stringIdx = s + 1;
                    foreach (var chord in alt.Tab)
                    {
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

        return new ChatResponse(
            narrative.ToString(),
            [],
            DebugParams: new { Mode = "PathOptimization", Cost = solution.TotalPhysicalCost });
    }

    private async Task<ChatResponse> HandleTabAnalysisAsync(string tab, CancellationToken ct)
    {
        var result = await tabAnalyzer.AnalyzeAsync(tab);
        if (result.Events.Count == 0)
            return new ChatResponse("I detected tab but couldn't parse any chords.", [], DebugParams: new { Status = "Empty" });

        var events = result.Events.ToList();
        var embeddingTasks = events.Select(async e =>
        {
            var emb = await embeddingGenerator.GenerateEmbeddingAsync(e.Document);
            return e with { Document = e.Document with { Embedding = emb } };
        }).ToList();

        events = [.. await Task.WhenAll(embeddingTasks)];
        var progression = events.Select(e => e.Document).ToList();

        var targets = modulationAnalyzer.IdentifyTargets(progression);
        var suggestions = await suggestionService.SuggestNextAsync(progression.Last(), topK: 3);

        return presenter.FormatAnalysis(result, suggestions, targets);
    }

    private static bool IsAskingForOptimization(string query)
    {
        var q = query.ToLowerInvariant();
        return q.Contains("smooth") || q.Contains("easy") || q.Contains("optimize") ||
               q.Contains("ergonomic") || q.Contains("better path");
    }

    private static string ExtractTabLines(string query) =>
        string.Join("\n", query.Split('\n').Where(l => l.Contains('|') || l.Contains("--")));
}
