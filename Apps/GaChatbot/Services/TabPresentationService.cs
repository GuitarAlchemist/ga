namespace GaChatbot.Services;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using GaChatbot.Models;
using GA.Business.ML.Tabs.Models;
using GA.Business.ML.Musical.Explanation;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Wavelets;

using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;

/// <summary>
/// Responsible for formatting Tab Analysis results into user-facing ChatResponses.
/// Separates presentation logic from the orchestration and analysis logic.
/// </summary>
public class TabPresentationService
{
    private readonly VoicingExplanationService _explainer;
    private readonly ProgressionSignalService _signalService;
    private readonly StyleClassifierService _styleClassifier;

    public TabPresentationService(
        VoicingExplanationService explainer,
        ProgressionSignalService signalService,
        StyleClassifierService styleClassifier)
    {
        _explainer = explainer;
        _signalService = signalService;
        _styleClassifier = styleClassifier;
    }

    public ChatResponse FormatAnalysis(
        TabAnalysisResult result, 
        List<NextChordSuggestionService.SuggestionResult>? suggestions = null,
        List<ModulationAnalyzer.ModulationTarget>? modulations = null)
    {
        if (result.Events.Count == 0)
        {
            return new ChatResponse(
                "I detected tablature but couldn't find any valid notes to analyze.", 
                new List<CandidateVoicing>(), 
                null, 
                new { Mode = "TabAnalysis", Status = "Empty" });
        }

        // Identify distinct chords
        var distinctChords = result.Events
            .Select(e => e.Document)
            .GroupBy(d => d.PitchClassSet) 
            .Select(g => g.First())
            .ToList();

        var candidates = new List<CandidateVoicing>();
        foreach (var doc in distinctChords)
        {
            var explanation = _explainer.Explain(doc);
            var displayName = doc.ChordName ?? $"Unknown Chord (Root {doc.RootPitchClass})";
            
            // For candidates list
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

        // --- Style Classification ---
        var progression = result.Events.Select(e => e.Document).ToList();
        var stylePrediction = _styleClassifier.PredictStyle(progression);
        if (stylePrediction.Confidence > 0.5)
        {
            narrative.AppendLine();
            narrative.AppendLine($"**Style Prediction:** This sequence feels like **{stylePrediction.PredictedStyle}** ({stylePrediction.Confidence * 100:F0}% match).");
        }

        // --- Motion Analysis ---
        var signals = _signalService.ExtractSignals(progression);
        
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

        // --- Modulation ---
        if (modulations != null && modulations.Count > 0)
        {
            narrative.AppendLine();
            narrative.AppendLine("**Key Drift:** The progression feels centered around:");
            foreach (var mod in modulations.Take(2))
            {
                narrative.AppendLine($"- **{mod.Key.ToSharpNote()}** ({mod.Confidence * 100:F0}% confidence)");
            }
        }

        // --- Suggestions ---
        if (suggestions != null && suggestions.Count > 0)
        {
            narrative.AppendLine();
            narrative.AppendLine("**What's next? (Smoothest Path):**");
            foreach (var sugg in suggestions)
            {
                narrative.AppendLine($"- **{sugg.Doc.ChordName}** (Ergonomic match: {10.0 - sugg.PhysicalCost:F1}/10)");
            }
        }
        
        return new ChatResponse(
            narrative.ToString(), 
            candidates,
            null,
            new { 
                Mode = "TabAnalysis", 
                Events = result.Events.Count, 
                Unique = distinctChords.Count, 
                Cadence = result.DetectedCadence,
                TopKey = modulations?.FirstOrDefault()?.Key.ToSharpNote().ToString()
            });
    }

    private string GenerateSparkline(double[] values)
    {
        if (values.Length == 0) return "";
        
        // Simple ASCII Sparkline:  _ . - ^
        // Map 0.0-1.0 to 4 levels
        char[] levels = ['_', '.', '-', '^'];
        var sb = new StringBuilder();
        
        foreach (var v in values)
        {
            int index = (int)(v * 3.99); // 0, 1, 2, 3
            sb.Append(levels[Math.Clamp(index, 0, 3)]);
        }
        return sb.ToString();
    }
}
