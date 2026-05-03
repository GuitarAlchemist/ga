namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;

/// <summary>
/// Encapsulates tab-path optimization and tab-analysis flows previously
/// embedded as private methods in <see cref="ProductionOrchestrator"/>.
/// Extracting them lets the new intent layer
/// (<c>TabOptimizeIntent</c>, <c>TabAnalyzeIntent</c>) call the same logic
/// without leaking orchestrator internals.
/// </summary>
/// <remarks>
/// All dependencies are already DI-registered for the orchestrator; this
/// service is a Scoped facade over them.
/// </remarks>
public sealed partial class TabAnalysisOrchestrationService(
    TabAnalysisService tabAnalyzer,
    AdvancedTabSolver tabSolver,
    AlternativeFingeringService altService,
    MusicalEmbeddingGenerator embeddingGenerator,
    ModulationAnalyzer modulationAnalyzer,
    NextChordSuggestionService suggestionService,
    TabPresentationService presenter)
{
    [GeneratedRegex(@"([x\d]{1,2}-){5}[x\d]{1,2}", RegexOptions.CultureInvariant)]
    private static partial Regex CompactDiagramRegex();

    /// <summary>
    /// Re-calculates the optimal fingering path for a tab in the query —
    /// minimises hand movement and transitions, returns the optimised tab
    /// plus alternative-style suggestions.
    /// </summary>
    public async Task<ChatResponse> OptimizePathAsync(string query, CancellationToken cancellationToken = default)
    {
        var diagramMatch = CompactDiagramRegex().Match(query);
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
        {
            return new ChatResponse(
                "I need a tab in the message to optimize a path. " +
                "Paste a tab block (e.g. `e|---0---3---|` lines) or a compact diagram (e.g. `x-3-2-0-1-0`).",
                []);
        }

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

    /// <summary>
    /// Analyses a tab block: parses chords, generates embeddings for each
    /// event, identifies modulation targets, suggests next chords, and
    /// formats via the presenter.
    /// </summary>
    public async Task<ChatResponse> AnalyzeTabAsync(string tab, CancellationToken cancellationToken = default)
    {
        var result = await tabAnalyzer.AnalyzeAsync(tab);
        if (result.Events.Count == 0)
            return new ChatResponse("I detected tab but couldn't parse any chords.", [], DebugParams: new { Status = "Empty" });

        var events = result.Events.ToList();
        var embeddingTasks = events.Select(async e =>
        {
            try
            {
                var emb = await embeddingGenerator.GenerateEmbeddingAsync(e.Document);
                return e with { Document = e.Document with { Embedding = emb } };
            }
            catch (Exception)
            {
                // Degrade gracefully — proceed without embedding for this event
                return e;
            }
        }).ToList();

        events = [.. await Task.WhenAll(embeddingTasks)];
        var progression = events.Select(e => e.Document).ToList();

        var targets = modulationAnalyzer.IdentifyTargets(progression);
        var suggestions = await suggestionService.SuggestNextAsync(progression.Last(), topK: 3);

        return presenter.FormatAnalysis(result, suggestions, targets);
    }

    /// <summary>Extracts tab lines (containing <c>|</c> or <c>--</c>) from a query.</summary>
    public static string ExtractTabLines(string query) =>
        string.Join("\n", query.Split('\n').Where(l => l.Contains('|') || l.Contains("--")));
}
