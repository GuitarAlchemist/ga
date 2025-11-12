namespace GaApi.Services.DocumentProcessing;

using GA.Data.MongoDB;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

/// <summary>
/// Specialized processor for guitar method books and technique libraries
/// Extracts fingering patterns, exercises, progressions, and technique descriptions
/// Enables chatbot queries like "show me jazz voicings for ii-V-I"
/// </summary>
public class GuitarTechniqueProcessor
{
    private readonly ILogger<GuitarTechniqueProcessor> _logger;
    private readonly MongoDbService _mongoDb;
    private readonly IOllamaChatService _ollamaChat;
    private readonly IEmbeddingService _embeddingService;

    public GuitarTechniqueProcessor(
        ILogger<GuitarTechniqueProcessor> logger,
        MongoDbService mongoDb,
        IOllamaChatService ollamaChat,
        IEmbeddingService embeddingService)
    {
        _logger = logger;
        _mongoDb = mongoDb;
        _ollamaChat = ollamaChat;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Process a guitar technique document (PDF, markdown, or text)
    /// </summary>
    public async Task<GuitarTechniqueProcessingResult> ProcessDocumentAsync(
        string documentId,
        string title,
        string content,
        string sourceType,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing guitar technique document: {Title}", title);

        try
        {
            // Step 1: Chunk the content for processing
            var chunks = ChunkText(content, maxChunkSize: 2000, overlap: 200);
            _logger.LogInformation("Split document into {ChunkCount} chunks", chunks.Count);

            // Step 2: Extract guitar-specific knowledge from each chunk
            var extractedTechniques = new List<GuitarTechniqueKnowledge>();
            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                _logger.LogInformation("Processing chunk {Index}/{Total}", index + 1, chunks.Count);
                var knowledge = await ExtractGuitarTechniqueAsync(chunk, cancellationToken);
                extractedTechniques.Add(knowledge);
            }

            // Step 3: Generate summary
            _logger.LogInformation("Generating summary for document: {Title}", title);
            var summary = await GenerateSummaryAsync(content, extractedTechniques, cancellationToken);

            // Step 4: Generate embeddings for semantic search
            _logger.LogInformation("Generating embeddings for document: {Title}", title);
            var embedding = await _embeddingService.GenerateEmbeddingAsync(summary);

            // Step 5: Store in MongoDB
            _logger.LogInformation("Storing guitar technique document in MongoDB: {Title}", title);
            var storedDocId = await StoreGuitarTechniqueDocumentAsync(
                documentId,
                title,
                content,
                summary,
                extractedTechniques,
                embedding,
                sourceType,
                sourceUrl,
                cancellationToken);

            return new GuitarTechniqueProcessingResult
            {
                Success = true,
                DocumentId = storedDocId,
                Summary = summary,
                ExtractedTechniques = extractedTechniques,
                ChunkCount = chunks.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing guitar technique document: {Title}", title);
            return new GuitarTechniqueProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract guitar-specific knowledge from a text chunk using Ollama
    /// </summary>
    private async Task<GuitarTechniqueKnowledge> ExtractGuitarTechniqueAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze the following guitar technique text and extract structured knowledge.
Focus on:
1. Fingering patterns (e.g., CAGED shapes, barre chord fingerings, specific fret positions)
2. Exercises (e.g., chromatic exercises, scale patterns, arpeggio exercises)
3. Chord voicings (e.g., drop-2 voicings, shell voicings, rootless voicings)
4. Techniques (e.g., alternate picking, sweep picking, legato, tapping, hybrid picking)
5. Progressions (e.g., ii-V-I in different positions, turnarounds, common jazz progressions)
6. Styles (e.g., jazz, blues, rock, classical, fingerstyle)
7. Fretboard positions (e.g., position I-XII, specific string sets)

Text to analyze:
{text}

Return a JSON object with this structure:
{{
  ""fingeringPatterns"": [""pattern description with fret numbers""],
  ""exercises"": [""exercise name and description""],
  ""chordVoicings"": [""chord name and voicing (e.g., Cmaj7 drop-2 on strings 2-5)""],
  ""techniques"": [""technique name and description""],
  ""progressions"": [""progression with chord names and positions""],
  ""styles"": [""musical style""],
  ""fretboardPositions"": [""position description (e.g., Position V, strings 1-3)""],
  ""keyInsights"": [""important concepts or tips""]
}}

Return ONLY the JSON, no additional text.";

        try
        {
            var response = await _ollamaChat.ChatAsync(prompt, cancellationToken: cancellationToken);

            // Parse JSON response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var knowledge = JsonSerializer.Deserialize<GuitarTechniqueKnowledge>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return knowledge ?? new GuitarTechniqueKnowledge();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract guitar technique knowledge, using empty result");
        }

        return new GuitarTechniqueKnowledge();
    }

    /// <summary>
    /// Generate a summary of the guitar technique document
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        string fullText,
        List<GuitarTechniqueKnowledge> extractedTechniques,
        CancellationToken cancellationToken)
    {
        // Combine all extracted knowledge
        var allFingeringPatterns = extractedTechniques.SelectMany(k => k.FingeringPatterns ?? []).Distinct().ToList();
        var allExercises = extractedTechniques.SelectMany(k => k.Exercises ?? []).Distinct().ToList();
        var allChordVoicings = extractedTechniques.SelectMany(k => k.ChordVoicings ?? []).Distinct().ToList();
        var allTechniques = extractedTechniques.SelectMany(k => k.Techniques ?? []).Distinct().ToList();
        var allProgressions = extractedTechniques.SelectMany(k => k.Progressions ?? []).Distinct().ToList();

        var prompt = $@"Generate a concise summary (2-3 paragraphs) of this guitar technique document.

Extracted Knowledge:
- Fingering Patterns: {string.Join(", ", allFingeringPatterns.Take(10))}
- Exercises: {string.Join(", ", allExercises.Take(10))}
- Chord Voicings: {string.Join(", ", allChordVoicings.Take(10))}
- Techniques: {string.Join(", ", allTechniques.Take(10))}
- Progressions: {string.Join(", ", allProgressions.Take(10))}

Full text (first 1000 chars):
{fullText.Substring(0, Math.Min(1000, fullText.Length))}

Summary:";

        try
        {
            return await _ollamaChat.ChatAsync(prompt, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate summary, using default");
            return "Guitar technique document with various fingering patterns, exercises, and chord voicings.";
        }
    }

    /// <summary>
    /// Store the processed guitar technique document in MongoDB
    /// </summary>
    private async Task<string> StoreGuitarTechniqueDocumentAsync(
        string documentId,
        string title,
        string content,
        string summary,
        List<GuitarTechniqueKnowledge> extractedTechniques,
        List<float> embedding,
        string sourceType,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        var database = typeof(MongoDbService)
            .GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_mongoDb) as IMongoDatabase
            ?? throw new InvalidOperationException("Could not access MongoDB database");

        var collection = database.GetCollection<BsonDocument>("guitar_technique_library");

        var document = new BsonDocument
        {
            ["document_id"] = documentId,
            ["source_type"] = sourceType,
            ["source_url"] = sourceUrl ?? "",
            ["title"] = title,
            ["content"] = content,
            ["summary"] = summary,
            ["extracted_knowledge"] = new BsonDocument
            {
                ["fingering_patterns"] = new BsonArray(extractedTechniques.SelectMany(k => k.FingeringPatterns ?? []).Distinct()),
                ["exercises"] = new BsonArray(extractedTechniques.SelectMany(k => k.Exercises ?? []).Distinct()),
                ["chord_voicings"] = new BsonArray(extractedTechniques.SelectMany(k => k.ChordVoicings ?? []).Distinct()),
                ["techniques"] = new BsonArray(extractedTechniques.SelectMany(k => k.Techniques ?? []).Distinct()),
                ["progressions"] = new BsonArray(extractedTechniques.SelectMany(k => k.Progressions ?? []).Distinct()),
                ["styles"] = new BsonArray(extractedTechniques.SelectMany(k => k.Styles ?? []).Distinct()),
                ["fretboard_positions"] = new BsonArray(extractedTechniques.SelectMany(k => k.FretboardPositions ?? []).Distinct()),
                ["key_insights"] = new BsonArray(extractedTechniques.SelectMany(k => k.KeyInsights ?? []).Distinct())
            },
            ["embedding"] = new BsonArray(embedding.Select(e => new BsonDouble(e))),
            ["processed_at"] = DateTime.UtcNow
        };

        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("document_id", documentId),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return documentId;
    }

    /// <summary>
    /// Chunk text into smaller pieces for processing
    /// </summary>
    private List<string> ChunkText(string text, int maxChunkSize, int overlap)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new List<string>();
        var currentSize = 0;

        foreach (var word in words)
        {
            currentChunk.Add(word);
            currentSize += word.Length + 1;

            if (currentSize >= maxChunkSize)
            {
                chunks.Add(string.Join(' ', currentChunk));
                
                // Keep overlap words for next chunk
                var overlapWords = currentChunk.TakeLast(overlap / 10).ToList();
                currentChunk = overlapWords;
                currentSize = overlapWords.Sum(w => w.Length + 1);
            }
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(string.Join(' ', currentChunk));
        }

        return chunks;
    }
}

/// <summary>
/// Result of guitar technique document processing
/// </summary>
public class GuitarTechniqueProcessingResult
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Summary { get; set; }
    public List<GuitarTechniqueKnowledge>? ExtractedTechniques { get; set; }
    public int ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Structured guitar technique knowledge extracted from text
/// </summary>
public class GuitarTechniqueKnowledge
{
    public List<string>? FingeringPatterns { get; set; }
    public List<string>? Exercises { get; set; }
    public List<string>? ChordVoicings { get; set; }
    public List<string>? Techniques { get; set; }
    public List<string>? Progressions { get; set; }
    public List<string>? Styles { get; set; }
    public List<string>? FretboardPositions { get; set; }
    public List<string>? KeyInsights { get; set; }
}

