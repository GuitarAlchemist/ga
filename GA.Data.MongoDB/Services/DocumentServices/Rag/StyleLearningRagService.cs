namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using GA.Data.MongoDB.Models.Rag;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.Extensions.Logging;
using global::MongoDB.Driver;

/// <summary>
/// Multi-stage RAG service for style learning library
/// Stage 1: Document analysis and summarization
/// Stage 2: Deep style extraction and embedding generation
/// </summary>
public class StyleLearningRagService(
    ILogger<StyleLearningRagService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    : MultiStageRagService<StyleLearningRagDocument>(logger, mongoDb, embeddingService)
{
    protected override IMongoCollection<StyleLearningRagDocument> GetCollection()
    {
        var database = typeof(MongoDbService)
            .GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(MongoDb) as IMongoDatabase
            ?? throw new InvalidOperationException("Could not access MongoDB database");

        return database.GetCollection<StyleLearningRagDocument>("style_learning_library_rag");
    }

    /// <summary>
    /// Stage 1: Analyze and summarize style learning documents
    /// This stage performs lightweight analysis without expensive LLM calls
    /// </summary>
    protected override async Task<List<StyleLearningRagDocument>> AnalyzeAndSummarizeAsync(
        List<StyleLearningRagDocument> documents)
    {
        Logger.LogInformation("Stage 1: Analyzing {Count} style learning documents", documents.Count);

        foreach (var doc in documents)
        {
            // Basic analysis: extract patterns from content using regex or simple parsing
            doc.CharacteristicProgressions = ExtractProgressions(doc.Content);
            doc.SignatureVoicings = ExtractVoicings(doc.Content);
            doc.MelodicPatterns = ExtractMelodicPatterns(doc.Content);
            doc.RhythmicCharacteristics = ExtractRhythmicCharacteristics(doc.Content);
            doc.HarmonicTechniques = ExtractHarmonicTechniques(doc.Content);
            doc.PlayingTechniques = ExtractPlayingTechniques(doc.Content);
            doc.TonalPreferences = ExtractTonalPreferences(doc.Content);
            doc.StylisticInfluences = ExtractStylisticInfluences(doc.Content);
            
            // Generate search text for embedding
            doc.GenerateSearchText();
        }

        return await Task.FromResult(documents);
    }

    /// <summary>
    /// Stage 2: Deep style extraction and embedding generation
    /// This stage performs expensive operations using LLM and embedding services
    /// </summary>
    protected override async Task<List<StyleLearningRagDocument>> DeepProcessAsync(
        List<StyleLearningRagDocument> documents)
    {
        Logger.LogInformation("Stage 2: Deep processing {Count} style learning documents", documents.Count);

        foreach (var doc in documents)
        {
            // Generate embeddings for semantic search
            var embedding = await EmbeddingService.GenerateEmbeddingAsync(doc.SearchText);
            doc.Embedding = embedding.ToArray();
        }

        return documents;
    }

    /// <summary>
    /// Search for style characteristics by query
    /// </summary>
    public async Task<List<StyleLearningRagDocument>> SearchAsync(
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
                    Similarity = CosineSimilarity(queryEmbedding.ToArray(), doc.Embedding)
                })
                .Where(x => x.Similarity >= minSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Take(limit)
                .Select(x => x.Document)
                .ToList();

            Logger.LogInformation("Found {Count} style learning documents matching query: {Query}",
                rankedDocs.Count, query);

            return rankedDocs;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching style learning documents");
            return [];
        }
    }

    /// <summary>
    /// Search for documents by artist or style name
    /// </summary>
    public async Task<List<StyleLearningRagDocument>> SearchByArtistOrStyleAsync(
        string artistOrStyle,
        int limit = 10)
    {
        try
        {
            var collection = GetCollection();
            var filter = Builders<StyleLearningRagDocument>.Filter.Regex(
                x => x.ArtistOrStyle,
                new global::MongoDB.Bson.BsonRegularExpression(artistOrStyle, "i"));

            var docs = await collection.Find(filter).Limit(limit).ToListAsync();

            Logger.LogInformation("Found {Count} documents for artist/style: {ArtistOrStyle}",
                docs.Count, artistOrStyle);

            return docs;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching by artist/style");
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
    
    private List<string> ExtractProgressions(string content)
    {
        var progressions = new List<string>();
        
        // Look for common progression patterns
        var patterns = new[] { "ii-V-I", "I-IV-V", "I-vi-IV-V", "iii-vi-ii-V", "turnaround", "rhythm changes" };
        foreach (var pattern in patterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                progressions.Add(pattern);
            }
        }
        
        return progressions.Distinct().ToList();
    }

    private List<string> ExtractVoicings(string content)
    {
        var voicings = new List<string>();
        
        // Look for voicing keywords
        var keywords = new[] { "drop-2", "drop-3", "rootless", "shell voicing", "quartal", "cluster", "spread voicing" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                voicings.Add(keyword);
            }
        }
        
        return voicings.Distinct().ToList();
    }

    private List<string> ExtractMelodicPatterns(string content)
    {
        var patterns = new List<string>();
        
        // Look for melodic pattern keywords
        var keywords = new[] { "bebop", "chromatic", "diatonic", "pentatonic", "blues scale", "arpeggio", "sequence" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                patterns.Add(keyword);
            }
        }
        
        return patterns.Distinct().ToList();
    }

    private List<string> ExtractRhythmicCharacteristics(string content)
    {
        var characteristics = new List<string>();
        
        // Look for rhythmic keywords
        var keywords = new[] { "swing", "syncopation", "straight eighth", "triplet", "polyrhythm", "displacement" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                characteristics.Add(keyword);
            }
        }
        
        return characteristics.Distinct().ToList();
    }

    private List<string> ExtractHarmonicTechniques(string content)
    {
        var techniques = new List<string>();
        
        // Look for harmonic technique keywords
        var keywords = new[] { "reharmonization", "tritone substitution", "modal interchange", "secondary dominant", "diminished substitution" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                techniques.Add(keyword);
            }
        }
        
        return techniques.Distinct().ToList();
    }

    private List<string> ExtractPlayingTechniques(string content)
    {
        var techniques = new List<string>();
        
        // Look for playing technique keywords
        var keywords = new[] { "fingerstyle", "pick", "hybrid picking", "sweep picking", "legato", "staccato", "vibrato", "bending" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                techniques.Add(keyword);
            }
        }
        
        return techniques.Distinct().ToList();
    }

    private List<string> ExtractTonalPreferences(string content)
    {
        var preferences = new List<string>();
        
        // Look for key/mode/scale keywords
        var keywords = new[] { "major", "minor", "Dorian", "Mixolydian", "Lydian", "blues", "pentatonic" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                preferences.Add(keyword);
            }
        }
        
        return preferences.Distinct().ToList();
    }

    private List<string> ExtractStylisticInfluences(string content)
    {
        var influences = new List<string>();
        
        // Look for style/genre keywords
        var keywords = new[] { "bebop", "cool jazz", "hard bop", "fusion", "blues", "rock", "funk", "latin" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                influences.Add(keyword);
            }
        }
        
        return influences.Distinct().ToList();
    }
}

