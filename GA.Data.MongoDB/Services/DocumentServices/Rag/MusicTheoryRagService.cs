namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using GA.Data.MongoDB.Models.Rag;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.Extensions.Logging;
using global::MongoDB.Driver;

/// <summary>
/// Multi-stage RAG service for music theory knowledge base
/// Stage 1: Document analysis and summarization
/// Stage 2: Deep knowledge extraction and embedding generation
/// </summary>
public class MusicTheoryRagService(
    ILogger<MusicTheoryRagService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    : MultiStageRagService<MusicTheoryRagDocument>(logger, mongoDb, embeddingService)
{
    protected override IMongoCollection<MusicTheoryRagDocument> GetCollection()
    {
        var database = typeof(MongoDbService)
            .GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(MongoDb) as IMongoDatabase
            ?? throw new InvalidOperationException("Could not access MongoDB database");

        return database.GetCollection<MusicTheoryRagDocument>("music_theory_knowledge_base_rag");
    }

    /// <summary>
    /// Stage 1: Analyze and summarize music theory documents
    /// This stage performs lightweight analysis without expensive LLM calls
    /// </summary>
    protected override async Task<List<MusicTheoryRagDocument>> AnalyzeAndSummarizeAsync(
        List<MusicTheoryRagDocument> documents)
    {
        Logger.LogInformation("Stage 1: Analyzing {Count} music theory documents", documents.Count);

        foreach (var doc in documents)
        {
            // Basic analysis: extract patterns from content using regex or simple parsing
            doc.ChordScaleRelationships = ExtractChordScaleRelationships(doc.Content);
            doc.VoiceLeadingRules = ExtractVoiceLeadingRules(doc.Content);
            doc.HarmonicProgressions = ExtractHarmonicProgressions(doc.Content);
            doc.ChordSubstitutions = ExtractChordSubstitutions(doc.Content);
            doc.ModalTheory = ExtractModalTheory(doc.Content);
            doc.JazzHarmonyConcepts = ExtractJazzHarmonyConcepts(doc.Content);
            doc.FunctionalHarmony = ExtractFunctionalHarmony(doc.Content);
            doc.TheoreticalConcepts = ExtractTheoreticalConcepts(doc.Content);
            
            // Generate search text for embedding
            doc.GenerateSearchText();
        }

        return await Task.FromResult(documents);
    }

    /// <summary>
    /// Stage 2: Deep knowledge extraction and embedding generation
    /// This stage performs expensive operations using LLM and embedding services
    /// </summary>
    protected override async Task<List<MusicTheoryRagDocument>> DeepProcessAsync(
        List<MusicTheoryRagDocument> documents)
    {
        Logger.LogInformation("Stage 2: Deep processing {Count} music theory documents", documents.Count);

        foreach (var doc in documents)
        {
            // Generate embeddings for semantic search
            var embedding = await EmbeddingService.GenerateEmbeddingAsync(doc.SearchText);
            doc.Embedding = embedding.ToArray();
        }

        return documents;
    }

    /// <summary>
    /// Search for music theory concepts by query
    /// </summary>
    public async Task<List<MusicTheoryRagDocument>> SearchAsync(
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

            Logger.LogInformation("Found {Count} music theory documents matching query: {Query}",
                rankedDocs.Count, query);

            return rankedDocs;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching music theory documents");
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
    
    private List<string> ExtractChordScaleRelationships(string content)
    {
        var relationships = new List<string>();
        
        // Look for common chord-scale patterns
        var patterns = new[] { "over", "on", "with", "uses", "works with" };
        var chordTypes = new[] { "major", "minor", "dominant", "diminished", "augmented", "half-diminished", "altered" };
        var scaleTypes = new[] { "Dorian", "Mixolydian", "Lydian", "Phrygian", "Locrian", "Aeolian", "Ionian", "altered", "whole tone", "diminished" };
        
        foreach (var pattern in patterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Extract sentences containing the pattern
                var sentences = content.Split('.', StringSplitOptions.RemoveEmptyEntries);
                relationships.AddRange(sentences
                    .Where(s => s.Contains(pattern, StringComparison.OrdinalIgnoreCase) &&
                               (chordTypes.Any(c => s.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                                scaleTypes.Any(sc => s.Contains(sc, StringComparison.OrdinalIgnoreCase))))
                    .Select(s => s.Trim())
                    .Take(5));
            }
        }
        
        return relationships.Distinct().ToList();
    }

    private List<string> ExtractVoiceLeadingRules(string content)
    {
        var rules = new List<string>();
        
        // Look for voice leading keywords
        var keywords = new[] { "voice leading", "common tone", "contrary motion", "parallel", "oblique motion", "voice crossing", "doubling" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(keyword);
            }
        }
        
        return rules.Distinct().ToList();
    }

    private List<string> ExtractHarmonicProgressions(string content)
    {
        var progressions = new List<string>();
        
        // Look for common progression patterns
        var progressionKeywords = new[] { "ii-V-I", "I-IV-V", "I-vi-IV-V", "circle of fifths", "turnaround", "cadence", "modulation" };
        foreach (var keyword in progressionKeywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                progressions.Add(keyword);
            }
        }
        
        return progressions.Distinct().ToList();
    }

    private List<string> ExtractChordSubstitutions(string content)
    {
        var substitutions = new List<string>();
        
        // Look for substitution keywords
        var keywords = new[] { "tritone substitution", "diminished substitution", "secondary dominant", "modal interchange", "borrowed chord", "substitute" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                substitutions.Add(keyword);
            }
        }
        
        return substitutions.Distinct().ToList();
    }

    private List<string> ExtractModalTheory(string content)
    {
        var modes = new List<string>();
        
        // Look for mode names
        var modeNames = new[] { "Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian" };
        foreach (var mode in modeNames)
        {
            if (content.Contains(mode, StringComparison.OrdinalIgnoreCase))
            {
                modes.Add(mode);
            }
        }
        
        return modes.Distinct().ToList();
    }

    private List<string> ExtractJazzHarmonyConcepts(string content)
    {
        var concepts = new List<string>();
        
        // Look for jazz harmony keywords
        var keywords = new[] { "upper structure", "polychord", "quartal harmony", "slash chord", "sus chord", "add chord", "tension", "extension" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                concepts.Add(keyword);
            }
        }
        
        return concepts.Distinct().ToList();
    }

    private List<string> ExtractFunctionalHarmony(string content)
    {
        var functions = new List<string>();
        
        // Look for functional harmony keywords
        var keywords = new[] { "tonic", "subdominant", "dominant", "pre-dominant", "cadential", "resolution" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                functions.Add(keyword);
            }
        }
        
        return functions.Distinct().ToList();
    }

    private List<string> ExtractTheoreticalConcepts(string content)
    {
        var concepts = new List<string>();
        
        // Look for theoretical concepts
        var keywords = new[] { "enharmonic", "voice leading space", "neo-Riemannian", "Schenkerian", "set theory", "pitch class", "interval vector" };
        foreach (var keyword in keywords)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                concepts.Add(keyword);
            }
        }
        
        return concepts.Distinct().ToList();
    }
}

