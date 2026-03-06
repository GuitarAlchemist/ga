namespace GA.Business.Core.Orchestration.Services;

using System.Linq;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Rag.Models;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Services.Chords.Parsing;
using Microsoft.Extensions.Logging;

/// <summary>
/// Spectral RAG Orchestrator: retrieves voicings from the vector index,
/// narrates results through a grounded LLM, and supports chord comparison.
/// </summary>
public class SpectralRagOrchestrator(
    IVectorIndex index,
    SpectralRetrievalService retrievalService,
    QueryUnderstandingService queryExtractor,
    VoicingExplanationService explainer,
    MusicalEmbeddingGenerator generator,
    IGroundedNarrator narrator,
    ILogger<SpectralRagOrchestrator> logger) : IHarmonicChatOrchestrator
{
    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        var candidates = new List<CandidateVoicing>();
        var query = req.Message.Trim();
        var debugMode = "Search";

        // 0. CHECK STALENESS
        var isStale = await index.IsStaleAsync(EmbeddingSchema.Version);
        string? stalenessWarning = isStale
            ? "\n\n⚠️ **WARNING**: The voicing index is out of date (Schema Version Mismatch). Search results may be inaccurate. Please run `ga-cli index-voicings --index-all` to refresh."
            : null;

        // 1. Distance / Comparison shortcut
        if (query.Contains("distance", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("compare", StringComparison.OrdinalIgnoreCase) ||
            query.Contains("difference", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = await HandleChordComparisonAsync(query);
            if (comparison != null) return comparison;
        }

        // 2. IDENTITY LOOKUP (exact match by chord name)
        var exactMatch = index.FindByIdentity(query);
        float[]? queryVector = null;

        if (exactMatch?.Embedding is { Length: > 0 })
        {
            queryVector = exactMatch.Embedding;
            debugMode = $"IdentityMatch('{query}')";
        }
        else
        {
            // 3. TRY PARSING AS CHORD SYMBOL
            var parser = new ChordSymbolParser();
            if (parser.TryParse(query, out var chord) && chord != null)
            {
                queryVector = await generator.GenerateEmbeddingAsync(CreateVirtualDoc(chord));
                debugMode = $"ParsedChord('{chord.Symbol}')";
            }
        }

        // 4. EXTRACT FILTERS
        var filters = await queryExtractor.ExtractFiltersAsync(query);

        // 5. RESOLVE PRESET
        var preset = SpectralRetrievalService.SearchPreset.Tonal;
        if (query.Contains("jazz", StringComparison.OrdinalIgnoreCase)) preset = SpectralRetrievalService.SearchPreset.Jazz;
        else if (query.Contains("atonal", StringComparison.OrdinalIgnoreCase)) preset = SpectralRetrievalService.SearchPreset.Atonal;
        else if (query.Contains("guitar", StringComparison.OrdinalIgnoreCase)) preset = SpectralRetrievalService.SearchPreset.Guitar;

        // 6. RETRIEVAL (skip entirely if no meaningful query vector)
        if (queryVector != null)
        {
            var results = retrievalService.Search(
                queryVector,
                topK: 5,
                preset: preset,
                quality: filters?.Quality,
                extension: filters?.Extension,
                stackingType: filters?.StackingType,
                noteCount: filters?.NoteCount);

            foreach (var (doc, score) in results)
            {
                var explanation = explainer.Explain(doc);
                candidates.Add(new CandidateVoicing(
                    Id: doc.Id,
                    DisplayName: doc.ChordName ?? "Unknown",
                    Shape: doc.Diagram,
                    Score: score,
                    ExplanationFacts: explanation,
                    ExplanationText: explanation.Summary
                ));
            }
        }
        else
        {
            debugMode = "Fallback(NoVector)";
            logger.LogDebug("No query vector produced for '{Query}'; skipping retrieval.", query);
        }

        // 7. NARRATION with guardrails
        var narratorText = await narrator.NarrateAsync(req.Message, candidates);

        // 8. PROGRESSION GENERATION (heuristic)
        var progression = TryExtractProgression(req.Message, out var appendText);
        if (appendText != null) narratorText += appendText;
        if (stalenessWarning != null) narratorText += stalenessWarning;

        return new ChatResponse(
            narratorText,
            candidates,
            progression,
            QueryFilters: filters,
            DebugParams: new { Mode = debugMode, Stale = isStale });
    }

    private async Task<ChatResponse?> HandleChordComparisonAsync(string query)
    {
        var parser = new ChordSymbolParser();
        var tokens = query.Split([' ', ',', ':', '-', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var foundChords = new List<Chord>();
        foreach (var token in tokens)
        {
            if (parser.TryParse(token, out var chord) && chord != null)
                foundChords.Add(chord);
        }

        if (foundChords.Count < 2) return null;

        var c1 = foundChords[0];
        var c2 = foundChords[1];

        // Generate both embeddings concurrently — no data dependency between them
        var embeddingResults = await Task.WhenAll(
            generator.GenerateEmbeddingAsync(CreateVirtualDoc(c1)),
            generator.GenerateEmbeddingAsync(CreateVirtualDoc(c2)));
        var v1 = embeddingResults[0];
        var v2 = embeddingResults[1];

        double spectralSim = SpectralRetrievalService.CalculateWeightedSimilarity(v1, v2, SpectralRetrievalService.SearchPreset.Spectral);
        double tonalSim = SpectralRetrievalService.CalculateWeightedSimilarity(v1, v2, SpectralRetrievalService.SearchPreset.Tonal);

        var narrative = $"Comparing **{c1.Symbol}** and **{c2.Symbol}** using Spectral Geometry:\n\n";
        narrative += $"- **Harmonic Closeness (Spectral similarity)**: {spectralSim:P1}. ";

        if (spectralSim > 0.8) narrative += "These chords are very similar in color and function. They likely share many common tones or have a strong 'harmonic magnet' pulling them together.";
        else if (spectralSim > 0.5) narrative += "These chords are moderately related but have distinct color shifts.";
        else narrative += "These chords are spectrally distant, suggesting a significant modulation or 'jump' in harmonic space.";

        narrative += $"\n- **Theoretical Similarity**: {tonalSim:P1}.";

        var comparisonCandidates = new List<CandidateVoicing>();
        foreach (var c in foundChords.Take(2))
        {
            var match = index.FindByIdentity(c.Symbol);
            if (match != null)
            {
                var explanation = explainer.Explain(match);
                comparisonCandidates.Add(new CandidateVoicing(match.Id, c.Symbol, match.Diagram, 1.0, explanation, explanation.Summary));
            }
        }

        return new ChatResponse(narrative, comparisonCandidates, DebugParams: new { Mode = "Comparison", SpectralSim = spectralSim });
    }

    private static ChordVoicingRagDocument CreateVirtualDoc(Chord chord) =>
        new()
        {
            Id = "query",
            ChordName = chord.Symbol,
            RootPitchClass = chord.Root.PitchClass.Value,
            PitchClasses = [..chord.PitchClassSet.Select(pc => pc.Value)],
            MidiNotes = [..chord.Notes.Select(n => n.PitchClass.Value + 60)],
            Diagram = "query",
            SearchableText = chord.Symbol,
            PossibleKeys = [],
            SemanticTags = [],
            YamlAnalysis = "{}",
            PitchClassSet = chord.PitchClassSet.ToString(),
            IntervalClassVector = chord.PitchClassSet.IntervalClassVector.ToString(),
            AnalysisEngine = "QueryParser",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = chord.PitchClassSet.Id.ToString()
        };

    private GA.Domain.Core.Theory.Harmony.Progressions.Progression? TryExtractProgression(string message, out string? appendText)
    {
        appendText = null;
        if (!message.Contains("progression", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("analyze", StringComparison.OrdinalIgnoreCase)) return null;

        var symbolParser = new ChordSymbolParser();
        var tokens = message.Split([' ', ',', '-', ':', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var steps = new List<GA.Domain.Core.Theory.Harmony.Progressions.ProgressionStep>();
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Analyze", "Progression", "This", "Please", "Check", "Here", "Key", "Of", "In", "The", "A", "An", "Play", "Me" };

        foreach (var token in tokens)
        {
            if (stopWords.Contains(token)) continue;
            if (symbolParser.TryParse(token, out var chord) && chord != null)
            {
                var match = index.FindByIdentity(chord.Symbol);
                var displayLabel = chord.Symbol;
                if (match != null && !string.IsNullOrWhiteSpace(match.Diagram))
                    displayLabel += $" ({match.Diagram})";

                steps.Add(new GA.Domain.Core.Theory.Harmony.Progressions.ProgressionStep
                {
                    Label = displayLabel,
                    Voicing = new Voicing([], []),
                    DurationMs = 2000,
                    Function = ""
                });
            }
        }

        if (steps.Count > 0)
        {
            appendText = "\n\n(I have attached the analyzed progression for you to play!)";
            return new GA.Domain.Core.Theory.Harmony.Progressions.Progression
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Extracted Progression",
                Steps = steps
            };
        }

        return null;
    }
}
