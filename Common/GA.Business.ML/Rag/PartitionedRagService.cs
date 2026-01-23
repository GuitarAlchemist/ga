namespace GA.Business.ML.Rag;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GA.Data.MongoDB.Models.Rag;
using GA.Data.MongoDB.Services.DocumentServices.Rag;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of IPartitionedRagService that aggregates results from specialized
/// MongoDB RAG collections based on knowledge partitions.
/// </summary>
public class PartitionedRagService(
    ILogger<PartitionedRagService> logger,
    MusicTheoryRagService theoryService,
    GuitarTechniqueRagService techniqueService,
    StyleLearningRagService styleService,
    YouTubeTranscriptRagService youtubeService,
    EnhancedChordRagService chordService) : IPartitionedRagService
{
    private readonly ILogger<PartitionedRagService> _logger = logger;

    public async Task<PartitionedRagResponse> QueryAsync(string query, KnowledgeType[] partitions, int topK)
    {
        _logger.LogInformation("Partitioned RAG Query: '{Query}' in Partitions: {Partitions}", 
            query, string.Join(", ", partitions.Select(p => p.ToString())));

        var tasks = new List<Task<IEnumerable<RagResult>>>();

        foreach (var partition in partitions)
        {
            tasks.Add(SearchPartitionAsync(query, partition, topK));
        }

        var resultsArray = await Task.WhenAll(tasks);
        var allResults = resultsArray.SelectMany(r => r)
            .OrderByDescending(r => r.Score)
            .ToList();

        var counts = allResults.GroupBy(r => r.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PartitionedRagResponse(query, allResults, counts);
    }

    /// <summary>
    /// Parses a raw query into a structured form, identifying musical entities
    /// to better route to specific partitions.
    /// </summary>
    public StructuredMusicalQuery ParseStructuredQuery(string rawQuery)
    {
        var chords = new List<string>();
        var scales = new List<string>();
        var techniques = new List<string>();
        var intents = new List<KnowledgeType>();

        // Basic Regex for Chords (e.g., Cmaj7, Dm7, G7#9)
        var chordRegex = new Regex(@"\b[A-G][#b]?(?:maj|min|m|dim|aug|sus)?\d*[#b]?\d*\b", RegexOptions.IgnoreCase);
        foreach (Match match in chordRegex.Matches(rawQuery))
        {
            chords.Add(match.Value);
        }

        // Basic Regex for Scales (e.g., C Major, D Lydian)
        var scaleKeywords = new[] { "Major", "Minor", "Pentatonic", "Blues", "Lydian", "Mixolydian", "Dorian", "Phrygian", "Locrian" };
        var scaleRegex = new Regex(@"\b[A-G][#b]?\s+(?:" + string.Join("|", scaleKeywords) + @")\b", RegexOptions.IgnoreCase);
        foreach (Match match in scaleRegex.Matches(rawQuery))
        {
            scales.Add(match.Value);
        }

        // Technique detection
        var techniqueKeywords = new[] { "Sweep", "Alternate picking", "Economy picking", "Legato", "Tapping", "Hybrid picking", "Drop-2", "Drop-3" };
        foreach (var tech in techniqueKeywords)
        {
            if (rawQuery.Contains(tech, StringComparison.OrdinalIgnoreCase))
            {
                techniques.Add(tech);
                intents.Add(KnowledgeType.Technique);
            }
        }

        // Implicit intent detection
        if (chords.Any() || scales.Any() || rawQuery.Contains("theory", StringComparison.OrdinalIgnoreCase))
            intents.Add(KnowledgeType.Theory);
        
        if (rawQuery.Contains("video", StringComparison.OrdinalIgnoreCase) || rawQuery.Contains("youtube", StringComparison.OrdinalIgnoreCase))
            intents.Add(KnowledgeType.Corpus);

        return new StructuredMusicalQuery(
            rawQuery,
            chords.Distinct().ToList(),
            scales.Distinct().ToList(),
            techniques.Distinct().ToList(),
            intents.Distinct().ToList());
    }

    private async Task<IEnumerable<RagResult>> SearchPartitionAsync(string query, KnowledgeType partition, int topK)
    {
        try
        {
            switch (partition)
            {
                case KnowledgeType.Theory:
                    var theoryResults = await theoryService.SearchWithScoresAsync(query, topK);
                    return theoryResults.Select(r => new RagResult(
                        r.Document.Id.ToString(),
                        r.Document.Content,
                        r.Score,
                        KnowledgeType.Theory,
                        r.Document.Title,
                        r.Document.SourceUrl ?? ""));

                case KnowledgeType.Technique:
                    var techResults = await techniqueService.SearchWithScoresAsync(query, topK);
                    return techResults.Select(r => new RagResult(
                        r.Document.Id.ToString(),
                        r.Document.Content,
                        r.Score,
                        KnowledgeType.Technique,
                        r.Document.Title,
                        r.Document.SourceUrl ?? ""));

                case KnowledgeType.Corpus:
                    var chordTask = chordService.SearchWithScoresAsync(query, topK);
                    var youtubeTask = youtubeService.SearchWithScoresAsync(query, topK);
                    var styleTask = styleService.SearchWithScoresAsync(query, topK);

                    await Task.WhenAll(chordTask, youtubeTask, styleTask);

                    var combinedCorpus = chordTask.Result.Select(r => new RagResult(
                        r.Document.Id.ToString(),
                        $"Chord: {r.Document.Name} | {r.Document.Quality} | Notes: {string.Join(", ", r.Document.Notes)}",
                        r.Score,
                        KnowledgeType.Corpus,
                        r.Document.Name))
                    .Concat(youtubeTask.Result.Select(r => new RagResult(
                        r.Document.Id.ToString(),
                        r.Document.Transcript,
                        r.Score,
                        KnowledgeType.Corpus,
                        r.Document.Title,
                        r.Document.Url)))
                    .Concat(styleTask.Result.Select(r => new RagResult(
                        r.Document.Id.ToString(),
                        r.Document.Content,
                        r.Score,
                        KnowledgeType.Corpus,
                        $"{r.Document.Title}{(string.IsNullOrEmpty(r.Document.ArtistOrStyle) ? "" : $" - {r.Document.ArtistOrStyle}")}",
                        r.Document.SourceUrl ?? "")))
                    .OrderByDescending(r => r.Score)
                    .Take(topK);

                    return combinedCorpus;

                case KnowledgeType.Rules:
                    var ruleResults = await theoryService.SearchWithScoresAsync($"Constraint invariant rule: {query}", topK);
                    return ruleResults.Select(r => new RagResult(
                        r.Document.Id.ToString(),
                        r.Document.Content,
                        r.Score,
                        KnowledgeType.Rules,
                        r.Document.Title,
                        r.Document.SourceUrl ?? ""));

                default:
                    return Enumerable.Empty<RagResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching partition {Partition}", partition);
            return Enumerable.Empty<RagResult>();
        }
    }
}
