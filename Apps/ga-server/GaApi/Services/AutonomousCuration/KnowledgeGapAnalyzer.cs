namespace GaApi.Services.AutonomousCuration;

using Models.AutonomousCuration;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
/// Analyzes the knowledge base to identify gaps in coverage
/// </summary>
public class KnowledgeGapAnalyzer
{
    private readonly ILogger<KnowledgeGapAnalyzer> _logger;
    private readonly MongoDbService _mongoDb;
    private readonly IOllamaChatService _ollamaService;

    public KnowledgeGapAnalyzer(
        ILogger<KnowledgeGapAnalyzer> logger,
        MongoDbService mongoDb,
        IOllamaChatService ollamaService)
    {
        _logger = logger;
        _mongoDb = mongoDb;
        _ollamaService = ollamaService;
    }

    /// <summary>
    /// Analyze knowledge gaps across all categories
    /// </summary>
    public async Task<KnowledgeGapAnalysis> AnalyzeGapsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting knowledge gap analysis");

        var analysis = new KnowledgeGapAnalysis();

        // Analyze different categories in parallel
        var tasks = new[]
        {
            AnalyzeChordProgressionGapsAsync(cancellationToken),
            AnalyzeScaleGapsAsync(cancellationToken),
            AnalyzeTechniqueGapsAsync(cancellationToken),
            AnalyzeTheoryConceptGapsAsync(cancellationToken)
        };

        var results = await Task.WhenAll(tasks);
        analysis.Gaps.AddRange(results.SelectMany(r => r));

        // Prioritize gaps using Ollama
        await PrioritizeGapsAsync(analysis.Gaps, cancellationToken);

        _logger.LogInformation("Knowledge gap analysis complete: found {Count} gaps", analysis.Gaps.Count);
        return analysis;
    }

    /// <summary>
    /// Analyze chord progression coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeChordProgressionGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        // Get all documented progressions from MongoDB
        var filter = Builders<BsonDocument>.Filter.Eq("category", "ChordProgression");
        var documents = await _mongoDb.ProcessedDocuments
            .Find(filter)
            .ToListAsync(cancellationToken);

        var documentedProgressions = documents
            .Where(d => d.Contains("tags") && d["tags"].IsBsonArray)
            .SelectMany(d => d["tags"].AsBsonArray.Select(t => t.AsString))
            .Distinct()
            .ToList();

        // Essential progressions that should be covered
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

        return gaps;
    }

    /// <summary>
    /// Analyze scale coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeScaleGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        var filter = Builders<BsonDocument>.Filter.Eq("category", "Scale");
        var documents = await _mongoDb.ProcessedDocuments
            .Find(filter)
            .ToListAsync(cancellationToken);

        var documentedScales = documents
            .Where(d => d.Contains("tags") && d["tags"].IsBsonArray)
            .SelectMany(d => d["tags"].AsBsonArray.Select(t => t.AsString))
            .Distinct()
            .ToList();

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

        return gaps;
    }

    /// <summary>
    /// Analyze technique coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeTechniqueGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        var filter = Builders<BsonDocument>.Filter.Eq("category", "Technique");
        var documents = await _mongoDb.ProcessedDocuments
            .Find(filter)
            .ToListAsync(cancellationToken);

        var documentedTechniques = documents
            .Where(d => d.Contains("tags") && d["tags"].IsBsonArray)
            .SelectMany(d => d["tags"].AsBsonArray.Select(t => t.AsString))
            .Distinct()
            .ToList();

        var essentialTechniques = new[]
        {
            "Alternate Picking", "Sweep Picking", "Economy Picking",
            "Legato", "Tapping", "Bending", "Vibrato",
            "Hammer-on", "Pull-off", "Slides", "Palm Muting"
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

        return gaps;
    }

    /// <summary>
    /// Analyze music theory concept coverage gaps
    /// </summary>
    private async Task<List<KnowledgeGap>> AnalyzeTheoryConceptGapsAsync(CancellationToken cancellationToken)
    {
        var gaps = new List<KnowledgeGap>();

        var filter = Builders<BsonDocument>.Filter.Eq("category", "Theory");
        var documents = await _mongoDb.ProcessedDocuments
            .Find(filter)
            .ToListAsync(cancellationToken);

        var documentedConcepts = documents
            .Where(d => d.Contains("tags") && d["tags"].IsBsonArray)
            .SelectMany(d => d["tags"].AsBsonArray.Select(t => t.AsString))
            .Distinct()
            .ToList();

        var essentialConcepts = new[]
        {
            "Circle of Fifths", "Modes", "Intervals", "Chord Construction",
            "Voice Leading", "Functional Harmony", "Modulation",
            "Cadences", "Chord Substitution", "Tension and Resolution"
        };

        foreach (var concept in essentialConcepts)
        {
            if (!documentedConcepts.Any(c => c.Contains(concept, StringComparison.OrdinalIgnoreCase)))
            {
                gaps.Add(new KnowledgeGap
                {
                    Category = "Theory",
                    Topic = concept,
                    Description = $"Missing coverage of essential theory concept: {concept}",
                    Priority = "High",
                    SuggestedSearchQuery = $"guitar music theory {concept} tutorial"
                });
            }
        }

        return gaps;
    }

    /// <summary>
    /// Use Ollama to prioritize gaps based on foundational importance
    /// </summary>
    private async Task PrioritizeGapsAsync(List<KnowledgeGap> gaps, CancellationToken cancellationToken)
    {
        if (!gaps.Any()) return;

        try
        {
            var gapsList = string.Join("\n", gaps.Select((g, i) => $"{i + 1}. {g.Category}: {g.Topic}"));

            var prompt = $@"You are a music education expert. Prioritize these knowledge gaps for a guitar learning platform.

Knowledge Gaps:
{gapsList}

For each gap, assign a priority (Critical, High, Medium, Low) based on:
1. Foundational importance (is it needed to understand other concepts?)
2. Practical value for guitarists
3. Common usage in music

Respond with ONLY a JSON array:
[
  {{""index"": 1, ""priority"": ""Critical"", ""reason"": ""brief explanation""}},
  ...
]";

            var response = await _ollamaService.ChatAsync(prompt, cancellationToken: cancellationToken);

            // Parse JSON response and update priorities
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"\[[\s\S]*\]");
            if (jsonMatch.Success)
            {
                var json = System.Text.Json.JsonDocument.Parse(jsonMatch.Value);
                foreach (var element in json.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("index", out var indexProp) &&
                        element.TryGetProperty("priority", out var priorityProp))
                    {
                        var index = indexProp.GetInt32() - 1;
                        if (index >= 0 && index < gaps.Count)
                        {
                            gaps[index].Priority = priorityProp.GetString() ?? "Medium";
                            if (element.TryGetProperty("reason", out var reasonProp))
                            {
                                gaps[index].PriorityReason = reasonProp.GetString() ?? string.Empty;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prioritize gaps with Ollama, using default priorities");
        }
    }
}

