namespace GaApi.Services.DocumentProcessing;

using GA.Business.Core.AI.Services.Embeddings;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

/// <summary>
/// Specialized processor for artist/style transcriptions and analysis
/// Learns characteristic progressions, voicings, and techniques from specific artists/styles
/// Enables style-specific recommendations like "play like Wes Montgomery" or "jazz in the style of Bill Evans"
/// </summary>
public class StyleLearningProcessor
{
    private readonly ILogger<StyleLearningProcessor> _logger;
    private readonly MongoDbService _mongoDb;
    private readonly IOllamaChatService _ollamaChat;
    private readonly IEmbeddingService _embeddingService;

    public StyleLearningProcessor(
        ILogger<StyleLearningProcessor> logger,
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
    /// Process a style/artist document (transcription, analysis, or instructional content)
    /// </summary>
    public async Task<StyleLearningResult> ProcessDocumentAsync(
        string documentId,
        string title,
        string content,
        string artistOrStyle,
        string sourceType,
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing style learning document: {Title} for {ArtistOrStyle}", title, artistOrStyle);

        try
        {
            // Step 1: Chunk the content for processing
            var chunks = ChunkText(content, maxChunkSize: 2000, overlap: 200);
            _logger.LogInformation("Split document into {ChunkCount} chunks", chunks.Count);

            // Step 2: Extract style characteristics from each chunk
            var extractedStyles = new List<StyleCharacteristics>();
            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                _logger.LogInformation("Processing chunk {Index}/{Total}", index + 1, chunks.Count);
                var characteristics = await ExtractStyleCharacteristicsAsync(chunk, artistOrStyle, cancellationToken);
                extractedStyles.Add(characteristics);
            }

            // Step 3: Generate summary
            _logger.LogInformation("Generating summary for document: {Title}", title);
            var summary = await GenerateSummaryAsync(content, extractedStyles, artistOrStyle, cancellationToken);

            // Step 4: Generate embeddings for semantic search
            _logger.LogInformation("Generating embeddings for document: {Title}", title);
            var embeddingArray = await _embeddingService.GenerateEmbeddingAsync(summary);
            var embedding = embeddingArray.ToList();

            // Step 5: Store in MongoDB
            _logger.LogInformation("Storing style learning document in MongoDB: {Title}", title);
            var storedDocId = await StoreStyleLearningDocumentAsync(
                documentId,
                title,
                content,
                summary,
                artistOrStyle,
                extractedStyles,
                embedding,
                sourceType,
                sourceUrl,
                cancellationToken);

            return new StyleLearningResult
            {
                Success = true,
                DocumentId = storedDocId,
                Summary = summary,
                ExtractedStyles = extractedStyles,
                ChunkCount = chunks.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing style learning document: {Title}", title);
            return new StyleLearningResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Extract style characteristics from a text chunk using Ollama
    /// </summary>
    private async Task<StyleCharacteristics> ExtractStyleCharacteristicsAsync(
        string text,
        string artistOrStyle,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze the following music transcription/analysis for {artistOrStyle} and extract style characteristics.
Focus on:
1. Characteristic chord progressions (e.g., ii-V-I variations, turnarounds, specific voicings)
2. Signature voicings (e.g., drop-2, rootless, shell voicings, specific chord shapes)
3. Melodic patterns (e.g., bebop lines, chromaticism, intervallic patterns)
4. Rhythmic characteristics (e.g., swing feel, syncopation, rhythmic displacement)
5. Harmonic techniques (e.g., chord substitutions, reharmonization, modal interchange)
6. Playing techniques (e.g., fingerstyle, pick technique, articulation, dynamics)
7. Tonal preferences (e.g., favorite keys, modes, scales)
8. Stylistic influences (e.g., bebop, cool jazz, fusion, blues)

Text to analyze:
{text}

Return a JSON object with this structure:
{{
  ""characteristicProgressions"": [""progression description with chord symbols""],
  ""signatureVoicings"": [""voicing description with chord name and shape""],
  ""melodicPatterns"": [""pattern description""],
  ""rhythmicCharacteristics"": [""rhythmic feature description""],
  ""harmonicTechniques"": [""technique name and description""],
  ""playingTechniques"": [""technique name and description""],
  ""tonalPreferences"": [""key/mode/scale preference""],
  ""stylisticInfluences"": [""influence or genre""],
  ""keyInsights"": [""important observations about the style""]
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
                var characteristics = JsonSerializer.Deserialize<StyleCharacteristics>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return characteristics ?? new StyleCharacteristics();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract style characteristics, using empty result");
        }

        return new StyleCharacteristics();
    }

    /// <summary>
    /// Generate a summary of the style learning document
    /// </summary>
    private async Task<string> GenerateSummaryAsync(
        string fullText,
        List<StyleCharacteristics> extractedStyles,
        string artistOrStyle,
        CancellationToken cancellationToken)
    {
        // Combine all extracted characteristics
        var allProgressions = extractedStyles.SelectMany(s => s.CharacteristicProgressions ?? []).Distinct().ToList();
        var allVoicings = extractedStyles.SelectMany(s => s.SignatureVoicings ?? []).Distinct().ToList();
        var allMelodic = extractedStyles.SelectMany(s => s.MelodicPatterns ?? []).Distinct().ToList();
        var allRhythmic = extractedStyles.SelectMany(s => s.RhythmicCharacteristics ?? []).Distinct().ToList();

        var prompt = $@"Generate a concise summary (2-3 paragraphs) of this style analysis for {artistOrStyle}.

Extracted Characteristics:
- Progressions: {string.Join(", ", allProgressions.Take(10))}
- Voicings: {string.Join(", ", allVoicings.Take(10))}
- Melodic Patterns: {string.Join(", ", allMelodic.Take(10))}
- Rhythmic Features: {string.Join(", ", allRhythmic.Take(10))}

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
            return $"Style analysis for {artistOrStyle} covering characteristic progressions, voicings, melodic patterns, and playing techniques.";
        }
    }

    /// <summary>
    /// Store the processed style learning document in MongoDB
    /// </summary>
    private async Task<string> StoreStyleLearningDocumentAsync(
        string documentId,
        string title,
        string content,
        string summary,
        string artistOrStyle,
        List<StyleCharacteristics> extractedStyles,
        List<float> embedding,
        string sourceType,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        var database = typeof(MongoDbService)
            .GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_mongoDb) as IMongoDatabase
            ?? throw new InvalidOperationException("Could not access MongoDB database");

        var collection = database.GetCollection<BsonDocument>("style_learning_library");

        var document = new BsonDocument
        {
            ["document_id"] = documentId,
            ["source_type"] = sourceType,
            ["source_url"] = sourceUrl ?? "",
            ["title"] = title,
            ["content"] = content,
            ["summary"] = summary,
            ["artist_or_style"] = artistOrStyle,
            ["extracted_characteristics"] = new BsonDocument
            {
                ["characteristic_progressions"] = new BsonArray(extractedStyles.SelectMany(s => s.CharacteristicProgressions ?? []).Distinct()),
                ["signature_voicings"] = new BsonArray(extractedStyles.SelectMany(s => s.SignatureVoicings ?? []).Distinct()),
                ["melodic_patterns"] = new BsonArray(extractedStyles.SelectMany(s => s.MelodicPatterns ?? []).Distinct()),
                ["rhythmic_characteristics"] = new BsonArray(extractedStyles.SelectMany(s => s.RhythmicCharacteristics ?? []).Distinct()),
                ["harmonic_techniques"] = new BsonArray(extractedStyles.SelectMany(s => s.HarmonicTechniques ?? []).Distinct()),
                ["playing_techniques"] = new BsonArray(extractedStyles.SelectMany(s => s.PlayingTechniques ?? []).Distinct()),
                ["tonal_preferences"] = new BsonArray(extractedStyles.SelectMany(s => s.TonalPreferences ?? []).Distinct()),
                ["stylistic_influences"] = new BsonArray(extractedStyles.SelectMany(s => s.StylisticInfluences ?? []).Distinct()),
                ["key_insights"] = new BsonArray(extractedStyles.SelectMany(s => s.KeyInsights ?? []).Distinct())
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
/// Result of style learning document processing
/// </summary>
public class StyleLearningResult
{
    public bool Success { get; set; }
    public string? DocumentId { get; set; }
    public string? Summary { get; set; }
    public List<StyleCharacteristics>? ExtractedStyles { get; set; }
    public int ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Style characteristics extracted from text
/// </summary>
public class StyleCharacteristics
{
    public List<string>? CharacteristicProgressions { get; set; }
    public List<string>? SignatureVoicings { get; set; }
    public List<string>? MelodicPatterns { get; set; }
    public List<string>? RhythmicCharacteristics { get; set; }
    public List<string>? HarmonicTechniques { get; set; }
    public List<string>? PlayingTechniques { get; set; }
    public List<string>? TonalPreferences { get; set; }
    public List<string>? StylisticInfluences { get; set; }
    public List<string>? KeyInsights { get; set; }
}

