using System; // Required for Guid, StringComparison
using System.Collections.Generic;
using System.Linq;
using GA.Business.ML.Embeddings;
using GA.Business.Core.Chords;
using GA.Business.Core.Notes;
using GA.Business.Core.Fretboard.Voicings.Search;
using GaChatbot.Abstractions;
using GaChatbot.Models;

namespace GaChatbot.Services;

/// <summary>
/// Spectral RAG Orchestrator that retrieves voicings from the vector index.
/// Phase 14: Updated to use FileBasedVectorIndex and correct dimensions.
/// Phase 15: Integrated SpectralRetrievalService for weighted search.
/// </summary>
public class SpectralRagOrchestrator(
    IVectorIndex index,
    SpectralRetrievalService retrievalService,
    GA.Business.ML.Musical.Explanation.VoicingExplanationService explainer,
    GA.Business.ML.Embeddings.MusicalEmbeddingGenerator generator,
    IGroundedNarrator narrator) : IHarmonicChatOrchestrator
{
    public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
    {
        var candidates = new List<CandidateVoicing>();
        var debugMode = "Search";
        var query = req.Message.Trim();

        // 1. TRY IDENTITY LOOKUP (exact match by chord name)
        var exactMatch = index.FindByIdentity(query);
        double[]? queryVector = null;
        
        if (exactMatch != null && exactMatch.Embedding != null)
        {
            queryVector = exactMatch.Embedding;
            debugMode = $"IdentityMatch('{query}')";
        }
        else
        {
            // 2. TRY PARSING AS CHORD SYMBOL
            var parser = new ChordSymbolParser();
            if (parser.TryParse(query, out var chord) && chord != null)
            {
                // Create a virtual document to generate an embedding for the query
                var virtualDoc = new VoicingDocument
                {
                    Id = "query",
                    ChordName = chord.Symbol,
                    RootPitchClass = chord.Root.PitchClass.Value,
                    PitchClasses = chord.PitchClassSet.Select(pc => pc.Value).ToArray(),
                    MidiNotes = chord.Notes.Select(n => n.PitchClass.Value + 60).ToArray(), // Mid-range
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

                queryVector = await generator.GenerateEmbeddingAsync(virtualDoc);
                debugMode = $"ParsedChord('{chord.Symbol}')";
            }
        }

        // 3. RETRIEVAL
        if (queryVector != null)
        {
            var results = retrievalService.Search(queryVector, topK: 5, preset: SpectralRetrievalService.SearchPreset.Tonal);
            
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
            // FALLBACK: Return all/top voicings if no vector generated
            debugMode = "Fallback(All)";
            var zeroVector = new double[EmbeddingSchema.TotalDimension];
            var results = retrievalService.Search(zeroVector, topK: 5, preset: SpectralRetrievalService.SearchPreset.Tonal);
            
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

        // 4. NARRATION with Guardrails
        bool forceHallucination = req.Message.Contains("hallucinate"); // For testing
        var narratorText = await narrator.NarrateAsync(req.Message, candidates, forceHallucination);

        // 5. PROGRESSION GENERATION
        GA.Business.Core.Progressions.Progression? progression = null;
        // Basic heuristic: if user asks for progression or analysis and we find multiple chords
        if (req.Message.Contains("progression", StringComparison.OrdinalIgnoreCase) || 
            req.Message.Contains("analyze", StringComparison.OrdinalIgnoreCase))
        {
             var symbolParser = new ChordSymbolParser();
             // Split by common delimiters and removing empty entries
             var tokens = req.Message.Split(new[] { ' ', ',', '-', ':', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
             var steps = new List<GA.Business.Core.Progressions.ProgressionStep>();
             
             var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
             { 
                 "Analyze", "Progression", "This", "Please", "Check", "Here", "Key", "Of", "In", "The", "A", "An", "Play", "Me" 
             };

             foreach (var token in tokens)
             {
                 if (stopWords.Contains(token)) continue;

                 // Filter out tokens that don't look like chords (simple regex check or length) to avoid expensive TryParse on everything?
                 // ChordSymbolParser is strict, but "A" matches "A". "Me" matches "M" + "e" (invalid).
                 // Actually "Me" -> "M" (Major) + "e" (unknown suffix?). "M" is Major.
                 
                 if (symbolParser.TryParse(token, out var chord) && chord != null)
                 {
                      var match = index.FindByIdentity(chord.Symbol);
                      var voicing = new GA.Business.Core.Fretboard.Voicings.Core.Voicing(
                          Array.Empty<GA.Business.Core.Fretboard.Primitives.Position>(), 
                          Array.Empty<GA.Business.Core.Notes.Primitives.MidiNote>()
                      );

                      var displayLabel = chord.Symbol;

                      if (match != null && !string.IsNullOrWhiteSpace(match.Diagram))
                      {
                          // Parse Diagram "x-0-2-2-1-0" or "3x0003"
                          // Assuming hyphenated or compact format. VoicingDocument usually has normalized "x-x-x-x-x-x"
                          var parts = match.Diagram.Contains('-') 
                                      ? match.Diagram.Split('-') 
                                      : match.Diagram.Select(c => c.ToString()).ToArray();

                          if (parts.Length == 6)
                          {
                              // Logic to parse positions if needed...
                              // For now, just append diagram to label
                              displayLabel += $" ({match.Diagram})";
                          }
                      }

                      steps.Add(new GA.Business.Core.Progressions.ProgressionStep
                      {
                          Label = displayLabel,
                          Voicing = voicing, 
                          DurationMs = 2000,
                          Function = "" 
                      });
                 }
             }

             if (steps.Count > 0)
             {
                 progression = new GA.Business.Core.Progressions.Progression
                 {
                     Id = Guid.NewGuid().ToString(),
                     Name = "Extracted Progression",
                     Description = $"Extracted from: {req.Message}",
                     Steps = steps,
                     Tags = ["extracted", "user-query"]
                 };
                 narratorText += "\n\n(I have attached the analyze progression for you to play!)";
             }
        }
        
        return new ChatResponse(narratorText, candidates, progression, new { Mode = debugMode });
    }
}
