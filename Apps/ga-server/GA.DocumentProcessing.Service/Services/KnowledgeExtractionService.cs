namespace GA.DocumentProcessing.Service.Services;

using GA.DocumentProcessing.Service.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Service for extracting structured music theory knowledge (Stage 2 of NotebookLM pattern)
/// </summary>
public class KnowledgeExtractionService
{
    private readonly OllamaSummarizationService _ollamaService;
    private readonly ILogger<KnowledgeExtractionService> _logger;

    public KnowledgeExtractionService(
        OllamaSummarizationService ollamaService,
        ILogger<KnowledgeExtractionService> logger)
    {
        _ollamaService = ollamaService;
        _logger = logger;
    }

    /// <summary>
    /// Extract structured knowledge from document text
    /// </summary>
    public async Task<ExtractedKnowledge> ExtractKnowledgeAsync(string text, string summary, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use Ollama to extract structured concepts
            var conceptsJson = await _ollamaService.ExtractConceptsAsync(text, cancellationToken);

            // Parse JSON response
            var knowledge = ParseConceptsJson(conceptsJson);

            // Enhance with regex-based extraction as fallback
            EnhanceWithRegexExtraction(text, knowledge);

            _logger.LogInformation("Extracted knowledge: {ChordCount} progressions, {ScaleCount} scales, {TechniqueCount} techniques",
                knowledge.ChordProgressions.Count, knowledge.Scales.Count, knowledge.Techniques.Count);

            return knowledge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting knowledge");

            // Fallback to regex-only extraction
            var knowledge = new ExtractedKnowledge();
            EnhanceWithRegexExtraction(text, knowledge);
            return knowledge;
        }
    }

    private ExtractedKnowledge ParseConceptsJson(string json)
    {
        try
        {
            // Try to extract JSON from markdown code blocks if present
            var jsonMatch = Regex.Match(json, @"```json\s*(.*?)\s*```", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                json = jsonMatch.Groups[1].Value;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<JsonKnowledge>(json, options);

            return new ExtractedKnowledge
            {
                ChordProgressions = parsed?.ChordProgressions ?? new List<string>(),
                Scales = parsed?.Scales ?? new List<string>(),
                Techniques = parsed?.Techniques ?? new List<string>(),
                Concepts = parsed?.Concepts ?? new Dictionary<string, string>(),
                Styles = parsed?.Styles ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse concepts JSON, using empty knowledge");
            return new ExtractedKnowledge();
        }
    }

    private void EnhanceWithRegexExtraction(string text, ExtractedKnowledge knowledge)
    {
        // Extract chord progressions (e.g., "I-IV-V", "ii-V-I")
        var progressionPattern = @"\b([IViv]+(?:-[IViv]+)+)\b";
        var progressionMatches = Regex.Matches(text, progressionPattern);
        foreach (Match match in progressionMatches)
        {
            var progression = match.Groups[1].Value;
            if (!knowledge.ChordProgressions.Contains(progression))
            {
                knowledge.ChordProgressions.Add(progression);
            }
        }

        // Extract scale names
        var scalePattern = @"\b((?:Major|Minor|Dorian|Phrygian|Lydian|Mixolydian|Aeolian|Locrian|Harmonic|Melodic|Pentatonic|Blues|Chromatic)\s+(?:scale|mode))\b";
        var scaleMatches = Regex.Matches(text, scalePattern, RegexOptions.IgnoreCase);
        foreach (Match match in scaleMatches)
        {
            var scale = match.Groups[1].Value;
            if (!knowledge.Scales.Contains(scale))
            {
                knowledge.Scales.Add(scale);
            }
        }

        // Extract guitar techniques
        var techniquePattern = @"\b(alternate picking|sweep picking|legato|hammer-on|pull-off|tapping|bending|vibrato|slide|palm muting|fingerstyle|hybrid picking)\b";
        var techniqueMatches = Regex.Matches(text, techniquePattern, RegexOptions.IgnoreCase);
        foreach (Match match in techniqueMatches)
        {
            var technique = match.Groups[1].Value;
            if (!knowledge.Techniques.Contains(technique))
            {
                knowledge.Techniques.Add(technique);
            }
        }
    }

    // Helper class for JSON deserialization
    private class JsonKnowledge
    {
        public List<string>? ChordProgressions { get; set; }
        public List<string>? Scales { get; set; }
        public List<string>? Techniques { get; set; }
        public Dictionary<string, string>? Concepts { get; set; }
        public List<string>? Styles { get; set; }
    }
}

