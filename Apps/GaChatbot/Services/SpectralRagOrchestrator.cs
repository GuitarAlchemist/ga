namespace GaChatbot.Services;

using GA.Domain.Core.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Embeddings;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Core.Primitives;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using GaChatbot.Abstractions;
using GaChatbot.Models;
using GA.Business.ML.Musical.Explanation;
using Microsoft.Extensions.Logging;

/// <summary>
/// Spectral RAG Orchestrator that retrieves voicings from the vector index.
/// Integrated with QueryUnderstandingService for constraint extraction.
/// Implements Story 1.2.6 (Distance Comparison).
/// </summary>
public class SpectralRagOrchestrator(
    IVectorIndex index,
    SpectralRetrievalService retrievalService,
    QueryUnderstandingService queryExtractor,
    VoicingExplanationService explainer,
    MusicalEmbeddingGenerator generator,
    IGroundedNarrator narrator) : IHarmonicChatOrchestrator
{
    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        var candidates = new List<CandidateVoicing>();
        var query = req.Message.Trim();
        var debugMode = "Search";
        
        // 0. CHECK STALENESS
        var isStale = await index.IsStaleAsync(EmbeddingSchema.Version);
        string? stalenessWarning = isStale ? "\n\n⚠️ **WARNING**: The voicing index is out of date (Schema Version Mismatch). Search results may be inaccurate. Please run `ga-cli index-voicings --index-all` to refresh." : null;

        // 1. STORY 1.2.6: Distance Identification / Comparison
        if (query.Contains("distance", StringComparison.OrdinalIgnoreCase) || 
            query.Contains("compare", StringComparison.OrdinalIgnoreCase) || 
            query.Contains("difference", StringComparison.OrdinalIgnoreCase))
        {
            var comparison = await HandleChordComparisonAsync(query);
            if (comparison != null) return comparison;
        }

        // 2. IDENTITY LOOKUP (exact match by chord name)
        var exactMatch = index.FindByIdentity(query);
        double[]? queryVector = null;
        
        if (exactMatch != null && exactMatch.Embedding != null)
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

        // 4. EXTRACT FILTERS (Phase 5.1.2 Improvement)
        var filters = await queryExtractor.ExtractFiltersAsync(query);
        
        // 5. RESOLVE PRESET
        var preset = SpectralRetrievalService.SearchPreset.Tonal;
        if (query.Contains("jazz", StringComparison.OrdinalIgnoreCase)) preset = SpectralRetrievalService.SearchPreset.Jazz;
        else if (query.Contains("atonal", StringComparison.OrdinalIgnoreCase)) preset = SpectralRetrievalService.SearchPreset.Atonal;
        else if (query.Contains("guitar", StringComparison.OrdinalIgnoreCase)) preset = SpectralRetrievalService.SearchPreset.Guitar;

        // 6. RETRIEVAL
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
            // FALLBACK: No meaningful query vector could be produced.
            // A zero-vector produces arbitrary cosine similarity results, so we skip retrieval entirely
            // and let the narrator respond with the empty-candidates fallback message.
            debugMode = "Fallback(NoVector)";
        }

        // 7. NARRATION with Guardrails
        var narratorText = await narrator.NarrateAsync(req.Message, candidates);

        // 8. PROGRESSION GENERATION (Heuristic)
        var progression = TryExtractProgression(req.Message, out var updatedNarratorText);
        if (updatedNarratorText != null) narratorText += updatedNarratorText;
        if (stalenessWarning != null) narratorText += stalenessWarning;
        
        return new ChatResponse(narratorText, candidates, progression, new { Mode = debugMode, Filters = filters, Stale = isStale });
    }

    private async Task<ChatResponse?> HandleChordComparisonAsync(string query)
    {
        var parser = new ChordSymbolParser();
        // Look for two chords in the query
        var tokens = query.Split(new[] { ' ', ',', ':', '-', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var foundChords = new List<Chord>();
        foreach (var token in tokens)
        {
            if (parser.TryParse(token, out var chord) && chord != null)
            {
                foundChords.Add(chord);
            }
        }

        if (foundChords.Count >= 2)
        {
            var c1 = foundChords[0];
            var c2 = foundChords[1];

            // Generate both embeddings concurrently — no data dependency between them
            var embeddingResults = await Task.WhenAll(
                generator.GenerateEmbeddingAsync(CreateVirtualDoc(c1)),
                generator.GenerateEmbeddingAsync(CreateVirtualDoc(c2)));
            var v1 = embeddingResults[0];
            var v2 = embeddingResults[1];
            
            // Calculate Phase Sphere (Spectral) Similarity
            double spectralSim = SpectralRetrievalService.CalculateWeightedSimilarity(v1, v2, SpectralRetrievalService.SearchPreset.Spectral);
            double tonalSim = SpectralRetrievalService.CalculateWeightedSimilarity(v1, v2, SpectralRetrievalService.SearchPreset.Tonal);
            
            // Map spectral similarity [0, 1] to a pseudo-distance or just explain it
            var narrative = $"Comparing **{c1.Symbol}** and **{c2.Symbol}** using Spectral Geometry:\n\n";
            narrative += $"- **Harmonic Closeness (Spectral similarity)**: {spectralSim:P1}. ";
            
            if (spectralSim > 0.8) narrative += "These chords are very similar in color and function. They likely share many common tones or have a strong 'harmonic magnet' pulling them together.";
            else if (spectralSim > 0.5) narrative += "These chords are moderately related but have distinct color shifts.";
            else narrative += "These chords are spectrally distant, suggesting a significant modulation or 'jump' in harmonic space.";
            
            narrative += $"\n- **Theoretical Similarity**: {tonalSim:P1}.";
            
            // Find specific voicings for them to show the user
            var candidates = new List<CandidateVoicing>();
            foreach(var c in foundChords.Take(2))
            {
                var match = index.FindByIdentity(c.Symbol);
                if (match != null)
                {
                    var explanation = explainer.Explain(match);
                    candidates.Add(new CandidateVoicing(match.Id, c.Symbol, match.Diagram, 1.0, explanation, explanation.Summary));
                }
            }

            return new ChatResponse(narrative, candidates, null, new { Mode = "Comparison", SpectralSim = spectralSim });
        }

        return null;
    }

    private VoicingDocument CreateVirtualDoc(Chord chord)
    {
        return new VoicingDocument
        {
            Id = "query",
            ChordName = chord.Symbol,
            RootPitchClass = chord.Root.PitchClass.Value,
            PitchClasses = chord.PitchClassSet.Select(pc => pc.Value).ToArray(),
            MidiNotes = chord.Notes.Select(n => n.PitchClass.Value + 60).ToArray(),
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
    }

    private GA.Domain.Core.Theory.Harmony.Progressions.Progression? TryExtractProgression(string message, out string? appendText)
    {
        appendText = null;
        if (!message.Contains("progression", StringComparison.OrdinalIgnoreCase) && 
            !message.Contains("analyze", StringComparison.OrdinalIgnoreCase)) return null;

        var symbolParser = new ChordSymbolParser();
        var tokens = message.Split(new[] { ' ', ',', '-', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var steps = new List<GA.Domain.Core.Theory.Harmony.Progressions.ProgressionStep>();
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Analyze", "Progression", "This", "Please", "Check", "Here", "Key", "Of", "In", "The", "A", "An", "Play", "Me" };

        foreach (var token in tokens)
        {
            if (stopWords.Contains(token)) continue;
            if (symbolParser.TryParse(token, out var chord) && chord != null)
            {
                var match = index.FindByIdentity(chord.Symbol);
                var displayLabel = chord.Symbol;
                if (match != null && !string.IsNullOrWhiteSpace(match.Diagram)) displayLabel += $" ({match.Diagram})";

                steps.Add(new GA.Domain.Core.Theory.Harmony.Progressions.ProgressionStep
                {
                    Label = displayLabel,
                    Voicing = new GA.Domain.Core.Instruments.Fretboard.Voicings.Core.Voicing(Array.Empty<GA.Domain.Core.Instruments.Primitives.Position>(), Array.Empty<GA.Domain.Core.Primitives.MidiNote>()),
                    DurationMs = 2000,
                    Function = ""
                });
            }
        }

        if (steps.Count > 0)
        {
            appendText = "\n\n(I have attached the analyze progression for you to play!)";
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
