namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;
using GA.Business.ML.Tabs.Models;
using GA.Business.ML.Wavelets;

/// <summary>
/// Formats Tab Analysis results into user-facing ChatResponses.
/// Separates presentation logic from orchestration and analysis logic.
/// </summary>
public class TabPresentationService(
    VoicingExplanationService explainer,
    ProgressionSignalService signalService,
    StyleClassifierService styleClassifier)
{
    public ChatResponse FormatAnalysis(
        TabAnalysisResult result,
        List<NextChordSuggestionService.SuggestionResult>? suggestions = null,
        List<ModulationAnalyzer.ModulationTarget>? modulations = null)
    {
        if (result.Events.Count == 0)
        {
            return new ChatResponse(
                "I detected tablature but couldn't find any valid notes to analyze.",
                [],
                DebugParams: new { Mode = "TabAnalysis", Status = "Empty" });
        }

        var distinctChords = result.Events
            .Select(e => e.Document)
            .GroupBy(d => d.PitchClassSet)
            .Select(g => g.First())
            .ToList();

        var candidates = new List<CandidateVoicing>();
        foreach (var doc in distinctChords)
        {
            var explanation = explainer.Explain(doc);
            var displayName = doc.ChordName ?? $"Unknown Chord (Root {doc.RootPitchClass})";
            candidates.Add(new CandidateVoicing(
                Id: doc.Id,
                DisplayName: displayName,
                Shape: doc.Diagram,
                Score: 1.0,
                ExplanationFacts: explanation,
                ExplanationText: explanation.Summary
            ));
        }

        var narrative = new StringBuilder();
        narrative.AppendLine($"I analyzed the riff and found {result.Events.Count} harmonic events. Here are the distinct chord structures identified:");

        foreach (var doc in distinctChords)
        {
            var name = doc.ChordName ?? "Unknown";
            narrative.AppendLine($"- **{name}**: {doc.Diagram}");
        }

        // Style Classification
        var progression = result.Events.Select(e => e.Document).ToList();
        var stylePrediction = styleClassifier.PredictStyle(progression);
        if (stylePrediction.Confidence > 0.5)
        {
            narrative.AppendLine();
            narrative.AppendLine($"**Style Prediction:** This sequence feels like **{stylePrediction.PredictedStyle}** ({stylePrediction.Confidence * 100:F0}% match).");
        }

        // Motion Analysis
        var signals = signalService.ExtractSignals(progression);
        if (signals.Tension.Length > 1)
        {
            var sparkline = GenerateSparkline(signals.Tension);
            narrative.AppendLine();
            narrative.AppendLine("**Tension Curve:**");
            narrative.AppendLine($"`{sparkline}`");
            narrative.AppendLine("*Low (stable) to High (tense)*");
        }

        if (!string.IsNullOrEmpty(result.DetectedCadence))
        {
            narrative.AppendLine();
            narrative.AppendLine($"**Harmonic Motion:** The phrase concludes with a **{result.DetectedCadence}**.");
        }

        // Modulation
        if (modulations is { Count: > 0 })
        {
            narrative.AppendLine();
            narrative.AppendLine("**Key Drift:** The progression feels centered around:");
            foreach (var mod in modulations.Take(2))
                narrative.AppendLine($"- **{mod.Key.ToSharpNote()}** ({mod.Confidence * 100:F0}% confidence)");
        }

        // Suggestions
        if (suggestions is { Count: > 0 })
        {
            narrative.AppendLine();
            narrative.AppendLine("**What's next? (Smoothest Path):**");
            foreach (var sugg in suggestions)
                narrative.AppendLine($"- **{sugg.Doc.ChordName}** (Ergonomic match: {10.0 - sugg.PhysicalCost:F1}/10)");
        }

        return new ChatResponse(
            narrative.ToString(),
            candidates,
            DebugParams: new
            {
                Mode = "TabAnalysis",
                Events = result.Events.Count,
                Unique = distinctChords.Count,
                Cadence = result.DetectedCadence,
                TopKey = modulations?.FirstOrDefault()?.Key.ToSharpNote().ToString()
            });
    }

    private static string GenerateSparkline(double[] values)
    {
        if (values.Length == 0) return "";
        char[] levels = ['_', '.', '-', '^'];
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            int index = (int)(v * 3.99);
            sb.Append(levels[Math.Clamp(index, 0, 3)]);
        }
        return sb.ToString();
    }
}
