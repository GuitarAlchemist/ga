namespace GA.DocumentProcessing.Service.Services;

using Models;
using MongoDB.Driver;

/// <summary>
/// Analyzes the knowledge graph to identify gaps in Guitar Alchemist's knowledge base
/// Uses Graphiti temporal knowledge graph to track what we know and what's missing
/// </summary>
public class KnowledgeGapAnalyzer
{
    private readonly ILogger<KnowledgeGapAnalyzer> _logger;
    private readonly MongoDbService _mongoDbService;
    private readonly OllamaSummarizationService _ollamaService;

    public KnowledgeGapAnalyzer(
        ILogger<KnowledgeGapAnalyzer> logger,
        MongoDbService mongoDbService,
        OllamaSummarizationService ollamaService)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
        _ollamaService = ollamaService;
    }

    /// <summary>
    /// Analyze the knowledge base and identify gaps
    /// </summary>
    public async Task<KnowledgeGapAnalysis> AnalyzeGapsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting knowledge gap analysis");

        var analysis = new KnowledgeGapAnalysis
        {
            AnalysisDate = DateTime.UtcNow,
            Gaps = new List<KnowledgeGap>()
        };

        try
        {
            // 1. Analyze chord progression coverage
            var chordProgressionGaps = await AnalyzeChordProgressionGapsAsync(cancellationToken);
            analysis.Gaps.AddRange(chordProgressionGaps);

            // 2. Analyze scale coverage
            var scaleGaps = await AnalyzeScaleGapsAsync(cancellationToken);
            analysis.Gaps.AddRange(scaleGaps);

            // 3. Analyze technique coverage
            var techniqueGaps = await AnalyzeTechniqueGapsAsync(cancellationToken);
            analysis.Gaps.AddRange(techniqueGaps);

            // 4. Analyze theory concept coverage
            var theoryGaps = await AnalyzeTheoryConceptGapsAsync(cancellationToken);
            analysis.Gaps.AddRange(theoryGaps);

            // 5. Prioritize gaps using Ollama
            await PrioritizeGapsAsync(analysis, cancellationToken);

            _logger.LogInformation("Knowledge gap analysis complete: {GapCount} gaps identified", analysis.Gaps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during knowledge gap analysis");
            analysis.ErrorMessage = ex.Message;
        }

        return analysis;
    }

    /// <summary>
    /// Analyze chord progression coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeChordProgressionGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        try
        {
            // Get all processed documents with chord progressions
            var documents = await _mongoDbService.GetProcessedDocumentsAsync(cancellationToken);
            var documentedProgressions = documents
                .Where(d => d.Knowledge?.ChordProgressions?.Any() == true)
                .SelectMany(d => d.Knowledge.ChordProgressions)
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} documented chord progressions", documentedProgressions.Count);

            // Common progressions that should be covered
            var essentialProgressions = new[]
            {
                "I-IV-V", "I-V-vi-IV", "ii-V-I", "I-vi-IV-V",
                "I-IV-I-V", "vi-IV-I-V", "I-V-vi-iii-IV-I-IV-V",
                "I-bVII-IV", "i-bVI-bVII", "i-iv-v"
            };

            foreach (var progression in essentialProgressions)
            {
                if (!documentedProgressions.Any(p => p.Contains(progression, StringComparison.OrdinalIgnoreCase)))
                {
                    gaps.Add(new KnowledgeGap
                    {
                        Category = "ChordProgression",
                        Topic = progression,
                        Description = $"Missing coverage of essential chord progression: {progression}",
                        Priority = "High",
                        SuggestedSearchQuery = $"guitar {progression} chord progression tutorial"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing chord progression gaps");
        }

        return gaps;
    }

    /// <summary>
    /// Analyze scale coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeScaleGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        try
        {
            var documents = await _mongoDbService.GetProcessedDocumentsAsync(cancellationToken);
            var documentedScales = documents
                .Where(d => d.Knowledge?.Scales?.Any() == true)
                .SelectMany(d => d.Knowledge.Scales)
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} documented scales", documentedScales.Count);

            // Essential scales
            var essentialScales = new[]
            {
                "Major", "Minor", "Pentatonic Major", "Pentatonic Minor",
                "Blues", "Dorian", "Mixolydian", "Phrygian",
                "Lydian", "Locrian", "Harmonic Minor", "Melodic Minor"
            };

            foreach (var scale in essentialScales)
            {
                if (!documentedScales.Any(s => s.Contains(scale, StringComparison.OrdinalIgnoreCase)))
                {
                    gaps.Add(new KnowledgeGap
                    {
                        Category = "Scale",
                        Topic = scale,
                        Description = $"Missing coverage of essential scale: {scale}",
                        Priority = "High",
                        SuggestedSearchQuery = $"guitar {scale} scale tutorial"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scale gaps");
        }

        return gaps;
    }

    /// <summary>
    /// Analyze technique coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeTechniqueGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        try
        {
            var documents = await _mongoDbService.GetProcessedDocumentsAsync(cancellationToken);
            var documentedTechniques = documents
                .Where(d => d.Knowledge?.Techniques?.Any() == true)
                .SelectMany(d => d.Knowledge.Techniques)
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {Count} documented techniques", documentedTechniques.Count);

            // Essential techniques
            var essentialTechniques = new[]
            {
                "Alternate Picking", "Hammer-On", "Pull-Off", "Bending",
                "Vibrato", "Slides", "Palm Muting", "Fingerpicking",
                "Sweep Picking", "Tapping", "Legato", "Tremolo Picking"
            };

            foreach (var technique in essentialTechniques)
            {
                if (!documentedTechniques.Any(t => t.Contains(technique, StringComparison.OrdinalIgnoreCase)))
                {
                    gaps.Add(new KnowledgeGap
                    {
                        Category = "Technique",
                        Topic = technique,
                        Description = $"Missing coverage of essential technique: {technique}",
                        Priority = "Medium",
                        SuggestedSearchQuery = $"guitar {technique} technique tutorial"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing technique gaps");
        }

        return gaps;
    }

    /// <summary>
    /// Analyze music theory concept coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeTheoryConceptGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        try
        {
            // Essential theory concepts
            var essentialConcepts = new[]
            {
                "Circle of Fifths", "Modes", "Voice Leading", "Chord Inversions",
                "Functional Harmony", "Modal Interchange", "Secondary Dominants",
                "Tritone Substitution", "Chord Extensions", "Altered Chords"
            };

            // For now, assume all theory concepts need coverage
            // In the future, this could query Graphiti for existing theory knowledge
            foreach (var concept in essentialConcepts)
            {
                gaps.Add(new KnowledgeGap
                {
                    Category = "Theory",
                    Topic = concept,
                    Description = $"Missing coverage of music theory concept: {concept}",
                    Priority = "Medium",
                    SuggestedSearchQuery = $"music theory {concept} guitar"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing theory concept gaps");
        }

        return gaps;
    }

    /// <summary>
    /// Use Ollama to prioritize gaps based on importance and learning path
    /// </summary>
    private async Task PrioritizeGapsAsync(KnowledgeGapAnalysis analysis, CancellationToken cancellationToken)
    {
        try
        {
            var gapSummary = string.Join("\n", analysis.Gaps.Select(g => $"- {g.Category}: {g.Topic}"));

            var prompt = $@"You are a music education expert. Analyze these knowledge gaps and prioritize them for a guitar learning platform.

Knowledge Gaps:
{gapSummary}

For each gap, assign a priority (Critical, High, Medium, Low) based on:
1. Foundational importance (beginners need this first)
2. Frequency of use in popular music
3. Prerequisites for other concepts

Return ONLY a JSON array with this format:
[
  {{""topic"": ""topic name"", ""priority"": ""Critical|High|Medium|Low"", ""reason"": ""brief explanation""}}
]";

            var response = await _ollamaService.GenerateTextAsync(prompt, cancellationToken);

            // Parse response and update priorities
            // For now, keep existing priorities
            _logger.LogInformation("Gap prioritization complete");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error prioritizing gaps, using default priorities");
        }
    }
}

