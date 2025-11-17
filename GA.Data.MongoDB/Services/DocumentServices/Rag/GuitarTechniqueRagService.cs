namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using GA.Data.MongoDB.Models.Rag;
using Embeddings;
using Microsoft.Extensions.Logging;
using global::MongoDB.Driver;

/// <summary>
/// Multi-stage RAG service for guitar technique library
/// Stage 1: Document analysis and summarization
/// Stage 2: Deep knowledge extraction and embedding generation
/// </summary>
public class GuitarTechniqueRagService(
    ILogger<GuitarTechniqueRagService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    : MultiStageRagService<GuitarTechniqueRagDocument>(logger, mongoDb, embeddingService)
{
    protected override IMongoCollection<GuitarTechniqueRagDocument> GetCollection()
    {
        var database = typeof(MongoDbService)
            .GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(MongoDb) as IMongoDatabase
            ?? throw new InvalidOperationException("Could not access MongoDB database");

        return database.GetCollection<GuitarTechniqueRagDocument>("guitar_technique_library_rag");
    }

    /// <summary>
    /// Stage 1: Analyze and summarize guitar technique documents
    /// This stage performs lightweight analysis without expensive LLM calls
    /// </summary>
    protected override async Task<List<GuitarTechniqueRagDocument>> AnalyzeAndSummarizeAsync(
        List<GuitarTechniqueRagDocument> documents)
    {
        Logger.LogInformation("Stage 1: Analyzing {Count} guitar technique documents", documents.Count);

        foreach (var doc in documents)
        {
            // Basic analysis: extract patterns from content using regex or simple parsing
            doc.FingeringPatterns = ExtractFingeringPatterns(doc.Content);
            doc.Exercises = ExtractExercises(doc.Content);
            doc.ChordVoicings = ExtractChordVoicings(doc.Content);
            doc.Techniques = ExtractTechniques(doc.Content);
            doc.Progressions = ExtractProgressions(doc.Content);
            doc.Styles = ExtractStyles(doc.Content);
            doc.FretboardPositions = ExtractFretboardPositions(doc.Content);

            // Generate search text for embedding
            doc.GenerateSearchText();
        }

        return await Task.FromResult(documents);
    }

    /// <summary>
    /// Stage 2: Deep knowledge extraction and embedding generation
    /// This stage performs expensive operations using LLM and embedding services
    /// </summary>
    protected override async Task<List<GuitarTechniqueRagDocument>> DeepProcessAsync(
        List<GuitarTechniqueRagDocument> documents)
    {
        Logger.LogInformation("Stage 2: Deep processing {Count} guitar technique documents", documents.Count);

        foreach (var doc in documents)
        {
            // Generate embeddings for semantic search
            var embedding = await EmbeddingService.GenerateEmbeddingAsync(doc.SearchText);
            doc.Embedding = [.. embedding];
        }

        return documents;
    }

    /// <summary>
    /// Search for guitar techniques by query
    /// </summary>
    public async Task<List<GuitarTechniqueRagDocument>> SearchAsync(
        string query,
        int limit = 10,
        double minSimilarity = 0.7)
    {
        try
        {
            // Generate embedding for query
            var queryEmbedding = await EmbeddingService.GenerateEmbeddingAsync(query);

            // Get collection
            var collection = GetCollection();

            // Get all documents (in production, use vector search index)
            var allDocs = await collection.Find(_ => true).ToListAsync();

            // Calculate cosine similarity and rank
            var rankedDocs = allDocs
                .Select(doc => new
                {
                    Document = doc,
                    Similarity = CosineSimilarity([.. queryEmbedding], doc.Embedding)
                })
                .Where(x => x.Similarity >= minSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Take(limit)
                .Select(x => x.Document)
                .ToList();

            Logger.LogInformation("Found {Count} guitar technique documents matching query: {Query}",
                rankedDocs.Count, query);

            return rankedDocs;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching guitar technique documents");
            return [];
        }
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0) return 0;

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }

    // Extraction methods (simple pattern matching - can be enhanced with LLM)

    private List<string> ExtractFingeringPatterns(string content)
    {
        var patterns = new List<string>();

        // Look for fret number patterns like "3-5-7" or "x-3-2-0-1-0"
        var fretPatternRegex = new System.Text.RegularExpressions.Regex(@"\b[\dx]-[\dx]-[\dx]");
        var matches = fretPatternRegex.Matches(content);
        patterns.AddRange(matches.Select(m => m.Value).Distinct());

        // Look for CAGED references
        if (content.Contains("CAGED", StringComparison.OrdinalIgnoreCase))
        {
            patterns.Add("CAGED system");
        }

        return [.. patterns.Distinct()];
    }

    private List<string> ExtractExercises(string content)
    {
        var exercises = new List<string>();

        // Look for exercise keywords
        var exerciseKeywords = new[] { "exercise", "drill", "practice", "workout", "etude" };
        foreach (var keyword in exerciseKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Extract sentence containing the keyword
                var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);
                exercises.AddRange(sentences
                    .Where(s => s.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Trim())
                    .Take(3));
            }
        }

        return [.. exercises.Distinct()];
    }

    private List<string> ExtractChordVoicings(string content)
    {
        var voicings = new List<string>();

        // Look for chord voicing keywords
        var voicingKeywords = new[] { "drop-2", "drop-3", "shell voicing", "rootless", "close voicing", "open voicing" };
        foreach (var keyword in voicingKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                voicings.Add(keyword);
            }
        }

        // Look for chord symbols with extensions (e.g., Cmaj7, Dm7b5)
        var chordRegex = new System.Text.RegularExpressions.Regex(@"\b[A-G][#b]?(maj|min|m|dim|aug|sus)?\d*[#b]?\d*\b");
        var matches = chordRegex.Matches(content);
        voicings.AddRange(matches.Select(m => m.Value).Distinct().Take(10));

        return [.. voicings.Distinct()];
    }

    private List<string> ExtractTechniques(string content)
    {
        var techniques = new List<string>();

        // Common guitar techniques
        var techniqueKeywords = new[]
        {
            "alternate picking", "sweep picking", "economy picking", "hybrid picking",
            "legato", "tapping", "hammer-on", "pull-off", "slide", "bend",
            "vibrato", "tremolo", "palm muting", "fingerpicking", "fingerstyle",
            "strumming", "arpeggiation", "string skipping"
        };

        foreach (var keyword in techniqueKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                techniques.Add(keyword);
            }
        }

        return [.. techniques.Distinct()];
    }

    private List<string> ExtractProgressions(string content)
    {
        var progressions = new List<string>();

        // Look for common progression patterns
        var progressionKeywords = new[] { "ii-V-I", "I-IV-V", "I-vi-IV-V", "12-bar blues", "turnaround", "progression" };
        foreach (var keyword in progressionKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                progressions.Add(keyword);
            }
        }

        return [.. progressions.Distinct()];
    }

    private List<string> ExtractStyles(string content)
    {
        var styles = new List<string>();

        // Musical styles
        var styleKeywords = new[]
        {
            "jazz", "blues", "rock", "classical", "flamenco", "country",
            "metal", "funk", "soul", "R&B", "folk", "fingerstyle",
            "bebop", "swing", "latin", "bossa nova"
        };

        foreach (var keyword in styleKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                styles.Add(keyword);
            }
        }

        return [.. styles.Distinct()];
    }

    private List<string> ExtractFretboardPositions(string content)
    {
        var positions = new List<string>();

        // Look for position references (e.g., "Position V", "5th position")
        var positionRegex = new System.Text.RegularExpressions.Regex(@"(?:position\s+)?([IVX]+|[0-9]+(?:st|nd|rd|th))\s+position", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var matches = positionRegex.Matches(content);
        positions.AddRange(matches.Select(m => m.Value).Distinct());

        // Look for string set references (e.g., "strings 1-3", "top 3 strings")
        var stringSetRegex = new System.Text.RegularExpressions.Regex(@"strings?\s+[1-6](?:-[1-6])?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        matches = stringSetRegex.Matches(content);
        positions.AddRange(matches.Select(m => m.Value).Distinct());

        return [.. positions.Distinct()];
    }
}

