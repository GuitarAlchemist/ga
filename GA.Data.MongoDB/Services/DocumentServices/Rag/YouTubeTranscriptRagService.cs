namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using Embeddings;
using Microsoft.Extensions.Logging;
using Models.Rag;
using Models.References;
using global::MongoDB.Driver;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Multi-stage RAG service for YouTube transcripts
/// Stage 1: Summarize transcript and extract topics (lightweight)
/// Stage 2: Deep knowledge extraction (chord progressions, scales, techniques) and embedding generation
/// </summary>
[UsedImplicitly]
public sealed class YouTubeTranscriptRagService(
    ILogger<YouTubeTranscriptRagService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    : MultiStageRagService<YouTubeTranscriptRagDocument>(logger, mongoDb, embeddingService),
        IRagSyncService<YouTubeTranscriptRagDocument>
{
    /// <summary>
    /// Stage 1: Analyze transcripts and generate summaries
    /// This stage extracts basic information without expensive LLM calls
    /// </summary>
    protected override async Task<List<YouTubeTranscriptRagDocument>> AnalyzeAndSummarizeAsync(
        List<YouTubeTranscriptRagDocument> documents)
    {
        Logger.LogInformation("Stage 1: Analyzing {Count} YouTube transcripts", documents.Count);

        foreach (var doc in documents)
        {
            // Extract topics using simple keyword matching (no LLM needed)
            doc.Topics = ExtractTopicsFromTranscript(doc.Transcript);

            // Generate simple summary (first 500 characters + ellipsis)
            doc.Summary = GenerateSimpleSummary(doc.Transcript);

            // Estimate difficulty level based on keywords
            doc.DifficultyLevel = EstimateDifficultyLevel(doc.Transcript);
        }

        Logger.LogInformation("Stage 1 complete: Analyzed {Count} transcripts", documents.Count);
        return await Task.FromResult(documents);
    }

    /// <summary>
    /// Stage 2: Deep knowledge extraction and embedding generation
    /// This stage performs expensive operations using LLM and embedding services
    /// </summary>
    protected override async Task<List<YouTubeTranscriptRagDocument>> DeepProcessAsync(
        List<YouTubeTranscriptRagDocument> documents)
    {
        Logger.LogInformation("Stage 2: Deep processing {Count} YouTube transcripts", documents.Count);

        foreach (var doc in documents)
        {
            // Extract structured knowledge from transcript
            doc.ChordProgressions = ExtractChordProgressions(doc.Transcript);
            doc.Scales = ExtractScales(doc.Transcript);
            doc.Techniques = ExtractTechniques(doc.Transcript);
            doc.TheoryConcepts = ExtractTheoryConcepts(doc.Transcript);
        }

        // Generate embeddings for all documents
        await GenerateEmbeddingsAsync(documents);

        Logger.LogInformation("Stage 2 complete: Processed {Count} transcripts", documents.Count);
        return documents;
    }

    protected override IMongoCollection<YouTubeTranscriptRagDocument> GetCollection()
    {
        // Access the private _database field through reflection or add a public property
        // For now, we'll create a new collection accessor
        var databaseField = typeof(MongoDbService).GetField("_database",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var database = (IMongoDatabase)databaseField!.GetValue(MongoDb)!;
        return database.GetCollection<YouTubeTranscriptRagDocument>("youtube_transcripts_rag");
    }

    /// <summary>
    /// Sync all YouTube transcripts (placeholder - actual sync would come from autonomous curation)
    /// </summary>
    public async Task<bool> SyncAsync()
    {
        Logger.LogInformation("YouTube transcript sync is managed by autonomous curation system");
        return await Task.FromResult(true);
    }

    #region Stage 1 Helper Methods (Lightweight)

    private static List<string> ExtractTopicsFromTranscript(string transcript)
    {
        var topics = new List<string>();
        var lowerTranscript = transcript.ToLowerInvariant();

        // Music theory topics
        var topicKeywords = new Dictionary<string, string[]>
        {
            ["Chord Progressions"] = ["progression", "ii-v-i", "i-iv-v", "chord sequence"],
            ["Scales"] = ["scale", "mode", "dorian", "mixolydian", "pentatonic"],
            ["Harmony"] = ["harmony", "harmonic", "voice leading", "chord tones"],
            ["Rhythm"] = ["rhythm", "timing", "groove", "syncopation"],
            ["Improvisation"] = ["improvisation", "improv", "solo", "soloing"],
            ["Technique"] = ["technique", "fingering", "picking", "strumming"],
            ["Theory"] = ["theory", "interval", "diatonic", "chromatic"],
            ["Jazz"] = ["jazz", "bebop", "swing"],
            ["Blues"] = ["blues", "12-bar", "shuffle"],
            ["Rock"] = ["rock", "power chord", "riff"]
        };

        foreach (var (topic, keywords) in topicKeywords)
        {
            if (keywords.Any(keyword => lowerTranscript.Contains(keyword)))
            {
                topics.Add(topic);
            }
        }

        return topics;
    }

    private static string GenerateSimpleSummary(string transcript)
    {
        // Simple summary: first 500 characters
        if (transcript.Length <= 500)
            return transcript;

        var summary = transcript[..500];
        var lastSpace = summary.LastIndexOf(' ');
        if (lastSpace > 0)
            summary = summary[..lastSpace];

        return summary + "...";
    }

    private static string EstimateDifficultyLevel(string transcript)
    {
        var lowerTranscript = transcript.ToLowerInvariant();

        // Advanced keywords
        var advancedKeywords = new[] { "altered", "diminished", "augmented", "modal interchange", "reharmonization" };
        if (advancedKeywords.Any(keyword => lowerTranscript.Contains(keyword)))
            return "Advanced";

        // Intermediate keywords
        var intermediateKeywords = new[] { "seventh chord", "extension", "substitution", "voice leading" };
        if (intermediateKeywords.Any(keyword => lowerTranscript.Contains(keyword)))
            return "Intermediate";

        // Default to beginner
        return "Beginner";
    }

    #endregion

    #region Stage 2 Helper Methods (Deep Processing)

    private static List<ProgressionReference> ExtractChordProgressions(string transcript)
    {
        var progressions = new List<ProgressionReference>();

        // Common progression patterns
        var progressionPatterns = new Dictionary<string, List<string>>
        {
            [@"ii-?v-?i"] = ["ii", "V", "I"],
            [@"i-?iv-?v"] = ["I", "IV", "V"],
            [@"i-?vi-?iv-?v"] = ["I", "vi", "IV", "V"],
            [@"vi-?ii-?v-?i"] = ["vi", "ii", "V", "I"],
            [@"iii-?vi-?ii-?v"] = ["iii", "vi", "ii", "V"]
        };

        foreach (var (pattern, chords) in progressionPatterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            if (regex.IsMatch(transcript))
            {
                progressions.Add(new ProgressionReference(
                    pattern.Replace(@"-?", "-").ToUpperInvariant(),
                    chords
                ));
            }
        }

        return progressions;
    }

    private static List<ScaleReference> ExtractScales(string transcript)
    {
        var scales = new List<ScaleReference>();
        var lowerTranscript = transcript.ToLowerInvariant();

        // Common scales with their notes (simplified - just using scale names)
        var scalePatterns = new[]
        {
            "major", "minor", "pentatonic", "blues", "dorian", "mixolydian",
            "phrygian", "lydian", "locrian", "harmonic minor", "melodic minor"
        };

        foreach (var scaleName in scalePatterns)
        {
            if (lowerTranscript.Contains(scaleName))
            {
                // ScaleReference(string Name, string Category, List<string> Notes)
                scales.Add(new ScaleReference(
                    scaleName,
                    "Diatonic", // Simplified category
                    [] // Empty notes list for now
                ));
            }
        }

        return scales;
    }

    private static List<string> ExtractTechniques(string transcript)
    {
        var techniques = new List<string>();
        var lowerTranscript = transcript.ToLowerInvariant();

        var techniqueKeywords = new[]
        {
            "fingerpicking", "strumming", "picking", "hammer-on", "pull-off",
            "bending", "vibrato", "slide", "tapping", "sweep picking",
            "alternate picking", "economy picking", "legato", "palm muting",
            "harmonics", "tremolo", "arpeggiation"
        };

        foreach (var technique in techniqueKeywords)
        {
            if (lowerTranscript.Contains(technique))
            {
                techniques.Add(technique);
            }
        }

        return techniques.Distinct().ToList();
    }

    private static List<string> ExtractTheoryConcepts(string transcript)
    {
        var concepts = new List<string>();
        var lowerTranscript = transcript.ToLowerInvariant();

        var conceptKeywords = new[]
        {
            "interval", "chord tone", "tension", "resolution", "voice leading",
            "harmonic function", "tonic", "dominant", "subdominant",
            "diatonic", "chromatic", "modal", "cadence", "modulation",
            "key signature", "time signature", "rhythm", "meter"
        };

        foreach (var concept in conceptKeywords)
        {
            if (lowerTranscript.Contains(concept))
            {
                concepts.Add(concept);
            }
        }

        return concepts.Distinct().ToList();
    }

    #endregion
}

